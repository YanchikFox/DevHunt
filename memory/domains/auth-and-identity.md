---
title: Auth & Identity — registration, login, OAuth, 2FA, profile, follows, BYOK
type: domain
status: verified
sources:
  - DevHunt.AuthService/Controllers/AuthController.cs
  - DevHunt.CoreApi/Controllers/UsersController.cs
  - DevHunt.CoreApi/Controllers/ProfileController.cs
  - DevHunt.CoreApi/Controllers/UserApiKeysController.cs
  - DevHunt.AuthService/Services/
  - frontend/src/app/[locale]/(auth)/
  - frontend/src/app/[locale]/dashboard/profile/
  - frontend/src/middleware.ts
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Everything that turns a visitor into a known, authenticated user
on the platform: email/password registration with verification,
OAuth via Google and GitHub, TOTP 2FA, refresh-token rotation,
password reset, profile management with privacy and avatar, user
search, follow graph, and per-user BYOK API keys for the LLM
layer. Spans three deployables; the split is **AuthService owns
credentials & tokens**, **Core API owns the rest of the user
surface**.

## Surface

### HTTP — Auth Service (`/api/auth/*`)

Defined in
[DevHunt.AuthService/Controllers/AuthController.cs](DevHunt.AuthService/Controllers/AuthController.cs):

- `POST /register`, `POST /login`, `POST /refresh`, `POST /logout`
- `GET  /me` — returns the JWT-claim-derived user
- `POST /verify-email`, `POST /resend-verification`
- `POST /forgot-password`, `POST /reset-password`
- `GET  /check-username` — availability check
- `GET  /login/{provider}`, `GET  /callback/{provider}` —
  Google + GitHub OAuth start/callback (registered conditionally
  in `Program.cs:208-231` only when client id/secret configured)
- TOTP: `POST /totp/setup`, `POST /totp/verify-setup`,
  `POST /totp/disable`, `GET /totp/status`
- `GET  /csrf-token` (declared inline in
  [Program.cs:377](DevHunt.AuthService/Program.cs#L377))

### HTTP — Core API user surface

- `GET /api/users` — list/search; `GET /api/users/search`
- `GET /api/users/{id}`; `GET /api/users/online-status`
- `POST/DELETE /api/users/{userId}/follow`
- `GET /api/users/suggested`, `…/{userId}/followers`,
  `…/{userId}/following`
- `GET/PUT /api/users/me/settings`,
  `GET /api/users/{userId}/{activities, stats}`
- `GET/PUT /api/profile/me`, `GET /api/profile/{id}`,
  `POST /api/profile/deactivate|activate`,
  `POST/DELETE /api/profile/avatar`,
  `GET/PUT /api/profile/privacy`
- `GET/POST/DELETE /api/users/api-keys` (BYOK — file
  [UserApiKeysController.cs](DevHunt.CoreApi/Controllers/UserApiKeysController.cs))

### UI routes (Next.js)

- Public auth pages —
  [frontend/src/app/[locale]/(auth)/](frontend/src/app/[locale]/(auth)/):
  `login/`, `register/`, `register/complete-profile/`,
  `forgot-password/`, `reset-password/`, `verify-email/`
- OAuth landing —
  [frontend/src/app/[locale]/oauth-callback/](frontend/src/app/[locale]/oauth-callback/)
- Dashboard profile —
  [frontend/src/app/[locale]/dashboard/profile/](frontend/src/app/[locale]/dashboard/profile/):
  `edit/`, `security/` (TOTP), `privacy/`, `ai-keys/` (BYOK),
  `[userId]/` (read another profile)
- Public profile view —
  [frontend/src/app/[locale]/(public)/users/[userId]/](frontend/src/app/[locale]/(public)/users/[userId]/)
- Edge gating happens in
  [frontend/src/middleware.ts](frontend/src/middleware.ts):
  protected prefixes `/dashboard` and `/admin` redirect
  unauthenticated users to `/{locale}/login`.

### SignalR

`UsersController.online-status` (`/api/users/online-status`) is
served from `IPresenceService` (Redis-backed singleton from
`Program.cs:129`). Liveness updates flow through SignalR, but
this domain doesn't define hub methods of its own — presence is
read-only HTTP, real-time arrives via the Chat domain hubs.

## Entities involved

See [data/entity-catalog.md](../data/entity-catalog.md) for the
authoritative class locations.

- `User` (auth fields: GithubId, GoogleId, password reset, TOTP,
  suspension, username)
- `RefreshToken` (hashed at rest after
  `RefreshToken_AddHashing` migration)
- `UserPrivacySettings` (1:1 with User)
- `UserFollow` (self-junction Follower → Followed)
- `UserApiKey` (BYOK — encrypted via
  `IEncryptionService` registered as singleton)
- `UserActivityService` produces feed entries to `ActivityRecord`
  (lives in the activity-feed domain)

## Crosses these systems

- [systems/auth-service.md](../systems/auth-service.md) — owns
  credentials, JWT issuance, OAuth, TOTP, refresh-token rotation,
  forgot/reset flow.
- [systems/core-api.md](../systems/core-api.md) — owns profile,
  users list/search, follow graph, privacy, BYOK keys.
- [systems/frontend.md](../systems/frontend.md) — every UI page
  above; edge middleware enforces protection.
- [systems/notification-service.md](../systems/notification-service.md) —
  consumes events for transactional email (verification, reset)
  asynchronously when the bus is wired.
- [systems/api-gateway.md](../systems/api-gateway.md) — `/api/auth/*`
  routes to Auth Service, everything else lands on Core API.

## Known traps

- [gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md) —
  registration/login/logout events publish through
  `IEventBusService`. With no RabbitMQ wired, transactional
  emails never go out.
- AuthService and Core API have **different CSRF and CORS
  policies** — see "What I should NOT assume" in
  [systems/auth-service.md](../systems/auth-service.md) and
  [systems/core-api.md](../systems/core-api.md). A CSRF token
  fetched from one service is **not** interchangeable with the
  other.

## What I should NOT assume

- **AuthService does not own the User table writes you'd expect.**
  Profile updates, privacy, activation, deactivation all go to
  Core API even though they update `Users` rows. AuthService
  only mutates the auth-relevant subset (password hash, refresh
  tokens, TOTP secret, email verification, social ids).
- **`/api/auth/me` and `/api/profile/me` return different
  shapes.** The auth `me` is the JWT claim view; the profile `me`
  is the DB view including bio, avatar, skills. Don't conflate.
- **BYOK keys are encrypted at rest** via the shared
  `IEncryptionService`. Do not log `UserApiKey.EncryptedKey`
  raw, and treat `Encryption:Key` / `Encryption:IV` rotation as
  a cross-domain incident — see
  [gotchas/encryption-key-rotation-cross-domain.md](../gotchas/encryption-key-rotation-cross-domain.md).
- **TOTP enforcement state lives only in AuthService.** Core API
  has no idea whether a session went through a TOTP step — only
  the JWT claims it. Don't try to gate Core API endpoints on
  "did you 2FA".
- **OAuth providers are registered conditionally.** If
  `Authentication:Google:ClientId` (or GitHub) is missing,
  `/api/auth/login/google` returns 404 from MVC. Don't read that
  as "OAuth disabled by feature flag" — it's "not registered."
- **The follow graph is in Core API**, not AuthService. Auth
  Service does not know who follows whom.
