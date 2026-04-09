# Inventory LLM Demo

Local-first inventory demo repository built around ASP.NET Core, React, SQLite, whisper.cpp, Piper, and LM Studio. The app uses LM Studio running on the host at `http://localhost:1234`, while the rest of the stack runs on Docker Desktop with no cloud services.

## Repo structure

```text
/app
  /Server        ASP.NET Core API and static file host
  /ClientApp     React frontend (Vite)
/db              SQLite schema and seed SQL
/docker
  /app           App container Dockerfile
  /whisper       whisper.cpp container Dockerfile
  /models        Host-mounted model folders
  /data          Host-mounted SQLite data
  /temp          Temp audio files
/scripts         Start/stop scripts for macOS and Windows
docker-compose.yml
SYSTEM_PROMPT.md
```

## Prerequisites

- Docker Desktop installed and running
- LM Studio installed and running a local server on port `1234`
- macOS Apple Silicon or Windows 11
- Enough disk space for model files

The repo does not include the whisper.cpp model file. The startup scripts will download the default whisper.cpp model and Piper voice automatically if they are missing.

## Model file locations

Copy or move your model files into these folders:

- whisper.cpp model: [docker/models/whisper](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/models/whisper)
  - Default filename: `ggml-large-v3-turbo.bin`
  - This larger multilingual model is much stronger for Spanish, code-switching (Spanish and English), and noisy audio than the tiny model
- Piper voice model: [docker/models/piper](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/models/piper)
  - Included filename: `es_MX-claude-high.onnx`
  - Companion config file: `es_MX-claude-high.onnx.json`

Update `.env` if your filenames differ. Start by copying [.env.example](/Users/valeriynovytskyy/Desktop/inventory-llm/.env.example) to `.env`.

### LM Studio setup

The default configuration expects LM Studio to expose its local server on:

- `http://localhost:1234`

Inside Docker, the app reaches it through:

- `http://host.docker.internal:1234`

The app defaults to:

- `LLM_BASE_URL=http://host.docker.internal:1234`
- `LLM_MODEL=auto`
- `MCP_SERVER_URL=http://localhost:8080/mcp`
- `LLM_COMPLETION_PATH=/v1/chat/completions`
- `LLM_HEALTH_PATH=/v1/models`

With `LLM_MODEL=auto`, the backend will read `GET /v1/models` and use the first exposed model id automatically. If you prefer a specific loaded model, set `LLM_MODEL` explicitly in `.env`.

### Downloading the included Piper voice

This repo includes the Mexican Spanish `claude/high` Piper voice under [docker/models/piper](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/models/piper).

Manual download:

```bash
curl -L https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/es_MX-claude-high.onnx -o docker/models/piper/es_MX-claude-high.onnx
curl -L https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/es_MX-claude-high.onnx.json -o docker/models/piper/es_MX-claude-high.onnx.json
```

Windows PowerShell:

```powershell
Invoke-WebRequest https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/es_MX-claude-high.onnx -OutFile docker/models/piper/es_MX-claude-high.onnx
Invoke-WebRequest https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/es_MX-claude-high.onnx.json -OutFile docker/models/piper/es_MX-claude-high.onnx.json
```

### Downloading the default whisper.cpp model

This repo defaults to a higher-accuracy multilingual `whisper.cpp` model:

- Model repo: `https://huggingface.co/ggerganov/whisper.cpp`
- Default file: `ggml-large-v3-turbo.bin`

The startup scripts download this file into [docker/models/whisper](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/models/whisper) automatically when it is missing.

Manual download:

```bash
curl -L https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo.bin -o docker/models/whisper/ggml-large-v3-turbo.bin
```

Windows PowerShell:

```powershell
Invoke-WebRequest https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo.bin -OutFile docker/models/whisper/ggml-large-v3-turbo.bin
```

## Start

macOS:

```bash
chmod +x scripts/*.command
./scripts/start-local-demo.command
```

Windows:

```bat
scripts\start-local-demo.bat
```

The startup scripts:

- create local folders if missing
- create `.env` from `.env.example` if needed
- warn if LM Studio is not reachable on `localhost:1234`
- download `ggml-large-v3-turbo.bin` if missing
- download `es_MX-claude-high.onnx` and `es_MX-claude-high.onnx.json` if missing
- run `docker compose up -d --build`

Default local URLs:

- App: `http://localhost:8080`
- LM Studio: `http://localhost:1234`
- whisper.cpp: `http://localhost:8082`
- MCP server (HTTP): `http://localhost:8080/mcp`

## Local dev without app container

You can run frontend and backend locally while keeping only supporting services in Docker.

1. Stop just the app container:
```bash
docker compose stop app
```

2. Keep whisper running:
```bash
docker compose up -d stt
```

3. In terminal A, run backend from [app/Server](/Users/valeriynovytskyy/Desktop/inventory-llm/app/Server):
```bash
dotnet run
```

4. In terminal B, run frontend from [app/ClientApp](/Users/valeriynovytskyy/Desktop/inventory-llm/app/ClientApp):
```bash
npm run dev
```

Optional one-time local Piper setup (for `dotnet run` voice/TTS):

```bash
/opt/homebrew/bin/python3.11 -m venv app/Server/local-tools/piper-venv
app/Server/local-tools/piper-venv/bin/python -m pip install --upgrade pip
app/Server/local-tools/piper-venv/bin/python -m pip install piper-tts pathvalidate
```

