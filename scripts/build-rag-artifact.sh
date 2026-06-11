#!/usr/bin/env bash
set -euo pipefail

UNIVERSE="${1:?universe slug required}"
INPUT_DIR="${2:?input directory required}"
OUTPUT_DIR="${3:?output directory required}"
MODEL_PATH="${4:-${LOCAL_EMBEDDING_MODEL_PATH:-}}"

if [[ -z "${MODEL_PATH}" ]]; then
  echo "model path required as argument 4 or LOCAL_EMBEDDING_MODEL_PATH" >&2
  exit 1
fi

dotnet run --project src/LoreBot.Indexer -- build \
  --universe "${UNIVERSE}" \
  --input "${INPUT_DIR}" \
  --output "${OUTPUT_DIR}" \
  --model "${MODEL_PATH}"

if [[ -n "${VERIFY_QUERY:-}" ]]; then
  dotnet run --project src/LoreBot.Indexer -- verify \
    --artifact "${OUTPUT_DIR}/lorebot-rag-index.${UNIVERSE}.json" \
    --model "${MODEL_PATH}" \
    --universe "${UNIVERSE}" \
    --query "${VERIFY_QUERY}"
fi
