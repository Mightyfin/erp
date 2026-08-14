# ERP edge router

Fallback Cloudflare Worker routing for `erp.mightyfinance.co.zm`.

- `/api` and `/api/*` are forwarded through the `ERP_BACKEND` Workers VPC binding.
- All other requests are forwarded to the ERP Vercel production deployment.
- No production hostname route is currently attached to this Worker.

Production uses the `mightyfin-gate` Tunnel published application as a
catch-all for `erp.mightyfinance.co.zm`, with no Path value, targeting
`http://127.0.0.1:28912`. The server nginx proxy routes API requests to the
ASP.NET API and all other requests to the React SSR frontend.

The retained `ERP_BACKEND` binding targets the `erp-backend` VPC service at
`127.0.0.1:28912` through the existing Tunnel. It is available if the Worker
edge architecture is restored later.

Deploy from this directory with `npx wrangler deploy`.

The Vercel production alias is protected. Store its automation bypass value as
the Worker secret `VERCEL_BYPASS`; never commit the value.
