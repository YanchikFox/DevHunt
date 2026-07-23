---
sidebar_position: 2
title: How To Run The Frontend
description: Source-derived run guide for the Next.js frontend in local and Docker proxy modes.
sidebar_label: How To Run The Frontend
---

# How To Run The Frontend

> _If any detail here contradicts the code, trust the code — not this page._

The frontend can run either inside Docker Compose or locally through `next dev`.

## Docker Compose Path

Compose sets proxy mode explicitly:

- `NEXT_PUBLIC_USE_PROXY=true`
- `DOCKER_ENV=true`
- `NEXT_PUBLIC_API_URL=/api/proxy-core`
- `NEXT_PUBLIC_AUTH_URL=/api/proxy-auth`
- `NEXT_PUBLIC_AUTH_API_URL=/api/proxy-auth`
- `NEXT_PUBLIC_WS_URL=""`

Run the frontend with its required backend dependencies:

```bash
docker compose up -d db cache-service message-broker object-storage db-migrator auth-service core-api frontend
```

Default frontend host port is `3000`.

## Local Next.js Path

Run infrastructure/backend services first, then start Next.js:

```bash
docker compose up -d db cache-service message-broker object-storage db-migrator auth-service core-api
cd frontend
npm install
NEXT_PUBLIC_USE_PROXY=true npm run dev
```

With `NEXT_PUBLIC_USE_PROXY=true`, `next.config.mjs` rewrites `/api/proxy-core/*` and `/api/proxy-auth/*` to the configured backend targets. In local mode those destinations are `localhost:7002` and `localhost:7001`.

## Useful Commands

```bash
cd frontend
npm run type-check
npm run lint
npm run test
npm run test:e2e
npm run build
```

Use `npm run storybook` for component work. Storybook runs on port `6006` per the package script.

## Environment Notes

- `AUTH_TRUST_HOST=true` and `AUTH_URL` are set by Compose for NextAuth.
- `AUTH_SERVICE_URL` points server-side auth work to `http://auth-service:8080` in Docker.
- `NEXT_PUBLIC_WS_URL` is empty in Compose, so WebSocket paths are relative and go through the gateway/proxy path.
- Image remote patterns allow DevHunt domains, DiceBear, localhost object storage, and `object-storage:8333`.

## Common Run Failures

- If pages render but API calls fail, check whether proxy mode is on and whether rewrites are active.
- If protected pages redirect unexpectedly, check NextAuth session state and locale stripping in middleware.
- If SignalR cannot connect, check that the path is `/chatHub` or `/notificationHub`; `/api/*` does not forward WebSocket upgrades through Nginx.
- If uploads appear broken while APIs are healthy, remember object-storage bucket initialization is non-fatal in Core API startup.
