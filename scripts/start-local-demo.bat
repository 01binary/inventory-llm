@echo off
setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set REPO_ROOT=%SCRIPT_DIR%..
set DEFAULT_LLAMA_FILE=gemma-3-4b-it-Q4_K_M.gguf
set DEFAULT_LLAMA_BYTES=2489757856
set DEFAULT_WHISPER_FILE=ggml-tiny-q5_1.bin
set DEFAULT_PIPER_FILE=es_MX-claude-high.onnx
set LLAMA_MODEL_URL=https://huggingface.co/lmstudio-community/gemma-3-4b-it-GGUF/resolve/main/gemma-3-4b-it-Q4_K_M.gguf
set WHISPER_MODEL_URL=https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny-q5_1.bin
set PIPER_MODEL_URL=https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/es_MX-claude-high.onnx
set PIPER_CONFIG_URL=https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_MX/claude/high/es_MX-claude-high.onnx.json

where docker >nul 2>nul
if errorlevel 1 (
  echo Docker Desktop CLI was not found. Install Docker Desktop first.
  exit /b 1
)

where powershell >nul 2>nul
if errorlevel 1 (
  echo PowerShell was not found. Install PowerShell and try again.
  exit /b 1
)

docker info >nul 2>nul
if errorlevel 1 (
  echo Docker daemon is not running. Start Docker Desktop and try again.
  exit /b 1
)

if not exist "%REPO_ROOT%\docker\data\sqlite" mkdir "%REPO_ROOT%\docker\data\sqlite"
if not exist "%REPO_ROOT%\docker\models\llama" mkdir "%REPO_ROOT%\docker\models\llama"
if not exist "%REPO_ROOT%\docker\models\whisper" mkdir "%REPO_ROOT%\docker\models\whisper"
if not exist "%REPO_ROOT%\docker\models\piper" mkdir "%REPO_ROOT%\docker\models\piper"
if not exist "%REPO_ROOT%\docker\temp" mkdir "%REPO_ROOT%\docker\temp"

if not exist "%REPO_ROOT%\.env" (
  copy "%REPO_ROOT%\.env.example" "%REPO_ROOT%\.env" >nul
  echo Created .env from .env.example
)

for /f "usebackq tokens=1,* delims==" %%A in ("%REPO_ROOT%\.env") do (
  if not "%%A"=="" if not "%%A:~0,1%%"=="#" set %%A=%%B
)

if "%USE_DOCKER_LLM%"=="" set USE_DOCKER_LLM=0
if "%LLAMA_MODEL_FILE%"=="" set LLAMA_MODEL_FILE=%DEFAULT_LLAMA_FILE%
if "%WHISPER_MODEL_FILE%"=="" set WHISPER_MODEL_FILE=%DEFAULT_WHISPER_FILE%
if "%PIPER_VOICE_FILE%"=="" set PIPER_VOICE_FILE=%DEFAULT_PIPER_FILE%

set LLAMA_MODEL_PATH=%REPO_ROOT%\docker\models\llama\%LLAMA_MODEL_FILE%
set WHISPER_MODEL_PATH=%REPO_ROOT%\docker\models\whisper\%WHISPER_MODEL_FILE%
set PIPER_MODEL_PATH=%REPO_ROOT%\docker\models\piper\%PIPER_VOICE_FILE%
set PIPER_CONFIG_PATH=%PIPER_MODEL_PATH%.json

if "%USE_DOCKER_LLM%"=="1" (
  set LLAMA_NEEDS_DOWNLOAD=
  if not exist "%LLAMA_MODEL_PATH%" (
    set LLAMA_NEEDS_DOWNLOAD=1
  ) else if /i "%LLAMA_MODEL_FILE%"=="%DEFAULT_LLAMA_FILE%" (
    for %%F in ("%LLAMA_MODEL_PATH%") do set ACTUAL_LLAMA_BYTES=%%~zF
    if not "!ACTUAL_LLAMA_BYTES!"=="%DEFAULT_LLAMA_BYTES%" (
      echo Existing llama.cpp model has the wrong size (!ACTUAL_LLAMA_BYTES! bytes). Expected %DEFAULT_LLAMA_BYTES%. Re-downloading.
      del "%LLAMA_MODEL_PATH%" >nul 2>nul
      set LLAMA_NEEDS_DOWNLOAD=1
    )
  )

  if defined LLAMA_NEEDS_DOWNLOAD (
    if /i "%LLAMA_MODEL_FILE%"=="%DEFAULT_LLAMA_FILE%" (
      echo Downloading llama.cpp model %LLAMA_MODEL_FILE%. This may take a while.
      powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest '%LLAMA_MODEL_URL%' -OutFile '%LLAMA_MODEL_PATH%'"
      if errorlevel 1 exit /b 1
    ) else (
      echo Warning: %LLAMA_MODEL_FILE% is missing in docker\models\llama and no automatic download is configured for custom filenames.
    )
  )
 ) else (
  powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest 'http://localhost:1234/v1/models' -UseBasicParsing > $null; exit 0 } catch { exit 1 }"
  if errorlevel 1 (
    echo Warning: LM Studio API was not reachable at http://localhost:1234/v1/models
    echo Start LM Studio local server before using chat features.
  )
)

if not exist "%WHISPER_MODEL_PATH%" (
  if /i "%WHISPER_MODEL_FILE%"=="%DEFAULT_WHISPER_FILE%" (
    echo Downloading whisper.cpp model %WHISPER_MODEL_FILE%.
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest '%WHISPER_MODEL_URL%' -OutFile '%WHISPER_MODEL_PATH%'"
    if errorlevel 1 exit /b 1
  ) else (
    echo Warning: %WHISPER_MODEL_FILE% is missing in docker\models\whisper and no automatic download is configured for custom filenames.
  )
)

if not exist "%PIPER_MODEL_PATH%" (
  if /i "%PIPER_VOICE_FILE%"=="%DEFAULT_PIPER_FILE%" (
    echo Downloading Piper voice %PIPER_VOICE_FILE%.
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest '%PIPER_MODEL_URL%' -OutFile '%PIPER_MODEL_PATH%'"
    if errorlevel 1 exit /b 1
  ) else (
    echo Warning: %PIPER_VOICE_FILE% is missing in docker\models\piper and no automatic download is configured for custom filenames.
  )
)

if not exist "%PIPER_CONFIG_PATH%" (
  if /i "%PIPER_VOICE_FILE%"=="%DEFAULT_PIPER_FILE%" (
    echo Downloading Piper voice config %PIPER_VOICE_FILE%.json.
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest '%PIPER_CONFIG_URL%' -OutFile '%PIPER_CONFIG_PATH%'"
    if errorlevel 1 exit /b 1
  )
)

cd /d "%REPO_ROOT%"
if "%USE_DOCKER_LLM%"=="1" (
  set COMPOSE_PROFILES=with-llm
  docker compose up -d --build
) else (
  docker compose up -d --build
)
if errorlevel 1 exit /b 1

echo.
echo Inventory demo is starting.
echo App URL: http://localhost:8080
if "%USE_DOCKER_LLM%"=="1" (
  echo llama.cpp URL: http://localhost:8081
) else (
  echo LM Studio URL: http://localhost:1234
)
echo whisper.cpp URL: http://localhost:8082
