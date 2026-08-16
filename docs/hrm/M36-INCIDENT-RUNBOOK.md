# HRM production incident runbook

## First response

Declare an owner and severity, start an incident timeline in UTC, preserve request IDs and container logs, and notify HR/payroll leadership when payroll, statutory, identity or employee data is affected. Never paste tokens, passwords, statutory identifiers or payroll payloads into chat or tickets.

## Public endpoint or readiness failure

1. Check `https://erp.mightyfinance.co.zm/health/live` and `/health/ready` separately.
2. Inspect `hrm-api-1`, `hrm-web-1`, `hrm-proxy-1`, `hrm-migrate-1` and PostgreSQL health and recent logs.
3. If readiness alone fails, stop release activity and inspect database reachability and migrations.
4. If the failure followed deployment, compare the running image IDs with the captured release record. Use `rollback-release.sh <sha> --dry-run` before an authorised rollback.

## Latency or capacity degradation

Run the bounded performance smoke, inspect request-duration logs and database pressure, then compare p95 with the accepted rehearsal. Do not increase timeouts to conceal saturation.

## Authentication loop or access denial

Confirm IdP discovery, redirect URI and browser callback state. A `login_required` result from silent SSO must show the sign-in page, not restart silent authentication. Confirm the identity has an explicit HRM workforce role; external tenant roles do not grant ERP entry.

## Payroll or integration incident

Freeze release/retry actions, retain run and operation IDs, compare control totals, and involve payroll plus finance owners. Retries must use the existing idempotency key. Do not edit payroll or outbox rows directly.

Close only after service recovery, reconciliation, stakeholder confirmation, evidence capture and a documented follow-up owner.
