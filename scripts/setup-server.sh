#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# setup-server.sh — Run once on a fresh Hetzner Ubuntu 24.04 VPS
# Usage: bash setup-server.sh <your-domain>
# Example: bash setup-server.sh 49.13.12.34.nip.io
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

DOMAIN="${1:?ERROR: pass your domain as the first argument, e.g.: bash setup-server.sh 49.13.12.34.nip.io}"
REPO_URL="https://github.com/carlabarintos/crm.git"
APP_DIR="/opt/crm"
EMAIL="admin@example.com"   # used by Let's Encrypt for expiry notices (can be any email)

echo ""
echo "=========================================="
echo "  CRM Server Setup"
echo "  Domain: $DOMAIN"
echo "=========================================="
echo ""

# ── 1. System update ──────────────────────────────────────────────────────────
echo "[1/6] Updating system packages..."
apt-get update -qq && apt-get upgrade -y -qq

# ── 2. Install Docker ─────────────────────────────────────────────────────────
echo "[2/6] Installing Docker..."
curl -fsSL https://get.docker.com | sh
systemctl enable --now docker
echo "Docker $(docker --version) installed."

# ── 3. Clone the repo ─────────────────────────────────────────────────────────
echo "[3/6] Cloning repo to $APP_DIR..."
if [ -d "$APP_DIR" ]; then
  echo "  Directory already exists — pulling latest..."
  git -C "$APP_DIR" pull
else
  git clone "$REPO_URL" "$APP_DIR"
fi
cd "$APP_DIR"

# ── 4. Create .env.production ─────────────────────────────────────────────────
echo "[4/6] Setting up .env.production..."
if [ ! -f ".env.production" ]; then
  cp .env.production.example .env.production
  sed -i "s|DOMAIN=.*|DOMAIN=$DOMAIN|" .env.production
  echo ""
  echo "  ┌──────────────────────────────────────────────────────────────┐"
  echo "  │  ACTION REQUIRED: Edit /opt/crm/.env.production              │"
  echo "  │  Replace all 'change_me' values with strong passwords.       │"
  echo "  │  Run:  nano /opt/crm/.env.production                         │"
  echo "  │  Then press Ctrl+X, Y, Enter to save.                        │"
  echo "  │  Then re-run this script to continue.                        │"
  echo "  └──────────────────────────────────────────────────────────────┘"
  echo ""
  exit 0
else
  echo "  .env.production already exists — skipping."
fi

# ── 5. SSL certificate via Let's Encrypt ──────────────────────────────────────
echo "[5/6] Getting SSL certificate for $DOMAIN..."
apt-get install -y -qq certbot

# Stop nginx if running (port 80 must be free for standalone challenge)
docker compose -f docker-compose.prod.yml stop nginx 2>/dev/null || true

certbot certonly \
  --standalone \
  --non-interactive \
  --agree-tos \
  --email "$EMAIL" \
  -d "$DOMAIN"

# Patch nginx.conf with the actual domain (replaces DOMAIN_PLACEHOLDER)
sed -i "s|DOMAIN_PLACEHOLDER|$DOMAIN|g" nginx/nginx.conf
echo "  SSL certificate obtained."

# ── 6. Start all services ─────────────────────────────────────────────────────
echo "[6/6] Starting services..."
docker compose -f docker-compose.prod.yml --env-file .env.production up -d

echo ""
echo "=========================================="
echo "  Done! App is live at https://$DOMAIN"
echo "=========================================="
