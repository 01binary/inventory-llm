@echo off
setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set REPO_ROOT=%SCRIPT_DIR%..
set DEFAULT_LLAMA_FILE=gemma-3-4b-it-Q4_K_M.gguf
set DEFAULT_PIPER_FILE=es_MX-claude-high.onnx
set LLAMA_MODEL_URL=https://huggingface.co/lmstudio-community/gemma-3-4b-it-GGUF/resolve/main/gemma-3-4b-it-Q4_K_M.gguf
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

if "%LLAMA_MODEL_FILE%"=="" set LLAMA_MODEL_FILE=%DEFAULT_LLAMA_FILE%
if "%PIPER_VOICE_FILE%"=="" set PIPER_VOICE_FILE=%DEFAULT_PIPER_FILE%

set LLAMA_MODEL_PATH=%REPO_ROOT%\docker\models\llama\%LLAMA_MODEL_FILE%
set PIPER_MODEL_PATH=%REPO_ROOT%\docker\models\piper\%PIPER_VOICE_FILE%
set PIPER_CONFIG_PATH=%PIPER_MODEL_PATH%.json

if not exist "%LLAMA_MODEL_PATH%" (
  if /i "%LLAMA_MODEL_FILE%"=="%DEFAULT_LLAMA_FILE%" (
    echo Downloading llama.cpp model %LLAMA_MODEL_FILE%. This may take a while.
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest '%LLAMA_MODEL_URL%' -OutFile '%LLAMA_MODEL_PATH%'"
    if errorlevel 1 exit /b 1
  ) else (
    echo Warning: %LLAMA_MODEL_FILE% is missing in docker\models\llama and no automatic download is configured for custom filenames.
  )
)

dir /b "%REPO_ROOT%\docker\models\whisper\*.bin" >nul 2>nul
if errorlevel 1 echo Warning: no whisper model found in docker\models\whisper

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
echo llama.cpp URL: http://localhost:8081
echo whisper.cpp URL: http://localhost:8082
