# HRM Module — Production Deployment

This document describes how the HRM module (ASP.NET Core 10 API + React 19 frontend) is deployed. **Everything lives on a single subdomain, `erp.mightyfinance.co.zm`**: the frontend is hosted on Vercel, and the API is served by a path-locked Cloudflare Tunnel rule appended to the same ERP hostname. The browser never leaves the ERP subdomain; no other names, records, or wildcards are introduced and no existing services are touched.

## Architecture

```
erp.mightyfinance.co.zm  (zone DNS, proxied)
│
├─ browsers ─────────────────► Vercel ───────────── frontend (/hrm/*)
│                                             │
│                                             └─ /api/hrm rewrite ──► https://erp.mightyfinance.co.zm/api/hrm/*
│                                                                        (server-side fetch via Cloudflare edge)
│
└─ Cloudflare Tunnel path rules on the same hostname
     ├─ / (root)     -> http://localhost:28910   [Go ERP API — existing]
     └─ /api/hrm     -> http://localhost:28912   [HRM proxy — appended]
          └─ (hrm-proxy nginx)
               ├─ /api/hrm/* -> hrn-api:8080 (.NET 10 API, erp DB via erp_default)
               └─ /health/*  -> hrn-api:8080
```

## Cloudflare Tunnel configuration

The API is exposed through a **path rule appended to the existing `erp.mightyfinance.co.zm` tunnel hostname** — no new zone name is created. The rule is **path-locked**: only `/api/hrm` requests reach the HRM API. All tunnel changes happen in the Zero Trust dashboard (Networks → Tunnels → tunnel → Public hostnames), using the existing token-based tunnel:

| Subdomain | Path | Type | URL | Status |
|---|---|---|---|---|
| erp.mightyfinance.co.zm | *(root)* | HTTP | http://localhost:28910 | existing — unchanged |
| erp.mightyfinance.co.zm | /api/hrm | HTTP | http://localhost:28912 | **new path rule, appended to same hostname** |

The `efaas-origin` and `auth` rules remain exactly as they are. Cloudflare resolves the DNS record for the hostname (either directly or via the tunnel's published CNAME); this zone-level DNS arrangement is managed by the zone owner.

## Vercel hosting

The Vercel project is created from the fork's `modules/hrm/frontend/module-connect` folder (root directory setting). Required project env vars:

```
VITE_HRM_API_BASE=/api/hrm
VITE_USE_REAL_API=true
VITE_HRM_TENANT_ID=019ffa8b-0fb0-71e6-849a-f76e5a28e0b5
```

`vercel.json` declares the TanStack Start framework, the `/api/hrm` rewrite to `https://erp.mightyfinance.co.zm/api/hrm/:path*` (Vercel's server-side fetch; the browser stays on `erp.mightyfinance.co.zm` throughout), SPA fallback routing for `/hrm/*`, and security headers. The domain `erp.mightyfinance.co.zm` is attached in Vercel, with the zone's DNS record pointing at Vercel as set by the zone owner.

## Server-side stack

The API stack runs as Docker Compose at `/home/mightyfin/production/hrm` on `187.124.27.67` (image built from this repo, tag `:local`, tests run during build, migrations applied at startup). It reuses the shared `erp-postgres-1` database over the `erp_default` network, binds only to `127.0.0.1` (API `:28911`, web proxy `:28912`), and follows the platform's env convention at `/home/mightyfin/.config/mightyfin/hrm/.env`. The Go ERP API on `:28910`, `admin-lms`, and the `efaas` stacks are untouched.

The `hrm-outbox-publisher-1` service uses the same API image in publisher mode. It joins the shared communications network and publishes `mightyfin.hrm.>` events to the `HRM_EVENTS` JetStream stream. Deploy Compose with the communications environment file so `NATS_AUTH_TOKEN` is supplied during interpolation, for example `docker compose --env-file /home/mightyfin/.config/mightyfin/communications-sandbox/.env -f docker-compose.prod.yml up -d`. Direct SMTP remains off unless `HRM__NotificationFallback=smtp` is set explicitly. See [M26 notification delivery](../../docs/hrm/M26-NOTIFICATION-DELIVERY.md).

## Conventions respected

The deployment deliberately introduces minimal new networking: it reuses the existing token-based Cloudflare Tunnel (remote config), the `erp_default` bridge network, the shared `erp-postgres-1` container, the `:local` image-tag convention, the `/home/mightyfin/production/*` stack layout, and the `/home/mightyfin/.config/mightyfin/*` env convention. The entire public surface is exactly one subdomain (`erp.mightyfinance.co.zm`); the API rule is appended as a path rule on the existing tunnel hostname, with no other names or records touched.
