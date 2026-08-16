# HRM rollback plan

Every release retains immutable API and web tags for the last known-good Git SHA plus a pre-change database backup reference. `capture-release.sh` creates the image tags; `rollback-release.sh <sha> --dry-run` verifies both images and the rendered Compose configuration without changing runtime state.

An executed rollback requires explicit `--execute` and `HRM_ROLLBACK_BACKUP_REFERENCE`. Application rollback and database restoration are separate decisions: EF migrations may be forward-only when new data has been written. The incident owner, technical owner, HR owner and payroll owner must decide whether to keep the forward-compatible schema, restore an isolated backup, or perform a production restore under the database recovery procedure.

After rollback, verify live/readiness, IdP login and HRM workforce-role admission, employee own-data boundaries, payroll run state, outbox backlog, integrations, reports and the last reconciled totals. Record image IDs, backup reference, timestamps, actors and verification results in the incident timeline.
