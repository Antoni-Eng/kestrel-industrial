namespace Kestrel.Core.Configuration;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string ActiveChatProfile { get; set; } = "local";
    public string ActiveEmbeddingProfile { get; set; } = "local";
    public Dictionary<string, LlmProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> RoleOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public LlmProfile GetChatProfile(string? role = null)
    {
        var name = ResolveProfileName(role, ActiveChatProfile);
        return GetRequiredProfile(name);
    }

    public LlmProfile GetEmbeddingProfile()
    {
        return GetRequiredProfile(ActiveEmbeddingProfile);
    }

    public string ResolveProfileName(string? role, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(role)
            && RoleOverrides.TryGetValue(role, out var overrideName)
            && !string.IsNullOrWhiteSpace(overrideName))
        {
            return overrideName;
        }

        return fallback;
    }

    public LlmProfile GetRequiredProfile(string name)
    {
        if (!Profiles.TryGetValue(name, out var profile))
        {
            throw new InvalidOperationException(
                $"LLM profile '{name}' was not found. Available: {string.Join(", ", Profiles.Keys)}");
        }

        return profile;
    }
}

public sealed class LlmProfile
{
    public string Provider { get; set; } = "Ollama";
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "qwen2.5:7b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string? ApiKey { get; set; }
    public bool SupportsTools { get; set; } = true;
    public int EmbeddingDimensions { get; set; } = 768;
}

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public string DataRoot { get; set; } = "../data";
    public string SqlitePath { get; set; } = "../data/kestrel.db";
    public int TopK { get; set; } = 12;
    public int RerankTopN { get; set; } = 5;
    public int ChunkSizeTokens { get; set; } = 400;
    public int ChunkOverlapTokens { get; set; } = 60;
    public int MaxInputChars { get; set; } = 4000;
}
