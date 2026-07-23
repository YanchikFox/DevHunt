# DevHunt Documentation Hub

## Overview

Central index for all project documentation: architecture, API references, operational guides, and developer onboarding.

## Quick Links

| Document                                          | Description                        |
| ------------------------------------------------- | ---------------------------------- |
| [Architecture](../AI_ARCHITECTURE.md)             | System architecture overview       |
| [Core API Reference](../DevHunt.CoreApi/API_DOCUMENTATION.md) | REST API endpoint documentation |
| [Core API Swagger](http://localhost:7002/swagger)  | Interactive API explorer          |
| [Auth API Swagger](http://localhost:7001/swagger)  | Auth service API explorer         |
| [Docker Cheat Sheet](../DOCKER_CHEAT_SHEET.md)     | Common Docker commands            |

## Backend Services

| Service               | Port  | Technology            | README                                        |
| --------------------- | ----- | --------------------- | --------------------------------------------- |
| Core API              | 7002  | .NET 10, EF Core 9    | [README](../DevHunt.CoreApi/README.md)        |
| Auth Service          | 7001  | .NET 10               | [README](../DevHunt.AuthService/README.md)    |
| ML Service            | 8000  | Python, FastAPI        | [README](../ml-service/README.md)             |
| Notification Service  | 5003  | Node.js, Express 5     | [README](../notification-service/README.md)   |
| Integration Gateway   | 5002  | Node.js, Express 5     | [README](../integration-gateway/README.md)    |

## Infrastructure

| Component       | Port(s)       | Technology       | README                                        |
| --------------- | ------------- | ---------------- | --------------------------------------------- |
| PostgreSQL      | 5432          | PostgreSQL 16    | —                                             |
| Redis           | 6379          | Redis 7          | —                                             |
| RabbitMQ        | 5672 / 15672  | RabbitMQ 3       | —                                             |
| SeaweedFS (S3)  | 8333 / 8888   | SeaweedFS        | [README](../seaweedfs/README.md)              |
| Nginx Gateway   | 80 / 443      | Nginx            | [README](../nginx/README.md)                  |
| Monitoring      | 9090 / 5080   | Prometheus / OO  | [README](../monitoring/README.md)             |

## Localized Docs

- [Polish docs](pl/) — feature guides translated to Polish.

## Contributing

- Keep documentation in sync with code changes.
- Use English as the primary language.
- Markdown format with consistent heading hierarchy.
