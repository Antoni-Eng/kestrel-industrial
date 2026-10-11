# Kestrel Industrial — Plant Assistant

Portfolio RAG system for a fictional manufacturer. Technicians ask questions over manuals and safety procedures; answers are cited and can call tools against maintenance records.

## What it demonstrates

- **Vector database (Qdrant)** with model-aware collection naming
- **RAG pipeline**: load → chunk → embed → retrieve → rewrite → rerank → grounded generate
- **Swappable LLM profiles** in `appsettings.json` (local Ollama, remote desktop Ollama, OpenAI)
- **API keys in appsettings** under `Llm:Profiles:*:ApiKey` (see `Kestrel.Api/appsettings.Example.json`)
- **Tool-augmented answers** over SQLite maintenance / work orders
- **SSE streaming** to an Angular UI with citations and safety banners
- **Google OAuth** (ID token in the browser; JWT validation on the API)
- **Eval harness** (Hit@5 / MRR) against a golden question set
- **.NET Aspire** orchestration + OpenTelemetry

## Stack

| Layer | Choice |
| --- | --- |
| Runtime | .NET 10 |
| Orchestration | Aspire 13 |
| LLM | Ollama (`qwen2.5:7b`) or OpenAI-compatible via config |
| Embeddings | `nomic-embed-text` (768-d) |
| Vectors | Qdrant |
| Structured data | SQLite |
| UI | Angular + Angular Material |

## Prerequisites

1. .NET 10 SDK  
2. Qdrant — use the **native Windows binary** (no WSL/Docker required):
   ```powershell
   .\scripts\start-qdrant.ps1
   ```
   Docker Desktop + WSL2 is optional later if you want Aspire to host Qdrant in a container.
3. Ollama for Windows + models:
   ```bash
   ollama pull qwen2.5:7b
   ollama pull nomic-embed-text
   ```
4. Node.js 22+ / 24 LTS and Angular CLI

## Quick start

```bash
# 1) Generate synthetic company docs + SQLite
dotnet run --project Kestrel.DataGen

# 2) Start native Qdrant (no Docker)
.\scripts\start-qdrant.ps1

# 3) Ingest into Qdrant
dotnet run --project Kestrel.Ingestion

# 4) API (http://localhost:5258)
dotnet run --project Kestrel.Api

# 5) Angular UI (http://localhost:4200)
cd kestrel-web
npm run dev
```

Aspire (`dotnet run --project Kestrel.AppHost`) can orchestrate API + web; Qdrant via Docker needs WSL installed separately.

## Secrets / API keys

Committed `appsettings.json` files keep `ApiKey` empty. Put real keys in gitignored `appsettings.Development.json` or `appsettings.Local.json`, for example:

```json
{
  "Llm": {
    "ActiveChatProfile": "openai",
    "Profiles": {
      "openai": { "ApiKey": "sk-..." }
    }
  }
}
```

See `Kestrel.Api/appsettings.Example.json` for profile shapes.

## Google sign-in (optional locally)

With `Authentication:Google:ClientId` empty, the API stays open in **Development** (handy for first-time setup). Set a Client ID on both API and Angular to require Google login.

**Never commit Client IDs or ApiKeys.** They belong only in gitignored files (see below). Committed examples use placeholders only.

1. In [Google Cloud Console](https://console.cloud.google.com/apis/credentials), create an OAuth client of type **Web application**.
2. Under **Authorized JavaScript origins**, add `http://localhost:4200` (and any other UI origin you use). A redirect URI is not required for the GIS ID-token button flow.
3. Configure local secrets:

```json
// Kestrel.Api/appsettings.Development.json (gitignored)
{
  "Authentication": {
    "Google": { "ClientId": "YOUR_ID.apps.googleusercontent.com" },
    "ApiKey": "long-random-string-for-eval",
    "CorsOrigins": [ "http://localhost:4200" ]
  }
}
```

```powershell
# Angular — copy example, then paste Client ID into the new file
Copy-Item kestrel-web\public\kestrel-config.local.example.js kestrel-web\public\kestrel-config.local.js
```

`kestrel-config.local.js` sets `window.__KESTREL_CONFIG__.googleClientId` and is gitignored. Committed `environment*.ts` files keep `googleClientId: ''`.

Protected: all `/api/*` except `/api/health/llm` (plus Aspire `/health` and `/alive`). Outside Development, a missing Client ID fails startup.

**IDE browsers:** Google Sign-In does not work in Cursor’s built-in browser (sandbox blocks OAuth popups). Open http://localhost:4200/ in Chrome, Edge, or Firefox.

## Switch LLM / use a more powerful PC

Edit `Kestrel.Api/appsettings.json` (or a Local/Development overlay):

```json
"Llm": {
  "ActiveChatProfile": "desktop",
  "ActiveEmbeddingProfile": "local",
  "Profiles": {
    "desktop": {
      "Provider": "Ollama",
      "Endpoint": "http://YOUR.DESKTOP.IP:11434",
      "ChatModel": "qwen2.5:32b",
      "ApiKey": "",
      "SupportsTools": true,
      "EmbeddingDimensions": 768
    },
    "openai": {
      "Provider": "OpenAI",
      "Endpoint": "https://api.openai.com/v1",
      "ChatModel": "gpt-4o-mini",
      "EmbeddingModel": "text-embedding-3-small",
      "ApiKey": "sk-...",
      "EmbeddingDimensions": 1536
    }
  }
}
```

On the desktop: install Ollama, set `OLLAMA_HOST=0.0.0.0`, allow port `11434`.

**Embedding safety:** collection names include the embedding model and dimensions (`kestrel_docs__nomic-embed-text__768`). Changing embedding models requires re-ingestion.

## Eval

With the API running:

```bash
dotnet run --project Kestrel.Eval
```

If Google auth is enabled, pass the same key as `Authentication:ApiKey`:

```bash
$env:KESTREL_API_KEY="long-random-string-for-eval"
dotnet run --project Kestrel.Eval
# or: dotnet run --project Kestrel.Eval -- http://localhost:5258 ./data your-api-key
```

Report: `data/eval/latest-report.md`

## Solution layout

- `Kestrel.AppHost` — Aspire orchestrator  
- `Kestrel.Api` — Minimal API (`/api/chat` SSE, `/api/search`, `/api/equipment`, `/api/feedback`) + Google JWT / API-key auth  
- `Kestrel.Core` — RAG, LLM factory, chunking, stores  
- `Kestrel.DataGen` — synthetic manuals, SOPs, maintenance DB, golden questions  
- `Kestrel.Ingestion` — embed + upsert  
- `Kestrel.Eval` — retrieval metrics (uses `X-Api-Key` when auth is on)  
- `kestrel-web` — Angular chat UI (GIS login gate when Client ID configured)  
- `tests/Kestrel.Tests` — unit tests  
- `kestrel-web/public/kestrel-config.local.example.js` — template for local Google Client ID  

Local-only (gitignored): `Kestrel.Api/appsettings.Development.json`, `kestrel-web/public/kestrel-config.local.js`, and optionally `docs/TECHNICAL.md`.

## Example questions

- How do I replace the spindle bearing on CNC-204?
- What is the LOTO sequence before opening guards?
- When was CNC-204 last serviced for a bearing job?
- What open work orders exist for PLANT-NORTH?

## License

Portfolio / demo project. Synthetic data only — not for real plant use.
