using System.Text.Json;
using System.Text.RegularExpressions;
using Kestrel.Core.Abstractions;
using Kestrel.Core.Models;
using UglyToad.PdfPig;

namespace Kestrel.Core.Ingestion;

public sealed class MarkdownDocumentLoader : IDocumentLoader
{
    public bool CanLoad(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    public async Task<SourceDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(path, cancellationToken);
        return MetadataParser.ToDocument(path, content);
    }
}

public sealed class PdfDocumentLoader : IDocumentLoader
{
    public bool CanLoad(string path) =>
        path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<SourceDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var document = PdfDocument.Open(path);
        var text = string.Join("\n\n", document.GetPages().Select(p => p.Text));
        return Task.FromResult(MetadataParser.ToDocument(path, text));
    }
}

public sealed class JsonDocumentLoader : IDocumentLoader
{
    public bool CanLoad(string path) =>
        path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    public async Task<SourceDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;

        var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? Path.GetFileName(path) : Path.GetFileName(path);
        var content = root.TryGetProperty("content", out var c) ? c.GetString() ?? root.ToString() : root.ToString();
        var docType = root.TryGetProperty("docType", out var d) ? d.GetString() ?? DocTypes.Runbook : d.GetString()!;
        var equipmentId = root.TryGetProperty("equipmentId", out var e) ? e.GetString() : null;
        var plantId = root.TryGetProperty("plantId", out var p) ? p.GetString() : null;
        var id = root.TryGetProperty("id", out var i) ? i.GetString() ?? Path.GetFileNameWithoutExtension(path)
            : Path.GetFileNameWithoutExtension(path);

        return new SourceDocument(id!, title, docType!, path, equipmentId, plantId, content!);
    }
}

public sealed class CompositeDocumentLoader(IEnumerable<IDocumentLoader> loaders) : IDocumentLoader
{
    private readonly IReadOnlyList<IDocumentLoader> _loaders = loaders.ToList();

    public bool CanLoad(string path) => _loaders.Any(l => l.CanLoad(path));

    public Task<SourceDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var loader = _loaders.FirstOrDefault(l => l.CanLoad(path))
            ?? throw new NotSupportedException($"No loader registered for '{path}'.");
        return loader.LoadAsync(path, cancellationToken);
    }
}

internal static partial class MetadataParser
{
    public static SourceDocument ToDocument(string path, string content)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var title = ExtractTitle(content) ?? fileName.Replace('-', ' ').Replace('_', ' ');
        var docType = InferDocType(path, content);
        var equipmentId = ExtractEquipmentId(content) ?? ExtractEquipmentId(fileName);
        var plantId = ExtractPlantId(content) ?? ExtractPlantId(path);
        var id = fileName.ToLowerInvariant();

        return new SourceDocument(id, title, docType, path, equipmentId, plantId, content);
    }

    private static string? ExtractTitle(string content)
    {
        var match = TitleLine().Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string InferDocType(string path, string content)
    {
        var lower = (path + " " + content[..Math.Min(400, content.Length)]).ToLowerInvariant();
        if (lower.Contains("safety") || lower.Contains("lockout") || lower.Contains("loto") || lower.Contains("ppe"))
        {
            return DocTypes.Safety;
        }

        if (lower.Contains("incident"))
        {
            return DocTypes.Incident;
        }

        if (lower.Contains("runbook") || lower.Contains("procedure"))
        {
            return DocTypes.Runbook;
        }

        return DocTypes.Manual;
    }

    private static string? ExtractEquipmentId(string text)
    {
        var match = EquipmentId().Match(text);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string? ExtractPlantId(string text)
    {
        var match = PlantId().Match(text);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    [GeneratedRegex(@"^#\s+(.*)$", RegexOptions.Multiline)]
    private static partial Regex TitleLine();

    [GeneratedRegex(@"\b([A-Z]{2,5}-\d{2,4})\b")]
    private static partial Regex EquipmentId();

    [GeneratedRegex(@"\b(PLANT-[A-Z0-9]+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlantId();
}
