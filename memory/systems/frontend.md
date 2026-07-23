---
title: frontend — Next.js 16 web app + edge auth/i18n + API proxy
type: system
status: verified
sources:
  - frontend/src/middleware.ts
  - frontend/next.config.mjs
  - frontend/package.json
  - frontend/Dockerfile
  - docker-compose.yml
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Next.js 16 (App Router) + React 19 web client. Server-side runtime
also acts as the **API proxy** for the browser — all backend calls
travel through Next.js route handlers / rewrites, never to backend
services directly. Auth is `next-auth` v5 beta; i18n is `next-intl`
with locale segments under `app/[locale]/`.

Compose service `frontend`, port `PORT_FRONTEND` (default 3000).
Built with `output: "standalone"` for thin Docker images.

## Entry-points

The framework picks up multiple roots; for navigating the system:

- **Edge middleware** —
  [frontend/src/middleware.ts](frontend/src/middleware.ts).
  Wraps `NextAuth(authConfig)` with `next-intl/middleware`. Runs on
  every non-excluded request.
- **Build/runtime config** —
  [frontend/next.config.mjs](frontend/next.config.mjs). Defines
  rewrites that proxy backend traffic; defines image hosts;
  applies the `next-intl` plugin.
- Auth wiring sits in `frontend/src/auth.config.ts` and
  `frontend/src/auth.ts` (referenced from middleware; not deeply
  read in Step 2).

## Middleware behavior (middleware.ts)

- Protected route prefixes (line 14): `/dashboard`, `/admin`. Every
  other path is public.
- Locale prefix is stripped before the protected check
  (lines 25–31), so `/{locale}/dashboard` is treated the same as
  `/dashboard`.
- Unauthenticated requests to a protected route redirect to
  `/{locale?}/login`.
- Authenticated or public requests fall through to the
  `next-intl` middleware (line 47).
- Matcher excludes `api`, `_next`, anything containing `chatHub`
  or `notificationHub`, and any path with a file extension
  (line 56).

## Rewrites (next.config.mjs `rewrites()`)

Active when `DOCKER_ENV === "true"` **or**
`NEXT_PUBLIC_USE_PROXY === "true"`.

| Source                                | Destination (Docker / local)                                 |
|---------------------------------------|--------------------------------------------------------------|
| `/api/proxy-core/:path*`              | `http://core-api:8080/api/:path*` / `localhost:7002`         |
| `/api/proxy-auth/:path*`              | `http://auth-service:8080/api/:path*` / `localhost:7001`     |
| `/{locale?}/chatHub/:path*`           | core-api `/chatHub/:path*`                                   |
| `/{locale?}/notificationHub/:path*`   | core-api `/notificationHub/:path*`                           |

There is no `proxy-ml` rewrite here, but Step 0 noted a route
handler at `frontend/src/app/api/proxy-ml/`; ML traffic flows
through that handler, not through `rewrites()`. (To be expanded
when domain entries are written.)

## Other config notable bits

- `productionBrowserSourceMaps: false` (line 21) — sourcemaps off
  in prod.
- `optimizePackageImports: ["lucide-react", "@radix-ui/react-dialog"]`
  (line 24) — only those two are tree-shaken specially.
- Image `remotePatterns` (lines 31–52): `**.devhunt.io`,
  `api.dicebear.com`, `localhost:8333`, `object-storage:8333` (the
  SeaweedFS S3 endpoint).
- Webpack alias `@` → `./src` (lines 107–112).

## Outbound dependencies (server side)

- **core-api** for almost all REST + SignalR.
- **auth-service** for credential and token endpoints.
- **ml-service** via `/api/proxy-ml/*` route handler.
- **object-storage** for image rendering (browser GETs go
  cross-origin to SeaweedFS; allowed by `remotePatterns`).

Browser never holds backend URLs directly — it always uses
`/api/proxy-*` paths.

## What I should NOT assume

- **App Router only.** There is no `pages/` directory in this
  app at the read level; assume App Router for routing,
  layouts, and metadata APIs.
- **Edge middleware does not gate `/api/*`.** The matcher
  excludes `api` (line 56), so route handlers must do their own
  auth. This is intentional — backend auth lives downstream and
  proxy handlers attach the user's session.
- **`reactStrictMode` is true** (line 15) — effects fire twice in
  Development. Don't chase mysterious "double mount" issues; that's
  Strict Mode.
- **`next-auth` is the v5 beta**, not v4. Patterns from older docs
  (`pages/api/auth/[...nextauth].ts`, `useSession` shape) may not
  apply. Treat upgrades cautiously.
- **The legacy `webpack` block (lines 107–113)** is kept for
  Turbopack compatibility — comment line 105 is explicit. Don't
  remove it without verifying alias resolution still works under
  Turbopack.
