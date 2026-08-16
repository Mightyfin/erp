#!/usr/bin/env bash
set -euo pipefail

release="${1:-}"
mode="${2:---dry-run}"
[[ "$release" =~ ^[0-9a-f]{7,40}$ ]] || { echo "Usage: $0 <git-sha> [--dry-run|--execute]" >&2; exit 2; }
[[ "$mode" == "--dry-run" || "$mode" == "--execute" ]] || { echo "Unknown mode: $mode" >&2; exit 2; }
api_image="mightyfin/hrm-api:$release"
web_image="mightyfin/hrm-web:$release"
docker image inspect "$api_image" "$web_image" >/dev/null
compose_file="$(cd "$(dirname "$0")" && pwd)/docker-compose.prod.yml"
env_file="${HRM_COMPOSE_ENV_FILE:-/home/mightyfin/.config/mightyfin/communications-sandbox/.env}"
HRM_API_IMAGE="$api_image" HRM_WEB_IMAGE="$web_image" docker compose --env-file "$env_file" -f "$compose_file" config --quiet
if [[ "$mode" == "--dry-run" ]]; then
  printf '{"status":"validated","mode":"dry-run","release":"%s","databaseChanged":false}\n' "$release"
  exit 0
fi
[[ -n "${HRM_ROLLBACK_BACKUP_REFERENCE:-}" ]] || {
  echo "HRM_ROLLBACK_BACKUP_REFERENCE is required for an executed rollback" >&2; exit 3;
}
HRM_API_IMAGE="$api_image" HRM_WEB_IMAGE="$web_image" docker compose --env-file "$env_file" -f "$compose_file" up -d --no-build
curl --fail --silent --show-error --retry 20 --retry-delay 2 https://erp.mightyfinance.co.zm/health/ready >/dev/null
printf '{"status":"rolled-back","release":"%s","backupReference":"%s"}\n' "$release" "$HRM_ROLLBACK_BACKUP_REFERENCE"
