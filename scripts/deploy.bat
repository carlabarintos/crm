@echo off
setlocal
cd /d "%~dp0\.."

REM =============================================================================
REM  deploy.bat — Manual deploy from Windows dev machine to Hetzner
REM  Run this when you want to deploy without waiting for GitHub Actions,
REM  e.g. for urgent fixes or the very first deployment.
REM
REM  Requirements:
REM    - Docker Desktop (running)
REM    - OpenSSH (built into Windows 11)
REM    - A GitHub Personal Access Token with "write:packages" scope
REM      Create one at: github.com -> Settings -> Developer settings -> Tokens
REM =============================================================================

REM ── Edit these values once ───────────────────────────────────────────────────
set GITHUB_OWNER=carlabarintos
set GITHUB_TOKEN=your_github_pat_here
set HETZNER_HOST=178.104.236.119
set HETZNER_USER=root
set SSH_KEY=%USERPROFILE%\.ssh\id_ed25519
REM ─────────────────────────────────────────────────────────────────────────────

echo.
echo  ==========================================
echo   CRM Manual Deploy
echo   Target: %HETZNER_USER%@%HETZNER_HOST%
echo  ==========================================
echo.

REM ── Step 1: Login to GitHub Container Registry ───────────────────────────────
echo [1/4] Logging in to ghcr.io...
echo %GITHUB_TOKEN% | podman login ghcr.io -u %GITHUB_OWNER% --password-stdin
if %errorlevel% neq 0 (
    echo.
    echo ERROR: Docker login failed.
    echo Make sure GITHUB_TOKEN has "write:packages" scope.
    exit /b 1
)

REM ── Step 2: Build images ─────────────────────────────────────────────────────
echo.
echo [2/4] Building images (this may take a few minutes)...
echo.

echo   Building API image...
podman build -f Dockerfile.api -t ghcr.io/%GITHUB_OWNER%/crm-api:latest .
if %errorlevel% neq 0 ( echo ERROR: API image build failed. & exit /b 1 )

echo   Building Web image...
podman build -f Dockerfile.web -t ghcr.io/%GITHUB_OWNER%/crm-web:latest .
if %errorlevel% neq 0 ( echo ERROR: Web image build failed. & exit /b 1 )

REM ── Step 3: Push images ──────────────────────────────────────────────────────
echo.
echo [3/4] Pushing images to ghcr.io...
podman push ghcr.io/%GITHUB_OWNER%/crm-api:latest
podman push ghcr.io/%GITHUB_OWNER%/crm-web:latest

REM ── Step 4: Deploy on server ─────────────────────────────────────────────────
echo.
echo [4/4] Deploying on Hetzner (%HETZNER_HOST%)...
ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% ^
  "cd /opt/crm && git pull && docker compose -f docker-compose.prod.yml pull crm-api crm-web && docker compose -f docker-compose.prod.yml up -d --remove-orphans && docker image prune -f"
if %errorlevel% neq 0 (
    echo.
    echo ERROR: SSH deploy failed.
    echo Make sure the server is reachable and your SSH key is correct.
    exit /b 1
)

echo.
echo  ==========================================
echo   Done! App is live at https://178.104.236.119.nip.io
echo  ==========================================
echo.
endlocal
