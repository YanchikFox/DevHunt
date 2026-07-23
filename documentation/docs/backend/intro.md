---
sidebar_position: 0
title: Backend Docs
description: Entry point for source-derived DevHunt backend documentation.
sidebar_label: Backend Docs
---

# Backend Docs

> _If any detail here contradicts the code, trust the code — not this page._

Start here when working on DevHunt backend code. The backend is not one service: Core API owns most product behavior, Auth Service owns credentials and token lifecycle, Infrastructure owns the EF Core model and migrations, and DatabaseMigrator applies schema changes before the APIs boot.

Recommended order:

1. [Backend Onboarding](./onboarding) — start here if you're new
2. [Backend Overview](./overview)
3. [How To Run The Backend](./how-to-run)
4. [How To Debug The Backend](./how-to-debug)
5. [Troubleshooting](./troubleshooting)
6. [Core API](./services/core)
7. [Auth Service](./services/auth)
8. [Database Overview](./database/overview)

Generated API references live under the API sidebar and DocFX output. Treat generated references as endpoint/member indexes; use source code and `memory/` entries for behavior.
