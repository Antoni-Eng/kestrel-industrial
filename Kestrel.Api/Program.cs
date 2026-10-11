using System.Security.Claims;
using System.Text.Json;
using Kestrel.Api.Auth;
using Kestrel.Core;
using Kestrel.Core.Abstractions;
using Kestrel.Core.Configuration;
using Kestrel.Core.Llm;
using Kestrel.Core.Models;
using Kestrel.Core.Rag;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Qdrant.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddKestrelCore(builder.Configuration);
builder.Services.Configure<AuthOptions>(
    builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.PostConfigure<RagOptions>(rag =>
{
    rag.DataRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, rag.DataRoot));
    rag.SqlitePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, rag.SqlitePath));
});
builder.Services.AddOpenApi();

var authOptions = builder.Configuration
    .GetSection(AuthOptions.SectionName)
    .Get<AuthOptions>() ?? new AuthOptions();
var googleClientId = authOptions.Google.ClientId?.Trim();
var authEnabled = !string.IsNullOrWhiteSpace(googleClientId);

if (!authEnabled && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Authentication:Google:ClientId is required outside Development. " +
        "Set it in appsettings or environment variables.");
}

var corsOrigins = authOptions.CorsOrigins is { Length: > 0 }
    ? authOptions.CorsOrigins
    : ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

if (authEnabled)
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = "https://accounts.google.com";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
                ValidateAudience = true,
                ValidAudience = googleClientId,
                ValidateLifetime = true,
                NameClaimType = "name",
                RoleClaimType = ClaimTypes.Role
            };
        })
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationHandler.SchemeName,
            _ => { });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("ApiUser", policy =>
            policy.AddAuthenticationSchemes(
                    JwtBearerDefaults.AuthenticationScheme,
                    ApiKeyAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser());
    });
}

if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("qdrant")))
{
    builder.AddQdrantClient("qdrant");
}
else
{
    var host = builder.Configuration["Qdrant:Host"] ?? "localhost";
    var port = int.TryParse(builder.Configuration["Qdrant:GrpcPort"], out var p) ? p : 6334;
    var key = builder.Configuration["Qdrant:ApiKey"];
    builder.Services.AddSingleton(_ =>
        string.IsNullOrWhiteSpace(key)
            ? new QdrantClient(host, port)
            : new QdrantClient(host, port, apiKey: key));
}

builder.Services.AddHealthChecks()
    .AddCheck<LlmHealthCheck>("llm");

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseCors("frontend");

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.Logger.LogInformation("Google JWT auth enabled (audience={ClientId})", googleClientId);
}
else
{
    app.Logger.LogWarning(
        "Authentication disabled: Authentication:Google:ClientId is empty (Development only).");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var maintenance = app.Services.GetRequiredService<IMaintenanceStore>();
await maintenance.InitializeAsync();

var anonymous = app.MapGroup("/api");
anonymous.MapGet("/health/llm", async (ILlmProviderFactory llm, CancellationToken ct) =>
{
    var result = await llm.CheckHealthAsync(ct);
    return Results.Ok(result);
});

var api = app.MapGroup("/api");
if (authEnabled)
{
    api.RequireAuthorization("ApiUser");
}

api.MapGet("/model", (ILlmProviderFactory llm, IOptions<LlmOptions> options) =>
{
    var chat = llm.GetActiveChatProfile();
    var emb = llm.GetActiveEmbeddingProfile();
    return Results.Ok(new
    {
        chatProfile = options.Value.ActiveChatProfile,
        embeddingProfile = options.Value.ActiveEmbeddingProfile,
        chatModel = chat.ChatModel,
        embeddingModel = emb.EmbeddingModel,
        supportsTools = chat.SupportsTools,
        collection = llm.GetCollectionName()
    });
});

api.MapGet("/equipment", async (IMaintenanceStore store, CancellationToken ct) =>
{
    var items = await store.GetEquipmentAsync(ct);
    return Results.Ok(items);
});

api.MapPost("/search", async (SearchRequest request, IRetriever retriever, CancellationToken ct) =>
{
    var hits = await retriever.RetrieveAsync(request.Query, request.EquipmentId, ct);
    return Results.Ok(hits.Select(h => new
    {
        h.Score,
        h.Rank,
        h.Chunk.DocumentId,
        h.Chunk.Title,
        h.Chunk.Section,
        h.Chunk.DocType,
        h.Chunk.EquipmentId,
        Excerpt = h.Chunk.Content.Length > 400 ? h.Chunk.Content[..400] + "..." : h.Chunk.Content
    }));
});

api.MapGet("/documents/{documentId}", async (
    string documentId,
    IOptions<RagOptions> ragOptions,
    IDocumentLoader loader,
    CancellationToken ct) =>
{
    var root = Path.GetFullPath(ragOptions.Value.DataRoot);
    if (!Directory.Exists(root))
    {
        return Results.NotFound(new { message = "Data root not found." });
    }

    var match = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
        .Where(f => loader.CanLoad(f))
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}eval{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(f =>
        {
            var name = Path.GetFileNameWithoutExtension(f);
            return name.Equals(documentId, StringComparison.OrdinalIgnoreCase)
                   || name.StartsWith(documentId + "-", StringComparison.OrdinalIgnoreCase)
                   || name.EndsWith("-" + documentId, StringComparison.OrdinalIgnoreCase);
        })
        .OrderBy(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
        .FirstOrDefault();

    if (match is null)
    {
        return Results.NotFound(new { message = $"Document '{documentId}' not found." });
    }

    var full = Path.GetFullPath(match);
    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { message = "Path escapes data root." });
    }

    var doc = await loader.LoadAsync(full, ct);
    return Results.Ok(new
    {
        id = doc.Id,
        title = doc.Title,
        docType = doc.DocType,
        sourcePath = doc.SourcePath,
        equipmentId = doc.EquipmentId,
        plantId = doc.PlantId,
        content = doc.Content
    });
});

