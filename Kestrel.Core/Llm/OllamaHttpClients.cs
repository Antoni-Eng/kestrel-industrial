namespace Kestrel.Core.Llm;

/// <summary>
/// Long-lived HttpClients for Ollama that are not wrapped by Aspire's short resilience timeouts.
/// </summary>
public sealed class OllamaHttpClients : IDisposable
{
    public HttpClient Chat { get; } = new()
    {
        Timeout = TimeSpan.FromMinutes(15)
    };

    public HttpClient Embed { get; } = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    public void Dispose()
    {
        Chat.Dispose();
        Embed.Dispose();
    }
}
