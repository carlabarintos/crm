@echo off
setlocal
cd /d "%~dp0"

REM =============================================================================
REM  add-domain.bat — Add zoeily.com SSL + CORS to the running Hetzner server
REM  Run from Windows dev machine (double-click from scripts\ folder).
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
echo   Server: %HETZNER_USER%@%HETZNER_HOST%
echo  ==========================================
echo.

REM ── Step 1: Pull latest code on server ───────────────────────────────────────
echo [1/3] Pulling latest code on server...
ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% "cd /opt/crm && git pull"
if %errorlevel% neq 0 (
    echo ERROR: git pull failed on server.
    exit /b 1
)

REM ── Step 2: Upload script to /tmp (avoids any git conflict) ──────────────────
echo [2/3] Uploading add-domain.sh to server...
scp -i "%SSH_KEY%" -o StrictHostKeyChecking=no add-domain.sh %HETZNER_USER%@%HETZNER_HOST%:/tmp/add-domain.sh
if %errorlevel% neq 0 (
    echo ERROR: Upload failed. Check HETZNER_HOST and SSH_KEY above.
    exit /b 1
)

REM ── Step 3: Fix line endings, run script ─────────────────────────────────────
echo [3/3] Running on server...
echo (This will take a few minutes for the SSL certificate)
echo.

set REMOTE_CMD=sed -i 's/\r//' /tmp/add-domain.sh ^&^& chmod +x /tmp/add-domain.sh ^&^& bash /tmp/add-domain.sh %NEW_DOMAIN%

ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% "%REMOTE_CMD%"
if %errorlevel% neq 0 (
    echo.
    echo ERROR: add-domain failed. Check output above.
    exit /b 1
)

echo.
echo  ==========================================
echo   Done! https://%NEW_DOMAIN% is now live.
echo   SSL cert and CORS are both configured.
echo  ==========================================
echo.
endlocal
