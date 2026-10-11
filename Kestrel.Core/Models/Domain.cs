namespace Kestrel.Core.Models;

public sealed record Plant(string Id, string Name, string Location);

public sealed record Equipment(
    string Id,
    string Name,
    string Model,
    string Type,
    string PlantId,
    string Area);

public sealed record SourceDocument(
    string Id,
    string Title,
    string DocType,
    string SourcePath,
    string? EquipmentId,
    string? PlantId,
    string Content);

public sealed record DocumentChunk(
    string Id,
    string DocumentId,
    string Title,
    string DocType,
    string SourcePath,
    string? EquipmentId,
    string? PlantId,
    string Section,
    string Content,
    int ChunkIndex);

public sealed record RetrievedChunk(
    DocumentChunk Chunk,
    float Score,
    int Rank);

public sealed record Citation(
    int Index,
    string DocumentId,
    string Title,
    string SourcePath,
    string Section,
    string Excerpt,
    string? EquipmentId,
    string DocType);

public sealed record ChatTurn(string Role, string Content);

public sealed record ChatRequest(
    string Message,
    IReadOnlyList<ChatTurn>? History,
    string? EquipmentId,
    string? ConversationId);

public sealed record FeedbackRequest(
    string ConversationId,
    string MessageId,
    bool Helpful,
    string? Comment,
    string? TraceId,
    string? UserId = null,
    string? UserEmail = null);

public sealed record MaintenanceLog(
    long Id,
    string EquipmentId,
    DateTime PerformedAt,
    string Technician,
    string Summary,
    string WorkType,
    int Hours);

public sealed record WorkOrder(
    long Id,
    string EquipmentId,
    string PlantId,
    string Title,
    string Status,
    string Priority,
    DateTime OpenedAt,
    DateTime? DueAt);

public sealed record EvalQuestion(
    string Id,
    string Question,
    IReadOnlyList<string> ExpectedDocumentIds,
    IReadOnlyList<string> KeyFacts,
    string? EquipmentId);

public static class DocTypes
{
    public const string Manual = "manual";
    public const string Safety = "safety";
    public const string Incident = "incident";
    public const string Runbook = "runbook";
}
