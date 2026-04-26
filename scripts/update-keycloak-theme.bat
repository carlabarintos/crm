@echo off
setlocal
cd /d "%~dp0\.."

REM =============================================================================
REM  update-keycloak-theme.bat — Push Keycloak login theme changes to production
REM
REM  The theme is bind-mounted into the Keycloak container, so no image rebuild
REM  is required. This script:
REM    1. Pulls the latest code on the server (picks up theme file changes)
REM    2. Restarts the keycloak container so it reloads the theme from disk
REM
REM  Edit the three variables below once, then just run the script.
REM =============================================================================

REM ── Edit these values once ───────────────────────────────────────────────────
set HETZNER_HOST=178.104.236.119
set HETZNER_USER=root
set SSH_KEY=%USERPROFILE%\.ssh\id_ed25519
REM ─────────────────────────────────────────────────────────────────────────────

echo.
echo  ==========================================
echo   Keycloak Theme Update
echo   Target: %HETZNER_USER%@%HETZNER_HOST%
echo  ==========================================
echo.

echo [1/2] Pulling latest code on server...
ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% ^
  "cd /opt/crm && git pull"
if %errorlevel% neq 0 (
    echo.
    echo ERROR: git pull failed. Check SSH access and server state.
    exit /b 1
)

echo.
echo [2/2] Restarting Keycloak to reload theme...
ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% ^
  "cd /opt/crm && docker compose -f docker-compose.prod.yml restart keycloak"
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Keycloak restart failed. Check docker compose status on the server.
    exit /b 1
)

echo.
echo  ==========================================
echo   Done! Theme is live at https://%HETZNER_HOST%.nip.io/auth
echo  ==========================================
echo.
endlocal
