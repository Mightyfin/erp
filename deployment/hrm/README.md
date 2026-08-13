# HRM Module — Production Deployment

This document describes how the HRM module (ASP.NET Core 10 API + React 19 frontend) is deployed. **The user-facing application lives on a single subdomain, `erp.mightyfinance.co.zm`**: the frontend is hosted on Vercel, and the API rides the existing Cloudflare Tunnel behind a path-locked hostname (`api.mightyfinance.co.zm`, invisible to users). The browser never leaves the ERP subdomain; no wildcards are used and no existing services are touched.

## Architecture

```
erp.mightyfinance.co.zm       (DNS CNAME -> Vercel, proxied)
  browsers ──────────────────────────► Vercel ── frontend (/hrm/*)
                                             │
                                             └─ /api/hrm rewrite ──► api.mightyfinance.co.zm
                                                                        │ (DNS CNAME -> cfargotunnel.com)
                                                                        │ Cloudflare Tunnel path rule:
                                                                        │   /api/hrm -> localhost:28912
                                                                        └─ (hrm-proxy nginx)
                                                                             ├─ /api/hrm/* -> hrn-api:8080 (.NET 10)
                                                                             └─ /health/*  -> hrn-api:8080
```

## Cloudflare Tunnel configuration

The API is exposed through a dedicated zone hostname, `api.mightyfinance.co.zm`, whose CNAME record is published by the tunnel itself (targeting `cfargotunnel.com`). The tunnel rule is **path-locked**: only `/api/hrm` requests reach the HRM API, so the hostname cannot be used for anything else. All tunnel changes happen in the Zero Trust dashboard (Networks → Tunnels → tunnel → Public hostnames), using the existing token-based tunnel:

| Subdomain | Path | Type | URL | Status |
|---|---|---|---|---|
| erp.mightyfinance.co.zm | *(root)* | HTTP | http://localhost:28910 | existing — unchanged |
| api.mightyfinance.co.zm | /api/hrm | HTTP | http://localhost:28912 | **new tunnel hostname, path-locked** |

The `efaas-origin` and `auth` rules remain exactly as they are. This keeps the ERP subdomain solely for the user-facing application; `api.mightyfinance.co.zm` is internal plumbing used only by Vercel's server-side rewrite and never surfaces in the browser.

## Vercel hosting

The Vercel project is created from the fork's `modules/hrm/frontend/module-connect` folder (root directory setting). Required project env vars:

```
VITE_HRM_API_BASE=/api/hrm
VITE_USE_REAL_API=true
VITE_HRM_TENANT_ID=019ffa8b-0fb0-71e6-849a-f76e5a28e0b5
```

`vercel.json` declares the TanStack Start framework, the `/api/hrm` rewrite to `https://api.mightyfinance.co.zm` (Vercel's server-side fetch; the browser stays on `erp.mightyfinance.co.zm`), SPA fallback routing for `/hrm/*`, and security headers. The domain `erp.mightyfinance.co.zm` is attached in Vercel with its DNS CNAME pointing at `vercel-dns.com`.

## Server-side stack

The API stack runs as Docker Compose at `/home/mightyfin/production/hrm` on `187.124.27.67` (image built from this repo, tag `:local`, tests run during build, migrations applied at startup). It reuses the shared `erp-postgres-1` database over the `erp_default` network, binds only to `127.0.0.1` (API `:28911`, web proxy `:28912`), and follows the platform's env convention at `/home/mightyfin/.config/mightyfin/hrm/.env`. The Go ERP API on `:28910`, `admin-lms`, and the `efaas` stacks are untouched.

## Conventions respected

The deployment deliberately introduces minimal new networking: it reuses the existing token-based Cloudflare Tunnel (remote config), the `erp_default` bridge network, the shared `erp-postgres-1` container, the `:local` image-tag convention, the `/home/mightyfin/production/*` stack layout, and the `/home/mightyfin/.config/mightyfin/*` env convention. The user-facing domain is exactly one subdomain (`erp.mightyfinance.co.zm`); `api.mightyfinance.co.zm` is added only as a path-locked tunnel hostname for the API surface, with no other names or records touched.
