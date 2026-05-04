#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# deploy-scanning.sh — Start ClamAV antivirus and document storage on the server
# Run as root on Hetzner:  bash /opt/crm/scripts/deploy-scanning.sh
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

APP_DIR="/opt/crm"
COMPOSE_MAIN="$APP_DIR/docker-compose.prod.yml"
COMPOSE_SCAN="$APP_DIR/docker-compose.scanning.yml"
ENV_FILE="$APP_DIR/.env.production"

COMPOSE="docker compose -f $COMPOSE_MAIN -f $COMPOSE_SCAN --env-file $ENV_FILE"

echo ""
echo "=============================================="
echo "  CRM — Deploy Document Scanning"
echo "  $(date)"
echo "=============================================="
echo ""

# ── 1. Pull latest code ───────────────────────────────────────────────────────
echo "[1/5] Pulling latest repo..."
git -C "$APP_DIR" pull

# ── 2. Pull the ClamAV image ─────────────────────────────────────────────────
echo "[2/5] Pulling clamav/clamav:stable..."
docker pull clamav/clamav:stable

# ── 3. Start ClamAV and wait for healthy ─────────────────────────────────────
echo "[3/5] Starting ClamAV container..."
$COMPOSE up -d clamav

echo ""
echo "  Waiting for ClamAV to load virus definitions."
echo "  First run downloads ~250 MB — this can take up to 10 minutes."
echo "  Subsequent starts use the cached crm-clamav-db volume (under 30 s)."
echo ""

max=600
elapsed=0
while true; do
  status=$(docker inspect --format='{{.State.Health.Status}}' crm-clamav 2>/dev/null || echo "unknown")
  if [ "$status" = "healthy" ]; then
    echo "  ClamAV is healthy. (${elapsed}s)"
    break
  fi
  if [ $elapsed -ge $max ]; then
    echo "  WARNING: ClamAV did not report healthy within ${max}s."
    echo "  Current status: $status"
    echo "  Check logs with:  docker logs crm-clamav"
    echo "  Continuing — the API rejects uploads gracefully if ClamAV is unavailable."
    break
  fi
  printf "  [%3ds] status: %s\n" "$elapsed" "$status"
  sleep 15
  elapsed=$((elapsed + 15))
done

# ── 4. Restart API with scanning env vars ────────────────────────────────────
echo ""
echo "[4/5] Restarting crm-api with document scanning configuration..."
$COMPOSE up -d --no-deps crm-api
echo "  Waiting 10 s for API to come up..."
sleep 10

# ── 5. Verify ────────────────────────────────────────────────────────────────
echo ""
echo "[5/5] Service status:"
$COMPOSE ps clamav crm-api

echo ""
echo "=============================================="
echo "  Done! Document scanning is active."
echo ""
echo "  Upload a document:  POST /api/orders/{id}/documents"
echo "  List documents:     GET  /api/orders/{id}/documents"
echo "  Download:           GET  /api/orders/{id}/documents/{docId}/download"
echo ""
echo "  Files are stored in Docker volume: crm-documents"
echo "  Virus definitions:  Docker volume: crm-clamav-db"
echo "=============================================="
echo ""
