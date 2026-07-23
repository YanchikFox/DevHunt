---
sidebar_position: 1
title: API Versioning Strategy
description: Current API versioning reality and constraints.
sidebar_label: API Versioning Strategy
---

# API Versioning Strategy

> _If any detail here contradicts the code, trust the code — not this page._

DevHunt does not currently use ASP.NET API versioning middleware or route-level API version attributes.

Current route patterns are unversioned:

- Core API controllers use routes such as `api/projects`, `api/users`, `api/chat`, `api/notifications`, and `api/admin`.
- Auth Service uses `api/auth`.
- SignalR hubs use `/chatHub` and `/notificationHub`.

Swagger UI labels the generated specs as `v1`, but that is a documentation/spec label, not a runtime versioning contract.

## What This Means

- Do not add breaking changes under the assumption that `/v1` clients are isolated.
- Prefer additive DTO fields and new endpoints for compatibility.
- Keep old request fields accepted until all known clients are migrated.
- If a breaking change is unavoidable, add an explicit new route family and document the migration path.

## Before Adding Versioning

If runtime API versioning is introduced later, update all of these together:

- ASP.NET routing and versioning middleware;
- OpenAPI generation for Core API and Auth Service;
- frontend proxy clients and route handlers;
- integration-gateway callbacks and sync calls;
- generated portal API docs;
- tests that assert concrete route paths.

Until that work is done, treat the public API as one unversioned surface.
