# CRM Server Cheatsheet

Server IP: `178.104.236.119`
App URL: `https://178.104.236.119.nip.io`

---

## SSH into the server

```bat
ssh -i %USERPROFILE%\.ssh\id_ed25519 root@178.104.236.119
```

---

## Deploy / Rebuild (from Windows dev machine)

```bat
scripts\rebuild.bat api       # rebuild API only
scripts\rebuild.bat web       # rebuild Web only
scripts\rebuild.bat all       # rebuild both
```

---

## Logs (from Windows dev machine)

```bat
scripts\logs.bat api          # tail API logs live
scripts\logs.bat web          # tail Web logs live
scripts\logs.bat keycloak     # tail Keycloak logs
scripts\logs.bat nginx        # tail Nginx logs
scripts\logs.bat seq          # open Seq UI at http://localhost:8090 via SSH tunnel
```

---

## Seq admin password hash

Seq uses a bcrypt hash for the admin password, not plain text.

Generate one (run on the server):
```bash
echo 'your-desired-password' | docker run --rm -i datalust/seq config hash
```

Copy the output (starts with `$2b$...`) and paste it into `.env.production`:
```bash
nano /opt/crm/.env.production
```
```
SEQ_ADMIN_PASSWORD_HASH=$2b$12$...paste-hash-here...
```

Then restart Seq:
```bash
docker compose -f /opt/crm/docker-compose.prod.yml --env-file .env.production up -d --force-recreate seq
```

---

## Edit environment variables on the server

```bash
nano /opt/crm/.env.production
```

After saving, restart the affected service:
```bash
# Restart API (picks up Keycloak, DB, Seq, etc.)
docker compose -f /opt/crm/docker-compose.prod.yml --env-file .env.production up -d --force-recreate crm-api

# Restart Web
docker compose -f /opt/crm/docker-compose.prod.yml --env-file .env.production up -d --force-recreate crm-web

# Restart all
docker compose -f /opt/crm/docker-compose.prod.yml --env-file .env.production up -d --force-recreate
```

---

## Check container status

```bash
docker compose -f /opt/crm/docker-compose.prod.yml --env-file .env.production ps
```

---

## Restart a single service

```bash
docker compose -f /opt/crm/docker-compose.prod.yml --env-file .env.production restart crm-api
```

---

## Pull latest code + restart (without rebuilding images)

```bash
cd /opt/crm && git pull
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --remove-orphans
```

---

## Renew SSL certificate

```bash
docker compose -f /opt/crm/docker-compose.prod.yml --env-file .env.production stop nginx
certbot renew
docker compose -f /opt/crm/docker-compose.prod.yml --env-file .env.production start nginx
```

---

## Keycloak admin console

URL: `https://178.104.236.119.nip.io/auth/admin/`

Credentials are in `.env.production` under `KEYCLOAK_ADMIN` / `KEYCLOAK_ADMIN_PASSWORD`.

---

## SuperAdmin login (CRM app)

Default credentials (set in `.env.production` under `SuperAdmin__Email` / `SuperAdmin__Password`):
- Email: `superadmin@crm.local`
- Password: `SuperAdmin@123`

Change these by adding to `.env.production`:
```
SuperAdmin__Email=you@example.com
SuperAdmin__Password=YourStrongPassword
```
Then restart the API — it creates the user on first startup if it doesn't exist.

---

## Switch to a real domain later

1. Point your domain's A record to `178.104.236.119`
2. Edit `.env.production` on the server: change `DOMAIN=yourdomain.com`
3. Get a new SSL cert:
   ```bash
   docker compose -f /opt/crm/docker-compose.prod.yml --env-file .env.production stop nginx
   certbot certonly --standalone -d yourdomain.com --non-interactive --agree-tos -m your@email.com
   ```
4. Restart everything:
   ```bash
   docker compose -f /opt/crm/docker-compose.prod.yml --env-file .env.production up -d --force-recreate
   ```
