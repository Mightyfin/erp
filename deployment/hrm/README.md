# HRM Module — Production Deployment

This document describes how the HRM module (ASP.NET Core 10 API + React 19 frontend) is deployed. **Everything public lives under a single subdomain, `erp.mightyfinance.co.zm`**: the frontend is hosted on Vercel, and the API is served by a path-based Cloudflare Tunnel rule on the same hostname. No new subdomains, no DNS records, and no wildcards are created.

## Architecture

```
erp.mightyfinance.co.zm  (DNS record, proxied, owned by the zone owner)
│
├─ browsers ────────────────────────────────► Vercel ── frontend (/hrm/*)
│
└─ /api/hrm calls (from the frontend) ──────► Vercel rewrite
                                              │
                                              └─ server-side fetch back to
                                                 erp.mightyfinance.co.zm/api/hrm
                                                    │ (Cloudflare edge)
                                                    └─ Tunnel path rule:
                                                       /api/hrm -> localhost:28912
                                                          (hrm-proxy nginx)
                                                          ├─ /api/hrm/* -> hrn-api:8080 (.NET 10)
                                                          └─ /health/*  -> hrn-api:8080
```

## Cloudflare Tunnel configuration

All tunnel changes happen in the Zero Trust dashboard (Networks → Tunnels → tunnel → Public hostnames), using the existing token-based tunnel. Two rules live on the **same existing hostname**:

| Subdomain | Path | Type | URL | Status |
|---|---|---|---|---|
| erp.mightyfinance.co.zm | *(root)* | HTTP | http://localhost:28910 | existing — unchanged |
| erp.mightyfinance.co.zm | /api/hrm | HTTP | http://localhost:28912 | **new path rule, appended** |

The `efaas-origin` and `auth` rules remain exactly as they are. The path rule guarantees the API surface is the only thing reachable through the new entry.

## Vercel hosting

The Vercel project is created from the fork's `modules/hrm/frontend/module-connect` folder (root directory setting). Required project env vars:

```
VITE_HRM_API_BASE=/api/hrm
VITE_USE_REAL_API=true
VITE_HRM_TENANT_ID=019ffa8b-0fb0-71e6-849a-f76e5a28e0b5
```

`vercel.json` declares the TanStack Start framework, the `/api/hrm` rewrite to the ERP subdomain, SPA fallback routing for `/hrm/*`, and security headers. The domain `erp.mightyfinance.co.zm` is attached in Vercel.

## Server-side stack

The API stack runs as Docker Compose at `/home/mightyfin/production/hrm` on `187.124.27.67` (image built from this repo, tag `:local`, tests run during build, migrations applied at startup). It reuses the shared `erp-postgres-1` database over the `erp_default` network, binds only to `127.0.0.1` (API `:28911`, web proxy `:28912`), and follows the platform's env convention at `/home/mightyfin/.config/mightyfin/hrm/.env`. The Go ERP API on `:28910`, `admin-lms`, and the `efaas` stacks are untouched.

## Conventions respected

The deployment deliberately introduces **no new networking**: it reuses the existing token-based Cloudflare Tunnel (remote config), the `erp_default` bridge network, the shared `erp-postgres-1` container, the `:local` image-tag convention, the `/home/mightyfin/production/*` stack layout, and the `/home/mightyfin/.config/mightyfin/*` env convention. No new subdomain or DNS record is created; only one path rule is appended to the existing ERP tunnel hostname.
