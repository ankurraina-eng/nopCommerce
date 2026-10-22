#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "${repo_root}"

export PATH="/usr/local/bin:${PATH}"

if ! pg_isready -q; then
  sudo service postgresql start
fi

for _ in $(seq 1 30); do
  if pg_isready -q; then
    break
  fi
  sleep 1
done

if ! pg_isready -q; then
  echo "PostgreSQL did not become ready within 30 seconds." >&2
  exit 1
fi

if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname = 'nopcommerce'" | grep -q 1; then
  sudo -u postgres psql -v ON_ERROR_STOP=1 \
    -c "CREATE USER nopcommerce WITH PASSWORD 'nopcommerce' CREATEDB;"
fi

if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname = 'nopcommerce'" | grep -q 1; then
  sudo -u postgres createdb -O nopcommerce nopcommerce
fi

sudo -u postgres psql -d nopcommerce -v ON_ERROR_STOP=1 \
  -c "CREATE EXTENSION IF NOT EXISTS citext; CREATE EXTENSION IF NOT EXISTS pgcrypto;"

exec dotnet run \
  --project src/Presentation/Nop.Web/Nop.Web.csproj \
  --no-launch-profile \
  --urls http://0.0.0.0:5000
