#!/usr/bin/env bash
set -euo pipefail

readonly DOTNET_VERSION="10.0.401"
readonly DOTNET_ROOT="/usr/local/share/dotnet"

repo_root="$(git rev-parse --show-toplevel)"
cd "${repo_root}"

export DEBIAN_FRONTEND=noninteractive
export PATH="/usr/local/bin:${PATH}"

if ! command -v psql >/dev/null 2>&1 || ! ldconfig -p | grep -q libgdiplus; then
  sudo apt-get update
  sudo apt-get install -y \
    ca-certificates \
    curl \
    libc6-dev \
    libgdiplus \
    postgresql \
    postgresql-contrib
fi

if ! "${DOTNET_ROOT}/dotnet" --list-sdks 2>/dev/null | grep -q "^${DOTNET_VERSION}"; then
  sdk_archive="$(mktemp)"
  trap 'rm -f "${sdk_archive}"' EXIT

  curl -fL \
    "https://dotnetcli.blob.core.windows.net/dotnet/Sdk/${DOTNET_VERSION}/dotnet-sdk-${DOTNET_VERSION}-linux-x64.tar.gz" \
    -o "${sdk_archive}"
  sudo mkdir -p "${DOTNET_ROOT}"
  sudo tar -xzf "${sdk_archive}" -C "${DOTNET_ROOT}"
fi

sudo ln -sf "${DOTNET_ROOT}/dotnet" /usr/local/bin/dotnet

dotnet restore src/NopCommerce.sln
dotnet build src/Presentation/Nop.Web/Nop.Web.csproj --no-restore
