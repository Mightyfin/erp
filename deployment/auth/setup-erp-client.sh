#!/usr/bin/env bash
# Mightyfin ERP — create/update the `erp-web` OIDC client in the
# `mightyfin-sandbox` Keycloak realm.
#
# Run on the production host (187.124.27.67) as mightyfin:
#   bash setup-erp-client.sh [list|apply]
#
# - list  : show the current erp-web client (idempotent, no changes)
# - apply : create if missing, update if present (idempotent)
#
# Auth design (see deployment/auth/README.md):
#   - Public client (no secret) — the ERP React shell holds the client id only.
#   - Authorization Code + PKCE flow via Keycloak's hosted login page.
#   - Silent SSO (prompt=none) gives automatic login when an IDP session exists;
#     otherwise the ERP shows its own email+password login page.
#   - Direct username/password grant (ROPC) is NOT enabled — Keycloak 26+
#     discourages it and it breaks MFA.
set -euo pipefail

BASE_URL="http://localhost:18081"
REALM="mightyfin-sandbox"
ADMIN_USER="local-admin"
ADMIN_PASS="44998aa24afc280b58152dde76e27088222f4d6ea6ef2eafb7a0607e24dbc1bf"

TOKEN=$(curl -s -X POST "${BASE_URL}/realms/master/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=admin-cli&username=${ADMIN_USER}&password=${ADMIN_PASS}" \
  | python3 -c "import json,sys; print(json.load(sys.stdin).get('access_token',''))")
[ -n "$TOKEN" ] || { echo "ERROR: could not obtain admin token"; exit 1; }
AUTH="Authorization: Bearer ${TOKEN}"

case "${1:-apply}" in
  list)
    curl -s -H "$AUTH" "${BASE_URL}/admin/realms/${REALM}/clients?clientId=erp-web"
    ;;
  apply)
    # Client payload: public SPA client, PKCE, standard + silent flows allowed
    PAYLOAD='{
      "clientId": "erp-web",
      "name": "Mightyfin ERP (web)",
      "description": "Public OIDC client for the ERP React shell",
      "enabled": true,
      "publicClient": true,
      "directAccessGrantsEnabled": false,
      "serviceAccountsEnabled": false,
      "standardFlowEnabled": true,
      "implicitFlowEnabled": false,
      "frontchannelLogout": true,
      "attributes": {
        "pkce.code.challenge.method": "S256",
        "post.logout.redirect.uris": "https://erp.mightyfinance.co.zm/*"
      },
      "redirectUris": [
        "https://erp.mightyfinance.co.zm/*",
        "http://localhost:5173/*",
        "http://localhost:3000/*"
      ],
      "webOrigins": [
        "https://erp.mightyfinance.co.zm",
        "http://localhost:5173",
        "http://localhost:3000"
      ]
    }'
    EXISTING=$(curl -s -H "$AUTH" "${BASE_URL}/admin/realms/${REALM}/clients?clientId=erp-web")
    ID=$(echo "$EXISTING" | python3 -c "import json,sys; r=json.load(sys.stdin); print(r[0]['id'] if r else '')")
    if [ -z "$ID" ]; then
      echo "Creating erp-web client..."
      curl -s -o /dev/null -w "HTTP %{http_code}\n" -X POST "${BASE_URL}/admin/realms/${REALM}/clients" \
        -H "$AUTH" -H "Content-Type: application/json" -d "$PAYLOAD"
    else
      echo "Updating erp-web client ($ID)..."
      curl -s -o /dev/null -w "HTTP %{http_code}\n" -X PUT "${BASE_URL}/admin/realms/${REALM}/clients/${ID}" \
        -H "$AUTH" -H "Content-Type: application/json" -d "$PAYLOAD"
    fi
    echo "Done. Verify:"
    curl -s "${BASE_URL}/realms/${REALM}/.well-known/openid-configuration" | python3 -c "
import json,sys
d=json.load(sys.stdin)
print('Issuer:', d.get('issuer'))"
    ;;
  *)
    echo "Usage: $0 [list|apply]"
    exit 1
    ;;
esac
