---
sidebar_position: 1
title: Frontend Overview
description: Source-derived overview of the DevHunt Next.js application, middleware, proxying, and runtime boundaries.
sidebar_label: Frontend Overview
---

# Frontend Overview

> _If any detail here contradicts the code, trust the code — not this page._

The DevHunt frontend is a Next.js 16 App Router application using React 19, TypeScript, NextAuth v5 beta, `next-intl`, TanStack Query, Zustand, Tailwind CSS, Radix UI, and SignalR.

The browser should not talk to internal backend container hostnames. Backend traffic flows through Next.js rewrites or route handlers.

## Runtime Responsibilities

| Area                  | Current behavior                                                               |
| --------------------- | ------------------------------------------------------------------------------ |
| Routing               | App Router under `frontend/src/app`, with locale segments under `app/[locale]` |
| Auth middleware       | NextAuth wrapped with `next-intl` middleware                                   |
| Protected UI prefixes | `/dashboard`, `/admin`                                                         |
| Public UI             | all paths outside protected prefixes, plus auth/public route groups            |
| API proxy             | `/api/proxy-core`, `/api/proxy-auth`, and route-handler ML proxy               |
| Real-time             | SignalR client connects to `/chatHub` and `/notificationHub` paths             |

The middleware matcher excludes `api`, `_next`, hub paths, and file-extension paths. API route handlers must therefore do their own auth or pass auth downstream.

## Proxy Model

`next.config.mjs` enables rewrites when `DOCKER_ENV=true` or `NEXT_PUBLIC_USE_PROXY=true`:

| Source                              | Destination in Docker                 |
| ----------------------------------- | ------------------------------------- |
| `/api/proxy-core/:path*`            | `http://core-api:8080/api/:path*`     |
| `/api/proxy-auth/:path*`            | `http://auth-service:8080/api/:path*` |
| `/{locale?}/chatHub/:path*`         | Core API `/chatHub/:path*`            |
| `/{locale?}/notificationHub/:path*` | Core API `/notificationHub/:path*`    |

ML traffic is not in this static rewrite table. It flows through the `/api/proxy-ml` route handler.

## Package Scripts

The frontend package exposes:

```bash
npm run dev
npm run build
npm run start
npm run lint
npm run type-check
npm run test
npm run test:e2e
npm run storybook
```

Use `npm run type-check` for TypeScript-only validation and `npm run lint` for ESLint. Unit tests use Vitest; e2e tests use Playwright.

## Important Frontend Constraints

- `next-auth` is v5 beta. Do not apply v4-only routing or session patterns without checking source.
- `reactStrictMode` is enabled, so Development effects can run twice.
- Edge middleware does not protect `/api/*`; auth enforcement belongs in route handlers or backend services.
- SignalR real-time data should be event-driven. Do not poll with `refetchInterval` for chat, notifications, or presence data.
- Production code under `src/` should not use `any` or unguarded forced casts; use typed interfaces, type guards, or Zod parsing.
- Browser-facing URLs should use proxy paths, not container service names.
