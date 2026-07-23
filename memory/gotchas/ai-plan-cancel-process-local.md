---
title: AI chat cancel is process-local — multi-instance scale-out silently breaks it
type: gotcha
status: verified
sources:
  - DevHunt.CoreApi/Controllers/AIController.cs
  - DevHunt.CoreApi/Services/Ai/Llm/AiInFlightRegistry.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## Symptom (will appear under scale-out)

A user clicks "stop" / "cancel" on a streaming AI chat response.
The HTTP `POST /api/ai/chat/conversations/{conversationId}/cancel/{requestId}`
returns 2xx. The streaming response keeps coming anyway, and
keeps consuming tokens until it finishes naturally.

Searches that should land here: "cancel doesn't work",
"in-flight registry", "ai cancel ignored", "stop button does
nothing".

## Cause

`IAiInFlightRegistry` is registered as a **singleton** at
[DevHunt.CoreApi/Program.cs:166](DevHunt.CoreApi/Program.cs#L166):

```csharp
builder.Services.AddSingleton<IAiInFlightRegistry, AiInFlightRegistry>();
```

The registry is in-process — a `ConcurrentDictionary` (or
similar) holding `CancellationTokenSource` references for each
in-flight chat request handled **on this Core API instance**.

The cancel endpoint
[AIController.cs:178](DevHunt.CoreApi/Controllers/AIController.cs#L178)
looks up the request id in the local registry and triggers the
local CTS. There is no Redis-backed broadcast, no RabbitMQ
fan-out, no DB lock — nothing crosses process boundaries.

Today the deployment is a single Core API container per Compose
stack, so the streaming request and the cancel request always
land on the same process. The bug is latent.

## When it fires

- Any `core-api` deployment with replicas > 1 and a load balancer
  that doesn't pin the cancel call to the same instance as the
  streaming call.
- Kubernetes Deployment with horizontal autoscaling.
- Blue/green or rolling deployment where the old instance is
  still streaming and the cancel hits the new one.

It will **not** fire under the current Compose layout.

## How to spot it

- Logs: the streaming request keeps emitting LLM provider
  output after the cancel endpoint logs success.
- Metrics: usage meter (`IAiUsageMeter` /
  `DbAiUsageMeter`) charges the user for tokens consumed after
  the cancel was issued.
- Frontend: the "stop" button says it succeeded but tokens keep
  arriving (or, if the connection was closed client-side, the
  server still spends tokens that no one is reading).

## What to do

- **Don't add a "fallback" client-side disconnect.** Closing the
  HTTP stream from the browser does not stop the server-side
  generator either; the LLM call runs to completion.
- The right fix when scale-out lands is one of:
  1. Move the registry behind Redis (the SignalR backplane
     already runs against Redis — same target, similar pattern).
  2. Route cancel calls to the originating instance via a sticky
     session header.
  3. Persist the cancel intent to a row that the streaming loop
     polls.
- Until that change is made, treat horizontal scale of `core-api`
  as a known regression for this feature.

## See also

[cross-cutting/scale-out-readiness.md](../cross-cutting/scale-out-readiness.md) —
architectural overview of all single-instance-safe / scale-out-unsafe
patterns in DevHunt.
