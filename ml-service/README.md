# DevHunt ML Service

FastAPI (Python) service that generates project and team recommendations. Supports Groq and Gemini as LLM providers. Connects to the shared PostgreSQL database directly via asyncpg.

## Local development

```bash
docker compose up ml-service
```

Health: `http://localhost:8000/health`

`DATABASE_URL` must be set before the service serves traffic. It is validated lazily when a DB
connection is first opened (not at import time), so importing the app for OpenAPI generation or
tests does not require it.
