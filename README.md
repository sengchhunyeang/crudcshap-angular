# crudcshap-angular

CRUD demo: **ASP.NET Core (.NET 10) API** + **Angular 20** + **Neon Postgres**, deployed as one Vercel project using [Vercel Services](https://vercel.com/docs/services).

- `backend/` – minimal API (`/api/products`, `/api/health`), runs as a container (`Dockerfile.vercel`)
- `frontend/` – Angular SPA served statically
- `vercel.json` – routes `/api/*` → backend, everything else → frontend

## Run locally

```bash
# backend (needs DATABASE_URL, e.g. a Neon connection string)
cd backend && DATABASE_URL="postgresql://..." dotnet run   # http://localhost:5080

# frontend (proxies /api to the backend)
cd frontend && npm install && npm start                    # http://localhost:4200
```

## Deploy

Set `DATABASE_URL` in the Vercel project, then `vercel deploy` (or push to GitHub with the repo connected).
