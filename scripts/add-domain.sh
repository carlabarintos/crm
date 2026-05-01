#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# add-domain.sh — Add an extra domain (e.g. zoeily.com) to a running CRM server
# Usage:  bash add-domain.sh <new-domain>
# Example: bash add-domain.sh zoeily.com
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

NEW_DOMAIN="${1:?ERROR: pass the new domain as the first argument, e.g.: bash add-domain.sh zoeily.com}"
APP_DIR="/opt/crm"
NGINX_CONF="$APP_DIR/nginx/nginx.conf"
COMPOSE_FILE="$APP_DIR/docker-compose.prod.yml"
ENV_FILE="$APP_DIR/.env.production"

echo ""
echo "=========================================="
echo "  Adding domain: $NEW_DOMAIN"
echo "  App dir:        $APP_DIR"
echo "=========================================="
echo ""

# ── Guard: already done? ──────────────────────────────────────────────────────
if grep -q "$NEW_DOMAIN" "$NGINX_CONF" 2>/dev/null; then
  NGINX_ALREADY_SET=1
else
  NGINX_ALREADY_SET=0
fi

# ── 1. Get SSL certificate ─────────────────────────────────────────────────────
if [ -d "/etc/letsencrypt/live/$NEW_DOMAIN" ]; then
  echo "[1/4] Certificate already exists for $NEW_DOMAIN — skipping certbot."
else
  echo "[1/4] Obtaining SSL certificate for $NEW_DOMAIN..."

  # Open firewall ports (UFW + Hetzner-level if ufw is active)
  ufw allow 80/tcp  2>/dev/null || true
  ufw allow 443/tcp 2>/dev/null || true
  ufw reload        2>/dev/null || true

  # Stop Docker nginx container
  docker compose -f "$COMPOSE_FILE" rm -sf nginx 2>/dev/null || true

  # Stop any system-level web servers that may hold port 80
  systemctl stop nginx  2>/dev/null || true
  systemctl stop apache2 2>/dev/null || true

  # Flush Docker's IPv4 and IPv6 NAT rules so no stale iptables entry
  # returns 404 for the challenge path on a dead container
  iptables  -t nat -F DOCKER-INGRESS 2>/dev/null || true
  ip6tables -t nat -F DOCKER-INGRESS 2>/dev/null || true
  fuser -k 80/tcp  2>/dev/null || true
  fuser -k 80/tcp6 2>/dev/null || true
  pkill -f 'docker-proxy.*:80' 2>/dev/null || true

  # Wait until port 80 is confirmed free (up to 30 s)
  echo "  Waiting for port 80 to be free..."
  for i in $(seq 1 30); do
    if ! ss -tlnp | grep -q ':80 '; then
      echo "  Port 80 is free after ${i}s."
      break
    fi
    sleep 1
  done

  # Show what is still on port 80 (helps diagnose if certbot fails again)
  echo "  Processes on port 80 right now:"
  ss -tlnp | grep ':80 ' || echo "  (none)"

  certbot certonly \
    --standalone \
    --non-interactive \
    --agree-tos \
    --email "admin@$NEW_DOMAIN" \
    -d "$NEW_DOMAIN"

  echo "  Certificate obtained."
fi

