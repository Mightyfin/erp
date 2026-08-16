#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "$0")" && pwd)"
base_url="${HRM_BASE_URL:-https://erp.mightyfinance.co.zm}"
run_id="$(date -u +%Y%m%dT%H%M%SZ)"
curl --fail --silent --show-error "$base_url/health/live" >/dev/null
curl --fail --silent --show-error "$base_url/health/ready" >/dev/null
unauthorised="$(curl --silent --output /dev/null --write-out '%{http_code}' "$base_url/api/hrm/go-live")"
[[ "$unauthorised" == "401" ]] || { echo "Expected unauthenticated go-live API to return 401, got $unauthorised" >&2; exit 1; }
"$script_dir/performance-smoke.sh"
if [[ "${HRM_REHEARSE_RESTORE:-true}" == "true" ]]; then "$script_dir/verify-backup-restore.sh"; fi
printf '{"control":"migration-rehearsal","status":"passed","evidenceReference":"M36-REHEARSAL-%s","health":"passed","admission":"passed"}\n' "$run_id"
