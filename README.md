# Inventory LLM Demo

Local-first inventory demo repository built around ASP.NET Core, React, SQLite, llama.cpp, whisper.cpp, and Piper. The full stack runs on Docker Desktop with no cloud services.

## Repo structure

```text
/app
  /Server        ASP.NET Core API and static file host
  /ClientApp     React frontend (Vite)
/db              SQLite schema and seed SQL
/docker
  /app           App container Dockerfile
  /llama         llama.cpp container Dockerfile
  /whisper       whisper.cpp container Dockerfile
  /models        Host-mounted model folders
  /data          Host-mounted SQLite data
  /temp          Temp audio files
/scripts         Start/stop scripts for macOS and Windows
docker-compose.yml
```

## Prerequisites

- Docker Desktop installed and running
- macOS Apple Silicon or Windows 11
- Enough disk space for model files

The repo does not include the llama.cpp or whisper.cpp model files. The startup scripts will download the default llama.cpp model and Piper voice automatically if they are missing.

## Model file locations

Copy or move your model files into these folders:

- llama.cpp GGUF model: [docker/models/llama](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/models/llama)
  - Default filename: `gemma-3-4b-it-Q4_K_M.gguf`
  - The upstream repo also publishes `Q3_K_L`, `Q6_K`, `Q8_0`, and `mmproj-model-f16.gguf`
- whisper.cpp model: [docker/models/whisper](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/models/whisper)
  - Example filename: `ggml-base.en.bin`
- Piper voice model: [docker/models/piper](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/models/piper)
  - Included filename: `es_MX-claude-high.onnx`
  - Companion config file: `es_MX-claude-high.onnx.json`

Update `.env` if your filenames differ. Start by copying [.env.example](/Users/valeriynovytskyy/Desktop/inventory-llm/.env.example) to `.env`.

### Downloading the included Piper voice

This repo now includes the Mexican Spanish `claude/high` Piper voice under [docker/models/piper](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/models/piper).

If you need to download it again manually:

```bash
curl -L https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/es_MX-claude-high.onnx -o docker/models/piper/es_MX-claude-high.onnx
curl -L https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/es_MX-claude-high.onnx.json -o docker/models/piper/es_MX-claude-high.onnx.json
```

Windows PowerShell:

```powershell
Invoke-WebRequest https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/es_MX-claude-high.onnx -OutFile docker/models/piper/es_MX-claude-high.onnx
Invoke-WebRequest https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/es_MX-claude-high.onnx.json -OutFile docker/models/piper/es_MX-claude-high.onnx.json
```

### Downloading the default llama.cpp model

This repo now defaults to the LM Studio Community GGUF build of Gemma 3 4B Instruct:

- Model repo: `https://huggingface.co/lmstudio-community/gemma-3-4b-it-GGUF`
- Default file: `gemma-3-4b-it-Q4_K_M.gguf`

The startup scripts download this file into [docker/models/llama](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/models/llama) automatically when it is missing.

Manual download:

```bash
curl -L https://huggingface.co/lmstudio-community/gemma-3-4b-it-GGUF/resolve/main/gemma-3-4b-it-Q4_K_M.gguf -o docker/models/llama/gemma-3-4b-it-Q4_K_M.gguf
```

Windows PowerShell:

```powershell
Invoke-WebRequest https://huggingface.co/lmstudio-community/gemma-3-4b-it-GGUF/resolve/main/gemma-3-4b-it-Q4_K_M.gguf -OutFile docker/models/llama/gemma-3-4b-it-Q4_K_M.gguf
```

This model repository also includes `mmproj-model-f16.gguf`, but this demo currently uses text completion only and does not mount or call the multimodal projector file.

## Start

macOS:

```bash
chmod +x scripts/*.command
./scripts/start-local-demo.command
```

The macOS script will:

- create local folders if missing
- create `.env` from `.env.example` if needed
- download `gemma-3-4b-it-Q4_K_M.gguf` if missing
- download `es_MX-claude-high.onnx` and `es_MX-claude-high.onnx.json` if missing
- run `docker compose up -d --build`

