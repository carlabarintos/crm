@echo off
setlocal
cd /d "%~dp0\.."

REM =============================================================================
REM  deploy-scanning.bat — Deploy ClamAV document scanning to Hetzner
REM  Run from your Windows dev machine to push files and trigger the server deploy.
REM
REM  Requirements:
REM    - OpenSSH (built into Windows 11)
REM    - SSH key with root access to the Hetzner server
REM
REM  What it does:
REM    1. Uploads docker-compose.scanning.yml to /opt/crm/
REM    2. Uploads deploy-scanning.sh   to /opt/crm/scripts/
REM    3. SSHes in and runs deploy-scanning.sh on the server
REM =============================================================================

REM ── Edit these values once ───────────────────────────────────────────────────
set HETZNER_HOST=178.104.236.119
set HETZNER_USER=root
set SSH_KEY=%USERPROFILE%\.ssh\id_ed25519
REM ─────────────────────────────────────────────────────────────────────────────

echo.
echo  ==========================================
echo   CRM Document Scanning ^— Deploy
echo   Target: %HETZNER_USER%@%HETZNER_HOST%
echo  ==========================================
echo.

REM ── Step 1: Upload compose override ──────────────────────────────────────────
echo [1/3] Uploading docker-compose.scanning.yml...
scp -i "%SSH_KEY%" -o StrictHostKeyChecking=no ^
  docker-compose.scanning.yml ^
  %HETZNER_USER%@%HETZNER_HOST%:/opt/crm/docker-compose.scanning.yml
if %errorlevel% neq 0 (
    echo.
    echo ERROR: SCP upload failed.
    echo Make sure the server is reachable and your SSH key is authorised.
    exit /b 1
)

REM ── Step 2: Upload server-side deploy script ──────────────────────────────────
echo [2/3] Uploading deploy-scanning.sh...
scp -i "%SSH_KEY%" -o StrictHostKeyChecking=no ^
  scripts\deploy-scanning.sh ^
  %HETZNER_USER%@%HETZNER_HOST%:/opt/crm/scripts/deploy-scanning.sh
if %errorlevel% neq 0 (
    echo.
    echo ERROR: SCP upload failed.
    exit /b 1
)

REM ── Step 3: Run deploy on the server ─────────────────────────────────────────
echo [3/3] Running document scanning deploy on server...
echo.
echo  Note: First run downloads ClamAV virus definitions (~250 MB).
echo  This step can take up to 10 minutes — please wait.
echo.
ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% ^
  "dos2unix /opt/crm/scripts/deploy-scanning.sh 2>/dev/null || true && chmod +x /opt/crm/scripts/deploy-scanning.sh && bash /opt/crm/scripts/deploy-scanning.sh"
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Remote deploy failed.
    echo.
    echo Troubleshoot with:
    echo   ssh %HETZNER_USER%@%HETZNER_HOST% docker logs crm-clamav
    echo   ssh %HETZNER_USER%@%HETZNER_HOST% docker ps
    exit /b 1
)

echo.
echo  ==========================================
echo   Done! Document scanning is live.
echo   https://zoeily.com
echo  ==========================================
echo.
endlocal
