# Local RAG Benchmark — Cheap Serverless Profile

Benchmark methodology and the Azure free-tier go/no-go decision for the optional
`cheap-serverless` RAG profile (local gguf embeddings + file vector store + DeepSeek chat).

Covers plan Task 46 (benchmark) and Task 45 Step 1 (deployment go/no-go).

## Profile under test

| Concern | openai-postgres (default) | cheap-serverless |
|---|---|---|
| Embeddings | OpenAI `text-embedding-3-small` (1536-d) | local quantized gguf via LLamaSharp |
| Vector store | PostgreSQL + pgvector (`VectorSearchService`) | static JSON artifact (`FileVectorSearchService`) |
| Chat | OpenAI / DeepSeek | DeepSeek (`deepseek-chat`) |
| Recurring infra cost | Postgres + OpenAI usage | DeepSeek usage only |

Provider selection is wired in `RagProviderRegistration.AddRagProviders` and driven by
`EMBEDDING_PROVIDER` / `VECTOR_STORE_PROVIDER` / `LLM_PROVIDER`.

## How to run

```bash
# 1. Download a quantized gguf embedding model (e.g. a ~300 MB MiniLM/bge-small build).
#    Place it at ./models/embedding.gguf

# 2. Run the harness against a universe corpus:
scripts/benchmark-local-rag.sh \
  --universe jojo \
  --input ./input-data/jojo \
  --model ./models/embedding.gguf \
  --output ./rag-data \
  --runs 10
```

The harness reports: artifact build time, artifact size on disk, cold per-invocation retrieval
latency (p50/p95), and peak RSS. Cold model-load time, embedding dimension and managed memory are
logged by `LLamaSharpEmbeddingModel` (`LoadMilliseconds` / `Dimension` / `ManagedMemoryBytes`).

A deterministic retrieval-quality gate (no model required) runs in CI:
`tests/LoreBot.Infrastructure.Tests/RagQuality/LocalRagRetrievalTests.cs`.

## Results

> Numbers below are **not yet measured** — they require running the harness with a real gguf model,
> which is not committed to the repo (model files are large and license-bound). Fill these in from a
> single benchmark run before flipping the go/no-go decision.

### Retrieval (Task 46 Step 1)

| Metric | Value |
|---|---|
| Cold model load time | TBD ms |
| Warm retrieval latency p50 | TBD ms |
| Warm retrieval latency p95 | TBD ms |
| Memory after model load | TBD MB |
| Memory after index load | TBD MB |
| Artifact size (per universe) | TBD MB |
| Embedding dimension | TBD |
| Top-K quality on fixed queries | TBD (deterministic gate: PASS) |

### API path (Task 46 Step 2)

| Metric | Value |
|---|---|
| Full API latency with DeepSeek (p50/p95) | TBD ms |
| Prompt tokens / answer tokens | TBD / TBD |
| Cost estimate per 1K questions | TBD |
| Cost estimate per 20K questions | TBD |
| Cache hit impact | TBD |

### Embedding model comparison (Task 46 Step 3)

| Model | Size | Dimension | Cold load | Retrieval quality | Notes |
|---|---|---|---|---|---|
| ~300 MB quantized (baseline) | TBD | TBD | TBD | TBD | candidate default |
| higher-quality < 1.5 GB | TBD | TBD | TBD | TBD | accept only if quality gain justifies RAM/cold-start |

## Go / No-Go decision (Task 45 Step 1)

**Current decision: `document only`.**

Rationale (evidence available at time of writing):

1. **No benchmark numbers yet.** A real gguf model is not present, so cold-start, latency, memory and
   answer-quality numbers cannot be measured here. Committing model-packaging infrastructure to Azure
   before measuring would violate Task 45 Step 1's own guidance.
2. **IaC implemented but not validated against Azure.** The Bicep modules
   (`functions.bicep`, `postgres.bicep`, `monitoring.bicep`, `staticwebapp.bicep`) and parameter files
   are now filled in (plan Task 37), with `main.bicep` wiring the secure params. They have **not** been
   `bicep build`-compiled or `what-if`-validated here (no az/bicep CLI in the environment), and no
   deployment has run. They also describe the default openai-postgres profile, not the cheap-serverless
   model-packaging path.
3. **Cold-start risk on Consumption.** Loading a ~300 MB model into a .NET isolated worker on the
   Consumption plan risks slow cold starts; this needs measurement (and likely Flex Consumption or a
   warmed instance) before it is a credible production path.
4. **Stores still need Postgres.** Rate limiting (`RateLimitService`) and the response cache
   (`CacheService`) are backed by `AppDbContext` (Postgres). The cheap-serverless profile only removes
   the *embedding* + *vector-store* cost; it does not yet remove the Postgres dependency, so the
   "no recurring infra" benefit is partial until those are moved to a file/in-memory store.

The capability itself is implemented, tested, and demonstrable locally (provider wiring + file vector
store + offline indexer + deterministic retrieval gate). That is the portfolio value; Azure packaging
is deferred, not abandoned.

### Flip to `go` when ALL of the following hold

- Task 37 Bicep compiles (`az bicep build`) and `what-if` succeeds against a subscription.
- Harness shows cold model load + first-token latency within an acceptable budget on the chosen plan.
- Artifact + model packaging fits the chosen hosting size limit (see CI size checks below).
- Rate-limit + cache stores have a serverless-friendly backing (or Postgres cost is accepted).

### If/when `go`: implementation outline (Task 45 Steps 2–5)

- Package the model/index intentionally: deploy with the Functions app **or** download from Blob
  Storage on cold start. The API owns model/index access — never ship model files in the frontend
  bundle.
- Functions app settings: `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`, `EMBEDDING_PROVIDER=local`,
  `VECTOR_STORE_PROVIDER=file`, `LLM_PROVIDER=deepseek`, `LOCAL_EMBEDDING_MODEL_PATH`, `RAG_DATA_PATH`,
  `DEEPSEEK_API_KEY`. Use Consumption or Flex Consumption per measured model-load behavior.
- Keep frontend on Static Web Apps Free; route `/api/*` to the Functions backend.
- Add CI size checks that fail when: the Functions package exceeds the hosting limit, the RAG artifact
  exceeds its budget, or the frontend artifact accidentally includes model files.
