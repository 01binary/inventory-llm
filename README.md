# Inventory LLM Demo

Inventory demo app that combines:

- React frontend
- ASP.NET Core backend
- SQLite persistence
- Model Context Protocol (MCP) tools
- LM Studio-compatible local LLM
- Browser-native speech-to-text and text-to-speech (Web Speech API)

## Architecture

- Frontend: `app/ClientApp` (Vite + React)
- Backend: `app/Server` (ASP.NET Core + Dapper + SQLite)
- DB schema + seed: `db/001_schema.sql`, `db/002_seed.sql`
- Prompt assets:
  - `SYSTEM_PROMPT.md`
  - `HELLO_PROMPT.md`
  - `FEW_SHOT_PROMPTS.json`

The backend serves both API endpoints and MCP tools at `/mcp`.  
The chat layer proxies completions to an OpenAI-compatible endpoint (LM Studio by default).

## Prerequisites

- Docker Desktop (for containerized run)
- LM Studio local server running on `http://localhost:1234`
- macOS or Windows

## Configuration

Copy `.env.example` to `.env` (only if `.env` does not exist yet).

Default `.env` values:

```env
LLM_BASE_URL=http://host.docker.internal:1234
LLM_MODEL=auto
LLM_COMPLETION_PATH=/v1/chat/completions
LLM_HEALTH_PATH=/v1/models
MCP_SERVER_URL=http://localhost:8080/mcp
```

`LLM_MODEL=auto` means the backend picks the first model returned by `GET /v1/models`.

## Quick Start (Docker)

macOS:

```bash
chmod +x scripts/*.command
./scripts/start-local-demo.command
```

Windows:

```bat
scripts\start-local-demo.bat
```

App URL: `http://localhost:8080`

## Local Dev (without app container)

1. Stop app container:

```bash
docker compose stop app
```

2. Run backend:

```bash
cd app/Server
dotnet run
```

3. Run frontend:

```bash
cd app/ClientApp
npm install
npm run dev
```

Dev URLs:

- Frontend: `http://localhost:5173`
- Backend: `http://localhost:5184`

## Current Features

- Inventory management (list/create/update/delete)
- Inventory transaction log
- Chat assistant with MCP tool calling
- Orders workflow:
  - create multi-item orders
  - append items to latest order across turns
  - view orders page with expandable order lines
- Browser STT/TTS voice interaction (no Whisper/Piper containers)

## API Endpoints

- `GET /api/health`
- `GET /api/config`
- `GET /api/items`
- `GET /api/items/{id}`
- `GET /api/items/validate-sku?sku=...`
- `POST /api/items`
- `PUT /api/items/{id}`
- `DELETE /api/items/{id}`
- `GET /api/transactions`
- `GET /api/orders`
- `GET /api/orders/latest`
- `GET /api/orders/{orderNumber}`
- `POST /api/orders`
- `POST /api/orders/latest/items`
- `POST /api/chat/complete`
- `GET /api/chat/system-prompt`
- `GET /api/chat/hello-prompt`
- `GET /api/chat/few-shot-prompts`

## MCP Tools

- `inventory_list_items`
- `inventory_search_status`
- `inventory_add_transaction`
- `orders_get_latest`
- `orders_create`
- `orders_add_items_to_latest`


## Troubleshooting

The diagnostics page in the app can help troubleshoot issues with configuration and running services.

- Verify LM Studio is reachable: `http://localhost:1234/v1/models`
- If chat works but tool calling fails, verify backend MCP URL config:
  - `MCP_SERVER_URL=http://localhost:8080/mcp`
- If no seed data appears, check DB volume at `docker/data/sqlite`

## First Files to Customize

- `SYSTEM_PROMPT.md`
- `HELLO_PROMPT.md`
- `FEW_SHOT_PROMPTS.json`
- `db/002_seed.sql`
- `app/Server/appsettings.json`
- `app/ClientApp/src/styles.css`