Local dev URLs:

- Frontend (Vite): `http://localhost:5173`
- Backend API: `http://localhost:5184`

Notes:

- The Vite proxy is already configured to send `/api` to `http://localhost:5184`.
- Backend development config now points SQLite SQL scripts to `../../db`, so DB init works when started from `app/Server`.
- LM Studio should still run on `http://localhost:1234`.

## Stop

macOS:

```bash
./scripts/stop-local-demo.command
```

Windows:

```bat
scripts\stop-local-demo.bat
```

## How the app works

- The React app is built in the app image and served by ASP.NET Core from `wwwroot`.
- The ASP.NET Core API is the only service that touches SQLite.
- SQLite persists on the host under [docker/data/sqlite](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/data/sqlite).
- On startup, the API creates the database directory, initializes schema from [db/001_schema.sql](/Users/valeriynovytskyy/Desktop/inventory-llm/db/001_schema.sql), and seeds data from [db/002_seed.sql](/Users/valeriynovytskyy/Desktop/inventory-llm/db/002_seed.sql) only when the `items` table is empty.
- Browser audio is uploaded to the API, which proxies it to whisper.cpp.
- The API converts browser-recorded audio to mono 16 kHz WAV with `ffmpeg` before sending it to whisper.cpp.
- Text-to-speech stays inside the app container, where the API shells out to Piper and returns `audio/wav`.
- Chat completion is a thin proxy from the API to LM Studio.
- The chat system prompt is loaded from [SYSTEM_PROMPT.md](/Users/valeriynovytskyy/Desktop/inventory-llm/SYSTEM_PROMPT.md) at startup (works in both Docker and local dev).
- Few-shot chat messages are loaded from [FEW_SHOT_PROMPTS.json](/Users/valeriynovytskyy/Desktop/inventory-llm/FEW_SHOT_PROMPTS.json) at startup using OpenAI-style `{ "role", "content" }` entries in a root JSON array.
- The dashboard chat bootstraps with hidden `system + hello` messages, then shows the model greeting as the first visible assistant message.
- The backend includes an MCP client that executes model-requested tool calls against its own MCP endpoint (`/mcp`) and returns tool results back to the model in a loop until a final answer is produced.

## API endpoints

- `GET /api/health`
- `GET /api/config`
- `GET /api/items`
- `GET /api/items/{id}`
- `POST /api/items`
- `PUT /api/items/{id}`
- `DELETE /api/items/{id}`
- `GET /api/transactions`
- `POST /api/voice/transcribe-proxy`
- `POST /api/voice/speak`
- `POST /api/chat/complete`
- `GET /api/chat/system-prompt`
- `GET /api/chat/few-shot-prompts`

## MCP server

The backend also hosts an MCP HTTP endpoint using the official C# SDK package (`ModelContextProtocol.AspNetCore`):

- `POST/GET /mcp` (Streamable HTTP transport; stateless mode)

Current MCP tools:

- `inventory_list_items`: list inventory items with SKU/name/quantity
- `inventory_search_status`: search item status by name/SKU (e.g. "do I have X?")
- `inventory_add_transaction`: add an inventory transaction and update quantity

## Inspecting SQLite in DBeaver

- Open the database file at [docker/data/sqlite/inventory.db](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/data/sqlite/inventory.db) after the app has started once.
- In DBeaver, create a new SQLite connection and point it at that file.
- Only the app service should write to the database while the stack is running.

## Troubleshooting

- If LM Studio chat is not working, verify LM Studio local server is enabled and reachable at `http://localhost:1234/v1/models`.
- If the whisper model download fails, confirm the default URL still resolves to `ggml-large-v3-turbo.bin` in `ggerganov/whisper.cpp`.
- If the app diagnostics show Piper missing, confirm the `.onnx` voice file exists in `docker/models/piper`.
- If the app diagnostics show Piper voice missing, also confirm the companion `.onnx.json` file is present next to the model.
- If the app image fails while installing Piper, check [docker/app/Dockerfile](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/app/Dockerfile) against the latest `OHF-Voice/piper1-gpl` wheel names.
- On macOS, if the `.command` scripts do not launch, run `chmod +x scripts/*.command` once.
- On Windows, run the `.bat` script from a normal Command Prompt after Docker Desktop is fully started.

## Known limitations

- Demo-quality local setup intended for a single machine
- CPU-oriented whisper.cpp setup for portability
- No authentication
- No cloud dependencies
- Model files are not bundled

## Files to customize first

- [docker-compose.yml](/Users/valeriynovytskyy/Desktop/inventory-llm/docker-compose.yml) for ports and mounted paths
- [app/Server/appsettings.json](/Users/valeriynovytskyy/Desktop/inventory-llm/app/Server/appsettings.json) for backend integration settings
- [SYSTEM_PROMPT.md](/Users/valeriynovytskyy/Desktop/inventory-llm/SYSTEM_PROMPT.md) for assistant behavior and tone
- [FEW_SHOT_PROMPTS.json](/Users/valeriynovytskyy/Desktop/inventory-llm/FEW_SHOT_PROMPTS.json) for few-shot examples used in chat completions
- [db/002_seed.sql](/Users/valeriynovytskyy/Desktop/inventory-llm/db/002_seed.sql) for your sample inventory
- [app/ClientApp/src/styles.css](/Users/valeriynovytskyy/Desktop/inventory-llm/app/ClientApp/src/styles.css) for UI appearance
