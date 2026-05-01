@echo off
setlocal
cd /d "%~dp0\.."

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

echo Connecting to server...
echo (This will take a few minutes for the SSL certificate)
echo.

set REMOTE_CMD=cd /opt/crm ^&^& git pull ^&^& chmod +x scripts/add-domain.sh ^&^& bash scripts/add-domain.sh %NEW_DOMAIN%

ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% "%REMOTE_CMD%"

if %errorlevel% neq 0 (
    echo.
    echo ERROR: add-domain failed. Check the output above.
    exit /b 1
)

echo.
echo  ==========================================
echo   Done! https://%NEW_DOMAIN% is now live.
echo   SSL cert and CORS are both configured.
echo  ==========================================
echo.
endlocal
