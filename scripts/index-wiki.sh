#!/usr/bin/env bash
set -euo pipefail
# Usage: ADMIN_API_KEY=... FUNCTIONS_URL=http://localhost:7071 ./scripts/index-wiki.sh <slug> <wiki-api-url> [maxPages]
SLUG="${1:?slug required}"
WIKI_API="${2:?wiki api url required}"
MAX_PAGES="${3:-50}"
curl -fsS -X POST "${FUNCTIONS_URL:-http://localhost:7071}/api/admin/index" \
  -H "x-admin-key: ${ADMIN_API_KEY:?ADMIN_API_KEY required}" \
  -H 'Content-Type: application/json' \
  -d "{\"universe\":\"${SLUG}\",\"wikiApiUrl\":\"${WIKI_API}\",\"maxPages\":${MAX_PAGES}}"
echo
