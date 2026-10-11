using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Kestrel.Core.Abstractions;
using Kestrel.Core.Guardrails;
using Kestrel.Core.Llm;
using Kestrel.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Kestrel.Core.Rag;

public sealed class AnswerService(
    IRetriever retriever,
    IMaintenanceStore maintenanceStore,
    ILlmProviderFactory llmFactory,
    InputGuard inputGuard,
    ILogger<AnswerService> logger) : IAnswerService
{
    public static readonly ActivitySource ActivitySource = new("Kestrel.Answer");

    public async IAsyncEnumerable<AnswerEvent> AnswerAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("answer");
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId!;
        var messageId = Guid.NewGuid().ToString("N");

        string? message = null;
        string? inputError = null;
        try
        {
            message = inputGuard.SanitizeUserInput(request.Message);
        }
        catch (Exception ex)
        {
            inputError = ex.Message;
        }

        if (inputError is not null)
        {
            yield return new ErrorEvent(inputError);
            yield break;
        }

        message ??= string.Empty;

        var chatProfile = llmFactory.GetActiveChatProfile();
        yield return new ModelInfoEvent(
            chatProfile.Provider,
            chatProfile.ChatModel,
            chatProfile.SupportsTools);

        IReadOnlyList<RetrievedChunk>? chunks = null;
        string? retrievalError = null;
        try
        {
            chunks = await retriever.RetrieveAsync(message, request.EquipmentId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Retrieval failed");
            retrievalError = $"Retrieval failed: {ex.Message}";
        }

        if (retrievalError is not null)
        {
            yield return new ErrorEvent(retrievalError);
            yield break;
        }

        chunks ??= [];

        var citations = new List<Citation>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i].Chunk;
            var citation = new Citation(
                i + 1,
                chunk.DocumentId,
                chunk.Title,
                chunk.SourcePath,
                chunk.Section,
                Truncate(chunk.Content, 500),
                chunk.EquipmentId,
                chunk.DocType);
            citations.Add(citation);
            yield return new CitationEvent(citation);
        }

        if (citations.Any(c => InputGuard.IsSafetyDocument(c.DocType)))
        {
            yield return new SafetyEvent(
                "This answer references a safety procedure. Follow site LOTO/PPE rules and verify against the cited source before acting.");
        }

        var toolContext = new StringBuilder();
        if (chatProfile.SupportsTools)
        {
            await foreach (var toolEvent in RunToolsAsync(message, request.EquipmentId, toolContext, cancellationToken))
            {
                yield return toolEvent;
            }
        }

        var systemPrompt = BuildSystemPrompt(chunks, citations, toolContext.ToString());
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt)
        };

        if (request.History is not null)
        {
            foreach (var turn in request.History.TakeLast(8))
            {
                var role = turn.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.Assistant
                    : ChatRole.User;
                messages.Add(new ChatMessage(role, turn.Content));
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, message));

        IChatClient client = llmFactory.CreateChatClient("generate");
        var answer = new StringBuilder();
        string? generateError = null;

        using (ActivitySource.StartActivity("generate"))
        {
            await using var enumerator = client
                .GetStreamingResponseAsync(messages, cancellationToken: cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                bool moved;
                try
                {
                    moved = await enumerator.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Generation failed");
                    generateError = ex.Message;
                    break;
                }

                if (!moved)
                {
                    break;
                }

                var text = enumerator.Current.Text;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                answer.Append(text);
                yield return new TokenEvent(text);
            }
        }

        if (generateError is not null)
        {
            yield return new ErrorEvent($"Generation failed: {generateError}");
        }
        else if (answer.Length == 0)
        {
            yield return new TokenEvent(
                "I don't know based on the available plant documentation. Try naming an equipment ID (for example CNC-204) or ask about a specific procedure.");
        }

        activity?.SetTag("answer.chars", answer.Length);
        yield return new DoneEvent(messageId, conversationId);
    }

    private async IAsyncEnumerable<ToolEvent> RunToolsAsync(
        string message,
        string? equipmentId,
        StringBuilder toolContext,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var id = equipmentId ?? Retriever.ExtractEquipmentId(message);
        if (!string.IsNullOrWhiteSpace(id))
        {
            yield return new ToolEvent("GetEquipmentInfo", $"Looking up equipment {id}...");
            var equipment = await maintenanceStore.GetEquipmentAsync(id, cancellationToken);
            if (equipment is not null)
            {
                var json = JsonSerializer.Serialize(equipment);
                toolContext.AppendLine($"EquipmentInfo: {json}");
                yield return new ToolEvent("GetEquipmentInfo", $"{equipment.Name} ({equipment.Model}) at {equipment.PlantId}/{equipment.Area}");
            }

            yield return new ToolEvent("GetMaintenanceHistory", $"Looking up maintenance history for {id}...");
            var history = await maintenanceStore.GetMaintenanceHistoryAsync(id, 5, cancellationToken);
            toolContext.AppendLine($"MaintenanceHistory: {JsonSerializer.Serialize(history)}");
            yield return new ToolEvent("GetMaintenanceHistory", $"{history.Count} recent work entries found");
        }

        if (message.Contains("work order", StringComparison.OrdinalIgnoreCase)
            || message.Contains("open wo", StringComparison.OrdinalIgnoreCase)
            || message.Contains("backlog", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ToolEvent("GetOpenWorkOrders", "Checking open work orders...");
            var plant = id is null ? null : (await maintenanceStore.GetEquipmentAsync(id, cancellationToken))?.PlantId;
            var orders = await maintenanceStore.GetOpenWorkOrdersAsync(plant, cancellationToken);
            toolContext.AppendLine($"OpenWorkOrders: {JsonSerializer.Serialize(orders.Take(10))}");
            yield return new ToolEvent("GetOpenWorkOrders", $"{orders.Count} open/in-progress work orders");
        }
    }

    private static string BuildSystemPrompt(
        IReadOnlyList<RetrievedChunk> chunks,
        IReadOnlyList<Citation> citations,
        string toolContext)
    {
        var context = new StringBuilder();
        for (var i = 0; i < chunks.Count; i++)
        {
            var c = chunks[i].Chunk;
            context.AppendLine($"[{i + 1}] Title: {c.Title} | Section: {c.Section} | Type: {c.DocType}");
            context.AppendLine(c.Content);
            context.AppendLine();
        }

        return
            $"""
            You are the Kestrel Industrial plant assistant for technicians and engineers.
            Answer ONLY using the provided context and tool results.
            Cite sources as [1], [2] matching the context numbers.
            If the context does not contain the answer, say "I don't know" and suggest what document or equipment ID would help.
            Never invent torque values, part numbers, or safety steps.
            When safety procedures are involved, remind the user to follow site LOTO/PPE rules.

            Context:
            {context}

            Tool results:
            {toolContext}

            Available citations: {string.Join(", ", citations.Select(c => $"[{c.Index}] {c.Title}"))}
            """;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";
}
