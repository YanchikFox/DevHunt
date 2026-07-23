---
sidebar_position: 4
title: Frontend Troubleshooting
description: Known frontend failure modes around auth, rewrites, route handlers, SignalR, and strict mode.
sidebar_label: Frontend Troubleshooting
---

# Frontend Troubleshooting

> _If any detail here contradicts the code, trust the code — not this page._

## Protected Page Redirects To Login

The middleware protects `/dashboard` and `/admin`, after stripping any locale prefix. Check:

- session state from NextAuth v5 beta;
- whether the route is actually under a protected prefix;
- whether `AUTH_URL`, `AUTH_SECRET`, and `AUTH_TRUST_HOST` are set correctly in the runtime mode.

## API Route Is Unauthenticated

Edge middleware excludes `/api/*`. Route handlers and backend services must enforce auth themselves.

Do not assume a page-route protection rule applies to API route handlers.

## REST Calls Hit The Wrong Host

Static rewrites are active only when `DOCKER_ENV=true` or `NEXT_PUBLIC_USE_PROXY=true`.

Expected browser paths:

- `/api/proxy-core/*`
- `/api/proxy-auth/*`
- `/api/proxy-ml/*` through route handler

Do not put Docker service names such as `core-api` into browser-facing URLs.

## SignalR Fails

SignalR paths are `/chatHub` and `/notificationHub`. Nginx does not forward WebSocket upgrades under `/api/*`.

For intermittent delivery under multiple Core API replicas, check Redis backplane configuration before changing client code.

## Effect Runs Twice In Development

`reactStrictMode` is true. Development-only double effects are expected. Make effects idempotent rather than hiding the issue with global flags.

## API Error Parsing Is Fragile

Handle unknown errors with type guards or schema parsing. Avoid `any` and forced casts in production frontend code.

## Real-Time Data Looks Stale

For chat, notifications, and presence, prefer SignalR events plus `queryClient.invalidateQueries`. Do not add `refetchInterval` polling for data already pushed through hubs.
