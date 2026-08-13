# HRM Module — Production Deployment

This document describes how the HRM module (ASP.NET Core 10 API + React 19 frontend) is deployed on the Mightyfin production server `187.124.27.67`, following the same conventions as the rest of the ERP platform.

## Architecture

The HRM stack runs as a Docker Compose stack on the production host behind the shared **Traefik** ingress (the same mechanism that exposes `efaas-origin.mightyfinance.co.zm` and `auth.mightyfinance.co.zm`). Nothing binds to a public interface: the web proxy binds `127.0.0.1:28912` and the API binds `127.0.0.1:28911`, and Traefik terminates TLS (Let's Encrypt) on `:443` using host-label routing.

```
Public (https://hrm.mightyfinance.co.zm)
        |  Cloudflare (proxy)
        v
Traefik (:443, websecure, letsencrypt certresolver)
        |  Host(`hrm.mightyfinance.co.zm`)
        v
hrm-proxy-1  nginx:alpine  (127.0.0.1:28912)
   ├─ /api/hrm/*  -> hrn-api:8080   (.NET 10 Minimal APIs)
   ├─ /health/*   -> hrn-api:8080
   └─ /*         -> hrn-web:3000    (TanStack Start SSR, Node 22)

hrn-api-1        (127.0.0.1:28911)  joins erp_default -> erp-postgres-1:5432
hrm-migrate-1    one-shot, applies EF Core migrations at each deploy
```

## Services

| Service | Image | Host port | Purpose |
|---|---|---|---|
| `hrn-api` | `mightyfin/hrm-api:local` | `127.0.0.1:28911` | ASP.NET Core 10 API. Builds and runs `dotnet test` then `dotnet publish` (multi-stage .NET 10 SDK → `aspnet:10.0-noble`). Health probe is a bash `/dev/tcp` check because the image has no curl/wget. |
| `hrm-migrate` | same image | — | Runs `dotnet Mightyfin.Erp.Hrm.Api.dll --apply-migrations-only`; `hrn-api` waits for `service_completed_successfully`. |
| `hrn-web` | `mightyfin/hrm-web:local` | 3000 (internal) | TanStack Start SSR build (`node-server` nitro preset), pnpm install, `VITE_HRM_API_BASE=/api/hrm`. |
| `hrm-proxy` | `nginx:alpine` | `127.0.0.1:28912` | Reverse proxy: `/api/hrm` → API, everything else → SSR frontend. Carries the Traefik labels. |

The `erp_default` network is declared external so the API can resolve `erp-postgres-1` by container name, reusing the existing ERP database (no second Postgres).

## Deployment layout on the server

```
/home/mightyfin/production/hrm/
├── code/            # git clone of the fork (deployed at main)
└── nginx.conf       # copy of deployment/hrm/nginx.conf mounted into hrm-proxy-1
```

Secrets/env live at `/home/mightyfin/.config/mightyfin/hrm/.env`, consistent with `/home/mightyfin/.config/mightyfin/erp/.env`:

```
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__Hrm=Host=erp-postgres-1;Port=5432;Database=erp;Username=erp;Password=***
ERP__AuthMode=disabled
HRM__DefaultTenantId=019ffa8b-0fb0-71e6-849a-f76e5a28e0b5
HRM__AllowedOrigins=https://hrm.mightyfinance.co.zm,http://localhost:5173
```

## Deploy / rebuild

```bash
cd /home/mightyfin/production/hrm/code && git pull
cd /home/mightyfin/production/hrm
docker compose -f code/deployment/hrm/docker-compose.prod.yml build   # runs 87+ backend tests
docker compose -f code/deployment/hrm/docker-compose.prod.yml up -d
```

The migrations service runs automatically on every `up` (apply-only mode); the API refuses to start until it succeeds.

## DNS note

`hrm.mightyfinance.co.zm` must resolve to `187.124.27.67` (proxied or DNS-only via Cloudflare) for Traefik's Let's Encrypt HTTP-01 challenge to succeed. Until the record exists, the route is reachable only by `Host` header on `https://127.0.0.1/`.

## Conventions respected

The deployment deliberately introduces **no new networking**: it reuses the existing Traefik ingress, the `erp_default` bridge network, the shared `erp-postgres-1` container, the `:local` image-tag convention, the `/home/mightyfin/production/*` stack layout, and the `/home/mightyfin/.config/mightyfin/*` env convention. The Go ERP API on `127.0.0.1:28910`, `admin-lms`, and the `efaas` stacks are untouched.
