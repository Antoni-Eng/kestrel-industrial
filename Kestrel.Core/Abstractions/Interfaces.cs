using Kestrel.Core.Models;

namespace Kestrel.Core.Abstractions;

public interface IDocumentLoader
{
    bool CanLoad(string path);
    Task<SourceDocument> LoadAsync(string path, CancellationToken cancellationToken = default);
}

public interface IChunker
{
    IReadOnlyList<DocumentChunk> Chunk(SourceDocument document);
}

public interface IRetriever
{
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        string? equipmentId,
        CancellationToken cancellationToken = default);
}

public interface IAnswerService
{
    IAsyncEnumerable<AnswerEvent> AnswerAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default);
}

public abstract record AnswerEvent;

public sealed record TokenEvent(string Text) : AnswerEvent;
public sealed record CitationEvent(Citation Citation) : AnswerEvent;
public sealed record ToolEvent(string Name, string Detail) : AnswerEvent;
public sealed record SafetyEvent(string Message) : AnswerEvent;
public sealed record ModelInfoEvent(string Profile, string Model, bool SupportsTools) : AnswerEvent;
public sealed record DoneEvent(string MessageId, string ConversationId) : AnswerEvent;
public sealed record ErrorEvent(string Message) : AnswerEvent;

public interface IVectorStore
{
    Task EnsureCollectionAsync(int dimensions, CancellationToken cancellationToken = default);
    Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<ReadOnlyMemory<float>> vectors, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        ReadOnlyMemory<float> vector,
        int topK,
        string? equipmentId,
        CancellationToken cancellationToken = default);
}

public interface IMaintenanceStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> GetEquipmentAsync(CancellationToken cancellationToken = default);
    Task<Equipment?> GetEquipmentAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenanceLog>> GetMaintenanceHistoryAsync(string equipmentId, int take = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkOrder>> GetOpenWorkOrdersAsync(string? plantId, CancellationToken cancellationToken = default);
    Task SaveFeedbackAsync(FeedbackRequest feedback, CancellationToken cancellationToken = default);
}
