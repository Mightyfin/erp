# M32 — HR administration and master-data hardening

M32 replaces the remaining mock-only worker import and bulk-update controls with a tenant-scoped, auditable backend workflow and a real HR administration screen.

## Delivered scope

- CSV worker import for up to 1,000 rows, with automatic employee numbering for new workers.
- Validation preview before mutation, including organisation/location codes, worker types, dates, archived records, duplicate employee numbers, email addresses, and NRC values.
- Controlled bulk updates for organisation, location, manager, grade, job title, worker status, contact fields, and Zambian statutory identities.
- Effective-dated organisation changes. A future change creates an approved movement and leaves the current worker projection unchanged; an immediate change creates an executed movement and updates the projection.
- Persisted batch history containing the submitted payload, privacy-restricted recovery snapshot, validation summary, actor identity, timestamps, and status.
- Thirty-day rollback for applied imports, bulk updates, and reactivations. Newly imported workers are archived during rollback instead of being deleted.
- Reason-gated archived-worker reactivation with a recoverable audit batch.
- HR operations UI for imports, bulk changes, batch recovery, reactivation, and current data-quality exceptions.
- HR role enforcement and existing global tenant query filters on all records.

## API surface

All routes are under `/api/hrm/master-data` and require authentication plus `hr_ops` or `hr_admin` authorization in the application service.

- `GET /batches`
- `POST /imports/preview`
- `POST /bulk/preview`
- `POST /batches/{id}/apply`
- `POST /batches/{id}/rollback`
- `POST /workers/{id}/reactivate`

Preview and apply are deliberately separate. Apply repeats validation inside the transaction so a stale preview cannot bypass current master-data constraints.

## Recovery semantics

Rollback restores the pre-change worker values and cancels any movement created by the batch. A worker created by an import is soft-archived on rollback to retain references and audit history. Recovery expires 30 days after application; the batch and audit history remain available after that window.

## Acceptance coverage

Backend tests cover import apply/rollback, identity collisions, archived overwrite prevention, future-effective movements, statutory field recovery, and reactivation recovery. The deployed Playwright journey covers CSV preview, apply, history, rollback, reactivation, and the quality dashboard.
