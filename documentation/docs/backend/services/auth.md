---
sidebar_position: 2
title: Auth Service
description: Source-derived service reference for DevHunt.AuthService.
sidebar_label: Auth Service
---

# Auth Service

> _If any detail here contradicts the code, trust the code — not this page._

`DevHunt.AuthService` is the ASP.NET Core / .NET 10 identity service. It owns credential and token lifecycle, not the full user profile surface.

## Runtime

| Detail          | Value                                     |
| --------------- | ----------------------------------------- |
| Compose service | `auth-service`                            |
| Container port  | `INTERNAL_PORT_AUTH`, default `8080`      |
| Host port       | `PORT_AUTH`, default `7001`               |
| Health          | `GET /health`                             |
| Metrics         | `GET /metrics` with custom IP/token guard |
| CSRF token      | `GET /api/auth/csrf-token`                |

## Responsibilities

Auth Service handles:

- registration and login;
- refresh-token rotation;
- logout;
- JWT issuance;
- `/api/auth/me` claim view;
- email verification and resend;
- forgot/reset password;
- username availability;
- Google/GitHub OAuth when configured;
- TOTP setup, verification, disable, and status.

Profile editing, follow graph, privacy settings, activation/deactivation, and BYOK keys belong to Core API.

## Security Setup

In Production, Auth Service rejects weak JWT keys shorter than 32 characters or containing weak patterns. JWT is read from the `access_token` httpOnly cookie first and the `Authorization` header second.

CSRF configuration uses cookie `CSRF-TOKEN` with `SameSite=Strict` and header `X-CSRF-TOKEN`.

OAuth providers are only registered when both client id and client secret are set. Missing config means the provider route is not active.

## Dependencies

Auth Service depends on:

- PostgreSQL through `DevHuntDbContext`;
- Redis for rate limiting when configured;
- OpenObserve for traces/logs when configured;
- SMTP via MailKit for email flows.

It does not use SignalR, RabbitMQ, or S3 directly.

## Do Not Conflate

- `/api/auth/me` is a JWT-claim view; `/api/profile/me` is a Core API DB-backed profile view.
- Auth Service CORS/CSRF behavior differs from Core API.
- Auth Service does not run migrations on startup; the migrator owns that.
