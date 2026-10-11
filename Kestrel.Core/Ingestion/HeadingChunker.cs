using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Kestrel.Core.Abstractions;
using Kestrel.Core.Configuration;
using Kestrel.Core.Models;
using Microsoft.Extensions.Options;

namespace Kestrel.Core.Ingestion;

public sealed partial class HeadingChunker(IOptions<RagOptions> options) : IChunker
{
    private readonly RagOptions _options = options.Value;

    public IReadOnlyList<DocumentChunk> Chunk(SourceDocument document)
    {
        var sections = SplitByHeadings(document.Content);
        var chunks = new List<DocumentChunk>();
        var index = 0;

        foreach (var section in sections)
        {
            foreach (var piece in SplitBySize(section.Body, _options.ChunkSizeTokens, _options.ChunkOverlapTokens))
            {
                if (string.IsNullOrWhiteSpace(piece))
                {
                    continue;
                }

                var content = string.IsNullOrWhiteSpace(section.Heading)
                    ? piece.Trim()
                    : $"## {section.Heading}\n\n{piece.Trim()}";

                var id = CreateChunkId(document.Id, index, content);
                chunks.Add(new DocumentChunk(
                    id,
                    document.Id,
                    document.Title,
                    document.DocType,
                    document.SourcePath,
                    document.EquipmentId,
                    document.PlantId,
                    section.Heading,
                    content,
                    index));
                index++;
            }
        }

        return chunks;
    }

    private static List<(string Heading, string Body)> SplitByHeadings(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var sections = new List<(string Heading, string Body)>();
        var currentHeading = "Overview";
        var body = new StringBuilder();

        foreach (var line in lines)
        {
            var match = HeadingLine().Match(line);
            if (match.Success)
            {
                if (body.Length > 0)
                {
                    sections.Add((currentHeading, body.ToString()));
                    body.Clear();
                }

                currentHeading = match.Groups[1].Value.Trim();
            }
            else
            {
                body.AppendLine(line);
            }
        }

        if (body.Length > 0)
        {
            sections.Add((currentHeading, body.ToString()));
        }

        if (sections.Count == 0)
        {
            sections.Add(("Overview", content));
        }

        return sections;
    }

    private static IEnumerable<string> SplitBySize(string text, int sizeTokens, int overlapTokens)
    {
        // Approximate tokens as ~4 chars.
        var size = Math.Max(200, sizeTokens * 4);
        var overlap = Math.Max(0, overlapTokens * 4);
        var normalized = Whitespace().Replace(text.Trim(), " ");

        if (normalized.Length <= size)
        {
            yield return normalized;
            yield break;
        }

        var start = 0;
        while (start < normalized.Length)
        {
            var length = Math.Min(size, normalized.Length - start);
            var end = start + length;

            if (end < normalized.Length)
            {
                var space = normalized.LastIndexOf(' ', end);
                if (space > start + size / 2)
                {
                    end = space;
                }
            }

            yield return normalized[start..end].Trim();
            if (end >= normalized.Length)
            {
                yield break;
            }

            start = Math.Max(end - overlap, start + 1);
        }
    }

    private static string CreateChunkId(string documentId, int index, string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{documentId}|{index}|{content}"));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    [GeneratedRegex(@"^#{1,3}\s+(.*)$")]
    private static partial Regex HeadingLine();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
