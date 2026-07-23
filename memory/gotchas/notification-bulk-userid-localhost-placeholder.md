---
title: notification-service /bulk uses `${userId}@devhunt.local` — intentional, NOT a bug
type: gotcha
status: verified
sources:
  - notification-service/src/index.js
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## The fact

[notification-service/src/index.js:247-256](notification-service/src/index.js#L247-L256)
fans out bulk notifications by emitting one email per user id, and
the recipient address is constructed as `${userId}@devhunt.local`:

```js
const results = await Promise.allSettled(
  finalUserIds.map((userId) =>
    sendEmail({
      to: `${userId}@devhunt.local`, // Placeholder - в реальности нужно получать email из БД
      …
    }),
  ),
);
```

The inline comment ("в реальности нужно получать email из БД")
admits the placeholder. **This is NOT a defect that future-me
should "fix" reflexively.** It is a deliberate stub that ships
this way today.

## Why this is in production code, not behind a flag

The endpoint exists for API-shape compatibility with Core API's
bulk-notify call — the contract on the Core API side is
"give me a list of user ids and a payload, I'll fan out." The
notification service has no DB connection (see
`systems/notification-service.md`), so resolving user id → email
must be done by the caller or by a different service. Until that
wiring is decided, the placeholder lets the contract be exercised
without writing real mail to real users.

## What to do

- **Do not "fix" this without coordinating with the user.** The
  decision about where the user-id → email join lives is open
  (likely Core API enriches before posting, or this service grows
  a Core API call). Picking one without the user is wrong.
- **Do treat the bulk endpoint as a stub** when reasoning about
  user-visible behavior: in any flow that ends in
  `/api/notifications/bulk`, real users do **not** receive email.
- **Do not include this endpoint** in load tests, deliverability
  monitoring, or "did the alert reach users" verification.
- If the user asks to make bulk real, the conversation needs to
  decide:
  1. Where the email lookup happens (Core API → enrich payload, or
     notification-service → call Core API per id).
  2. Whether unverified-email users should be skipped.
  3. Whether to keep the `Promise.allSettled` semantics or
     switch to all-or-nothing.

The single-recipient `POST /api/notifications` path (line 104)
does **not** have this placeholder — it accepts `recipient` from
the caller, so it can deliver real mail today.
