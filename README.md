# Inventory LLM Demo

Local-first inventory demo built with ASP.NET Core, React, SQLite, and LM Studio.

Speech input/output now uses browser-native APIs (Chrome SpeechRecognition + speechSynthesis), so this repo no longer runs Whisper or Piper services.

## Repo structure

```text
/app
  /Server        ASP.NET Core API and static file host
  /ClientApp     React frontend (Vite)
/db              SQLite schema and seed SQL
/docker
  /app           App container Dockerfile
  /models        Optional local model folder
  /data          Host-mounted SQLite data
/scripts         Start/stop scripts for macOS and Windows
docker-compose.yml
SYSTEM_PROMPT.md
```

## Prerequisites

- Docker Desktop installed and running
- LM Studio running a local server on port `1234`
- macOS Apple Silicon or Windows 11

## Configuration

Copy [.env.example](/Users/valeriynovytskyy/Desktop/inventory-llm/.env.example) to `.env` if needed.

Default values:

- `LLM_BASE_URL=http://host.docker.internal:1234`
- `LLM_MODEL=auto`
- `MCP_SERVER_URL=http://localhost:8080/mcp`
- `LLM_COMPLETION_PATH=/v1/chat/completions`
- `LLM_HEALTH_PATH=/v1/models`

With `LLM_MODEL=auto`, backend resolves the first model from `GET /v1/models`.

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

Default local URLs:

- App: `http://localhost:8080`
- LM Studio: `http://localhost:1234`
- MCP server: `http://localhost:8080/mcp`

## Local dev without app container

1. Stop the app container:

```bash
docker compose stop app
```

2. Run backend from [app/Server](/Users/valeriynovytskyy/Desktop/inventory-llm/app/Server):

```bash
dotnet run
```

3. Run frontend from [app/ClientApp](/Users/valeriynovytskyy/Desktop/inventory-llm/app/ClientApp):

```bash
npm run dev
```

Local dev URLs:

- Frontend (Vite): `http://localhost:5173`
- Backend API: `http://localhost:5184`

## How the app works

- React is served by ASP.NET Core from `wwwroot` in Docker mode.
- ASP.NET Core API is the only service writing to SQLite.
- Chat completion is proxied from API to LM Studio.
- Browser TTS/STT is handled on the client, not by backend containers.
- MCP endpoint is hosted by the backend at `/mcp`.

## API endpoints

- `GET /api/health`
- `GET /api/config`
- `GET /api/items`
- `GET /api/items/{id}`
- `POST /api/items`
- `PUT /api/items/{id}`
- `DELETE /api/items/{id}`
- `GET /api/transactions`
- `POST /api/chat/complete`
- `GET /api/chat/system-prompt`
- `GET /api/chat/few-shot-prompts`

## MCP tools

- `inventory_list_items`
- `inventory_search_status`
- `inventory_add_transaction`

## Troubleshooting

- Verify LM Studio local server is reachable at `http://localhost:1234/v1/models`.
- If diagnostics fail, confirm Docker and LM Studio are both running.

## Files to customize first

- [docker-compose.yml](/Users/valeriynovytskyy/Desktop/inventory-llm/docker-compose.yml)
- [app/Server/appsettings.json](/Users/valeriynovytskyy/Desktop/inventory-llm/app/Server/appsettings.json)
- [SYSTEM_PROMPT.md](/Users/valeriynovytskyy/Desktop/inventory-llm/SYSTEM_PROMPT.md)
- [FEW_SHOT_PROMPTS.json](/Users/valeriynovytskyy/Desktop/inventory-llm/FEW_SHOT_PROMPTS.json)
- [db/002_seed.sql](/Users/valeriynovytskyy/Desktop/inventory-llm/db/002_seed.sql)
- [app/ClientApp/src/styles.css](/Users/valeriynovytskyy/Desktop/inventory-llm/app/ClientApp/src/styles.css)
