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
        var docType = root.TryGetProperty("docType", out var d) ? d.GetString() ?? DocTypes.Runbook : DocTypes.Runbook;
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

/// <summary>
/// Reads metadata from explicit header lines ("Document type:", "Equipment:", "Plant:") and the
/// containing folder. Body text is never scanned for IDs: documents routinely cross-reference
/// other SOPs and equipment, which would mis-tag them.
/// </summary>
internal static partial class MetadataParser
{
    private static readonly Dictionary<string, string> FolderDocTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["manuals"] = DocTypes.Manual,
        ["safety"] = DocTypes.Safety,
        ["incidents"] = DocTypes.Incident,
        ["runbooks"] = DocTypes.Runbook
    };

    private static readonly HashSet<string> KnownDocTypes = new(FolderDocTypes.Values, StringComparer.OrdinalIgnoreCase);

    public static SourceDocument ToDocument(string path, string content)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var title = ExtractTitle(content) ?? fileName.Replace('-', ' ').Replace('_', ' ');
        var docType = ResolveDocType(path, content);
        var equipmentId = Header(EquipmentHeader(), content) ?? EquipmentFromFileName(fileName);
        var plantId = Header(PlantHeader(), content);
        var id = fileName.ToLowerInvariant();

        return new SourceDocument(id, title, docType, path, equipmentId?.ToUpperInvariant(), plantId?.ToUpperInvariant(), content);
    }

    private static string? ExtractTitle(string content)
    {
        var match = TitleLine().Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string ResolveDocType(string path, string content)
    {
        var declared = Header(DocTypeHeader(), content)?.ToLowerInvariant();
        if (declared is not null && KnownDocTypes.Contains(declared))
        {
            return declared;
        }

        var folder = Path.GetFileName(Path.GetDirectoryName(path));
        return folder is not null && FolderDocTypes.TryGetValue(folder, out var fromFolder)
            ? fromFolder
            : DocTypes.Manual;
    }

    private static string? EquipmentFromFileName(string fileName)
    {
        var match = EquipmentFileName().Match(fileName);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? Header(Regex pattern, string content)
    {
        var match = pattern.Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex(@"^#\s+(.*)$", RegexOptions.Multiline)]
    private static partial Regex TitleLine();

    [GeneratedRegex(@"^\s*Document type:\s*([A-Za-z]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex DocTypeHeader();

    [GeneratedRegex(@"^\s*Equipment:\s*([A-Za-z]{2,5}-\d{1,4})\b", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex EquipmentHeader();

    [GeneratedRegex(@"^\s*Plant:\s*(PLANT-[A-Za-z0-9]+)\b", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PlantHeader();

    [GeneratedRegex(@"^([A-Za-z]{2,5}-\d{1,4})-(?:manual|runbook)$", RegexOptions.IgnoreCase)]
    private static partial Regex EquipmentFileName();
}
