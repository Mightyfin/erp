# M36 — Production readiness and controlled go-live

M36 adds a fail-closed release decision at `/hrm/configuration/go-live`. The API derives technical and data gates from the current tenant, combines them with append-only operational evidence, and requires role-specific acceptance. `canGoLive` is true only when every gate passes and HR, payroll, finance, technical and tenant owners have approved.

## Automated gates

- no pending EF migrations;
- default employer TPIN, NAPSA and NHIMA references;
- active pay group, PAYE slabs, NAPSA and NHIMA rules;
- complete NRC, TPIN, NAPSA and NHIMA identity for every active worker;
- one released/closed and reconciled payroll rehearsal;
- no failed notification or failed/rejected integration operations.

## Evidence gates

Backup/restore, security test, migration rehearsal, performance test, monitoring/alerts, incident runbook, rollback rehearsal, HR/payroll UAT and HR/payroll training must each have a current passed evidence record. Evidence is append-only. A newer failed or expired record blocks release.

## Acceptance boundary

Sign-off is append-only and enforced by workforce role: `hr_admin` for HR, technical and tenant ownership, `payroll` for payroll, and `finance_approver` for finance. `finance_approver` is an HRM staff role; an identity with only an external-product role such as `tenant_owner` remains denied entry.

Run `deployment/hrm/go-live-rehearsal.sh` to exercise public health, unauthenticated admission, performance, and isolated backup restoration. Record its durable output reference only after reviewing the result. Human UAT, training, alert routing and acceptance must never be auto-certified.

## Controlled release

1. Tag the last known-good local images with `capture-release.sh <git-sha>`.
2. Run the rehearsal and retain its JSON output and logs.
3. Resolve all blockers shown in the readiness console.
4. Record real evidence and obtain all named sign-offs.
5. Deploy only the commit whose images, migration output, tests and evidence were reviewed.
6. Monitor health, authentication, notification delivery and payroll reconciliation through the agreed hypercare window.
