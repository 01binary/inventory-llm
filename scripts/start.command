#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker Desktop CLI was not found. Install Docker Desktop first."
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl was not found. Install curl and try again."
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "Docker daemon is not running. Start Docker Desktop and try again."
  exit 1
fi

mkdir -p "${REPO_ROOT}/docker/data/sqlite"

if [ ! -f "${REPO_ROOT}/.env" ]; then
  cp "${REPO_ROOT}/.env.example" "${REPO_ROOT}/.env"
  echo "Created .env from .env.example"
fi

if ! curl -fsS http://localhost:1234/v1/models >/dev/null 2>&1; then
  echo "Warning: LM Studio API was not reachable at http://localhost:1234/v1/models"
  echo "Start LM Studio local server before using chat features."
fi

cd "${REPO_ROOT}"
docker compose up -d --build

echo
echo "Inventory demo is starting."
echo "App URL: http://localhost:8080"
echo "LM Studio URL: http://localhost:1234"
