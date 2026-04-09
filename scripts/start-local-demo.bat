@echo off
setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set REPO_ROOT=%SCRIPT_DIR%..

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

if not exist "%REPO_ROOT%\.env" (
  copy "%REPO_ROOT%\.env.example" "%REPO_ROOT%\.env" >nul
  echo Created .env from .env.example
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest 'http://localhost:1234/v1/models' -UseBasicParsing > $null; exit 0 } catch { exit 1 }"
if errorlevel 1 (
  echo Warning: LM Studio API was not reachable at http://localhost:1234/v1/models
  echo Start LM Studio local server before using chat features.
)

cd /d "%REPO_ROOT%"
docker compose up -d --build
if errorlevel 1 exit /b 1

echo.
echo Inventory demo is starting.
echo App URL: http://localhost:8080
echo LM Studio URL: http://localhost:1234
