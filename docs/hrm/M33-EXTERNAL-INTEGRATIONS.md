# M33 — Finance, statutory, and external integrations

M33 establishes explicit, tenant-scoped boundaries between HRM and systems owned by finance, treasury, statutory compliance, document operations, and identity administration. External systems do not read HRM tables directly.

## Delivered scope

- Versioned integration catalogue with direction, transport, retry strategy, reconciliation process, and named operational owner.
- Durable integration-operation ledger with immutable payload snapshots, stable public ids, idempotency keys, attempts, external references, outcomes, and actor history.
- Balanced payroll-to-finance journal contract with summary entries, employee/department/cost-centre detail, component totals, and debit/credit controls.
- Bank payment hand-off gated by payroll release, payment approval/release, and primary bank details.
- Worker-level ZRA PAYE, NAPSA, and NHIMA hand-offs containing employer references, statutory identities, gross wages, employee/employer contributions, and control totals.
- Workforce identity full/delta hand-offs plus linked/unlinked worker monitoring. Subject links remain unique inside an HRM tenant.
- Document-storage adapter visibility in the integration catalogue.
- NATS/JetStream outbox event (`hrm.integration.ready`) for every new or retried hand-off.
- Integration operations UI for preparing files, reviewing contracts, downloading payloads, retrying failures, and recording reconciliation outcomes.

## API surface

All routes are under `/api/hrm/integrations` and require `payroll` or `hr_admin`; identity sync requires `hr_admin`.

- `GET /`
- `POST /finance-postings`
- `POST /payment-handoffs`
- `POST /statutory-handoffs`
- `POST /identity-sync`
- `POST /operations/{id}/retry`
- `POST /operations/{id}/reconcile`
- `GET /operations/{id}/download`

Creating the same business hand-off twice returns the original operation. A retry republishes the original contract and idempotency key instead of creating a duplicate external transaction.

## Comparison with the supplied legacy June payroll pack

The archive `JUNE-20260816T113046Z-1-001.zip` contains PAYE and NAPSA forms, employee PAYE detail, payroll-by-department summary/detail, journal-voucher summary/detail, and the NAPSA employer spreadsheet. It confirms four controls that the ERP must retain:

1. Company payroll totals reconcile from employee detail through department summaries.
2. The finance journal balances payments and deductions and preserves employee, transaction, department, and currency detail.
3. Statutory outputs carry employer references and worker NRC/NAPSA identity data, not only aggregate liability totals.
4. Employer and employee NAPSA shares reconcile to the remittance total.

M33 now carries those fields and controls in external hand-off contracts. The legacy pack also exposes a presentational gap: the ERP has live reports and CSV exports but does not yet reproduce all print-formatted department, JV, PAYE, NAPSA remittance/amendment, and authority-template documents. Those layouts belong to M35 reporting, where they should be generated from the same immutable released-run snapshots rather than reimplementing payroll calculations.

## Acceptance coverage

Backend tests cover finance balancing and idempotency, release gates, bank-detail gates, identity sync, failure reconciliation, and replay. The frontend build verifies the new route and generated route tree. Playwright covers a payroll administrator preparing a finance hand-off and recording the external journal reference.
