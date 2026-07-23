---
title: ml-service import fails without DATABASE_URL
type: gotcha
status: verified
sources:
  - ml-service/deps.py
  - ml-service/requirements.txt
verified_at: 2026-05-12
verified_against_commit: 4579ce0 + 2026-05-14 docs-build verification
last_user_review: 2026-05-12
---

`ml-service/deps.py` lines 17–22 raise `ValueError` at **module import time** if
`DATABASE_URL` is not set in the environment:

```python
DATABASE_URL = os.getenv("DATABASE_URL")
if not DATABASE_URL:
    raise ValueError(
        "DATABASE_URL environment variable is required. "
        "Set it in .env file or environment."
    )
```

This means any Python process that does `from main import app` (FastAPI's pattern)
will crash with `ValueError` unless `DATABASE_URL` is present — **even if no
database connection is ever opened**.

**Consequence for `gen-mlapi.js`**: the OpenAPI export script (`python3 -c 'from
main import app; ...'`) must pass `DATABASE_URL=postgresql://localhost/dummy` as an
environment variable. The value is never used for an actual connection at import
time; it just satisfies the presence check.

**If gen-mlapi.js starts failing** after a change to `deps.py`, check whether the
validation became stricter — e.g., started parsing the URL or attempting a connection
on import. The workaround only covers the current presence-only check.

**Python version gotcha for docs export**: `ml-service/requirements.txt` pins
`fastembed==0.4.2`, whose package metadata excludes Python 3.13+. On a host with
Python 3.13+, a local venv install of the ML docs dependencies fails before
OpenAPI export. The Docker documentation builder is the safer verification path
because it owns the Python environment; if the Docker build reaches
`validate-generated-docs.js`, an empty `ml-openapi.json` will fail the build.
