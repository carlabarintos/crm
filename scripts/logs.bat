@echo off
setlocal
cd /d "%~dp0\.."

REM =============================================================================
REM  logs.bat — View live logs or open Seq UI
REM  Usage:
REM    logs.bat api          — tail crm-api logs
REM    logs.bat web          — tail crm-web logs
REM    logs.bat keycloak     — tail keycloak logs
REM    logs.bat nginx        — tail nginx logs
REM    logs.bat seq          — open Seq UI in browser (via SSH tunnel)
REM =============================================================================

REM ── Edit these values once ───────────────────────────────────────────────────
set HETZNER_HOST=178.104.236.119
set HETZNER_USER=root
set SSH_KEY=%USERPROFILE%\.ssh\id_ed25519
REM ─────────────────────────────────────────────────────────────────────────────

set TARGET=%~1
if "%TARGET%"=="" (
    echo Usage: logs.bat [api ^| web ^| keycloak ^| nginx ^| seq]
    exit /b 1
)

if /i "%TARGET%"=="seq" (
    echo.
    echo  Opening Seq UI at http://localhost:8090
    echo  Press Ctrl+C to close the tunnel when done.
    echo.
    start "" "http://localhost:8090"
    ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no -N -L 8090:localhost:8090 %HETZNER_USER%@%HETZNER_HOST%
    goto :eof
)

set CONTAINER=crm-prod-%TARGET%-1
echo Tailing logs for %CONTAINER% (Ctrl+C to stop)...
echo.
ssh -i "%SSH_KEY%" -o StrictHostKeyChecking=no %HETZNER_USER%@%HETZNER_HOST% "docker logs %CONTAINER% -f --tail 100"

endlocal
