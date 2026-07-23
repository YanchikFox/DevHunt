# DevHunt Frontend

Next.js 16 App Router application (TypeScript). Browser requests to `/api/*` and `/auth/*` are reverse-proxied to `core-api` and `auth-service` respectively — the frontend never talks to the backends directly.

## Local development

```bash
docker compose up frontend
```

App: `http://localhost:3000`

To run outside Docker:

```bash
cd frontend
cp .env.example .env.local   # adjust API URLs if needed
npm install
npm run dev
```
