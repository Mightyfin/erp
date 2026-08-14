# ERP Authentication — Keycloak Integration

This directory contains the configuration and tooling that connect the Mightyfin ERP to the platform IDP: **Keycloak 26.7** (`mightyfin-efaas-identity-1`), exposed publicly at `https://auth.mightyfinance.co.zm` and running the realm `mightyfin-sandbox`.

## Authentication design (hybrid flow)

The ERP's auth behaviour must satisfy two modes with a single seamless experience:

> When the system recognises an IDP (Keycloak) session, the user is logged in automatically without any action. When the system cannot recognise an IDP connection or session, the ERP presents its own email-and-password login page — it never fails closed or redirects to an error page.

The implementation strategy is the standard **Authorization Code flow with PKCE** against Keycloak's hosted login, driven by the ERP React shell:

1. **Automatic login (silent SSO).** On app load the shell calls the token endpoint with `prompt=none`. If Keycloak has an active session cookie for the user, it returns tokens immediately — no UI is shown.
2. **ERP-hosted login page.** If silent SSO fails (no session, first visit, unrecognized IDP), the shell renders its own email/password login page and drives the full redirect-based code+PKCE flow through Keycloak. The user experience is a login form on the ERP; credential verification is still Keycloak's, so MFA and future password policies remain available.
3. **Session refresh** is handled by the silent flow on every app load, so long-lived sessions survive page reloads.

The legacy Resource Owner Password Credentials grant is deliberately **not** enabled (`directAccessGrantsEnabled: false`): Keycloak 26+ disables it by default because it bypasses MFA, and the hosted-login form above provides an equivalent UX without that cost.

## Keycloak client

The ERP web client (`erp-web`) is a **public** client (no client secret — secrets cannot be stored in a browser) with PKCE enforced. It is registered in the `mightyfin-sandbox` realm by the supplied script:

```bash
# On the production host, as mightyfin:
bash setup-erp-client.sh apply   # create or update the erp-web client
bash setup-erp-client.sh list    # inspect without changes
```

The client allows redirect URIs on `https://erp.mightyfinance.co.zm/*` (production) and `http://localhost:{5173,3000}/*` (local/TanStack dev). `webOrigins` are set to the same list so CORS preflight from the shell succeeds.

## Realm and endpoints

| Item | Value |
|---|---|
| Realm | `mightyfin-sandbox` |
| OIDC discovery | `https://auth.mightyfinance.co.zm/realms/mightyfin-sandbox/.well-known/openid-configuration` |
| Authorization endpoint | `.../protocol/openid-connect/auth` |
| Token endpoint | `.../protocol/openid-connect/token` |
| Realm settings | `loginWithEmailAllowed: true`, `registrationAllowed: false` |

## Server notes

Keycloak admin bootstrap credentials live in the efaas sandbox compose file (`compose.portable.yaml` → `KC_BOOTSTRAP_ADMIN_*`); the setup script embeds them so it can run unattended during platform provisioning. The traefik router `mightyfin-auth` exposes only `/realms` and `/resources` paths on `auth.mightyfinance.co.zm`, so the admin API is reachable from the host on `127.0.0.1:18081` but not from the internet.
