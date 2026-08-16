#!/usr/bin/env bash
set -euo pipefail

# M34 isolated restore rehearsal. The generated database name is validated and
# removed on exit; the production database is read through pg_dump only.
postgres_container="${HRM_POSTGRES_CONTAINER:-erp-postgres-1}"
run_id="$(date -u +%Y%m%dT%H%M%SZ)-$$"
restore_database="hrm_restore_test_${run_id//[^0-9A-Za-z]/_}"

if [[ ! "$restore_database" =~ ^hrm_restore_test_[0-9A-Za-z_]+$ ]]; then
  echo "Refusing unsafe restore database name" >&2
  exit 1
fi

docker exec \
  -e RESTORE_DATABASE="$restore_database" \
  -e REHEARSAL_ID="$run_id" \
  "$postgres_container" sh -euc '
    dump_file="$(mktemp /tmp/hrm-m34-restore.XXXXXX)"
    cleanup() {
      dropdb --if-exists --force -U "$POSTGRES_USER" "$RESTORE_DATABASE" >/dev/null 2>&1 || true
      rm -f "$dump_file"
    }
    trap cleanup EXIT INT TERM

    pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --schema=hrm --format=custom --file="$dump_file"
    createdb -U "$POSTGRES_USER" "$RESTORE_DATABASE"
    pg_restore -U "$POSTGRES_USER" -d "$RESTORE_DATABASE" --no-owner --no-privileges "$dump_file"

    source_tables="$(psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc "select count(*) from information_schema.tables where table_schema = \$\$hrm\$\$ and table_type = \$\$BASE TABLE\$\$")"
    restored_tables="$(psql -U "$POSTGRES_USER" -d "$RESTORE_DATABASE" -Atc "select count(*) from information_schema.tables where table_schema = \$\$hrm\$\$ and table_type = \$\$BASE TABLE\$\$")"
    source_migrations="$(psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc "select count(*) from hrm.__hrm_migrations")"
    restored_migrations="$(psql -U "$POSTGRES_USER" -d "$RESTORE_DATABASE" -Atc "select count(*) from hrm.__hrm_migrations")"

    test "$source_tables" = "$restored_tables"
    test "$source_migrations" = "$restored_migrations"
    printf "{\"control\":\"backup-restore\",\"status\":\"passed\",\"evidenceReference\":\"M34-RESTORE-%s\",\"tables\":%s,\"migrations\":%s}\n" \
      "$REHEARSAL_ID" "$restored_tables" "$restored_migrations"
  '
