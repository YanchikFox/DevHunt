---
title: Integrations & OAuth — GitHub/GitLab linking, webhooks, sync
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/IntegrationsController.cs
  - DevHunt.CoreApi/Services/Integrations/IntegrationAuthorizationService.cs
  - DevHunt.CoreApi/Services/Integrations/OAuthCallbackHandler.cs
  - DevHunt.Infrastructure/Integration.cs
  - integration-gateway/src/index.js
  - integration-gateway/src/routes/oauth.js
  - integration-gateway/src/routes/webhooks.js
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Each project can link an external repository host (GitHub or
GitLab today) for issue sync, code analysis, and webhook-driven
events. The flow has three actors: the user's browser, Core API
(persistence + authorization), and the integration-gateway
deployable (provider-specific OAuth + webhook signature
verification). Tokens are encrypted at rest in `Integration`
rows and decrypted on demand.

## Surface

### HTTP — Core API (`/api/integrations`)

[IntegrationsController.cs](DevHunt.CoreApi/Controllers/IntegrationsController.cs):

Per-project CRUD:

- `GET /project/{projectId}` — list the project's integrations
- `POST /project/{projectId}` — create
- `GET /{integrationId}`,
  `PUT /{integrationId}`,
  `PUT /{integrationId}/toggle`,
  `DELETE /{integrationId}`

Token / repo helpers:

- `GET /{integrationId}/token` — masked token / metadata
- `GET /{integrationId}/decrypt-token` — **plaintext** token
  (sensitive; auth-checked internally)
- `GET /{integrationId}/github-repos` — list user's
  accessible repos via the linked token

Sync + lookups:

- `POST /{integrationId}/sync` — fan out to integration-gateway
  to refresh repo data and trigger code analysis
- `GET /by-repository?...` — find an integration by
  `owner/repo` slug
- `GET /project/{projectId}/for-internal` — internal-service
  view of the integration row

OAuth (Core API leg):

- `GET /oauth/{serviceType}/authorize` — initiates the OAuth
  flow (start URL synthesized; logic in `OAuthCallbackHandler`)
- `GET /oauth/{serviceType}/callback` — provider redirects
  here; `OAuthCallbackHandler` exchanges the code, persists the
  encrypted token, finalizes the `Integration` row.

Webhook ingress:

- `POST /webhook/{integrationId}` — Core API endpoint that
  receives webhook deliveries (typically from the gateway, but
  can also be hit directly when configured to).

### HTTP — integration-gateway (`/api/oauth`, `/api/webhooks`)

OAuth side ([integration-gateway/src/routes/oauth.js](integration-gateway/src/routes/oauth.js)):

- `POST /api/oauth/authorize` (bearer)
- `GET /api/oauth/:provider/url` (bearer)
- `POST /api/oauth/callback` (bearer)
- `GET /api/oauth/:provider/callback` — provider redirect lands
  here on the gateway side

Webhooks ([integration-gateway/src/routes/webhooks.js](integration-gateway/src/routes/webhooks.js))
— **no bearer auth, signature-verified**:

- `POST /api/webhooks/verify-signature` — utility
- `POST /api/webhooks/github` — verifies HMAC-SHA256 with
  `GITHUB_WEBHOOK_SECRET`
- `POST /api/webhooks/gitlab`

Plus the inline gateway-side endpoints
`POST /api/sync`, `POST /api/webhooks` (creation),
`DELETE /api/webhooks/...` covered in
[systems/integration-gateway.md](../systems/integration-gateway.md).

### UI

There is no top-level `/integrations` path in the Next.js app
at this commit. Integration management is rendered inside the
project workspace
([frontend/src/app/[locale]/dashboard/projects/[id]/_components/](frontend/src/app/[locale]/dashboard/projects/[id]/_components/));
specific component file not enumerated in this pass.

### SignalR

None directly. Sync completion / webhook events publish to the
event bus and may surface as user notifications via the
`notifications` domain.

## Entities involved

- `Integration` — one row per project+provider. Stores the
  encrypted token, webhook id and secret, and provider config
  in **plain-text `ConfigJson`** (hand-rolled JSON; see traps).
  Migration: `IntegrationsImplementation` (2025-11-02).

Authoritative: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — controller,
  authorization service, OAuth callback handler, persistence.
- [systems/integration-gateway.md](../systems/integration-gateway.md) —
  provider OAuth start/exchange, webhook signature
  verification, fire-and-forget code-analysis trigger.
- [systems/infrastructure.md](../systems/infrastructure.md) —
  `Integration` entity + encryption columns.
- [systems/code-analyzer.md](../systems/code-analyzer.md) —
  triggered by the gateway after sync.
- [systems/frontend.md](../systems/frontend.md) — UI
  embedded inside the project workspace.

## Known traps

- [gotchas/integration-gateway-bare-axios-references.md](../gotchas/integration-gateway-bare-axios-references.md) —
  webhook create/delete handlers in the gateway reference an
  undeclared `axios` (lines 274, 460). If reachable, those
  paths throw at runtime and the Core API side of the
  Integration row is left half-configured.
- [gotchas/hand-rolled-json-text-columns.md](../gotchas/hand-rolled-json-text-columns.md) —
  `Integration.ConfigJson` is `text`, not `jsonb`. EF cannot
  filter inside it.
- [gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md) —
  sync-completed and webhook-received events publish via the
  bus.

## What I should NOT assume

- **OAuth has two callback endpoints, and they are not
  redundant.** The provider's redirect URL is configured on the
  *gateway* (`/api/oauth/:provider/callback`); the gateway
  exchanges the code for a token and then notifies Core API,
  which has its own `/api/integrations/oauth/{serviceType}/callback`
  to persist the result. The exact handoff (HTTP redirect vs
  internal call) was not traced fully in this pass — open
  `OAuthCallbackHandler.cs` and the gateway's callback handler
  before encoding the dance in new code.
- **Webhook signature verification lives in the gateway, not
  Core API.** Core API trusts gateway calls; if a webhook hits
  Core API directly via `/api/integrations/webhook/{id}`, the
  authentication story differs and must be checked in the
  controller body.
- **`GET /{integrationId}/decrypt-token` returns plaintext.**
  Treat the response as secret; do not log it. Authorization
  must be tight here — verify in
  `IntegrationAuthorizationService` before designing client
  code that calls it.
- **`Integration.WebhookSecret` is generated by the gateway**,
  not Core API. The gateway returns it in the create-webhook
  response and Core API stores it via
  `PUT /api/integrations/{id}/webhook` (called from the
  gateway, not the browser). If a webhook signature fails,
  suspect drift between the gateway-stored secret and the Core
  API row before suspecting the verifier.
- **Token encryption uses the shared `IEncryptionService`.** Key
  rotation is a cross-domain incident, not a local one — see
  [gotchas/encryption-key-rotation-cross-domain.md](../gotchas/encryption-key-rotation-cross-domain.md).
- **Recommendations engine is a separate domain**, not part of
  integrations — even though both touch external systems. The
  current memory base does not have a `domains/recommendations.md`
  entry; `RecommendationsController` exists at
  [DevHunt.CoreApi/Controllers/RecommendationsController.cs](DevHunt.CoreApi/Controllers/RecommendationsController.cs)
  and its compute lives in
  [systems/ml-service.md](../systems/ml-service.md). Flag if a
  task touches it — a new domain entry may be needed.
