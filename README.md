# LoreBot

A **RAG chatbot** that answers questions about anime/game universes using content retrieved from official wikis: grounded answers, source cards, structured response types, and guardrails against prompt injection.

Built on a production-oriented .NET + React stack with a pluggable provider system — swap the LLM, embedding model, and vector store via config without touching application code.

---

## Status

| Feature | State |
|---|---|
| JoJo's Bizarre Adventure — indexed & queryable | ✅ Working |
| Conversation context (multi-turn follow-ups) | ✅ Working |
| Source cards for retrieved wiki context | ✅ Working |
| OpenAI embeddings + pgvector RAG profile | ✅ Working |
| DeepSeek chat (OpenAI-compatible) | ✅ Working |
| Cheap-serverless profile (local gguf + file vector store) | ✅ Implemented, benchmark pending |
| Prompt-injection & jailbreak guardrails | ✅ Working |
| Output grounding & hallucination flagging | ✅ Working |
| Per-IP rate limiting | ✅ Postgres-backed or in-memory, depending on profile |
| Response cache (vector similarity) | ✅ Postgres profile |
| Admin indexing API | ✅ Working |
| Persona 5 & Chainsaw Man — not yet indexed | 🔄 In progress |
| Azure deployment | 🔄 IaC exists, deploy not validated yet |
| Evaluation quality gates | 🔄 In progress |
| CI/CD pipelines | 🔄 In progress |

---

## How it works

```
User question
    │
    ▼
Embed question (OpenAI text-embedding-3-small or local gguf)
    │
    ▼
Vector search → top-K similar wiki chunks (pgvector or file artifact)
    │
    ▼
Assemble context block + conversation history
    │
    ▼
IChatClient middleware pipeline
  UseFunctionInvocation → ObservabilityClient → GuardRailsClient → LoggingClient
    │
    ▼
LLM (DeepSeek / OpenAI) → structured JSON response
  { type, answer, confidence, cards }
    │
    ▼
React SPA renders answer, structured cards, and clickable source cards
```

Wiki content is ingested via MediaWiki API → `WikiScraper` → `TextChunker` (512-token chunks, 64-token overlap) → `IndexingPipeline` → pgvector embeddings. For the cheap-serverless profile, `LoreBot.Indexer` builds a static JSON RAG artifact offline, then Functions loads that artifact at runtime.

---

## Tech stack

| Layer | Technology |
|---|---|
| Backend | C# .NET 10, Azure Functions Isolated v4 |
| AI abstraction | `Microsoft.Extensions.AI` 9.5 (`IChatClient` pipeline) |
| LLM | DeepSeek `deepseek-chat` (OpenAI-compatible) / `gpt-4o-mini` |
| Embeddings | OpenAI `text-embedding-3-small` / local gguf via LLamaSharp |
| Vector store | PostgreSQL 16 + `pgvector` / file-based JSON artifact |
| ORM | EF Core 9 + Npgsql |
| Frontend | React 19, Vite, TypeScript, Tailwind CSS v4 |
| Observability | OpenTelemetry → Azure Monitor / Application Insights |
| Tests | xUnit, Vitest, `Microsoft.Extensions.AI.Evaluation` |
| IaC | Bicep (Azure Functions Consumption + Flexible PostgreSQL + Static Web App) |

---

## Provider profiles

Configured via environment variables — swap everything without changing code:

| Profile | Embeddings | Vector store | LLM | Notes |
|---|---|---|---|---|
| `openai-postgres` | `openai` | `postgres` | `openai` or `deepseek` | Default, cloud RAG |
| `cheap-serverless` | `local` (gguf) | `file` (JSON artifact) | `deepseek` | No cloud embedding/vector DB cost; uses per-instance soft rate limit and no-op cache |

---

## Project structure

