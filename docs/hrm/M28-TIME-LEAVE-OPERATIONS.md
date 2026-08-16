# M28 — Time, Attendance, and Leave Operations

M28 makes attendance and leave operable by HR without direct database changes or corrective spreadsheets.

## Delivered workflow

1. HR creates reusable shift rules with scheduled hours, unpaid breaks, daily overtime thresholds, and weekday/rest-day/public-holiday multipliers.
2. HR effective-dates a worker's shift and work-calendar assignment. A new assignment safely closes the prior open assignment.
3. HR imports attendance rows by employee number. Each batch persists reconciliation totals and row-level errors; accepted records retain their import batch reference.
4. Attendance calculates overnight-safe worked hours, regular hours, overtime hours, and the applicable overtime multiplier from the assigned shift and calendar.
5. Corrected attendance is recalculated through the same rules as imported and self-service attendance.
6. HR runs monthly leave accrual across active workers. A unique tenant/period control prevents duplicate accruals, and the run plus ledger changes are transactional.
7. HR posts reasoned positive or negative balance adjustments. The adjustment and its ledger entry are transactional and retain the acting OIDC subject.
8. HR runs overdue escalation for leave and attendance-correction workflows. Overdue items move up the manager chain (or back to the HR queue), record the escalation time, and receive a new due date.

Existing work-calendar and public-holiday administration remains the source for weekend and holiday classification. The new Time operations screen exposes the operational actions from the HRM navigation.

## API surface

- `GET /api/hrm/time/shifts`
- `POST /api/hrm/time/shifts`
- `POST /api/hrm/time/shifts/assign/{workerId}`
- `POST /api/hrm/time/attendance/import`
- `POST /api/hrm/time/leave/accruals/run`
- `POST /api/hrm/time/leave/balances/adjust`
- `POST /api/hrm/time/escalations/run`

## Controls

- Operational writes require `hr_ops` or `hr_admin`.
- Imports are capped at 10,000 rows, reject duplicate employee/date rows, and report unknown employees or invalid dates/times.
- Monthly accrual has a unique tenant/period key and an application idempotency check.
- Shift assignments and all operational records are tenant filtered by the shared HRM context.
- Import batches, accrual runs, adjustments, attendance records, and escalated workflow requests participate in the existing audit infrastructure.

## Verification

- Backend: 145 tests, including M28 overtime, reconciliation, accrual idempotency, balance adjustment, and escalation-scope cases.
- Frontend: production build and targeted lint pass for the changed M28 surfaces.
- Browser: Playwright covers the HR administrator attendance import and reconciliation flow in addition to the existing production checks.
- Deployment: migration `M28TimeLeaveOperations` creates the operational tables and extends attendance calculation fields.
