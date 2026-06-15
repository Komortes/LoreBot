# LoreBot

A production-grade **RAG (Retrieval-Augmented Generation) lore chatbot** that answers questions about
anime/game universes — **JoJo's Bizarre Adventure**, **Persona 5**, and **Chainsaw Man** — with
grounded, **cited** answers retrieved from official wikis.

Ask *"Who is Dio Brando?"* and get an answer assembled only from retrieved source chunks, with
inline `[N]` citations and source cards — never hallucinated lore.

---

## Highlights

- **Grounded RAG pipeline** — query → embed → vector search → context assembly → cited answer.
- **`Microsoft.Extensions.AI` `IChatClient` middleware pipeline** — observability → guardrails →
  logging, composed around any chat backend.
- **Pluggable provider profiles** — swap embeddings, vector store, and LLM by configuration
  (OpenAI + pgvector, or a fully local gguf + file-vector-store "cheap-serverless" profile).
- **Structured, component-driven responses** — the model returns a typed JSON envelope
  (`answer` / `character` / `timeline` / `comparison` / …) that the React UI renders as cards.
- **Guardrails, rate limiting, and response caching** built in.
- **Quality gates** — deterministic retrieval tests + `Microsoft.Extensions.AI.Evaluation` cases in CI.
- **Observability** — OpenTelemetry traces → Azure Monitor.

## Architecture

```
React SPA  ──HTTP──▶  Azure Functions (.NET 10 isolated)
                          │
                          ▼
                 IChatService (RAG orchestration)
   embed query ─▶ vector search ─▶ assemble context ─▶ IChatClient pipeline ─▶ cited answer
                          │                                  │
                   IEmbeddingService                 observability → guardrails → logging
                   IVectorSearchService                       │
                          │                                  ▼
              pgvector  OR  file artifact            OpenAI / DeepSeek (chat)
```

Content is ingested from MediaWiki APIs (`WikiScraper` → `TextChunker` → `IndexingPipeline`) into the
vector store. Rate limiting is enforced **once**, per-IP at the HTTP edge.

## Tech stack

C# **.NET 10**, Azure Functions (Isolated v4), `Microsoft.Extensions.AI` 9.5, OpenAI
(`gpt-4o-mini` + `text-embedding-3-small`) / DeepSeek (`deepseek-chat`), EF Core 9 + Npgsql +
`pgvector`, LLamaSharp (local gguf embeddings), React 19 + Vite + TypeScript + Tailwind 4,
OpenTelemetry → Azure Monitor, xUnit + `Microsoft.Extensions.AI.Evaluation`, Bicep, GitHub Actions.

## Provider profiles

Selected via environment variables (`RagProviderRegistration.AddRagProviders`):

| Profile | `EMBEDDING_PROVIDER` | `VECTOR_STORE_PROVIDER` | `LLM_PROVIDER` | Use case |
|---|---|---|---|---|
| **openai-postgres** (default) | `openai` | `postgres` | `openai` / `deepseek` | Cloud RAG with pgvector |
| **cheap-serverless** | `local` | `file` | `deepseek` | Local gguf embeddings + static JSON index, no OpenAI/pgvector cost |
| **dev-local** (fallback) | `openai` (no key) | `postgres` | — | Key-free local dev via hash-projection embedder |

See [`docs/benchmarks/local-rag.md`](docs/benchmarks/local-rag.md) for the cheap-serverless benchmark
methodology and the Azure deployment go/no-go decision.

## Project structure

```
src/
  LoreBot.Core/            Domain models, abstractions, RAG orchestration, guardrails, prompt builder
  LoreBot.Infrastructure/  EF Core + pgvector, embeddings, ingestion, file vector store, provider DI
  LoreBot.Functions/       Azure Functions HTTP edge + DI composition root
  LoreBot.Indexer/         Offline CLI: build/verify static RAG artifacts (cheap-serverless)
  LoreBot.Evaluations/     Microsoft.Extensions.AI.Evaluation quality tests
  LoreBot.Web/             React 19 + Vite + Tailwind SPA
tests/                     xUnit unit + retrieval-quality tests
infra/                     Bicep IaC (modules + parameters)
scripts/                   DB seeds, wiki indexing, artifact build, benchmark
```

