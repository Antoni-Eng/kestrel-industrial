using Kestrel.Core.Configuration;
using Kestrel.Core.Llm;

namespace Kestrel.Tests;

public class CollectionNamingTests
{
    [Fact]
    public void ForEmbedding_IncludesModelAndDimensions()
    {
        var profile = new LlmProfile
        {
            EmbeddingModel = "nomic-embed-text",
            EmbeddingDimensions = 768
        };

        var name = CollectionNaming.ForEmbedding(profile);

        Assert.Equal("kestrel_docs__nomic-embed-text__768", name);
    }

    [Fact]
    public void Sanitize_ReplacesSpacesAndSymbols()
    {
        Assert.Equal("my_model_v1", CollectionNaming.Sanitize("My Model/v1"));
    }
}
