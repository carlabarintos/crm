@echo off
setlocal
cd /d "%~dp0\.."

REM =============================================================================
REM  rebuild.bat — Rebuild and redeploy api, web, or both on Hetzner
REM  Usage:
REM    rebuild.bat api      — rebuild crm-api only
REM    rebuild.bat web      — rebuild crm-web only
REM    rebuild.bat all      — rebuild both
REM =============================================================================

REM ── Edit these values once ───────────────────────────────────────────────────
set HETZNER_HOST=178.104.236.119
set HETZNER_USER=root
set SSH_KEY=%USERPROFILE%\.ssh\id_ed25519
REM ─────────────────────────────────────────────────────────────────────────────

set TARGET=%~1
if "%TARGET%"=="" (
    echo Usage: rebuild.bat [api ^| web ^| all]
    echo.
    echo   api  — rebuild crm-api only
    echo   web  — rebuild crm-web only
    echo   all  — rebuild both
    exit /b 1
)

if /i not "%TARGET%"=="api" if /i not "%TARGET%"=="web" if /i not "%TARGET%"=="all" (
    echo ERROR: Unknown target "%TARGET%". Use: api, web, or all
    exit /b 1
)

echo.
echo  ==========================================
echo   CRM Rebuild: %TARGET%
echo   Server: %HETZNER_USER%@%HETZNER_HOST%
echo  ==========================================
echo.

REM Build the remote command based on target
if /i "%TARGET%"=="api" (
    set REMOTE_CMD=cd /opt/crm ^&^& git pull ^&^& docker build -f Dockerfile.api -t ghcr.io/carlabarintos/crm-api:latest . ^&^& docker compose -f docker-compose.prod.yml --env-file .env.production up -d --force-recreate crm-api ^&^& docker image prune -f
)
if /i "%TARGET%"=="web" (
    set REMOTE_CMD=cd /opt/crm ^&^& git pull ^&^& docker build -f Dockerfile.web -t ghcr.io/carlabarintos/crm-web:latest . ^&^& docker compose -f docker-compose.prod.yml --env-file .env.production up -d --force-recreate crm-web ^&^& docker image prune -f
)
if /i "%TARGET%"=="all" (
    set REMOTE_CMD=cd /opt/crm ^&^& git pull ^&^& docker build -f Dockerfile.api -t ghcr.io/carlabarintos/crm-api:latest . ^&^& docker build -f Dockerfile.web -t ghcr.io/carlabarintos/crm-web:latest . ^&^& docker compose -f docker-compose.prod.yml --env-file .env.production up -d --force-recreate crm-api crm-web ^&^& docker image prune -f
)

echo Connecting to server and rebuilding...
echo (This will take a few minutes)
echo.

ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% "%REMOTE_CMD%"

if %errorlevel% neq 0 (
    echo.
    echo ERROR: Rebuild failed. Check the output above.
    exit /b 1
)

echo.
echo  ==========================================
echo   Done! App is live at https://%HETZNER_HOST%.nip.io
echo  ==========================================
echo.
endlocal
