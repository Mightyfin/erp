# Milestone 1 — Production Time and Overtime Contract

## Objective

Provide a real PostgreSQL-backed workflow in which attendance-derived overtime is visible per employee, reviewable by authorised HR/payroll users, approved or rejected with an audit trail, and included exactly once in a payroll run when the run is calculated.

## Scope for this milestone

The milestone extends the existing `AttendanceRecord` model rather than creating a second time-entry table. Existing attendance import, clock-in/clock-out, shift rules, and `OvertimeHours`/`OvertimeMultiplier` calculation remain the source of the derived hours. The new production capability adds an approval state and payroll allocation state to each attendance record that has overtime.

| Area | Contract |
|---|---|
| Source | Existing PostgreSQL `hrm.attendance_records`, populated by clocking, import, or approved correction. |
| Derived values | `TotalHours`, `ScheduledHours`, `RegularHours`, `OvertimeHours`, and `OvertimeMultiplier` are recalculated from the assigned shift and calendar. |
| Approval | Overtime records begin as `pending`; authorised HR/payroll/manager users may approve or reject them with a reason. No rejected record can enter payroll. |
| Payroll period | An approved overtime record is selected by work date within the pay period and by worker/pay-group scope. |
| Payroll amount | `OvertimeHours × hourly basic rate × approved multiplier`, where hourly basic rate is the worker’s effective basic monthly amount divided by the configured monthly workday/hour basis. The exact basis is persisted in the payroll line explanation. |
| Idempotency | Each payroll calculation clears and rebuilds its run lines. Approved overtime is read again and represented once as an `overtime` earning component. Recalculation cannot double-pay it. |
| Lifecycle | Once a payroll run is released/closed, its overtime line is historical. Changes to attendance/overtime must not mutate the released line; a correction requires a controlled payroll correction or reversal. |
| Audit | Approval/rejection actor, timestamp, reason, and payroll allocation reference are persisted and included in the time/payroll audit trail. |
| Permissions | Employees may view their own overtime; managers/HR/payroll may review and decide within their scope; only payroll/HR may include or correct released payroll results. |
| Reports | Time operations and management reports show employee, work date, hours, multiplier, amount, status, and payroll reference. |

## Acceptance criteria

A test employee can receive an attendance record with overtime from a real import or punch. An authorised reviewer can see the employee-level record and approve it. A payroll run covering that work date calculates an overtime earning exactly once, with the amount and source attendance record visible on the payroll line. Recalculating the run does not duplicate the earning. Rejecting an overtime record prevents it from entering payroll. An unauthorised user cannot approve or alter another employee’s overtime. All actions remain traceable to the real database records and actor subject IDs.

## Explicit non-goals

This milestone does not implement timesheet-based pay, automatic external device synchronisation, complex union agreements, night-shift premiums beyond the existing shift multiplier, or a full configurable overtime policy engine. Those can be subsequent milestones after the base approval and payroll-linkage workflow is proven.

## Implementation status — 2026-08-22

Milestone 1 is now implemented on the production source branch and deployed to `https://erp.newworldcargo.com`. `AttendanceRecord` remains the single source record and now persists `OvertimeStatus` (`none`, `pending`, `approved`, `rejected`, `paid`), decision reason, reviewer subject, decision timestamp, payroll run ID, and payroll line ID. The database migration `20260822205857_M1OvertimeLifecycle` adds these columns and the tenant/status/date queue index to PostgreSQL.

The live API exposes `GET /api/hrm/time/overtime` with worker, date, and status filters and `POST /api/hrm/time/overtime/{id}/decide` for approve/reject decisions. Derived overtime is moved to `pending` during attendance recalculation; paid records cannot be reset. Approval/rejection is role-protected, branch-aware at list scope, reason-enforced for rejection, and emits `hrm.overtime.decided` outbox events.

Payroll calculation loads only approved, unallocated overtime for the pay period and adds one explainable `overtime` earning per worker before statutory components. Recalculation clears and rebuilds run lines without duplicating the earning. The source attendance is changed to `paid` and linked to the run/line only at final payroll release, not at calculation. Rejected and pending overtime are excluded.

The live React Time Operations page now contains a real overtime review queue and decision-reason field; production mode does not invoke mock loaders. Automated backend validation passed 312 tests, the frontend production build passed, the PostgreSQL migration applied successfully, and correction-safe UAT passed for derivation, approval, rejection, payroll inclusion, idempotent recalculation, release linkage, permission guards, and audit persistence. See `m1_overtime_uat_evidence.md` and `m1_browser_validation.md`.
