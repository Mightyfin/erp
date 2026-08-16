#!/usr/bin/env bash
set -euo pipefail

base_url="${HRM_BASE_URL:-https://erp.mightyfinance.co.zm}"
requests="${HRM_PERF_REQUESTS:-30}"
p95_limit_ms="${HRM_PERF_P95_LIMIT_MS:-1500}"

[[ "$requests" =~ ^[0-9]+$ ]] && (( requests >= 5 && requests <= 500 )) || {
  echo "HRM_PERF_REQUESTS must be between 5 and 500" >&2; exit 2;
}
[[ "$p95_limit_ms" =~ ^[0-9]+$ ]] || { echo "HRM_PERF_P95_LIMIT_MS must be numeric" >&2; exit 2; }
[[ "$base_url" == https://* || "$base_url" == http://127.0.0.1:* ]] || {
  echo "Only HTTPS or a loopback HTTP target is allowed" >&2; exit 2;
}

results="$(mktemp)"
trap 'rm -f "$results"' EXIT
errors=0
for ((i = 1; i <= requests; i++)); do
  read -r status seconds < <(curl --silent --show-error --output /dev/null --max-time 10 \
    --write-out '%{http_code} %{time_total}\n' "$base_url/health/ready")
  [[ "$status" == "200" ]] || errors=$((errors + 1))
  awk -v seconds="$seconds" 'BEGIN { printf "%.0f\n", seconds * 1000 }' >> "$results"
done

rank=$(( (requests * 95 + 99) / 100 ))
p95_ms="$(sort -n "$results" | sed -n "${rank}p")"
max_ms="$(sort -n "$results" | tail -1)"
status="passed"
if (( errors > 0 || p95_ms > p95_limit_ms )); then status="failed"; fi
printf '{"control":"performance-test","status":"%s","requests":%s,"errors":%s,"p95Ms":%s,"maxMs":%s,"limitMs":%s,"target":"%s"}\n' \
  "$status" "$requests" "$errors" "$p95_ms" "$max_ms" "$p95_limit_ms" "$base_url"
[[ "$status" == "passed" ]]
