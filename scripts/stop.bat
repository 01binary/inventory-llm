@echo off
setlocal

set SCRIPT_DIR=%~dp0
set REPO_ROOT=%SCRIPT_DIR%..

where docker >nul 2>nul
if errorlevel 1 (
  echo Docker Desktop CLI was not found.
  exit /b 1
)

cd /d "%REPO_ROOT%"
docker compose down
if errorlevel 1 exit /b 1

echo Inventory demo stopped.
