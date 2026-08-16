# M27 — Payroll Operations Completion

M27 completes the controlled payroll operating cycle from run preparation to bank reconciliation.

## Delivered workflow

1. Create a run and capture the preparer's OIDC subject.
2. Lock inputs, calculate gross-to-net, and review live control totals and employee lines.
3. Resolve, waive, or exclude every exception with an actor, reason, and timestamp. A component correction updates both the employee line and run totals without erasing the decision trail.
4. Approve the calculated run. The preparer cannot approve their own run.
5. Release payslips after statutory-readiness checks. The approver cannot also release them.
6. Generate and download the bank CSV, approve it with a different actor, then release the instruction with another actor.
7. Reconcile the bank acknowledgement. The actual amount must equal payroll net before the run can close.
8. Inspect or export the append-only run audit history as CSV.

Payment status is independent of payslip status and progresses through `not-created → generated → approved → released → reconciled`. This keeps payslip publication, movement of money, and final reconciliation as separate controlled decisions.

## API surface

- `GET /api/hrm/payroll/runs`
- `POST /api/hrm/payroll/runs/{runId}/lines/{lineId}/exception`
- `POST /api/hrm/payroll/runs/{runId}/lines/{lineId}/correction`
- `POST /api/hrm/payroll/runs/{runId}/payments/generate`
- `GET /api/hrm/payroll/runs/{runId}/payments/file`
- `POST /api/hrm/payroll/runs/{runId}/payments/approve`
- `POST /api/hrm/payroll/runs/{runId}/payments/release`
- `POST /api/hrm/payroll/runs/{runId}/reconcile`
- `GET /api/hrm/payroll/runs/{runId}/audit`
- `GET /api/hrm/payroll/runs/{runId}/audit/export`

## Verification

- Backend: 141 tests, including segregation, exception gating, corrections, bank-file generation, payment approval/release, reconciliation, and audit export.
- Frontend: production build and lint pass for the changed payroll surfaces.
- Browser: five Playwright production checks pass, including the complete M27 payment workflow through reconciliation.
- Deployment: migration `M27PayrollOperations` applied and API/web health checks pass.
