#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker Desktop CLI was not found."
  exit 1
fi

cd "${REPO_ROOT}"
docker compose down

echo "Inventory demo stopped."
