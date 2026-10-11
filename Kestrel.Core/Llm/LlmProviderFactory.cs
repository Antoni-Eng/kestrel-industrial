using System.ClientModel;
using Kestrel.Core.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;

namespace Kestrel.Core.Llm;

public interface ILlmProviderFactory
{
    IChatClient CreateChatClient(string? role = null);
    IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator();
    LlmProfile GetActiveChatProfile(string? role = null);
    LlmProfile GetActiveEmbeddingProfile();
    string GetCollectionName();
    Task<LlmHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record LlmHealthResult(
    bool ChatReachable,
    bool EmbeddingReachable,
    string ChatProfile,
    string EmbeddingProfile,
    string ChatModel,
    string EmbeddingModel,
    string? Error);

public sealed class LlmProviderFactory(
    IOptions<LlmOptions> options,
    OllamaHttpClients ollamaHttp,
    ILogger<LlmProviderFactory> logger) : ILlmProviderFactory
{
    private readonly LlmOptions _options = options.Value;

    public LlmProfile GetActiveChatProfile(string? role = null) => _options.GetChatProfile(role);

    public LlmProfile GetActiveEmbeddingProfile() => _options.GetEmbeddingProfile();

    public string GetCollectionName() => CollectionNaming.ForEmbedding(GetActiveEmbeddingProfile());

    public IChatClient CreateChatClient(string? role = null)
    {
        var profile = GetActiveChatProfile(role);
        logger.LogInformation(
            "Creating chat client profile={Profile} provider={Provider} model={Model} endpoint={Endpoint}",
            _options.ResolveProfileName(role, _options.ActiveChatProfile),
            profile.Provider,
            profile.ChatModel,
            profile.Endpoint);

        return CreateChatClient(profile);
    }

    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator()
    {
        var profile = GetActiveEmbeddingProfile();
        logger.LogInformation(
            "Creating embedding generator profile={Profile} provider={Provider} model={Model}",
            _options.ActiveEmbeddingProfile,
            profile.Provider,
            profile.EmbeddingModel);

        return CreateEmbeddingGenerator(profile);
    }

    public async Task<LlmHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var chatProfile = GetActiveChatProfile();
        var embeddingProfile = GetActiveEmbeddingProfile();

        try
        {
            var chat = CreateChatClient();
            var embedding = CreateEmbeddingGenerator();

            var chatOk = await ProbeChatAsync(chat, cancellationToken);
            var embOk = await ProbeEmbeddingAsync(embedding, cancellationToken);

            return new LlmHealthResult(
                chatOk,
                embOk,
                _options.ActiveChatProfile,
                _options.ActiveEmbeddingProfile,
                chatProfile.ChatModel,
                embeddingProfile.EmbeddingModel,
                chatOk && embOk ? null : "One or more LLM probes failed.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM health check failed");
            return new LlmHealthResult(
                false,
                false,
                _options.ActiveChatProfile,
                _options.ActiveEmbeddingProfile,
                chatProfile.ChatModel,
                embeddingProfile.EmbeddingModel,
                ex.Message);
        }
    }

    private IChatClient CreateChatClient(LlmProfile profile)
    {
        return profile.Provider.ToLowerInvariant() switch
        {
            "ollama" => CreateOllamaChatClient(profile),
            "openai" or "openaicompatible" or "openai-compatible" or "azureopenai" or "azure"
                => CreateOpenAiChatClient(profile),
            _ => throw new NotSupportedException($"LLM provider '{profile.Provider}' is not supported.")
        };
    }

    private IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(LlmProfile profile)
    {
        return profile.Provider.ToLowerInvariant() switch
        {
            "ollama" => CreateOllamaEmbeddingGenerator(profile),
            "openai" or "openaicompatible" or "openai-compatible" or "azureopenai" or "azure"
                => CreateOpenAiEmbeddingGenerator(profile),
            _ => throw new NotSupportedException($"Embedding provider '{profile.Provider}' is not supported.")
        };
    }

    private IChatClient CreateOllamaChatClient(LlmProfile profile)
        => new OllamaApiClient(ollamaHttp.GetChat(profile.Endpoint), profile.ChatModel);

    private IEmbeddingGenerator<string, Embedding<float>> CreateOllamaEmbeddingGenerator(LlmProfile profile)
        => new OllamaApiClient(ollamaHttp.GetEmbed(profile.Endpoint), profile.EmbeddingModel);

    private static IChatClient CreateOpenAiChatClient(LlmProfile profile)
    {
        var client = CreateOpenAiClient(profile);
        return client.GetChatClient(profile.ChatModel).AsIChatClient();
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateOpenAiEmbeddingGenerator(LlmProfile profile)
    {
        var client = CreateOpenAiClient(profile);
        return client.GetEmbeddingClient(profile.EmbeddingModel).AsIEmbeddingGenerator();
    }

    private static OpenAIClient CreateOpenAiClient(LlmProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ApiKey))
        {
            throw new InvalidOperationException(
                $"Profile '{profile.ChatModel}' provider '{profile.Provider}' requires ApiKey in appsettings.json under Llm:Profiles.");
        }

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(NormalizeEndpoint(profile.Endpoint))
        };

        return new OpenAIClient(new ApiKeyCredential(profile.ApiKey), options);
    }

    private static string NormalizeEndpoint(string endpoint)
        => endpoint.EndsWith('/') ? endpoint : endpoint + "/";

    private static async Task<bool> ProbeChatAsync(IChatClient chat, CancellationToken ct)
    {
        var response = await chat.GetResponseAsync("Reply with OK", cancellationToken: ct);
        return !string.IsNullOrWhiteSpace(response.Text);
    }

    private static async Task<bool> ProbeEmbeddingAsync(
        IEmbeddingGenerator<string, Embedding<float>> embedding,
        CancellationToken ct)
    {
        var result = await embedding.GenerateAsync(["health-check"], cancellationToken: ct);
        return result.Count > 0 && result[0].Vector.Length > 0;
    }
}
