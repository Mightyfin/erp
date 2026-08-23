# New World Cargo HRM — Leave CRUD Coverage Audit

**Audit date:** 23 August 2026  
**System:** `https://erp.newworldcargo.com`  
**Scope:** Holiday List, Leave Type, Leave Period, Leave Policy, Leave Block List, Allocation, Leave Allocation, Leave Policy Assignment, Leave Control Panel, Leave Encashment, Leave Application, and Compensatory Leave Request.

## Executive conclusion

The deployed HRM has a real leave foundation backed by PostgreSQL and protected API routes, but it does **not yet expose the complete leave administration model as separate CRUD pages**. The current implementation supports leave types, holiday calendars, leave applications, accrual runs, balance adjustments, and leave encashment. Leave Period, Leave Policy, Leave Block List, Leave Allocation as a named allocation object, Policy Assignment, Leave Control Panel, and Compensatory Leave Request are either missing as dedicated resources or only represented by lower-level equivalents.

The system should not label the missing concepts as complete until dedicated persistence, permissions, audit history, API contracts, and browser workflows have been added.

## Coverage matrix

| Requested link | Current backend resource or equivalent | Current deployed UI | Status | Required next step |
|---|---|---|---|---|
| **Holiday List** | `WorkCalendar` and `PublicHoliday`; routes under `/api/hrm/admin/calendars` and `/api/hrm/admin/holidays` | Holidays are bundled under Business setup; no dedicated Holiday List administration page | **Backend CRUD exists; UI partial** | Add a dedicated calendar/holiday page with create, edit, delete/archive, recurring-date handling, and browser validation |
| **Leave Type** | `LeaveType`; `/api/hrm/admin/leave-types`, `/api/hrm/admin/leave-types/full`, POST/PATCH routes | Leave types are available through configuration/setup data and used by the leave form; no clearly separated live Leave Type page was confirmed | **Backend CRUD exists; UI partial** | Add a dedicated live Leave Type page with effective dates, evidence, notice, carry-forward, partial-day, and active-state controls |
| **Leave Period** | No dedicated leave-period entity. `LeaveAccrualRun.Period` stores an accrual period string such as `yyyy-MM`; payroll `PayPeriod` is a different concept | No dedicated leave-period page | **Missing** | Add a leave period model with open/closed status, dates, entitlement/accrual boundaries, and immutability after close |
| **Leave Policy** | No dedicated `LeavePolicy` entity or policy CRUD contract. Some rules currently live directly on `LeaveType` | Process configuration page is release-gated | **Missing** | Add effective-dated policy persistence and a policy editor; keep historical policy versions explainable |
| **Leave Block List** | No dedicated block-list entity or API | No page | **Missing** | Add blocked dates/ranges with reason, scope, recurrence, and validation against new leave applications |
| **Allocation** | `LeaveBalanceLedger`, accrual runs, and manual balance adjustments provide ledger-level allocation behavior | Operations/history and leave balances expose outcomes; no generic allocation page | **Partial equivalent** | Add a first-class allocation record and allocation list/detail view while preserving ledger entries as the accounting history |
| **Leave Allocation** | `POST /api/hrm/time/leave/accruals/run`, `POST /api/hrm/time/leave/balances/adjust`, and balance queries | Accrual/adjustment controls are not exposed as a complete dedicated administration screen | **Backend workflow exists; UI partial** | Add allocation run/adjustment page with preview, explicit execution, idempotency, and audit trail |
| **Leave Policy Assignment** | No dedicated assignment entity or route connecting a policy to a worker, group, department, branch, or effective date | No page | **Missing** | Add policy-assignment persistence, scope selectors, effective dates, overlap validation, and assignment history |
| **Leave Control Panel** | No single control-panel resource. Existing leave list, balances, accrual, correction, and encashment services are separate | Leave inbox and operations pages exist, but there is no unified control panel | **Missing as a page/workspace** | Build an operational control panel that combines pending applications, blocked conflicts, accrual status, balances, encashment, and cutoff warnings without duplicating records |
| **Leave Encashment** | `LeaveEncashmentRequest`; list, rate quote, create, and decision routes under `/api/hrm/time/leave/encashments`; approval posts a ledger reduction | Backend is real; the deployed Time Off in Lieu/leave surface is still release-gated or not a dedicated encashment screen | **Backend real; UI missing/partial** | Add a dedicated Leave Encashment page with rate quote, balance checks, approval/rejection, and payroll explanation |
| **Leave Application** | `LeaveRequest`; list/create/decide routes under `/api/hrm/time/leave`; balance reservation and cutoff checks are implemented | `/hrm/leave`, `/hrm/leave/new`, and approval routes are live | **Live workflow** | Improve dedicated admin controls and persistent evidence/attachment handling |
| **Compensatory Leave Request** | No dedicated compensatory-leave request entity or route. `LeaveEncashmentRequest` is cash encashment, not compensatory time off | `/hrm/time/toil` is release-gated | **Missing** | Add a dedicated request model linked to approved overtime/attendance, with conversion rate, expiry, approval, ledger posting, and cancellation rules |

## Exact live route evidence

The currently registered leave routes include:

- `GET /api/hrm/time/leave`
- `POST /api/hrm/time/leave`
- `POST /api/hrm/time/leave/{id}/decide`
- `GET /api/hrm/time/leave/balances/{workerId}`
- `POST /api/hrm/time/leave/accruals/run`
- `POST /api/hrm/time/leave/balances/adjust`
- `GET /api/hrm/time/leave/encashments`
- `GET /api/hrm/time/leave/encashments/rate/{workerId}`
- `POST /api/hrm/time/leave/encashments`
- `POST /api/hrm/time/leave/encashments/{id}/decide`
- `GET /api/hrm/admin/leave-types`
- `GET /api/hrm/admin/leave-types/full`
- `POST /api/hrm/admin/leave-types`
- `PATCH /api/hrm/admin/leave-types/{id}`
- `GET /api/hrm/admin/calendars`
- `POST/PATCH/DELETE /api/hrm/admin/holidays...`

These endpoints are protected by authorization and tenant/branch scope rules. The presence of an endpoint does not by itself mean that the corresponding user-facing CRUD page is complete.

## Recommended implementation order

The safest order is to build the leave foundation before adding more workflows:

1. **Leave Period and Leave Policy** with effective dating and close/immutability controls.
2. **Leave Policy Assignment** so the system knows which policy applies to which worker or organisational scope.
3. **Leave Block List** and application validation against blocked dates and policy windows.
4. **Leave Allocation** as a first-class auditable allocation record over the existing balance ledger and accrual engine.
5. **Dedicated Holiday List and Leave Type pages** using the already available backend CRUD.
6. **Leave Control Panel** for operations visibility and exception handling.
7. **Compensatory Leave Request** linked to approved overtime, separate from Leave Encashment.
8. **Leave Encashment UI** over the existing real backend workflow.

## Current conclusion

The requested leave model is **partly present, not fully added**. Leave Application is the strongest complete user workflow. Leave Type and Holiday List have real backend foundations but need dedicated UI. Leave Encashment has a real backend workflow but needs a dedicated UI. The remaining policy, period, assignment, block-list, control-panel, allocation-record, and compensatory-leave concepts require new implementation. No production-readiness approval should be inferred from the existing partial coverage.

**No application data was changed during this audit.**

*Prepared by Manus AI for the New World Cargo project.*

## References

[1]: https://erp.newworldcargo.com "New World Cargo HRM deployed application"
[2]: https://github.com/georgemunganga/erp "New World Cargo HRM source repository"
