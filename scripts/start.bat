@echo off
setlocal
cd /d "%~dp0\.."

REM =============================================================================
REM  start.bat — Pull latest config and start all services on Hetzner
REM  Run this after first-time cert setup or when the server needs a cold start.
REM =============================================================================

REM ── Edit these values once ───────────────────────────────────────────────────
set HETZNER_HOST=178.104.236.119
set HETZNER_USER=root
set SSH_KEY=%USERPROFILE%\.ssh\id_ed25519
REM ─────────────────────────────────────────────────────────────────────────────

echo.
echo  ==========================================
echo   CRM Start
echo   Server: %HETZNER_USER%@%HETZNER_HOST%
echo  ==========================================
echo.

echo Connecting to server and starting all services...
echo.

ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% ^
  "cd /opt/crm && git pull && docker compose -f docker-compose.prod.yml --env-file .env.production up -d && docker image prune -f"

if %errorlevel% neq 0 (
    echo.
    echo ERROR: Start failed. Check the output above.
    exit /b 1
)

echo.
echo  ==========================================
echo   Done! App is live at https://zoeily.com
echo  ==========================================
echo.
endlocal
