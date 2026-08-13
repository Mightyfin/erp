# HRM Module — Production Deployment

This document describes how the HRM module (ASP.NET Core 10 API + React 19 frontend) is deployed. **Everything public lives under a single subdomain, `erp.mightyfinance.co.zm`**: the frontend is hosted on Vercel, and the API rides the existing Cloudflare Tunnel behind a wildcard hostname rule, so the browser never sees any other host.

## Architecture

```
erp.mightyfinance.co.zm  (DNS -> Vercel, Vercel TLS cert)
├─ /hrm/*               frontend routes        <- served by Vercel
└─ /api/hrm/*           vercel.json rewrite    -> https://erp.mightyfinance.co.zm/api/hrm/*
                                                    (leaves Vercel edge, hits Cloudflare edge,
                                                     tunnel wildcard rule picks it up)

Cloudflare Tunnel (remote config)
├─ rule: erp.mightyfinance.co.zm (root) -> http://localhost:28910   [Go ERP API, unchanged]
└─ rule: *.mightyfinance.co.zm path /api/hrm -> http://localhost:28912
     (hrm-proxy nginx)
     ├─ /api/hrm/* -> hrn-api:8080  (.NET 10 API, erp DB via erp_default)
     └─ /health/*  -> hrn-api:8080
```

The wildcard rule requires **no DNS record** — tunnel wildcard hostnames match at Cloudflare's edge regardless of DNS, and it does not create or alter any DNS entry. It cannot be reached by typing the name in a browser (no DNS, no resolution); it is only reachable via the rewrite that Vercel performs on the ERP subdomain. The Go ERP API's root tunnel rule is untouched.

## Vercel hosting

The Vercel project is created from the fork's `modules/hrm/frontend/module-connect` folder (root directory setting). Required project env vars:

```
VITE_HRM_API_BASE=/api/hrm
VITE_USE_REAL_API=true
VITE_HRM_TENANT_ID=019ffa8b-0fb0-71e6-849a-f76e5a28e0b5
```

`vercel.json` in the module folder declares the build (`vite build` → `.output/public` static export for Vercel), the `/api/hrm` rewrite back to the ERP subdomain, and security headers. The domain `erp.mightyfinance.co.zm` is attached in Vercel and its Cloudflare DNS record points at Vercel.

## Cloudflare Tunnel configuration

All changes happen in the Zero Trust dashboard (Networks → Tunnels → tunnel → Public hostnames), using the existing tunnel:

| Subdomain | Path | Type | URL | Effect |
|---|---|---|---|---|
| erp.mightyfinance.co.zm | *(root)* | HTTP | http://localhost:28910 | existing rule — unchanged |
| *.mightyfinance.co.zm | /api/hrm | HTTP | http://localhost:28912 | new rule, appended |

The existing `erp.mightyfinance.co.zm` root rule and the `efaas-origin` / `auth` rules remain exactly as they are.

## Server-side stack

The API stack runs as Docker Compose at `/home/mightyfin/production/hrm` on `187.124.27.67` (image built from this repo, tag `:local`, tests run during build, migrations applied at startup). It reuses the shared `erp-postgres-1` database over the `erp_default` network, binds only to `127.0.0.1` (API `:28911`, web proxy `:28912`), and follows the platform's env convention at `/home/mightyfin/.config/mightyfin/hrm/.env`. The Go ERP API on `:28910`, `admin-lms`, and the `efaas` stacks are untouched.

## Conventions respected

The deployment deliberately introduces **no new networking**: it reuses the existing token-based Cloudflare Tunnel (remote config), the `erp_default` bridge network, the shared `erp-postgres-1` container, the `:local` image-tag convention, the `/home/mightyfin/production/*` stack layout, and the `/home/mightyfin/.config/mightyfin/*` env convention. No new subdomain or DNS record is created.
