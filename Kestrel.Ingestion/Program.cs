using Kestrel.Core;
using Kestrel.Core.Abstractions;
using Kestrel.Core.Configuration;
using Kestrel.Core.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables();

// Allow pointing at sibling Api settings when run from solution.
var apiSettings = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Kestrel.Api", "appsettings.json"));
if (File.Exists(apiSettings))
{
    builder.Configuration.AddJsonFile(apiSettings, optional: true, reloadOnChange: false);
}

builder.Services.AddKestrelCore(builder.Configuration);
builder.Services.PostConfigure<RagOptions>(rag =>
{
    var contentRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    rag.DataRoot = Path.GetFullPath(Path.Combine(contentRoot, rag.DataRoot));
    rag.SqlitePath = Path.GetFullPath(Path.Combine(contentRoot, rag.SqlitePath));
});

var qdrantHost = builder.Configuration["Qdrant:Host"] ?? "localhost";
var qdrantPort = int.TryParse(builder.Configuration["Qdrant:Port"], out var p) ? p : 6334;
var qdrantKey = builder.Configuration["Qdrant:ApiKey"];
builder.Services.AddSingleton(_ =>
    string.IsNullOrWhiteSpace(qdrantKey)
        ? new QdrantClient(qdrantHost, qdrantPort)
        : new QdrantClient(qdrantHost, qdrantPort, apiKey: qdrantKey));

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Ingestion");
var rag = host.Services.GetRequiredService<IOptions<RagOptions>>().Value;
var llm = host.Services.GetRequiredService<ILlmProviderFactory>();
var loader = host.Services.GetRequiredService<IDocumentLoader>();
var chunker = host.Services.GetRequiredService<IChunker>();
var store = host.Services.GetRequiredService<IVectorStore>();
var maintenance = host.Services.GetRequiredService<IMaintenanceStore>();

var dataRoot = Path.GetFullPath(rag.DataRoot);
if (!Directory.Exists(dataRoot))
{
    logger.LogError("Data root not found: {Path}. Run Kestrel.DataGen first.", dataRoot);
    return 1;
}

await maintenance.InitializeAsync();

var embeddingProfile = llm.GetActiveEmbeddingProfile();
var collection = llm.GetCollectionName();
logger.LogInformation(
    "Ingesting from {Root} into collection {Collection} using {Model}",
    dataRoot,
    collection,
    embeddingProfile.EmbeddingModel);

await store.EnsureCollectionAsync(embeddingProfile.EmbeddingDimensions);

var files = Directory.EnumerateFiles(dataRoot, "*.*", SearchOption.AllDirectories)
    .Where(f => loader.CanLoad(f))
    .Where(f => !f.EndsWith("company.json", StringComparison.OrdinalIgnoreCase))
    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}eval{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
    .ToList();

logger.LogInformation("Found {Count} documents", files.Count);

var embedder = llm.CreateEmbeddingGenerator();
const int batchSize = 16;
var totalChunks = 0;

foreach (var batch in files.Chunk(5))
{
    var chunks = new List<Kestrel.Core.Models.DocumentChunk>();
    foreach (var file in batch)
    {
        try
        {
            var doc = await loader.LoadAsync(file);
            var docChunks = chunker.Chunk(doc);
            chunks.AddRange(docChunks);
            logger.LogInformation("Loaded {File} -> {Chunks} chunks", Path.GetRelativePath(dataRoot, file), docChunks.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load {File}", file);
        }
    }

    for (var i = 0; i < chunks.Count; i += batchSize)
    {
        var slice = chunks.Skip(i).Take(batchSize).ToList();
        var texts = slice.Select(c => c.Content).ToList();
        var embeddings = await embedder.GenerateAsync(texts);
        var vectors = embeddings.Select(e => e.Vector).ToList();
        await store.UpsertAsync(slice, vectors);
        totalChunks += slice.Count;
        logger.LogInformation("Upserted {Count} chunks (total {Total})", slice.Count, totalChunks);
    }
}

logger.LogInformation("Ingestion complete. {Total} chunks in {Collection}", totalChunks, collection);
return 0;
