---
sidebar_position: 3
title: How To Debug The Frontend
description: Debugging guide for routes, auth middleware, proxy rewrites, SignalR, and frontend checks.
sidebar_label: How To Debug The Frontend
---

# How To Debug The Frontend

> _If any detail here contradicts the code, trust the code — not this page._

Frontend debugging usually starts with one question: did the request fail in Next.js middleware, a Next.js route handler/rewrite, or a backend service?

## Local Checks

```bash
cd frontend
npm run type-check
npm run lint
npm run test
```

For browser flows:

```bash
npm run test:e2e
```

For component isolation:

```bash
npm run storybook
```

## Route And Middleware Checks

The middleware protects `/dashboard` and `/admin`. It strips locale before checking the prefix, so `/{locale}/dashboard` is protected the same way as `/dashboard`.

The middleware matcher excludes:

- `/api/*`
- `/_next/*`
- paths containing `chatHub`
- paths containing `notificationHub`
- paths with a file extension

If an API route behaves unauthenticated, do not expect edge middleware to have protected it.

## Proxy Checks

Static rewrites are active only when `DOCKER_ENV=true` or `NEXT_PUBLIC_USE_PROXY=true`.

Check these paths first:

- `/api/proxy-core/*` should reach Core API `/api/*`.
- `/api/proxy-auth/*` should reach Auth Service `/api/*`.
- localized `/chatHub/*` should reach Core API `/chatHub`.
- localized `/notificationHub/*` should reach Core API `/notificationHub`.

ML proxying is separate from the rewrite table; check the `/api/proxy-ml` route handler path when debugging AI/recommendation traffic.

## Auth Checks

Auth pages live under the public auth route group. Dashboard profile, security, privacy, and AI keys live under protected dashboard paths.

Remember the backend split:

- Auth Service owns credential and token operations.
- Core API owns profile, follow graph, privacy settings, user search, activation/deactivation, and BYOK keys.

`/api/auth/me` and `/api/profile/me` are not the same shape.

## SignalR Checks

SignalR client behavior depends on backend hub paths and Redis when scaled:

- `/chatHub` handles chat.
- `/notificationHub` handles notifications.
- both require JWT at connection time.
- without Redis backplane, multi-replica fan-out fails silently across Core API instances.

For real-time data, prefer hub events plus query invalidation. Do not add polling intervals for chat, notifications, or presence.

## Type And Runtime Safety

When handling unknown API failures, use a type guard or schema parse before reading fields. Avoid `any`, `value as { ... }`, and `value as unknown as { ... }` in production code.

Use `console.warn` or `console.error` only in meaningful catch/error paths. Development-only logs should be gated by `process.env.NODE_ENV === "development"`.
