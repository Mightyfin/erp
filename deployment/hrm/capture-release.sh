#!/usr/bin/env bash
set -euo pipefail

release="${1:-}"
[[ "$release" =~ ^[0-9a-f]{7,40}$ ]] || { echo "Usage: $0 <git-sha>" >&2; exit 2; }
docker image inspect mightyfin/hrm-api:local mightyfin/hrm-web:local >/dev/null
docker tag mightyfin/hrm-api:local "mightyfin/hrm-api:$release"
docker tag mightyfin/hrm-web:local "mightyfin/hrm-web:$release"
printf '{"status":"captured","release":"%s","apiImage":"mightyfin/hrm-api:%s","webImage":"mightyfin/hrm-web:%s"}\n' "$release" "$release" "$release"