# ── 2. Add nginx server block ─────────────────────────────────────────────────
if [ "$NGINX_ALREADY_SET" -eq 0 ]; then
  echo "[2/4] Adding nginx HTTPS block for $NEW_DOMAIN..."

  NEW_BLOCK="
    # ── HTTPS: $NEW_DOMAIN ────────────────────────────────────────────────────
    server {
        listen 443 ssl;
        listen [::]:443 ssl;
        server_name $NEW_DOMAIN www.$NEW_DOMAIN;

        ssl_certificate     /etc/letsencrypt/live/$NEW_DOMAIN/fullchain.pem;
        ssl_certificate_key /etc/letsencrypt/live/$NEW_DOMAIN/privkey.pem;
        ssl_protocols       TLSv1.2 TLSv1.3;
        ssl_ciphers         HIGH:!aNULL:!MD5;

        location /auth/ {
            proxy_pass         http://keycloak:8080/auth/;
            proxy_set_header   Host              \$host;
            proxy_set_header   X-Real-IP         \$remote_addr;
            proxy_set_header   X-Forwarded-For   \$proxy_add_x_forwarded_for;
            proxy_set_header   X-Forwarded-Proto \$scheme;
            proxy_set_header   X-Forwarded-Host  \$host;
            proxy_buffer_size  128k;
            proxy_buffers      4 256k;
        }

        location /api/notifications/stream {
            proxy_pass             http://crm-api:8080;
            proxy_set_header       Host              \$host;
            proxy_set_header       X-Real-IP         \$remote_addr;
            proxy_set_header       X-Forwarded-For   \$proxy_add_x_forwarded_for;
            proxy_set_header       X-Forwarded-Proto \$scheme;
            proxy_set_header       Connection        '';
            proxy_http_version     1.1;
            proxy_buffering        off;
            proxy_cache            off;
            proxy_read_timeout     3600s;
            chunked_transfer_encoding on;
            add_header             X-Accel-Buffering no;
        }

        location /api/ {
            proxy_pass         http://crm-api:8080;
            proxy_set_header   Host              \$host;
            proxy_set_header   X-Real-IP         \$remote_addr;
            proxy_set_header   X-Forwarded-For   \$proxy_add_x_forwarded_for;
            proxy_set_header   X-Forwarded-Proto \$scheme;
            proxy_read_timeout 120s;
        }

        location / {
            proxy_pass         http://crm-web:8080;
            proxy_set_header   Host              \$host;
            proxy_set_header   X-Real-IP         \$remote_addr;
            proxy_set_header   X-Forwarded-For   \$proxy_add_x_forwarded_for;
            proxy_set_header   X-Forwarded-Proto \$scheme;
        }
    }
"

  python3 - "$NGINX_CONF" "$NEW_BLOCK" <<'PYEOF'
import sys

conf_path = sys.argv[1]
new_block  = sys.argv[2]

with open(conf_path, 'r') as f:
    content = f.read()

last_brace = content.rfind('\n}')
if last_brace == -1:
    print("ERROR: could not find closing brace in nginx.conf", file=sys.stderr)
    sys.exit(1)

updated = content[:last_brace] + '\n' + new_block + '\n}'
with open(conf_path, 'w') as f:
    f.write(updated)

print("  nginx.conf updated.")
PYEOF
else
  echo "[2/4] nginx.conf already contains $NEW_DOMAIN — skipping."
fi

# ── 3. Update AllowedOrigins in .env.production ───────────────────────────────
echo "[3/4] Updating CORS origins in $ENV_FILE..."

if grep -q "EXTRA_ALLOWED_ORIGINS" "$ENV_FILE" 2>/dev/null; then
  CURRENT=$(grep "^EXTRA_ALLOWED_ORIGINS=" "$ENV_FILE" | cut -d= -f2-)
  if echo "$CURRENT" | grep -q "https://$NEW_DOMAIN"; then
    echo "  https://$NEW_DOMAIN already in EXTRA_ALLOWED_ORIGINS."
  else
    NEW_ORIGINS="${CURRENT:+${CURRENT},}https://$NEW_DOMAIN"
    sed -i "s|^EXTRA_ALLOWED_ORIGINS=.*|EXTRA_ALLOWED_ORIGINS=$NEW_ORIGINS|" "$ENV_FILE"
    echo "  EXTRA_ALLOWED_ORIGINS updated to: $NEW_ORIGINS"
  fi
else
  echo "EXTRA_ALLOWED_ORIGINS=https://$NEW_DOMAIN" >> "$ENV_FILE"
  echo "  Added EXTRA_ALLOWED_ORIGINS=https://$NEW_DOMAIN"
fi

# ── 4. Restart nginx + crm-api ────────────────────────────────────────────────
echo "[4/4] Restarting services..."
cd "$APP_DIR"
docker compose -f docker-compose.prod.yml --env-file .env.production up -d nginx
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --no-deps crm-api

echo ""
echo "=========================================="
echo "  Done!  https://$NEW_DOMAIN is now live."
echo "  CORS allows: https://$(grep ^DOMAIN= "$ENV_FILE" | cut -d= -f2) and https://$NEW_DOMAIN"
echo "=========================================="
