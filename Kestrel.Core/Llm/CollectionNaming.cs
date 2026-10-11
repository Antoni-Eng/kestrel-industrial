using System.Text.RegularExpressions;
using Kestrel.Core.Configuration;

namespace Kestrel.Core.Llm;

public static partial class CollectionNaming
{
    public const string CollectionPrefix = "kestrel_docs";

    public static string ForEmbedding(LlmProfile embeddingProfile)
    {
        var model = Sanitize(embeddingProfile.EmbeddingModel);
        return $"{CollectionPrefix}__{model}__{embeddingProfile.EmbeddingDimensions}";
    }

    public static string Sanitize(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        return NonSafeChars().Replace(lowered, "_");
    }

    [GeneratedRegex(@"[^a-z0-9._-]+")]
    private static partial Regex NonSafeChars();
}
