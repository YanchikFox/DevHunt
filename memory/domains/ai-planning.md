---
title: AI Planning — chat assist, plan generation, tool-calling, BYOK LLM
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/AIController.cs
  - DevHunt.CoreApi/Controllers/AiPlansController.cs
  - DevHunt.CoreApi/Controllers/AiPlanDraftsController.cs
  - DevHunt.CoreApi/Controllers/LlmController.cs
  - DevHunt.CoreApi/Services/Ai/
  - DevHunt.CoreApi/Services/Ai/Llm/
  - DevHunt.CoreApi/Program.cs
  - frontend/src/app/[locale]/dashboard/ai/
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

The "AI assistant" surface inside a project workspace. Three
related but distinct flows: (1) **multi-turn chat** with project
context, where the model can call typed tools that mutate the
project; (2) **plan generation** — given a project, the model
produces a structured plan (tasks, tech stack, architecture
diagram) that the user can review and apply; (3) **plan drafts**
— transient drafts ahead of a real plan. Multi-provider via a
BYOK (bring-your-own-key) registry so each user picks their own
provider + model.

## Surface

### HTTP — Core API

[AIController.cs](DevHunt.CoreApi/Controllers/AIController.cs)
(`/api/ai`) — chat-style assist:

- `POST /chat/conversations/{conversationId}` — chat completion
  in a project chat conversation
- `POST /chat/conversations/{conversationId}/regenerate`
- `POST /chat/conversations/{conversationId}/estimate` — token /
  cost estimate
- `POST /chat/conversations/{conversationId}/cancel/{requestId}` —
  cancel an in-flight request via `IAiInFlightRegistry`
- `POST /chat/conversations/{conversationId}/tools/execute` —
  invoke a tool the model proposed
- `POST /tech-stack` — quick standalone suggestion (no
  conversation)

[AiPlansController.cs](DevHunt.CoreApi/Controllers/AiPlansController.cs)
(`/api/projects/{projectId}/ai/plans`) — persisted plans:

- `POST /tech-stack`, `POST /`, `GET /{planId}`,
  `POST /{planId}/apply`, `POST /refine`,
  `POST /tech-stack/refine`, `POST /diagram`

[AiPlanDraftsController.cs](DevHunt.CoreApi/Controllers/AiPlanDraftsController.cs)
(`/api/ai/plans/draft`) — transient drafts:

- `POST /`, `POST /refine`, `POST /tech-stack`

[LlmController.cs](DevHunt.CoreApi/Controllers/LlmController.cs)
(`/api/llm`) — provider/model registry:

- `GET /providers`, `GET /models`,
  `POST /providers/{provider}/models/sync`

### UI routes

- AI dashboard —
  [frontend/src/app/[locale]/dashboard/ai/](frontend/src/app/[locale]/dashboard/ai/)
  with `_components/` colocated.
- BYOK key management lives under the **profile** path
  (`dashboard/profile/ai-keys/`), owned by the auth-and-identity
  domain.

### SignalR

Chat assist responses do not push through `/chatHub` for the
assistant message itself — the `POST /chat/conversations/...`
endpoint streams or returns directly. User-visible status
updates (cancel, retry) are client-driven through
`IAiInFlightRegistry`. Conversation-level events (user typing in
the same chat as the assistant, message arrivals) ride
`/chatHub` and belong to `chat-and-channels`.

### Tool catalogue

Tools registered in
[Program.cs:173-182](DevHunt.CoreApi/Program.cs#L173-L182) (each
implements `IAiTool`):

- Task mutators: `CreateTaskTool`, `UpdateTaskTool`,
  `MoveTaskTool`, `DeleteTaskTool`, plus the bulk variants
  `CreateMultipleTasksTool`, `MoveMultipleTasksTool`,
  `DeleteMultipleTasksTool`.
- Project mutators: `UpdateProjectTool`.
- Memory: `RecordDecisionTool`, `ReadDocumentTool`.

Dispatch goes through `IAiToolDispatcher` →
`IAiToolExecutionService`.

## Entities involved

- `AiPlan` — persisted plan rows; status + lifecycle.
- `AiOperationLog` — per-operation audit (FK to `AiPlan`,
  `Project`, `User`).
- `ProjectArtifact` — outputs (e.g., diagrams, summaries) that
  outlive a single chat turn.
- `Message.AiMetadataJson` — **jsonb** column on chat messages
  carrying assistant metadata.
- `AiMessageDetails` — 1:1 sidecar to `Message`, jsonb
  `FullPayloadJson` with the full LLM exchange. Pruned by
  `AiMessageDetailsPruneWorker` (`Program.cs:163`, hosted
  service).
- `LlmModel` — provider+model registry; seeded on startup by
  `LlmModelSeeder`.
- `UserApiKey` — encrypted per-user provider keys; **owned by
  auth-and-identity**, consumed here.

Authoritative entity locations: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — every endpoint;
  the planner, validator, applier, strategy router, in-flight
  registry, rate limiter, project context builder, and tool
  dispatcher all live in `Services/Ai/` and `Services/Ai/Llm/`.
- [systems/infrastructure.md](../systems/infrastructure.md) —
  AI entities and the jsonb columns.
- [systems/frontend.md](../systems/frontend.md) — dashboard AI UI
  and BYOK key management.
- [systems/ml-service.md](../systems/ml-service.md) — **does not
  serve this domain.** ml-service exposes its own `/api/ai`
  router for recommendation/passport flows (a different domain);
  the planning surface above runs entirely inside Core API
  against external LLM providers. If a future change routes
  planning through ml-service, this paragraph needs revision.

## Known traps

- [gotchas/events-silently-dropped-without-rabbitmq.md](../gotchas/events-silently-dropped-without-rabbitmq.md) —
  AI usage / plan-applied events publish via `IEventBusService`;
  with the bus disabled they're swallowed.
- [gotchas/hand-rolled-json-text-columns.md](../gotchas/hand-rolled-json-text-columns.md) —
  not in this domain (planning uses real jsonb), but a useful
  contrast: `Message.AiMetadataJson` and
  `AiMessageDetails.FullPayloadJson` *are* jsonb and rely on
  `EnableDynamicJson` being wired in
  [DevHunt.CoreApi/Extensions/DatabaseExtensions.cs:21](DevHunt.CoreApi/Extensions/DatabaseExtensions.cs#L21).
- **`AiMessageDetails` is pruned periodically** by a hosted
  worker. If a debugging session expects to inspect old payloads,
  they may be gone — capture before relying on retention.

## What I should NOT assume

- **There is more than one `IAiPlannerStrategy`-shaped extension
  point**, but at this commit only `V1AiPlannerStrategy` is
  registered. Don't claim "we have a v2" until a real
  `V2AiPlannerStrategy` is wired.
- **BYOK is per-user, not per-project.** A project's owner does
  not implicitly grant their key to teammates. Each user must
  add their own.
- **Plan apply mutates the project** (creates tasks, updates
  fields) — it's not a no-op review step. The `apply` endpoint
  is permission-gated; verify with `IProjectPermissionService`
  before calling from a new code path.
- **`ProjectMemoryService` and `ProjectContextBuilder` are
  separate.** Memory is what the assistant explicitly recorded
  via `RecordDecisionTool`; context is what's auto-assembled per
  request from the project state. Don't conflate them when
  reasoning about "what does the model know."
- **The cancel endpoint is process-local.** See
  [gotchas/ai-plan-cancel-process-local.md](../gotchas/ai-plan-cancel-process-local.md) —
  works today under single-instance Compose, latent regression
  under k8s scale-out.
