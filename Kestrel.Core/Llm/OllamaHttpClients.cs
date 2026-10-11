using System.Collections.Concurrent;

namespace Kestrel.Core.Llm;

/// <summary>
/// Long-lived HttpClients for Ollama that are not wrapped by Aspire's short resilience timeouts.
/// One client per endpoint, so profiles and role overrides can target different machines.
/// </summary>
public sealed class OllamaHttpClients : IDisposable
{
    private static readonly TimeSpan ChatTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan EmbedTimeout = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, Lazy<HttpClient>> _chat = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Lazy<HttpClient>> _embed = new(StringComparer.OrdinalIgnoreCase);

    public HttpClient GetChat(string endpoint) => Get(_chat, endpoint, ChatTimeout);

    public HttpClient GetEmbed(string endpoint) => Get(_embed, endpoint, EmbedTimeout);

    private static HttpClient Get(
        ConcurrentDictionary<string, Lazy<HttpClient>> clients,
        string endpoint,
        TimeSpan timeout)
    {
        var baseAddress = new Uri(endpoint.EndsWith('/') ? endpoint : endpoint + "/");
        return clients.GetOrAdd(
            baseAddress.AbsoluteUri,
            _ => new Lazy<HttpClient>(() => new HttpClient { BaseAddress = baseAddress, Timeout = timeout })).Value;
    }

    public void Dispose()
    {
        foreach (var client in _chat.Values.Concat(_embed.Values).Where(c => c.IsValueCreated))
        {
            client.Value.Dispose();
        }
    }
}
