using Kestrel.Core.Abstractions;
using Kestrel.Core.Configuration;
using Kestrel.Core.Guardrails;
using Kestrel.Core.Ingestion;
using Kestrel.Core.Llm;
using Kestrel.Core.Rag;
using Kestrel.Core.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kestrel.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddKestrelCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        // Named clients intentionally avoid IHttpClientFactory defaults from Aspire
        // (standard resilience times out at 10s and kills local LLM calls).
        services.AddSingleton<OllamaHttpClients>();

        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();
        services.AddSingleton<IChunker, HeadingChunker>();
        services.AddSingleton<MarkdownDocumentLoader>();
        services.AddSingleton<PdfDocumentLoader>();
        services.AddSingleton<JsonDocumentLoader>();
        services.AddSingleton<IDocumentLoader>(sp => new CompositeDocumentLoader(
        [
            sp.GetRequiredService<MarkdownDocumentLoader>(),
            sp.GetRequiredService<PdfDocumentLoader>(),
            sp.GetRequiredService<JsonDocumentLoader>()
        ]));
        services.AddSingleton<InputGuard>();
        services.AddSingleton<IMaintenanceStore, SqliteMaintenanceStore>();
        services.AddSingleton<IVectorStore, QdrantVectorStore>();
        services.AddSingleton<IRetriever, Retriever>();
        services.AddSingleton<IAnswerService, AnswerService>();

        return services;
    }
}
