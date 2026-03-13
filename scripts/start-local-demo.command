#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DEFAULT_LLAMA_FILE="gemma-3-4b-it-Q4_K_M.gguf"
DEFAULT_WHISPER_FILE="ggml-tiny-q5_1.bin"
DEFAULT_PIPER_FILE="es_MX-claude-high.onnx"
LLAMA_MODEL_URL="https://huggingface.co/lmstudio-community/gemma-3-4b-it-GGUF/resolve/main/${DEFAULT_LLAMA_FILE}"
WHISPER_MODEL_URL="https://huggingface.co/ggerganov/whisper.cpp/resolve/main/${DEFAULT_WHISPER_FILE}"
PIPER_MODEL_URL="https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/${DEFAULT_PIPER_FILE}"
PIPER_CONFIG_URL="https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/${DEFAULT_PIPER_FILE}.json"

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

mkdir -p \
  "${REPO_ROOT}/docker/data/sqlite" \
  "${REPO_ROOT}/docker/models/llama" \
  "${REPO_ROOT}/docker/models/whisper" \
  "${REPO_ROOT}/docker/models/piper" \
  "${REPO_ROOT}/docker/temp"

if [ ! -f "${REPO_ROOT}/.env" ]; then
  cp "${REPO_ROOT}/.env.example" "${REPO_ROOT}/.env"
  echo "Created .env from .env.example"
fi

. "${REPO_ROOT}/.env"

LLAMA_MODEL_FILE="${LLAMA_MODEL_FILE:-${DEFAULT_LLAMA_FILE}}"
WHISPER_MODEL_FILE="${WHISPER_MODEL_FILE:-${DEFAULT_WHISPER_FILE}}"
PIPER_VOICE_FILE="${PIPER_VOICE_FILE:-${DEFAULT_PIPER_FILE}}"

LLAMA_MODEL_PATH="${REPO_ROOT}/docker/models/llama/${LLAMA_MODEL_FILE}"
WHISPER_MODEL_PATH="${REPO_ROOT}/docker/models/whisper/${WHISPER_MODEL_FILE}"
PIPER_MODEL_PATH="${REPO_ROOT}/docker/models/piper/${PIPER_VOICE_FILE}"
PIPER_CONFIG_PATH="${PIPER_MODEL_PATH}.json"

if [ ! -f "${LLAMA_MODEL_PATH}" ]; then
  if [ "${LLAMA_MODEL_FILE}" = "${DEFAULT_LLAMA_FILE}" ]; then
    echo "Downloading llama.cpp model ${LLAMA_MODEL_FILE}. This may take a while."
    curl -L "${LLAMA_MODEL_URL}" -o "${LLAMA_MODEL_PATH}"
  else
    echo "Warning: ${LLAMA_MODEL_FILE} is missing in docker/models/llama and no automatic download is configured for custom filenames."
  fi
fi

if [ ! -f "${WHISPER_MODEL_PATH}" ]; then
  if [ "${WHISPER_MODEL_FILE}" = "${DEFAULT_WHISPER_FILE}" ]; then
    echo "Downloading whisper.cpp model ${WHISPER_MODEL_FILE}."
    curl -L "${WHISPER_MODEL_URL}" -o "${WHISPER_MODEL_PATH}"
  else
    echo "Warning: ${WHISPER_MODEL_FILE} is missing in docker/models/whisper and no automatic download is configured for custom filenames."
  fi
fi

if [ ! -f "${PIPER_MODEL_PATH}" ] || [ ! -f "${PIPER_CONFIG_PATH}" ]; then
  if [ "${PIPER_VOICE_FILE}" = "${DEFAULT_PIPER_FILE}" ]; then
    if [ ! -f "${PIPER_MODEL_PATH}" ]; then
      echo "Downloading Piper voice ${PIPER_VOICE_FILE}."
      curl -L "${PIPER_MODEL_URL}" -o "${PIPER_MODEL_PATH}"
    fi

    if [ ! -f "${PIPER_CONFIG_PATH}" ]; then
      echo "Downloading Piper voice config ${PIPER_VOICE_FILE}.json."
      curl -L "${PIPER_CONFIG_URL}" -o "${PIPER_CONFIG_PATH}"
    fi
  else
    echo "Warning: ${PIPER_VOICE_FILE} is missing in docker/models/piper and no automatic download is configured for custom filenames."
  fi
fi

cd "${REPO_ROOT}"
docker compose up -d --build

echo
echo "Inventory demo is starting."
echo "App URL: http://localhost:8080"
echo "llama.cpp URL: http://localhost:8081"
echo "whisper.cpp URL: http://localhost:8082"
