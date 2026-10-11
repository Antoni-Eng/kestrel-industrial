using Kestrel.Core.Configuration;
using Kestrel.Core.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kestrel.Tests;

public class LlmRoutingTests
{
    [Fact]
    public void OllamaHttpClients_ReusesClientPerEndpoint()
    {
        using var clients = new OllamaHttpClients();

        var a = clients.GetChat("http://localhost:11434");
        var b = clients.GetChat("http://localhost:11434/");
        var c = clients.GetChat("http://192.168.1.50:11434");

        Assert.Same(a, b);
        Assert.NotSame(a, c);
        Assert.Equal(new Uri("http://192.168.1.50:11434/"), c.BaseAddress);
    }

    [Fact]
    public void RoleOverride_RoutesToItsOwnEndpoint()
    {
        var options = new LlmOptions
        {
            ActiveChatProfile = "local",
            ActiveEmbeddingProfile = "local",
            Profiles =
            {
                ["local"] = new LlmProfile { Endpoint = "http://localhost:11434" },
                ["desktop"] = new LlmProfile { Endpoint = "http://192.168.1.50:11434" }
            },
            RoleOverrides = { ["generate"] = "desktop" }
        };
        using var clients = new OllamaHttpClients();
        var factory = new LlmProviderFactory(Options.Create(options), clients, NullLogger<LlmProviderFactory>.Instance);

        var rewrite = factory.CreateChatClient("rewrite");
        var generate = factory.CreateChatClient("generate");

        Assert.Equal(new Uri("http://localhost:11434/"), ProviderUri(rewrite));
        Assert.Equal(new Uri("http://192.168.1.50:11434/"), ProviderUri(generate));
    }

    private static Uri? ProviderUri(IChatClient client)
        => client.GetService<ChatClientMetadata>()?.ProviderUri;
}
