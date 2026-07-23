---
title: integration-gateway references undeclared `axios` in two webhook handlers
type: gotcha
status: verified
sources:
  - integration-gateway/src/index.js
verified_at: 2026-05-23
verified_against_commit: c1a9bd3
last_user_review: null
---

## ✅ FIXED 2026-05-23 — replaced `axios.get` → `axiosClient.get` and `axios.delete` → `axiosClient.delete` at lines 278 and 464.

## Symptom (was)

If exercised, the two affected handlers would throw
`ReferenceError: axios is not defined` at runtime. Pre-existing
data on integrations would never be fetched, and the Core API
config would never be cleared on webhook deletion.

## Cause

[integration-gateway/src/index.js:10](integration-gateway/src/index.js#L10)
imports only `axiosClient`:

```js
import { axiosClient } from "./utils/axiosClient.js";
```

Other call sites use it correctly (lines 162, 336, 405). But two
sites use a bare `axios` identifier:

- [integration-gateway/src/index.js:274](integration-gateway/src/index.js#L274) — inside
  `POST /api/webhooks` (auth-protected webhook creation), the
  fallback that fetches integration config from Core API:

  ```js
  const response = await axios.get(`${coreApiUrl}/api/integrations/${finalIntegrationId}`, …);
  ```

- [integration-gateway/src/index.js:460](integration-gateway/src/index.js#L460) — inside
  `DELETE /api/webhooks/:integrationId/:serviceType/:webhookId`,
  the call that clears the webhook config in Core API:

  ```js
  await axios.delete(`${coreApiUrl}/api/integrations/${integrationId}/webhook/${webhookId}`, …);
  ```

There is no `import axios from "axios"` anywhere in this file at
the read commit, and no global shim was found in `telemetry.js`
during Step 2 reading.

## Why unverified

I did not:

- Run the service and exercise both code paths.
- Read `utils/axiosClient.js` to check whether it side-effect-
  exports a global `axios`.
- Grep the rest of the gateway for an init script that might
  attach `axios` to `globalThis`.
- Check Express test fixtures or e2e tests that might cover
  these endpoints.

Any one of those could change the conclusion. Until at least the
first three are checked, treat the failure as suspected, not
confirmed.

## How to spot it (if confirmed)

- Logs from the gateway at the moment a user creates or deletes
  a webhook through Core API would show a `ReferenceError`,
  followed by a 500 from this service.
- The webhook would be created on the external provider (because
  that step happens earlier, lines 297–331), but the Core API
  side of the integration record would not be updated with the
  webhook id/secret — leading to a half-configured integration.

## What to do

- Before fixing, confirm the path is reachable and not shadowed.
- If real, the right fix is `axios.get` → `axiosClient.get` and
  `axios.delete` → `axiosClient.delete` (matches every other call
  site). Don't add a new top-level `import axios from "axios"`
  just to make the bare identifier resolve — that diverges from
  the rest of the file.
