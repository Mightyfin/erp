# M34 — Security, tenancy, audit, and compliance hardening

M34 makes the HRM security boundary enforceable and reviewable. A shared Keycloak identity still needs an explicit HRM workforce role, and all persisted HRM data remains scoped to the tenant resolved from the authenticated request.

## Delivered controls

- Global EF query filters scope every HRM entity to the current tenant.
- New records always receive the request tenant; caller-supplied tenant values are overwritten.
- Modified or deleted records whose tenant differs from the request are rejected with `cross-tenant-write`.
- Entity audit evidence now covers create, update, and delete, records the actor and request correlation id, and redacts statutory identifiers, bank accounts, tokens, secrets, passwords, and integration payloads.
- Entity audit, privileged-action, and compliance evidence rows are append-only.
- Every authenticated privileged API mutation records a request-level outcome, including failed and denied attempts. Source network addresses are deliberately not persisted.
- Manager directory reads mask statutory identifiers and omit date of birth, IdP subject links, emergency contacts, and payment details. Employees continue to use subject-bound `/hrm/me` routes for their own data.
- The live backend role matrix documents the enforced capability, allowed roles, data scope, sensitivity, and compensating control.
- Retention rules are visible with an explicit legal-hold override. HR administrators can place and release tenant-scoped legal holds with reasons and actor history.
- Backup/restore, tenant-isolation, and security-test evidence can be recorded with execution and expiry dates.
- The old .NET 10 release-candidate persistence packages were upgraded to supported patch releases. `dotnet list package --vulnerable --include-transitive` reports no known vulnerable packages.

## API surface

All endpoints require the `hr_admin` role in addition to normal HRM workforce admission.

- `GET /api/hrm/security`
- `GET /api/hrm/security/audit/export`
- `POST /api/hrm/security/evidence`
- `POST /api/hrm/security/legal-holds`
- `POST /api/hrm/security/legal-holds/{id}/release`

The same handlers are available below `/api/v1/hrm/security`.

## Backup and restore rehearsal

Run from the repository root on an authorised operations host:

```bash
deployment/hrm/verify-backup-restore.sh
```

The script creates a uniquely named temporary database, dumps only the production `hrm` schema, restores it, reconciles table and migration counts, prints a machine-readable evidence reference, and removes the temporary database on exit. It validates the generated database name before any drop operation and never writes to the source database.

Record the returned evidence reference in **Configuration → Security and compliance → Control evidence**. Evidence expires according to the organisation's control calendar; an expired rehearsal returns the posture control to `action-required`.

## Acceptance evidence

- Backend tests cover tenant-filtered reads, untrusted tenant overwrite, rejected cross-tenant writes, audit create/update coverage, sensitive-value redaction, audit immutability, manager masking, external-role admission denial, role matrix, legal holds, and compliance evidence.
- Playwright covers an HR administrator reviewing controls and the enforced role matrix, inspecting privileged audit evidence, placing a legal hold, and recording a successful restore rehearsal.
- Production acceptance also requires the complete Playwright suite, a successful isolated restore rehearsal, healthy API readiness, and an unauthenticated `401` from the security API.
