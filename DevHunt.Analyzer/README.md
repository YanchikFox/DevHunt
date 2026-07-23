# DevHunt Code Analyzer

Python static analysis service using tree-sitter and Semgrep. Runs as a plain stdlib HTTP server (not FastAPI) on port 8090. Core API calls it to analyze project repositories and store results with pgvector semantic search.

## Local development

```bash
docker compose up code-analyzer
```

Health: `http://localhost:8090/health`
