using System.Diagnostics;
using System.Text.RegularExpressions;
using Kestrel.Core.Abstractions;
using Kestrel.Core.Configuration;
using Kestrel.Core.Guardrails;
using Kestrel.Core.Llm;
using Kestrel.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kestrel.Core.Rag;

public sealed partial class Retriever(
    IVectorStore vectorStore,
    IMaintenanceStore maintenanceStore,
    ILlmProviderFactory llmFactory,
    IOptions<RagOptions> options,
    ILogger<Retriever> logger) : IRetriever
{
    public static readonly ActivitySource ActivitySource = new("Kestrel.Retriever");

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        string? equipmentId,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("retrieve");
        var rewritten = await RewriteQueryAsync(query, cancellationToken);
        activity?.SetTag("query.rewritten", rewritten);

        var inferredEquipment = equipmentId
            ?? await KnownEquipmentAsync(ExtractEquipmentId(query), cancellationToken)
            ?? await KnownEquipmentAsync(ExtractEquipmentId(rewritten), cancellationToken);
        activity?.SetTag("equipment.id", inferredEquipment);

        var embeddingClient = llmFactory.CreateEmbeddingGenerator();
        var embedding = await embeddingClient.GenerateAsync([rewritten], cancellationToken: cancellationToken);
        var vector = embedding[0].Vector;

        var hits = await vectorStore.SearchAsync(
            vector,
            options.Value.TopK,
            inferredEquipment,
            cancellationToken);

        var sanitized = hits
            .Select(h => h with
            {
                Chunk = h.Chunk with { Content = InputGuard.SanitizeRetrievedText(h.Chunk.Content) }
            })
            .ToList();

        var reranked = await RerankAsync(rewritten, sanitized, cancellationToken);
        logger.LogInformation(
            "Retrieved {Count} chunks for query (equipment={Equipment})",
            reranked.Count,
            inferredEquipment ?? "-");
        return reranked;
    }

    private async Task<string> RewriteQueryAsync(string query, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("rewrite");
        try
        {
            var client = llmFactory.CreateChatClient("rewrite");
            var prompt =
                $"""
                Rewrite the user question into a standalone search query for a manufacturing knowledge base.
                Keep equipment IDs like CNC-204 intact. Return only the rewritten query.
                Question: {query}
                """;
            var response = await client.GetResponseAsync(prompt, cancellationToken: cancellationToken);
            var text = response.Text?.Trim();
            return string.IsNullOrWhiteSpace(text) ? query : text;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Query rewrite failed; using original query");
            return query;
        }
    }

    private async Task<IReadOnlyList<RetrievedChunk>> RerankAsync(
        string query,
        IReadOnlyList<RetrievedChunk> hits,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("rerank");
        if (hits.Count <= options.Value.RerankTopN)
        {
            return hits;
        }

        try
        {
            var client = llmFactory.CreateChatClient("rerank");
            var catalog = string.Join(
                "\n",
                hits.Select((h, i) => $"{i + 1}. [{h.Chunk.Title} / {h.Chunk.Section}] {Truncate(h.Chunk.Content, 240)}"));

            var prompt =
                $"""
                Rank the passages by relevance to the query. Return a comma-separated list of passage numbers, best first.
                Query: {query}
                Passages:
                {catalog}
                """;

            var response = await client.GetResponseAsync(prompt, cancellationToken: cancellationToken);
            var order = ParseRanks(response.Text ?? string.Empty, hits.Count);
            if (order.Count == 0)
            {
                return hits.Take(options.Value.RerankTopN).ToList();
            }

            return order
                .Select((idx, rank) => hits[idx] with { Rank = rank + 1 })
                .Take(options.Value.RerankTopN)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rerank failed; using vector order");
            return hits.Take(options.Value.RerankTopN).ToList();
        }
    }

    private static List<int> ParseRanks(string text, int count)
    {
        var numbers = RankNumbers().Matches(text)
            .Select(m => int.Parse(m.Value) - 1)
            .Where(i => i >= 0 && i < count)
            .Distinct()
            .ToList();
        return numbers;
    }

    private async Task<string?> KnownEquipmentAsync(string? candidate, CancellationToken cancellationToken)
    {
        if (candidate is null)
        {
            return null;
        }

        var equipment = await maintenanceStore.GetEquipmentAsync(candidate, cancellationToken);
        return equipment?.Id;
    }

    public static string? ExtractEquipmentId(string text)
    {
        var match = EquipmentId().Match(text);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";

    [GeneratedRegex(@"\d+")]
    private static partial Regex RankNumbers();

    // Standalone IDs only: rejects the "LOTO-01" inside "SOP-LOTO-01" and "CNC-204" inside "KST-CNC-204-BRG".
    [GeneratedRegex(@"(?<![A-Za-z0-9-])([A-Z]{2,5}-\d{1,4})(?![A-Za-z0-9-])", RegexOptions.IgnoreCase)]
    private static partial Regex EquipmentId();
}
