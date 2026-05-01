@echo off
setlocal
cd /d "%~dp0\.."

REM =============================================================================
REM  add-domain.bat — Add zoeily.com SSL + CORS to the running Hetzner server
REM  Run from Windows dev machine (scripts\ folder or double-click from explorer).
REM
REM  Requirements:
REM    - OpenSSH (built into Windows 11)
REM    - SSH private key that matches the server's authorized_keys
REM =============================================================================

REM ── Edit these values once ───────────────────────────────────────────────────
set HETZNER_HOST=178.104.236.119
set HETZNER_USER=root
set SSH_KEY=%USERPROFILE%\.ssh\id_ed25519
set NEW_DOMAIN=zoeily.com
REM ─────────────────────────────────────────────────────────────────────────────

echo.
echo  ==========================================
echo   CRM  ^>  Add domain: %NEW_DOMAIN%
echo   Target: %HETZNER_USER%@%HETZNER_HOST%
echo  ==========================================
echo.

REM ── Step 1: Pull latest code + images ───────────────────────────────────────
echo [1/2] Pulling latest code and images on server...
ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% ^
  "cd /opt/crm && rm -f scripts/add-domain.sh && git pull && sed -i 's/\r//' scripts/add-domain.sh && chmod +x scripts/add-domain.sh && docker compose -f docker-compose.prod.yml --env-file .env.production pull crm-api crm-web"
if %errorlevel% neq 0 (
    echo.
    echo ERROR: git pull or docker pull failed on the server.
    pause & exit /b 1
)

REM ── Step 2: Run add-domain.sh on the server ──────────────────────────────────
echo.
echo [2/2] Running add-domain.sh %NEW_DOMAIN% on server...
ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% ^
  "bash /opt/crm/scripts/add-domain.sh %NEW_DOMAIN%"
if %errorlevel% neq 0 (
    echo.
    echo ERROR: add-domain.sh reported a failure. Check output above.
    pause & exit /b 1
)

echo.
echo  ==========================================
echo   Done! https://%NEW_DOMAIN% is now live.
echo   SSL cert and CORS are both configured.
echo  ==========================================
echo.
endlocal