```
src/
  LoreBot.Core/          Domain models, RAG orchestration, IChatClient middleware, guardrails
  LoreBot.Infrastructure/ EF Core, pgvector, embeddings, ingestion pipeline, file vector store
  LoreBot.Functions/     Azure Functions HTTP edge, DI composition root
  LoreBot.Indexer/       Offline CLI: build static RAG artifacts for cheap-serverless profile
  LoreBot.Evaluations/   AI.Evaluation quality tests (retrieval + answer accuracy)
  LoreBot.Web/           React SPA (chat UI, inline citations, dark mode)
tests/                   xUnit unit + retrieval-quality tests
infra/                   Bicep IaC
```

---

## Commands

| Command | Description |
|---|---|
| `dotnet restore LoreBot.slnx` | Restore .NET dependencies |
| `dotnet build LoreBot.slnx --no-restore` | Build backend, Functions, indexer, and evaluations |
| `dotnet test LoreBot.slnx --no-restore --disable-build-servers` | Run .NET tests |
| `cd src/LoreBot.Web && npm ci` | Install frontend dependencies |
| `cd src/LoreBot.Web && npm run lint` | Run frontend lint |
| `cd src/LoreBot.Web && npm test` | Run frontend unit tests |
| `cd src/LoreBot.Web && npm run build` | Build frontend |
| `scripts/benchmark-local-rag.sh --universe jojo --input ./input-data/jojo --model ./models/embedding.gguf` | Benchmark local gguf + file-vector RAG profile |

---

## Local development

### Prerequisites

- .NET 10 SDK
- Node.js 22+
- Docker (for PostgreSQL + pgvector)
- Azure Functions Core Tools v4
- OpenAI API key for the default embedding profile
- DeepSeek API key if `LLM_PROVIDER=deepseek`
- Optional local `.gguf` embedding model for the cheap-serverless profile

### 1. Start PostgreSQL for the default profile

```bash
docker compose up -d postgres
```

### 2. Apply migrations

```bash
dotnet ef database update --project src/LoreBot.Infrastructure \
  --startup-project src/LoreBot.Functions
```

### 3. Configure

```bash
cp src/LoreBot.Functions/local.settings.json.example \
   src/LoreBot.Functions/local.settings.json
# Edit provider settings and keys.
# Default profile: add OPENAI_API_KEY and DATABASE_CONNECTION_STRING.
# DeepSeek chat: add DEEPSEEK_API_KEY and set LLM_PROVIDER=deepseek.
```

### 4. Seed universes and index content

```bash
# Start the API first (step 5), then:
curl -X POST http://localhost:7071/api/manage/index \
  -H "Content-Type: application/json" \
  -H "x-admin-key: local-admin-secret" \
  -d '{
    "universe": "jojo",
    "wikiApiUrl": "https://jojo.fandom.com/api.php",
    "titles": ["Dio Brando","Jotaro Kujo","Stand","Yoshikage Kira","Giorno Giovanna"]
  }'
```

Pass `"titles": [...]` to index specific pages, or omit it with `"maxPages": 100` to crawl alphabetically (skips `#REDIRECT` pages automatically).

### 5. Run the API

```bash
cd src/LoreBot.Functions && func start
```

### 6. Run the frontend

```bash
cd src/LoreBot.Web && npm install && npm run dev
# → http://localhost:5173
```

---