Windows:

```bat
scripts\start-local-demo.bat
```

The Windows script does the same downloads before starting Docker Compose.

Default local URLs:

- App: `http://localhost:8080`
- llama.cpp: `http://localhost:8081`
- whisper.cpp: `http://localhost:8082`

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
- Text-to-speech stays inside the app container, where the API shells out to Piper and returns `audio/wav`.
- Chat completion is a thin proxy from the API to llama.cpp.

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

## Inspecting SQLite in DBeaver

- Open the database file at [docker/data/sqlite/inventory.db](/Users/valeriynovytskyy/Desktop/inventory-llm/docker/data/sqlite/inventory.db) after the app has started once.
- In DBeaver, create a new SQLite connection and point it at that file.
- Only the app service should write to the database while the stack is running.

## Configuration

The backend reads `appsettings.json` and environment variable overrides from Docker Compose.

Most useful settings:

- SQLite path: [app/Server/appsettings.json](/Users/valeriynovytskyy/Desktop/inventory-llm/app/Server/appsettings.json)
- External model URLs: [app/Server/appsettings.json](/Users/valeriynovytskyy/Desktop/inventory-llm/app/Server/appsettings.json)
- Compose environment overrides: [docker-compose.yml](/Users/valeriynovytskyy/Desktop/inventory-llm/docker-compose.yml)
- Model filenames: [.env.example](/Users/valeriynovytskyy/Desktop/inventory-llm/.env.example)

## Troubleshooting

- If `docker compose up` fails because a model file is missing, verify the filename in `.env` exactly matches the file on disk.
- If the Gemma download fails, check whether Hugging Face is rate-limiting or requiring a refreshed browser/session for that model URL.
- If the app diagnostics show Piper missing, confirm the `.onnx` voice file exists in `docker/models/piper`.
- If the app diagnostics show Piper voice missing, also confirm the companion `.onnx.json` file is present next to the model.
- If whisper.cpp or llama.cpp fail to build, the upstream server binary or flags may have changed. Check the corresponding Dockerfile and container logs.
- On macOS, if the `.command` scripts do not launch, run `chmod +x scripts/*.command` once.
- On Windows, run the `.bat` script from a normal Command Prompt after Docker Desktop is fully started.
- First build may take a while because Docker needs to compile `llama.cpp` and `whisper.cpp`.

## Known limitations

- Demo-quality local setup intended for a single machine
- CPU-oriented inference for portability
- No authentication
- No cloud dependencies
- Model files are not bundled
- Command-line flags for llama.cpp and whisper.cpp can vary by upstream version
- The default Gemma model download is several gigabytes and will make first startup much slower

## Files to customize first

- [docker-compose.yml](/Users/valeriynovytskyy/Desktop/inventory-llm/docker-compose.yml) for ports, model filenames, and mounted paths
- [app/Server/appsettings.json](/Users/valeriynovytskyy/Desktop/inventory-llm/app/Server/appsettings.json) for backend integration settings
- [db/002_seed.sql](/Users/valeriynovytskyy/Desktop/inventory-llm/db/002_seed.sql) for your sample inventory
- [app/ClientApp/src/styles.css](/Users/valeriynovytskyy/Desktop/inventory-llm/app/ClientApp/src/styles.css) for UI appearance

## Assumptions

- `llama.cpp` exposes `POST /completion` and `GET /health`
- `gemma-3-4b-it-Q4_K_M.gguf` is compatible with the chosen `llama.cpp` server build
- `whisper.cpp` exposes `POST /inference` and responds on `/`
- Piper release archives follow the `piper_linux_<arch>.tar.gz` naming pattern for `amd64` and `arm64`
- Piper can be invoked with `--model <voice.onnx> --output_file <wav>`
- The `es_MX-claude-high` voice works with the `.onnx` model plus its adjacent `.onnx.json` config file
