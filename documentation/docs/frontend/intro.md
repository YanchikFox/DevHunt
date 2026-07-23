---
sidebar_position: 0
title: Frontend Docs
description: Entry point for source-derived DevHunt frontend documentation.
sidebar_label: Frontend Docs
---

# Frontend Docs

> _If any detail here contradicts the code, trust the code — not this page._

Start here when working on the Next.js frontend. The app uses Next.js 16 App Router, React 19, NextAuth v5 beta, `next-intl`, TanStack Query, Zustand, Tailwind CSS, Radix UI, and SignalR.

Recommended order:

1. [Frontend Overview](./overview)
2. [How To Run The Frontend](./how-to-run)
3. [How To Debug The Frontend](./how-to-debug)
4. [Troubleshooting](./troubleshooting)
5. [Onboarding](./onboarding)

The browser should use proxy paths such as `/api/proxy-core` and `/api/proxy-auth`, not internal container hostnames. Edge middleware protects page routes, not `/api/*` route handlers.
