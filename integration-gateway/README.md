# DevHunt Integration Gateway

Express 5 (Node.js) service that handles GitHub/GitLab OAuth flows and incoming webhooks. Tokens and webhook secrets must be set in `.env` — the service hard-fails at startup in production if they are absent.

## Local development

```bash
docker compose up integration-gateway
```

Health: `http://localhost:5002/health`

GitHub/GitLab OAuth credentials are optional for local dev (OAuth flows will simply fail if unset).
