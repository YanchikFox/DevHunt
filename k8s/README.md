# Kubernetes manifests (work in progress)

This directory is an early, incomplete port of the stack to Kubernetes. It currently
covers only 3 of the ~16 services (`auth-service`, `core-api`, `frontend` — see
`namespace.yaml` and the two `*-deployment.yaml` files) and has not been kept in sync
with the current docker-compose stack (env vars, ports, and dependencies have drifted).

Do not use these manifests to run DevHunt. **`docker-compose.yml`** (plus the
`docker-compose.prod.yml` override for hardened production settings) is the supported
and maintained deployment path — see the root [`README.md`](../README.md#quick-start).
