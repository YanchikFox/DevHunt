---
title: Rotating Encryption:Key/IV silently breaks 5 unrelated features at once
type: gotcha
status: verified
sources:
  - DevHunt.CoreApi/Program.cs
  - DevHunt.CoreApi/Security/EncryptionService.cs
  - DevHunt.CoreApi/Controllers/IntegrationsController.cs
  - DevHunt.CoreApi/Controllers/AdminChatController.cs
  - DevHunt.CoreApi/Hubs/ChatHub.cs
  - DevHunt.CoreApi/Services/Chat/ChatService.cs
  - DevHunt.CoreApi/Services/Ai/Llm/UserApiKeyService.cs
  - DevHunt.CoreApi/Services/Ai/Llm/LlmChatService.cs
  - DevHunt.CoreApi/Services/Integrations/OAuthCallbackHandler.cs
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## Symptom

After an ops change to `Encryption:Key` and/or `Encryption:IV`,
seemingly unrelated features start failing in production:

- Linked GitHub/GitLab integrations stop working — token sync
  fails, "decryption failed" buried in logs.
- AI chat / planning stops calling LLM providers — BYOK keys
  cannot be decrypted.
- Old chat messages render as garbage / fail to load — they
  were stored under the old key.
- Admin chat tooling shows the same garbage.

There is no single error message tying these together. Each
domain looks like an independent regression.

Searches that should land here: "decryption failed",
"encryption rotation", "BYOK keys lost", "integrations broken
after deploy", "chat messages garbled".

## Cause

`IEncryptionService` is registered as a **singleton** at
[DevHunt.CoreApi/Program.cs:128](DevHunt.CoreApi/Program.cs#L128)
and consumes two configuration values:

- `Encryption:Key` (must be exactly 32 chars; required)
- `Encryption:IV` (must be exactly 16 chars; required)

Both are validated at boot via `:?` substitution in
[docker-compose.yml:232-233](docker-compose.yml#L232-L233) — the
service refuses to start without them, but it does not detect
that they have *changed* between deploys.

The same service is injected into **five distinct domains**:

| Caller | Domain | What is encrypted |
|---|---|---|
| [`OAuthCallbackHandler.cs:40`](DevHunt.CoreApi/Services/Integrations/OAuthCallbackHandler.cs#L40), [`IntegrationsController.cs:36`](DevHunt.CoreApi/Controllers/IntegrationsController.cs#L36) | integrations-oauth | OAuth access tokens stored in `Integration` rows |
| [`UserApiKeyService.cs:11`](DevHunt.CoreApi/Services/Ai/Llm/UserApiKeyService.cs#L11) | auth-and-identity (BYOK) | Per-user provider keys in `UserApiKey` rows |
| [`LlmChatService.cs:48`](DevHunt.CoreApi/Services/Ai/Llm/LlmChatService.cs#L48) | ai-planning | Reads the same `UserApiKey` rows back to call providers |
| [`ChatService.cs:26`](DevHunt.CoreApi/Services/Chat/ChatService.cs#L26), [`ChatHub.cs:45`](DevHunt.CoreApi/Hubs/ChatHub.cs#L45) | chat-and-channels | Chat message content (per the dependency; exact field scope not enumerated in this pass) |
| [`AdminChatController.cs:21`](DevHunt.CoreApi/Controllers/AdminChatController.cs#L21) | moderation / admin | Reading chat content for admin view |

Every row written by these callers stored ciphertext keyed on
the *old* `Encryption:Key`+`Encryption:IV` pair. After rotation,
no row in those tables can be decrypted; the `EncryptionService`
will throw or return junk depending on the AES mode and padding
behavior in
[DevHunt.CoreApi/Security/EncryptionService.cs](DevHunt.CoreApi/Security/EncryptionService.cs).

## When this fires

- Any rotation of either secret without a coordinated re-encrypt
  migration of the four affected tables (`Integrations`,
  `UserApiKeys`, `Messages`, plus any others I missed).
- Restoring a database backup taken under one key into an
  environment configured with a different key.
- Cloning prod data into staging with staging's own key set.
- Container redeploys that auto-generate secrets when
  `Encryption:Key` is missing — *if* such auto-generation
  exists. (Today the Compose `:?` requires the value, so this
  path doesn't exist for the dev-mode boot.)

## How to spot it

- Logs from any of the five callers around encryption / decryption.
- Symptoms cluster: if integrations + AI + chat all break in the
  same deploy, suspect this before suspecting each individually.
- `EncryptionExample.cs` exists at
  [DevHunt.CoreApi/Security/EncryptionExample.cs](DevHunt.CoreApi/Security/EncryptionExample.cs)
  and may serve as a sanity-check entrypoint.

## What to do

- **Treat `Encryption:Key` and `Encryption:IV` as one-shot
  secrets.** Rotation requires a planned migration, not an env
  flip:
  1. Stand up the service with both old and new keys exposed
     to a re-encrypt job.
  2. Decrypt-with-old / encrypt-with-new every row in
     `Integrations`, `UserApiKeys`, `Messages`, and any other
     ciphertext table.
  3. Then flip the running service to the new key only.
- **Don't ship a "fallback to plaintext on decryption error"
  branch.** It would mask the rotation incident and leak data
  on real corruption.
- **Backups carry the key implicitly.** A DB dump is unusable
  in another environment without the matching `Encryption:Key`
  / `Encryption:IV`. Document them alongside the backup or
  expect surprise restores to fail.
- **If the user asks to add a new encrypted field**, route it
  through the same `IEncryptionService` rather than introducing
  a parallel key — that keeps the rotation cost from
  multiplying further.