api.MapPost("/feedback", async (
    FeedbackRequest feedback,
    HttpContext http,
    IMaintenanceStore store,
    CancellationToken ct) =>
{
    var userId = http.User.FindFirstValue("sub")
        ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    var email = http.User.FindFirstValue("email")
        ?? http.User.FindFirstValue(ClaimTypes.Email);

    await store.SaveFeedbackAsync(
        feedback with { UserId = userId, UserEmail = email },
        ct);
    return Results.Accepted();
});

var sseJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

api.MapPost("/chat", async (
    HttpContext http,
    ChatRequest request,
    IAnswerService answers,
    CancellationToken ct) =>
{
    http.Response.Headers.ContentType = "text/event-stream";
    http.Response.Headers.CacheControl = "no-cache";
    http.Response.Headers.Connection = "keep-alive";

    await foreach (var evt in answers.AnswerAsync(request, ct))
    {
        var (eventName, payload) = evt switch
        {
            TokenEvent t => ("token", JsonSerializer.SerializeToElement(new { text = t.Text }, sseJson)),
            CitationEvent c => ("citation", JsonSerializer.SerializeToElement(c.Citation, sseJson)),
            ToolEvent t => ("tool", JsonSerializer.SerializeToElement(new { name = t.Name, detail = t.Detail }, sseJson)),
            SafetyEvent s => ("safety", JsonSerializer.SerializeToElement(new { message = s.Message }, sseJson)),
            ModelInfoEvent m => ("model", JsonSerializer.SerializeToElement(new { profile = m.Profile, model = m.Model, supportsTools = m.SupportsTools }, sseJson)),
            DoneEvent d => ("done", JsonSerializer.SerializeToElement(new { messageId = d.MessageId, conversationId = d.ConversationId }, sseJson)),
            ErrorEvent e => ("error", JsonSerializer.SerializeToElement(new { message = e.Message }, sseJson)),
            _ => ("message", JsonSerializer.SerializeToElement(new { }, sseJson))
        };

        await http.Response.WriteAsync($"event: {eventName}\n", ct);
        await http.Response.WriteAsync($"data: {payload}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }
});

app.MapGet("/", () => Results.Ok(new
{
    service = "Kestrel.Api",
    docs = "/openapi/v1.json",
    auth = authEnabled ? "google+apikey" : "disabled"
}));

app.Run();

public sealed record SearchRequest(string Query, string? EquipmentId);

public sealed class LlmHealthCheck(ILlmProviderFactory llm) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await llm.CheckHealthAsync(cancellationToken);
        return result.ChatReachable && result.EmbeddingReachable
            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                $"{result.ChatModel} / {result.EmbeddingModel}")
            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(result.Error ?? "LLM probe failed");
    }
}

public partial class Program;
