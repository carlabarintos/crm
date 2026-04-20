# Deploying CRM Sales to Hetzner (Singapore)

This guide walks through everything needed to get the app running on a Hetzner VPS — from creating the server to making your first push auto-deploy.

---

## What gets deployed

```
Internet → Nginx (HTTPS)
             ├── /          Blazor WASM web app
             ├── /api/      REST API
             └── /auth/     Keycloak (identity / login)

Internal Docker network:
  postgres   database
  rabbitmq   message queue
  keycloak   authentication
  crm-api    .NET 10 API
  crm-web    .NET 10 Blazor host
```

Every push to `master` triggers GitHub Actions to build new Docker images and redeploy automatically.

---

## Step 1 — Generate your SSH key (Windows, skip if you have one)

Open **PowerShell** and run:

```powershell
ssh-keygen -t ed25519 -C "crm-deploy"
```

Press Enter for all prompts. Two files are created in `C:\Users\YourName\.ssh\`:

| File | What it is |
|------|-----------|
| `id_ed25519` | **Private key** — never share this |
| `id_ed25519.pub` | **Public key** — paste into Hetzner |

To view your public key:
```powershell
type $env:USERPROFILE\.ssh\id_ed25519.pub
```
Copy the whole line starting with `ssh-ed25519 ...`

---

## Step 2 — Create the Hetzner server

1. Go to [console.hetzner.cloud](https://console.hetzner.cloud)
2. Create a new **Project** → name it `crm`
3. In the left sidebar → **Security** → **SSH Keys** → **Add SSH Key**
   - Paste the `ssh-ed25519 ...` line from Step 1
   - Name it `crm-deploy` → Save
4. **Servers** → **Add Server**:
   - Location: **Singapore**
   - Image: **Ubuntu 24.04**
   - Type: **CX22** (2 vCPU / 4 GB RAM)
   - SSH Keys: tick **crm-deploy**
   - Click **Create & Buy**
5. Note the **IPv4 address** — e.g. `49.13.12.34`
6. **Firewalls** → **Create Firewall**, add these inbound rules:
   - TCP **22** (SSH)
   - TCP **80** (HTTP)
   - TCP **443** (HTTPS)
   - Apply firewall to your server

---

## Step 3 — Your hostname (no domain needed)

Since you don't have a custom domain yet, use **nip.io** — a free service that maps any IP to a hostname automatically.

> If your IP is `49.13.12.34`, your hostname is `49.13.12.34.nip.io`

No signup, no DNS changes — it just works. Let's Encrypt also accepts nip.io hostnames for SSL certificates.

**When you get a real domain later:**
- Point its A record to your Hetzner IP
- SSH into the server, edit `/opt/crm/.env.production` and change `DOMAIN=`
- Run: `docker compose -f docker-compose.prod.yml up -d`
- Renew the SSL cert: `certbot certonly --standalone -d yourcomain.com`

---

## Step 4 — Set up the server (run once)

SSH into the server:
```bash
ssh root@49.13.12.34
```

Run the setup script (replace with your actual IP):
```bash
bash <(curl -s https://raw.githubusercontent.com/carlabarintos/crm/master/scripts/setup-server.sh) 49.13.12.34.nip.io
```

The script will:
1. Update system packages
2. Install Docker
3. Clone the repo to `/opt/crm`
4. Create `/opt/crm/.env.production` and then **pause** asking you to fill in passwords

**Fill in your passwords:**
```bash
nano /opt/crm/.env.production
```
Change every `change_me` value to a strong password. Press `Ctrl+X`, `Y`, `Enter` to save.

Then run the script again to finish:
```bash
bash /opt/crm/scripts/setup-server.sh 49.13.12.34.nip.io
```

This gets the SSL certificate and starts all services. When it finishes, visit `https://49.13.12.34.nip.io`.

---

## Step 5 — Wire up GitHub Actions (automated deploys)

You need your **private** key to give GitHub access to the server.

On Windows:
```powershell
type $env:USERPROFILE\.ssh\id_ed25519
```
Copy everything including the `-----BEGIN` and `-----END` lines.

In GitHub → your repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:

| Secret name | Value |
|-------------|-------|
| `HETZNER_HOST` | `49.13.12.34` |
| `HETZNER_USER` | `root` |
| `HETZNER_SSH_KEY` | Your full private key (including BEGIN/END lines) |

That's it. Every `git push` to `master` now:
1. Builds new Docker images
2. Pushes them to GitHub Container Registry
3. SSHes into Hetzner and restarts the containers

---

## Manual deploy from Windows (without waiting for CI)

Useful for urgent fixes or the very first deployment.

**Before first use**, edit `scripts/deploy.bat` and set:
- `GITHUB_OWNER` — your GitHub username
- `GITHUB_TOKEN` — a Personal Access Token with `write:packages` scope
  (create at github.com → Settings → Developer settings → Personal access tokens)
- `HETZNER_HOST` — your server IP
- `SSH_KEY` — path to your private key (default is `%USERPROFILE%\.ssh\id_ed25519`)

Then double-click `scripts\deploy.bat` or run it from a terminal.

---

## Useful server commands

SSH into the server first: `ssh root@49.13.12.34`

```bash
# Check all containers are running
docker compose -f /opt/crm/docker-compose.prod.yml ps

# View API logs (live)
docker compose -f /opt/crm/docker-compose.prod.yml logs -f crm-api

# Restart a single service
docker compose -f /opt/crm/docker-compose.prod.yml restart crm-api

# Pull latest images and restart (manual redeploy)
cd /opt/crm
docker compose -f docker-compose.prod.yml pull crm-api crm-web
docker compose -f docker-compose.prod.yml up -d

# Renew SSL certificate
certbot renew
docker compose -f /opt/crm/docker-compose.prod.yml restart nginx
```
