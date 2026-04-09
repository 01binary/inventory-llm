@echo off
setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set REPO_ROOT=%SCRIPT_DIR%..
set DEFAULT_WHISPER_FILE=ggml-large-v3-turbo.bin
set DEFAULT_PIPER_FILE=es_MX-claude-high.onnx
set WHISPER_MODEL_URL=https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo.bin
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

if "%WHISPER_MODEL_FILE%"=="" set WHISPER_MODEL_FILE=%DEFAULT_WHISPER_FILE%
if "%PIPER_VOICE_FILE%"=="" set PIPER_VOICE_FILE=%DEFAULT_PIPER_FILE%

set WHISPER_MODEL_PATH=%REPO_ROOT%\docker\models\whisper\%WHISPER_MODEL_FILE%
set PIPER_MODEL_PATH=%REPO_ROOT%\docker\models\piper\%PIPER_VOICE_FILE%
set PIPER_CONFIG_PATH=%PIPER_MODEL_PATH%.json

powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest 'http://localhost:1234/v1/models' -UseBasicParsing > $null; exit 0 } catch { exit 1 }"
if errorlevel 1 (
  echo Warning: LM Studio API was not reachable at http://localhost:1234/v1/models
  echo Start LM Studio local server before using chat features.
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
docker compose up -d --build
if errorlevel 1 exit /b 1

echo.
echo Inventory demo is starting.
echo App URL: http://localhost:8080
echo LM Studio URL: http://localhost:1234
echo whisper.cpp URL: http://localhost:8082
