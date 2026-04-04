#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${REPO_ROOT}/app/Server"

export ASPNETCORE_ENVIRONMENT=Development
export AppPaths__DatabasePath="${REPO_ROOT}/docker/data/sqlite/inventory.db"
export AppPaths__SqlScriptsPath="${REPO_ROOT}/db"
export AppPaths__TempAudioDirectory="${REPO_ROOT}/docker/temp"
export ModelServices__WhisperBaseUrl=http://localhost:8082

echo "Starting backend server with hot reload..."
echo "Database: ${AppPaths__DatabasePath}"
echo "Whisper: ${ModelServices__WhisperBaseUrl}"
echo "Press Ctrl+C to stop"
echo

dotnet watch run --urls=http://localhost:5184