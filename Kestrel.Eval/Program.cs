using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kestrel.Core.Models;

var apiBase = args.ElementAtOrDefault(0) ?? "http://localhost:5258";
var dataRoot = args.ElementAtOrDefault(1) ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data"));
var goldenPath = Path.Combine(dataRoot, "eval", "golden-questions.json");
var reportDir = Path.Combine(dataRoot, "eval");
Directory.CreateDirectory(reportDir);

if (!File.Exists(goldenPath))
{
    Console.Error.WriteLine($"Golden set not found: {goldenPath}");
    return 1;
}

var questions = JsonSerializer.Deserialize<List<EvalQuestion>>(
    await File.ReadAllTextAsync(goldenPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

using var http = new HttpClient { BaseAddress = new Uri(apiBase), Timeout = TimeSpan.FromMinutes(5) };

// Optional machine auth when API has Authentication:Google:ClientId set.
var apiKey = Environment.GetEnvironmentVariable("KESTREL_API_KEY")
    ?? args.ElementAtOrDefault(2);
if (!string.IsNullOrWhiteSpace(apiKey))
{
    http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
}

string chatProfile = "unknown";
string chatModel = "unknown";
try
{
    var model = await http.GetFromJsonAsync<JsonElement>("/api/model");
    chatProfile = model.GetProperty("chatProfile").GetString() ?? chatProfile;
    chatModel = model.GetProperty("chatModel").GetString() ?? chatModel;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"API not reachable at {apiBase}: {ex.Message}");
    return 1;
}

var hitsAt5 = 0;
var mrrSum = 0.0;
var rows = new StringBuilder();
rows.AppendLine("| Id | Hit@5 | MRR | Latency ms | Question |");
rows.AppendLine("| --- | --- | --- | --- | --- |");

foreach (var q in questions)
{
    var sw = Stopwatch.StartNew();
    var response = await http.PostAsJsonAsync("/api/search", new { query = q.Question, equipmentId = q.EquipmentId });
    sw.Stop();
    response.EnsureSuccessStatusCode();
    var hits = await response.Content.ReadFromJsonAsync<List<SearchHit>>() ?? [];

    var expected = q.ExpectedDocumentIds.Select(x => x.ToLowerInvariant()).ToHashSet();
    var rank = hits.Select((h, i) => (h, i))
        .FirstOrDefault(x => expected.Contains(x.h.DocumentId.ToLowerInvariant())).i;
    var hit = hits.Take(5).Any(h => expected.Contains(h.DocumentId.ToLowerInvariant()));
    if (hit) hitsAt5++;
    var rr = rank >= 0 && hits.Count > 0 && expected.Contains(hits[rank].DocumentId.ToLowerInvariant())
        ? 1.0 / (rank + 1)
        : 0.0;
    // Fix rank detection
    rr = 0;
    for (var i = 0; i < hits.Count; i++)
    {
        if (expected.Contains(hits[i].DocumentId.ToLowerInvariant()))
        {
            rr = 1.0 / (i + 1);
            break;
        }
    }

    mrrSum += rr;
    rows.AppendLine($"| {q.Id} | {(hit ? "Y" : "N")} | {rr:0.00} | {sw.ElapsedMilliseconds} | {q.Question.Replace("|", "/")} |");
    Console.WriteLine($"{q.Id}: hit@5={hit} mrr={rr:0.00} ({sw.ElapsedMilliseconds} ms)");
}

var hitRate = questions.Count == 0 ? 0 : (double)hitsAt5 / questions.Count;
var mrr = questions.Count == 0 ? 0 : mrrSum / questions.Count;

var report =
    $"""
    # Kestrel Eval Report

    Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC
    API: {apiBase}
    Profile: `{chatProfile}` / model `{chatModel}`
    Questions: {questions.Count}

    ## Retrieval metrics
    - **Hit@5:** {hitRate:P1} ({hitsAt5}/{questions.Count})
    - **MRR:** {mrr:0.000}

    ## Per-question

    {rows}
    """;

var reportPath = Path.Combine(reportDir, "latest-report.md");
await File.WriteAllTextAsync(reportPath, report);
Console.WriteLine($"Wrote {reportPath}");
Console.WriteLine($"Hit@5={hitRate:P1} MRR={mrr:0.000}");
return 0;

sealed record SearchHit(string DocumentId, string Title, float Score, int Rank);
