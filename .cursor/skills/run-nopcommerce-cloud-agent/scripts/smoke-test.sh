#!/usr/bin/env bash
set -euo pipefail

readonly base_url="${NOPCOMMERCE_URL:-http://127.0.0.1:5000}"
readonly attempts="${NOPCOMMERCE_SMOKE_ATTEMPTS:-60}"

for attempt in $(seq 1 "${attempts}"); do
  status="$(curl -L -A "Mozilla/5.0" -sS -o /tmp/nopcommerce-smoke.html -w '%{http_code}' "${base_url}/" || true)"

  if [[ "${status}" == "200" ]]; then
    title="$(python3 - <<'PY'
import re
from pathlib import Path

html = Path("/tmp/nopcommerce-smoke.html").read_text(errors="replace")
match = re.search(r"<title>(.*?)</title>", html, re.IGNORECASE | re.DOTALL)
print(match.group(1).strip() if match else "no title")
PY
)"
    echo "nopCommerce is ready: HTTP 200 (${title})"
    exit 0
  fi

  echo "Waiting for nopCommerce (${attempt}/${attempts}, HTTP ${status:-unavailable})..."
  sleep 2
done

echo "nopCommerce did not become ready at ${base_url}." >&2
exit 1
