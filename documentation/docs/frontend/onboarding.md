---
sidebar_position: 5
title: Frontend Onboarding
description: Orientation for frontend engineers joining the DevHunt codebase.
sidebar_label: Frontend Onboarding
---

# Frontend Onboarding

> _If any detail here contradicts the code, trust the code — not this page._

Use this path to get productive without learning the whole app at once.

## First Map

| Area            | Where to start                                        |
| --------------- | ----------------------------------------------------- |
| Routing         | `frontend/src/app/[locale]/`                          |
| Middleware      | `frontend/src/middleware.ts`                          |
| Runtime config  | `frontend/next.config.mjs`                            |
| Auth setup      | `frontend/src/auth.config.ts`, `frontend/src/auth.ts` |
| Package scripts | `frontend/package.json`                               |

## Mental Model

- App Router is the routing model.
- Locale segments are first-class.
- `/dashboard` and `/admin` are protected UI prefixes.
- `/api/*` is excluded from edge middleware.
- Backend calls should use proxy paths.
- Real-time updates come through SignalR hubs hosted by Core API.

## First Commands

```bash
cd frontend
npm install
npm run type-check
npm run lint
npm run test
```

Run the app with proxy mode when working against local backend ports:

```bash
NEXT_PUBLIC_USE_PROXY=true npm run dev
```

## Coding Standards To Keep Close

- No `any` in production code.
- No unguarded forced casts for API errors or unknown data.
- No production `console.log`, `console.debug`, or `console.info`.
- Avoid inline arrow handlers in component props when they cause repeated rerenders; use stable callbacks.
- Keep deeply nested JSX in extracted components.
- Use Tailwind CSS for styling.

## Good First Debug Targets

- Follow a protected route through `middleware.ts`.
- Trace one Core API request through `/api/proxy-core`.
- Trace one Auth request through `/api/proxy-auth`.
- Trace one SignalR connection to `/chatHub` or `/notificationHub`.
- Compare `/api/auth/me` with `/api/profile/me`; they are intentionally different surfaces.
