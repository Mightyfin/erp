# New World Cargo HRM — Expanded CRUD Equivalents Audit

**Environment:** [erp.newworldcargo.com](https://erp.newworldcargo.com)  
**Audit date:** 23 August 2026  
**Scope:** Expense, Travel, Shift, Attendance, Time, Activity, Overtime, and Slip-related links.  
**Method:** Live authenticated API probes, deployed browser inspection, and deployed source/adapter inspection. No application data was changed.

## Executive answer

The expanded list contains a mixture of **master data**, **transaction workflows**, **derived read models**, and **documents/slips**. They should not all be implemented as ordinary CRUD tables.

The deployed system already has real equivalents for **shift definitions, shift assignment, attendance, attendance corrections, employee clocking, attendance-based Timesheets, overtime review, and payroll-linked overtime evidence**. It does not currently have live PostgreSQL/API resources for **Expense Claim, Expense Claim Type, Travel Request, Purpose of Travel, Shift Request, Activity Type, Overtime Type, or Overtime Slip as a standalone document**.

The correct model for the current New World Cargo HRM is:

> **Master data is CRUD. Operational time and overtime are controlled workflows and derived records. Payroll-linked evidence is a detail/slip projection, not an independently editable slip.**

## Detailed coverage matrix

| Requested item | Closest current equivalent | Live backend evidence | Current deployed UI | Classification |
|---|---|---|---|---|
| **Expense Claim** | None | No expense route or live adapter; `/api/hrm/time/expenses` returned HTTP 404 | `/hrm/time/expenses` is release-gated as “Not in this release” | **Missing live CRUD** |
| **Expense Claim Type** | None | No expense-type route, model, or adapter | No live configuration page | **Missing** |
| **Travel Request** | None | No travel route or live adapter; `/api/hrm/time/travel` returned HTTP 404 | `/hrm/time/travel` is release-gated | **Missing live workflow** |
| **Purpose of Travel** | None | No travel-purpose resource found | No live configuration page | **Missing master data** |
| **Shift Type** | **Shift definition** | `/api/hrm/time/shifts` list and `/api/hrm/time/shifts` create; `ShiftCreateRequest` stores code, name, start/end, break, standard hours, daily threshold, and weekday/rest-day/holiday overtime multipliers | Schedules page has live “Create shift” and live Shift definitions | **Live CRUD equivalent, with limited update/delete** |
| **Shift Location** | **Work Location / Branch** | `/api/hrm/admin/locations` list/create/update | Organisation setup supports live branch/location creation; shift itself does not have a dedicated location field | **Partial equivalent** |
| **Shift Assignment** | Worker shift assignment | `/api/hrm/time/shifts/assign/{workerId}` creates an assignment with shift, calendar, effective-from/to | Schedules page provides live “Assign to worker” | **Live create workflow; no full edit/delete UI** |
| **Shift Schedule** | Worker roster / My schedule | `/api/hrm/time/roster/{workerId}` provides a roster read model | Schedules page renders a 14-day schedule, but the top self-service schedule has mock/timeclock elements and a hardcoded self-service worker context | **Partial; live roster plus mock schedule shell** |
| **Shift Schedule Assignment** | Shift assignment with calendar/effective dates | Same assignment endpoint and `ShiftAssignmentRequest` | Live assignment dialog exists | **Live equivalent, not a separate resource** |
| **Shift Request** | Shift-change request | No live shift-request endpoint found | “Request a shift change” exists on the Schedules page but remains mock/self-service behavior | **Mock/partial; missing live workflow** |
| **Shift Assignment Tool** | Create shift + Assign to worker dialogs | Live shift list/create and assignment endpoints | Live controls exist on Schedules page | **Live operational tool, limited scope** |
| **Attendance** | Attendance summary/import/worker history | `/api/hrm/time/attendance`, worker attendance history, clock-in/out, attendance import, and roster routes | Timesheets and attendance import are live; there is no unrestricted raw attendance edit/delete register | **Live operational record, not ordinary CRUD** |
| **Attendance Request** | Attendance correction request | `/api/hrm/time/corrections` list/create/decide and self-service correction create route | Attendance corrections list and “Raise a correction” form are live | **Live workflow; evidence upload still non-persistent** |
| **Employee Checkin** | Self-service clock-in/out | `/api/hrm/me/attendance/today`, `/me/attendance`, `/me/attendance/clock-in`, `/me/attendance/clock-out` | `/hrm/attendance/clock` exists and calls live actions, but the current admin identity returned HTTP 422 `worker-not-linked` | **Live clock workflow; not an HR CRUD register** |
| **Employee Attendance Tool** | Combined Attendance, Timesheets, Checkin, Corrections, and Import pages | All corresponding live endpoints exist for these separate purposes | The functionality is split across separate pages by purpose | **No single tool; live components exist separately** |
| **Time / Timesheet** | Attendance-only Timesheet summary | `/api/hrm/time/attendance` provides the live data | `/hrm/time/timesheets` supports Today, Week, Month, Custom, Table, Week layout, Month layout, details, and overtime decisions | **Live attendance-derived Timesheet equivalent** |
| **Activity Type** | None, intentionally | No activity-type route or adapter | No activity-type page | **Not applicable to the attendance-only Timesheet design; missing if later required** |
| **Overtime** | Attendance overtime lifecycle and review queue | `/api/hrm/time/overtime`, `/overtime/{id}/decide`; attendance stores overtime hours, multiplier, state, decision metadata, and payroll links | Overtime review page and Timesheets detail drawer are live | **Live controlled workflow, not free-form CRUD** |
| **Overtime Type** | Shift overtime multiplier fields | `ShiftCreateRequest` stores weekday, rest-day, and holiday multipliers; attendance stores the applied multiplier | No separate Overtime Type master page | **No standalone type CRUD; equivalent is partly embedded in Shift definition** |
| **Overtime Slip** | Attendance detail drawer plus payroll line/payslip evidence | Attendance record stores payroll run/line links after release; payroll line stores dedicated overtime earning and explanation | Timesheets drawer shows status, decision note, payroll linkage; payroll line shows explainable overtime component | **Live equivalent as derived evidence; no independently editable slip CRUD** |

## What is already real and usable

### Shift definitions and assignments

The closest live equivalent to **Shift Type** is the Shift definition resource. It is not merely a label: it contains working hours, unpaid break, standard hours, overtime threshold, and overtime multipliers for weekday, rest-day, and holiday work. The system can list and create shifts through the live API and the deployed Schedules page exposes “Create shift”.

The closest equivalent to both **Shift Assignment** and **Shift Schedule Assignment** is the worker assignment record. It ties a worker to a shift and optional calendar with effective start and end dates. The deployed UI exposes assignment to a worker. It is not a separate duplicate resource and should remain one controlled assignment model.

**Shift Location** is currently represented by the organisation’s Work Location/Branch resource and worker placement, not by a dedicated location property on each shift definition. If New World Cargo needs the same shift to vary by branch, the correct future design is either a location-scoped assignment or a shift-location rule, not a second unrelated CRUD page.

### Attendance and Employee Checkin

Attendance is a real operational record derived from imports and clocking. It is intentionally not an unrestricted table where users can overwrite or delete historical attendance, because payroll and overtime decisions depend on an auditable source. The current live surfaces are Attendance import, Timesheet summary, worker attendance history, Employee Checkin, and Attendance Request/corrections.

Employee Checkin is a live self-service punch workflow, not a full HR register. The current browser account is not linked to an HRM worker, and the live endpoint correctly returned HTTP 422 with `worker-not-linked`. Linking the local account to a worker is required before exercising the clock-in/clock-out UI for that account.

Attendance Request is a genuine workflow. A worker can raise a correction and an authorised reviewer can decide it. However, the current evidence upload text is explicitly non-persistent and should be replaced with the shared document/storage contract before the workflow is considered complete.

### Timesheets and overtime

The deployed Timesheet page is already the correct equivalent for the attendance-only **Time / Timesheet** requirement. It uses live attendance data and supports day, week, month, and custom range projections. It does not introduce projects, activities, billable flags, cost centres, or project approval, which is correct for New World Cargo’s attendance model.

Overtime is also real, but it is a controlled lifecycle rather than a generic CRUD table. Overtime is derived from clock-in/out and shift rules, reviewed, approved or rejected, and then allocated to payroll only when appropriate. The Timesheets detail drawer and Overtime review queue provide the operational equivalent of an Overtime Slip by showing the attendance evidence, decision note, lifecycle status, and payroll linkage. The payroll line is the authoritative financial result.

The current system has no separate **Overtime Type** master. The applied type is effectively expressed by the shift rule and multiplier: weekday, rest-day, or holiday. If the business requires named types such as “Normal overtime”, “Sunday overtime”, “Public holiday overtime”, or “Call-out overtime”, that should be introduced as explicit master data only after the calculation and payroll rules are defined.

## What is missing or still mock-only

Expense Claim and Travel Request are not live modules. The deployed pages are release-gated, the live API returned 404 for their candidate resources, and the frontend adapter contains no real methods for claims, claim types, travel requests, travel purposes, or travel advances. Their old source surfaces are demonstration/mock-oriented and must not be enabled for production use without a PostgreSQL model, API, permissions, audit trail, attachments, approval lifecycle, and browser validation.

There is no live Activity Type resource. This is not necessarily a defect because New World Cargo has explicitly defined Timesheets as attendance-only. Activity types would only be needed if the product later changes to track projects, tasks, work items, billable time, or another activity dimension.

Shift Request is also not a live workflow. The UI has a request-shift-change action, but no corresponding backend endpoint was found. It should remain disabled or visibly marked as unavailable until the request lifecycle, manager decision, effective date, conflict checks, and audit requirements are implemented.

## Recommended pre-Milestone-2 implementation order

| Priority | Implementation | Why |
|---:|---|---|
| 1 | Finish the live Shift definition and assignment UI, including edit/close behavior and clear branch/calendar scope | Shift rules directly drive attendance and overtime calculations |
| 2 | Fix Employee Checkin account-link testing and replace hardcoded self-service context with the authenticated worker link | Clocking is the source event for attendance and overtime |
| 3 | Complete Attendance Request evidence persistence and correction detail | Corrections must be auditable before payroll relies on them |
| 4 | Decide whether named Overtime Types are needed; otherwise keep multipliers embedded in Shift definitions | Avoid creating duplicate rule systems |
| 5 | Keep Activity Type out of Timesheets unless the business changes the attendance-only requirement | Prevent project-style complexity from entering the HRM |
| 6 | Implement Compensatory Leave separately as a controlled leave workflow | It is adjacent to overtime but is not the same as an overtime slip |
| 7 | Design and implement Expense Claim Type before Expense Claim | Claim transactions require a stable policy/category master first |
| 8 | Implement Purpose of Travel before Travel Request | Travel requests need controlled purpose, policy, advance, approval, and settlement data |
| 9 | Build Expense Claim and Travel Request as separate financial-control modules | They need different approvals, attachments, reimbursement/advance, and audit rules |

## Conclusion

For the expanded list, the system is strongest in **attendance, check-in, corrections, Timesheets, shifts, assignments, and overtime review/payroll evidence**. The correct equivalents are already present for most time-and-attendance needs, but they are distributed across purpose-specific pages rather than one giant CRUD screen.

The system does **not** currently have live CRUD for Expense Claim, Expense Claim Type, Travel Request, Purpose of Travel, Shift Request, Activity Type, Overtime Type as a standalone master, or Overtime Slip as an independently editable document. Where appropriate, the existing equivalent is a controlled workflow or derived evidence view rather than a CRUD table.

## References

[1]: https://erp.newworldcargo.com/hrm/time/schedules "Live Schedules and shift assignment page"

[2]: https://erp.newworldcargo.com/hrm/attendance "Live Attendance corrections page"

[3]: https://erp.newworldcargo.com/hrm/attendance/clock "Live Employee Checkin page"

[4]: https://erp.newworldcargo.com/hrm/time/timesheets "Live attendance-only Timesheet page"

[5]: https://erp.newworldcargo.com/hrm/time/operations "Live Overtime review page"

[6]: https://erp.newworldcargo.com/hrm/time/expenses "Live Expense route, currently release-gated"

[7]: https://erp.newworldcargo.com/hrm/time/travel "Live Travel route, currently release-gated"

[8]: https://github.com/georgemunganga/erp/tree/2f9a0fb "Authoritative deployed ERP/HRM source repository"