## Getting started

### Prerequisites
- .NET 10 SDK, Node.js 22, a PostgreSQL 16 instance with the `vector` extension.

### 1. Database
```bash
# Apply EF migrations, then seed the three universes:
dotnet ef database update --project src/LoreBot.Infrastructure
psql "$DATABASE_CONNECTION_STRING" -f scripts/seed-jojo.sql
psql "$DATABASE_CONNECTION_STRING" -f scripts/seed-persona.sql
psql "$DATABASE_CONNECTION_STRING" -f scripts/seed-chainsaw-man.sql
```

### 2. Configure
```bash
cp src/LoreBot.Functions/local.settings.json.example src/LoreBot.Functions/local.settings.json
# Fill in keys / connection string and pick a provider profile.
```

### 3. Run the API
```bash
cd src/LoreBot.Functions && func start    # Azure Functions Core Tools
```

### 4. Run the web app
```bash
cd src/LoreBot.Web && npm install && npm run dev
```

### 5. Index content (populate the vector store)
```bash
scripts/index-wiki.sh           # MediaWiki → pgvector (openai-postgres profile)
```

## Configuration

| Variable | Description |
|---|---|
| `EMBEDDING_PROVIDER` | `openai` \| `local` |
| `VECTOR_STORE_PROVIDER` | `postgres` \| `file` |
| `LLM_PROVIDER` | `openai` \| `deepseek` |
| `OPENAI_API_KEY` | OpenAI key (embeddings + chat) |
| `DEEPSEEK_API_KEY` | DeepSeek key (required when `LLM_PROVIDER=deepseek`) |
| `LOCAL_EMBEDDING_MODEL_PATH` | gguf model path (required when `EMBEDDING_PROVIDER=local`) |
| `RAG_DATA_PATH` | RAG artifact path/dir (required when `VECTOR_STORE_PROVIDER=file`) |
| `DATABASE_CONNECTION_STRING` | PostgreSQL (also backs rate limiting + response cache) |
| `ADMIN_API_KEY` | Protects the admin index endpoint |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Azure Monitor / OpenTelemetry export |

## HTTP API

All routes are served under `/api`:

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/chat` | Ask a question: `{ universe, message, sessionId? }` → cited structured answer |
| `GET`  | `/api/universes` | List active universes |
| `GET`  | `/api/universes/{slug}/stats` | Indexed document stats for a universe |
| `POST` | `/api/manage/index` | Admin: trigger ingestion (requires `ADMIN_API_KEY`) |
| `GET`  | `/api/health` | Health check |

## Offline indexer (cheap-serverless)

Build a static, self-contained vector artifact from local text using a quantized gguf model:

```bash
# Build:
dotnet run --project src/LoreBot.Indexer -- build \
  --universe jojo --input ./input-data/jojo --output ./rag-data \
  --model ./models/embedding.gguf

# Verify retrieval:
dotnet run --project src/LoreBot.Indexer -- verify \
  --artifact ./rag-data/lorebot-rag-index.jojo.json \
  --model ./models/embedding.gguf --universe jojo \
  --query "Who is Dio Brando?"
```

The artifact embeds an embedding-model hash + dimension; `FileVectorSearchService` validates them at
load time so a mismatched model/index pair fails fast.

## Testing

```bash
dotnet test LoreBot.slnx                 # .NET unit + retrieval-quality + evaluation tests
cd src/LoreBot.Web && npm test           # frontend (Vitest)
scripts/benchmark-local-rag.sh ...       # local RAG cold-load / latency / memory benchmark
```

## Deployment

- **Infrastructure:** `infra/main.bicep` provisions Postgres (flexible server + `vector`), Function
  App (Consumption), Static Web App (Free), and Log Analytics + Application Insights.
- **CI/CD:** `.github/workflows/` builds + tests both stacks, runs evaluation gates, and deploys the
  Functions app and Static Web App on `main`.

> **Note:** the Azure *cheap-serverless* (local-model) deployment profile is intentionally deferred to
> `document-only` pending benchmarks — see [`docs/benchmarks/local-rag.md`](docs/benchmarks/local-rag.md).
> The committed Bicep targets the default openai-postgres profile.
