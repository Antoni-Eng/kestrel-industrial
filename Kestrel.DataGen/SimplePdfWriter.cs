using System.Text;

namespace Kestrel.DataGen;

/// <summary>Minimal PDF writer so ingestion can demonstrate PdfPig loading.</summary>
public static class SimplePdfWriter
{
    public static void WriteTextPdf(string path, string title, string body)
    {
        var lines = new List<string> { title, "" };
        foreach (var paragraph in body.Replace("\r\n", "\n").Split('\n'))
        {
            var text = paragraph.Trim();
            if (text.StartsWith('#'))
            {
                text = text.TrimStart('#', ' ');
            }

            if (text.Length == 0)
            {
                lines.Add("");
                continue;
            }

            while (text.Length > 90)
            {
                lines.Add(text[..90]);
                text = text[90..];
            }

            lines.Add(text);
        }

        var content = new StringBuilder();
        content.AppendLine("BT /F1 10 Tf 50 780 Td");
        var first = true;
        foreach (var line in lines.Take(60))
        {
            var safe = line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            if (!first)
            {
                content.AppendLine("0 -12 Td");
            }

            content.AppendLine($"({safe}) Tj");
            first = false;
        }

        content.AppendLine("ET");
        var stream = content.ToString();
        var streamBytes = Encoding.ASCII.GetBytes(stream);

        var objects = new List<string>
        {
            "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n",
            "2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj\n",
            "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources<< /Font<< /F1 5 0 R >> >> >>endobj\n",
            $"4 0 obj<< /Length {streamBytes.Length} >>stream\n{stream}endstream\nendobj\n",
            "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n"
        };

        using var fs = File.Create(path);
        using var writer = new StreamWriter(fs, Encoding.ASCII);
        writer.Write("%PDF-1.4\n");
        writer.Flush();
        var offsets = new List<long> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(fs.Position);
            writer.Write(obj);
            writer.Flush();
        }

        var xrefPos = fs.Position;
        writer.Write($"xref\n0 {objects.Count + 1}\n");
        writer.Write("0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
        {
            writer.Write($"{offsets[i]:D10} 00000 n \n");
        }

        writer.Write($"trailer<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF");
    }
}
