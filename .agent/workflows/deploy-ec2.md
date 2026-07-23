---
description: Deploy DevHunt to an AWS EC2 instance via rsync + Docker Compose
version: 1.1.0
last-updated: 2026-05-25
maintainer: ops
requires-env:
  - EC2_HOST       # Public IP or DNS of the EC2 instance
  - EC2_USER       # SSH username (typically "ubuntu")
  - SSH_KEY_PATH   # Absolute path to the .pem key file
  - PROJECT_PATH   # Absolute local path to the repo root
---

# Deploy DevHunt to AWS EC2

All connection details are read from environment variables — never hardcode IP
addresses, usernames, or key paths in this file.

```bash
# Export required vars before running any command below
export EC2_HOST="<ip-or-hostname>"
export EC2_USER="ubuntu"
export SSH_KEY_PATH="$HOME/.ssh/devhunt-ec2.pem"
export PROJECT_PATH="$(pwd)"   # repo root
```

:::warning
Never commit `.env`, `*.pem`, or any file containing real credentials.
Store production secrets in AWS Secrets Manager or Parameter Store.
:::

---

## Quick Deploy (after initial setup is complete)

```bash
# Full deploy: sync files → build images → restart services
./scripts/deployment/deploy.sh deploy

# Individual steps
./scripts/deployment/deploy.sh sync      # Sync files only
./scripts/deployment/deploy.sh restart   # Restart services only
./scripts/deployment/deploy.sh status    # Check service status
./scripts/deployment/deploy.sh logs      # Stream all logs
./scripts/deployment/deploy.sh ssh       # Open interactive SSH session
```

---

## Step 1 — Verify SSH Connectivity

```bash
ssh -i "$SSH_KEY_PATH" -o StrictHostKeyChecking=no \
    "${EC2_USER}@${EC2_HOST}" "echo 'SSH connection successful'"
```

---

## Step 2 — Install Docker on EC2 (first time only)

```bash
# Run setup script on the remote host
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}" "bash -s" \
    < scripts/deployment/setup-ec2-docker.sh
```

Or manually via SSH:

```bash
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}"

# On the EC2 instance:
sudo apt update && sudo apt upgrade -y
sudo apt install -y docker.io docker-compose-v2 git
sudo systemctl enable docker && sudo systemctl start docker
sudo usermod -aG docker "$USER"
exit

# Reconnect to apply the docker group membership
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}"
docker --version
```

---

## Step 3 — Sync Project Files

Preferred: `rsync` (incremental, excludes build artifacts)

```bash
rsync -avz --progress \
  --exclude 'node_modules' \
  --exclude '.git' \
  --exclude 'bin' \
  --exclude 'obj' \
  --exclude '__pycache__' \
  --exclude '*.log' \
  -e "ssh -i ${SSH_KEY_PATH}" \
  "${PROJECT_PATH}/" \
  "${EC2_USER}@${EC2_HOST}:~/devhunt/"
```

Alternative: archive and upload (slower, works without rsync on host)

```bash
# Create archive (run from repo root)
tar --exclude='node_modules' --exclude='.git' \
    --exclude='bin' --exclude='obj' --exclude='__pycache__' \
    -czvf /tmp/devhunt-deploy.tar.gz -C "$PROJECT_PATH" .

# Upload and extract
scp -i "$SSH_KEY_PATH" /tmp/devhunt-deploy.tar.gz \
    "${EC2_USER}@${EC2_HOST}:~/"

ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}" \
    "mkdir -p ~/devhunt && cd ~/devhunt && \
     tar -xzf ~/devhunt-deploy.tar.gz && \
     rm ~/devhunt-deploy.tar.gz"
```

---

## Step 4 — Deploy Production Secrets

```bash
scp -i "$SSH_KEY_PATH" \
    scripts/deployment/.env.production \
    "${EC2_USER}@${EC2_HOST}:~/devhunt/.env"
```

:::danger
`.env.production` must be listed in `.gitignore` and must never be committed.
Use AWS Parameter Store or Secrets Manager for long-term secret storage.
:::

---

## Step 5 — AWS Security Group Configuration

Ensure inbound rules include:

| Port | Protocol | Source | Purpose |
|------|----------|--------|---------|
| 22 | TCP | Your IP only | SSH access |
| 80 | TCP | 0.0.0.0/0 | HTTP |
| 443 | TCP | 0.0.0.0/0 | HTTPS |

Do **not** open application ports (7001, 7002, 5080, etc.) publicly — route everything through Nginx on 80/443.

---

## Step 6 — Build and Start Services

```bash
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}" \
    "cd ~/devhunt && docker compose build && docker compose up -d"
```

Or interactively:

```bash
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}"
cd ~/devhunt

# Start infrastructure first and wait
docker compose up -d db cache-service message-broker object-storage
sleep 30

# Start all services
docker compose up -d

# Verify
docker compose ps
docker compose logs -f
```

---

## Step 7 — Verify Deployment

```bash
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}" bash <<'EOF'
docker compose ps
curl -sf http://localhost/health     || echo "nginx: FAIL"
curl -sf http://localhost:7001/health || echo "auth-service: FAIL"
curl -sf http://localhost:7002/health || echo "core-api: FAIL"
EOF
```

Access your deployed application:

| Endpoint | URL |
|----------|-----|
| Frontend | `http://${EC2_HOST}` |
| Core API | `http://${EC2_HOST}/api` |
| Docs portal | `http://${EC2_HOST}:9000` |

---

## Troubleshooting

```bash
# Stream logs for a specific service
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}" \
    "cd ~/devhunt && docker compose logs -f core-api"

# Restart a single service
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}" \
    "cd ~/devhunt && docker compose restart core-api"

# Full rebuild (clears image cache)
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}" "cd ~/devhunt && \
    docker compose down && docker compose build --no-cache && docker compose up -d"

# Disk / memory diagnostics
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}" \
    "df -h && docker system df && free -h && docker stats --no-stream"

# Prune unused images (recovers disk space)
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}" \
    "docker system prune -af"
```

---

## Incremental Update Workflow

After local code changes are committed:

```bash
# 1. Sync only changed files
rsync -avz --checksum \
  --exclude 'node_modules' --exclude '.git' \
  --exclude 'bin' --exclude 'obj' \
  -e "ssh -i ${SSH_KEY_PATH}" \
  "${PROJECT_PATH}/" "${EC2_USER}@${EC2_HOST}:~/devhunt/"

# 2. Rebuild and restart the affected service
ssh -i "$SSH_KEY_PATH" "${EC2_USER}@${EC2_HOST}" \
    "cd ~/devhunt && docker compose build core-api && docker compose up -d core-api"
```

:::tip
Use `chore(infra): …` or `ci(infra): …` commit messages for deployment script changes.
Never append AI co-author lines to deployment commits.
:::
