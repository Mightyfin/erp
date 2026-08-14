# ERP edge router

Cloudflare Worker routing for `erp.mightyfinance.co.zm`.

- `/api` and `/api/*` are forwarded through the `ERP_BACKEND` Workers VPC binding.
- All other requests are forwarded to the ERP Vercel production deployment.
- The production Worker route is `erp.mightyfinance.co.zm/*` in the
  `mightyfinance.co.zm` zone. It reuses the existing proxied ERP DNS record;
  no backend or origin subdomain is required.

The `ERP_BACKEND` binding targets the `erp-backend` VPC service, which reaches
the ASP.NET HRM stack at `127.0.0.1:28912` through the existing
`mightyfin-gate` Cloudflare Tunnel.

Deploy from this directory with `npx wrangler deploy`.

The Vercel production alias is protected. Store its automation bypass value as
the Worker secret `VERCEL_BYPASS`; never commit the value.
