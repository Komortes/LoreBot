#!/usr/bin/env bash
#
# Benchmark the cheap-serverless local RAG profile (Task 46).
#
# Measures, for a given quantized embedding model + source corpus:
#   - artifact build time and chunk count
#   - artifact size on disk (the file vector store payload)
#   - cold model load time + dimension + managed memory (from the indexer log)
#   - cold per-invocation retrieval latency over a fixed query set (p50/p95)
#
# Warm in-process p50/p95 is captured in a running deployment by ObservabilityChatClient
# (OpenTelemetry); this harness measures the offline/cold path that drives Azure cold starts.
#
# Usage:
#   scripts/benchmark-local-rag.sh \
#     --universe jojo \
#     --input ./input-data/jojo \
#     --model ./models/embedding.gguf \
#     [--output ./rag-data] \
#     [--runs 10] \
#     [--query "Who is Dio Brando?"] [--query "What can Star Platinum do?"]
#
set -euo pipefail

UNIVERSE=""
INPUT_DIR=""
MODEL_PATH="${LOCAL_EMBEDDING_MODEL_PATH:-}"
OUTPUT_DIR="./rag-data"
RUNS=10
QUERIES=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --universe) UNIVERSE="$2"; shift 2 ;;
    --input) INPUT_DIR="$2"; shift 2 ;;
    --model) MODEL_PATH="$2"; shift 2 ;;
    --output) OUTPUT_DIR="$2"; shift 2 ;;
    --runs) RUNS="$2"; shift 2 ;;
    --query) QUERIES+=("$2"); shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

: "${UNIVERSE:?--universe is required}"
: "${INPUT_DIR:?--input is required}"
: "${MODEL_PATH:?--model (or LOCAL_EMBEDDING_MODEL_PATH) is required}"

if [[ ! -f "${MODEL_PATH}" ]]; then
  echo "model file not found: ${MODEL_PATH}" >&2
  echo "Download a quantized gguf embedding model first (see docs/benchmarks/local-rag.md)." >&2
  exit 1
fi

if [[ ${#QUERIES[@]} -eq 0 ]]; then
  QUERIES=("Who is the main antagonist?" "What ability stops time?" "Describe the protagonist.")
fi

ARTIFACT_PATH="${OUTPUT_DIR}/lorebot-rag-index.${UNIVERSE}.json"

# Portable monotonic milliseconds.
now_ms() { python3 -c 'import time; print(int(time.monotonic()*1000))'; }

# Portable "peak RSS in MB" wrapper around a command (best effort).
peak_rss_mb() {
  local label="$1"; shift
  if /usr/bin/time -l true >/dev/null 2>&1; then
    /usr/bin/time -l "$@" 2> >(awk '/maximum resident set size/ {printf "  %s peak RSS: %.1f MB\n", "'"${label}"'", $1/1048576 > "/dev/stderr"}')
  elif /usr/bin/time -v true >/dev/null 2>&1; then
    /usr/bin/time -v "$@" 2> >(awk '/Maximum resident set size/ {printf "  %s peak RSS: %.1f MB\n", "'"${label}"'", $NF/1024 > "/dev/stderr"}')
  else
    "$@"
  fi
}

echo "== LoreBot local RAG benchmark =="
echo "universe=${UNIVERSE} model=${MODEL_PATH} runs=${RUNS}"
echo

echo "1) Build artifact"
build_start="$(now_ms)"
peak_rss_mb "build" dotnet run -c Release --project src/LoreBot.Indexer -- build \
  --universe "${UNIVERSE}" --input "${INPUT_DIR}" --output "${OUTPUT_DIR}" --model "${MODEL_PATH}"
build_ms=$(( $(now_ms) - build_start ))
echo "  build wall time: ${build_ms} ms"

if [[ -f "${ARTIFACT_PATH}" ]]; then
  size_bytes=$(wc -c < "${ARTIFACT_PATH}" | tr -d ' ')
  printf "  artifact size: %.2f MB (%s bytes)\n" "$(echo "${size_bytes}/1048576" | bc -l)" "${size_bytes}"
fi
echo

echo "2) Cold retrieval latency over ${RUNS} runs (model reloaded each run)"
declare -a samples=()
for ((i = 1; i <= RUNS; i++)); do
  q_args=()
  for q in "${QUERIES[@]}"; do q_args+=(--query "$q"); done
  start="$(now_ms)"
  dotnet run -c Release --project src/LoreBot.Indexer -- verify \
    --artifact "${ARTIFACT_PATH}" --model "${MODEL_PATH}" --universe "${UNIVERSE}" \
    "${q_args[@]}" >/dev/null
  samples+=( $(( $(now_ms) - start )) )
done

# p50 / p95 over the cold-invocation samples.
sorted=$(printf '%s\n' "${samples[@]}" | sort -n)
count=${#samples[@]}
p_idx() { python3 -c "import math,sys; n=int(sys.argv[1]); p=float(sys.argv[2]); print(max(0, math.ceil(n*p)-1))" "$count" "$1"; }
mapfile -t arr <<< "${sorted}"
echo "  samples (ms): ${samples[*]}"
echo "  p50: ${arr[$(p_idx 0.50)]} ms"
echo "  p95: ${arr[$(p_idx 0.95)]} ms"
echo
echo "Note: read the 'LoadMilliseconds' / 'ManagedMemoryBytes' / 'Dimension' line from the indexer"
echo "log above for cold model-load time and memory. Record results in docs/benchmarks/local-rag.md."