## HTTP API

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/api/chat` | — | `{ universe, message, sessionId?, history? }` → cited answer |
| `GET` | `/api/universes` | — | List active universes |
| `GET` | `/api/universes/{slug}/stats` | — | Chunk count + metadata for a universe |
| `POST` | `/api/manage/index` | `x-admin-key` | Trigger wiki ingestion |
| `GET` | `/api/health` | — | `{ status, database }` |

`/api/chat` supports multi-turn context via `history`:

```json
{
  "universe": "jojo",
  "message": "What are his abilities?",
  "sessionId": "abc123",
  "history": [
    { "role": "user", "content": "Who is Dio Brando?" },
    { "role": "assistant", "content": "Dio Brando is the main antagonist..." }
  ]
}
```

---

## Configuration

| Variable | Required | Description |
|---|---|---|
| `LLM_PROVIDER` | ✅ | `openai` \| `deepseek` |
| `EMBEDDING_PROVIDER` | ✅ | `openai` \| `local` |
| `VECTOR_STORE_PROVIDER` | ✅ | `postgres` \| `file` |
| `OPENAI_API_KEY` | When using OpenAI | Embeddings and/or chat |
| `DEEPSEEK_API_KEY` | When `LLM_PROVIDER=deepseek` | DeepSeek chat |
| `DATABASE_CONNECTION_STRING` | When `VECTOR_STORE_PROVIDER=postgres` | PostgreSQL, pgvector, rate limiting, and response cache |
| `ADMIN_API_KEY` | ✅ | Protects `/api/manage/index` |
| `LOCAL_EMBEDDING_MODEL_PATH` | When `EMBEDDING_PROVIDER=local` | Path to `.gguf` model |
| `RAG_DATA_PATH` | When `VECTOR_STORE_PROVIDER=file` | Path to built artifact |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Optional | OpenTelemetry export |

### Cheap-serverless profile

Use this profile when you want to prebuild embeddings locally and run the API without a vector database:

```bash
export EMBEDDING_PROVIDER=local
export VECTOR_STORE_PROVIDER=file
export LLM_PROVIDER=deepseek
export LOCAL_EMBEDDING_MODEL_PATH=./models/embedding.gguf
export RAG_DATA_PATH=./rag-data/lorebot-rag-index.jojo.json
export DEEPSEEK_API_KEY=...
```

Tradeoffs:
- Retrieval can run without Postgres.
- Rate limiting is a soft in-memory per-instance guard.
- Response cache is disabled (`NoOpCacheService`).
- Real Azure packaging still needs benchmark data for cold start, memory, and artifact size.

---

## Security & guardrails

Protection is layered across the `IChatClient` middleware pipeline and the HTTP edge.

**Input guardrails** (`InputGuardRails`) — checked before the request reaches the LLM:
- Jailbreak / prompt-injection patterns (`"ignore previous instructions"`, `"you are now"`, `"forget your system prompt"`, Russian equivalents, etc.)
- NSFW keyword list

**Output guardrails** (`OutputGuardRails`) — checked on every LLM response:
- Model-disclaimer detection (`"as a language model"`, `"как языковая модель"`) — flags responses where the model broke character
- Citation presence check — warns if the answer contains no `[N]` source reference
- Grounding check — token-overlap heuristic between the answer and the retrieved context, flags likely hallucinations

**HTTP edge** (`ChatFunction`):
- Per-IP rate limiting — Postgres-backed in the default profile, in-memory soft limit in the cheap-serverless profile. Blocked requests return a `rate_limited` structured response, never a 429
- `ADMIN_API_KEY` header required on `/api/manage/index`

All blocks and flags are logged with structured fields (`LoreBot.GuardRailBlocked`, `LoreBot.OutputFlagged`) for observability.

---

## Deployment

Bicep templates in `infra/` provision:
- Azure Functions (Consumption plan)
- Azure Database for PostgreSQL Flexible Server with `pgvector`
- Azure Static Web Apps
- Log Analytics + Application Insights

> Azure deployment is not yet live. IaC exists, but the deployment path still needs subscription-level validation (`bicep build` / `what-if`) and real cheap-serverless benchmarks before using that profile in production.

---

## Roadmap

- [ ] Index Persona 5 and Chainsaw Man universes
- [ ] Deploy to Azure (IaC ready)
- [ ] Set up GitHub Actions CI/CD
- [ ] Evaluation quality gates (`Microsoft.Extensions.AI.Evaluation`)
- [ ] Fill in real cheap-serverless benchmark numbers
- [ ] Streaming responses (SSE)
