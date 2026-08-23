# Milestone 1 rerun — live API findings

Date: 2026-08-23
Environment: https://erp.newworldcargo.com
Tenant: local-tenant

A read-only authenticated browser check returned HTTP 200 for:

- `GET /api/hrm/time/attendance?from=2026-09-01&to=2026-09-30`
- `GET /api/hrm/time/overtime?from=2026-09-01&to=2026-09-30`
- `GET /api/hrm/payroll/runs?page=1&pageSize=20`

The attendance and overtime responses returned the same two persisted UAT records for EMP-0005 / UAT Eunice:

| Work date | Clock | Total | Regular | OT | Multiplier | Status | Payroll link |
|---|---|---:|---:|---:|---:|---|---|
| 2026-09-03 | 08:00–20:00 | 11h | 8h | 3h | 1.5 | paid | Sep 2026 run and payroll line present |
| 2026-09-04 | 08:00–19:00 | 10h | 8h | 2h | 1.5 | rejected | no payroll run or payroll line |

The approved/paid row retained a decision timestamp and payroll run/line IDs. The rejected row retained the decision reason `M1 UAT rejection path`, decision timestamp, and no payroll linkage.

The September payroll response returned HTTP 200 and included a released run with:

- period: Sep 2026
- total gross: ZMW 35,764.90
- total deductions: ZMW 5,512.4755
- total net: ZMW 30,252.4245
- employer cost: ZMW 37,906.90
- employee count: 5
- exception count: 0

The audit command attempted to launch a second API process inside the running API container with a status argument; that produced an expected address-in-use error because port 8080 was already occupied. It did not replace or stop the running API container. The API service itself remained up.


A read-only authenticated check of `/api/hrm/payroll/pay-groups/full` and `/api/hrm/payroll/pay-groups` returned HTTP 200. The active default group is `ZMW-STANDARD-PG` / `ZMW Standard — New World Cargo UAT`, id `01a02337-b8e9-7a63-b127-506e826636b1`, monthly frequency, calendar day 25, ZMW currency. The existing period list must be checked before creating a new run; no August run will be altered.

Further authenticated live checks against released run `01a02b54-ba69-7db2-9682-7a57419f9e13` returned HTTP 200 for run details, lines, and audit. The run is `released`, Sep 2026, 5 employees, gross ZMW 35,764.90, deductions ZMW 5,512.4755, net ZMW 30,252.4245, employer cost ZMW 37,906.90, zero exceptions, and `paymentStatus=not-created` (no bank/payment file action). UAT Eunice (`EMP-0005`) has basic ZMW 3,000, a single dedicated `overtime` earning of ZMW 64.90, and explanation `3.00 approved attendance overtime hour(s), weighted by recorded shift multiplier; basic K3,000.00 / 208 standard monthly hours`; gross is ZMW 3,064.90. The run audit shows created -> inputs-locked -> calculated -> calculated again (idempotency rerun) -> approved with `M1 UAT maker-checker approval; synthetic test only.` -> payslips-released, with distinct preparation, approval, and release subjects. A controlled attempted decision on the already-paid Sep 3 overtime record returned HTTP 422 `overtime-already-paid` and did not change the record.


Post-guard attendance re-read returned HTTP 200 and confirmed no mutation: Sep 4 remains `overtimeStatus=rejected`, 2 overtime hours, reason `M1 UAT rejection path`, and null payroll run/line IDs; Sep 3 remains `overtimeStatus=paid`, 3 overtime hours, and links to the same Sep run and payroll line. The API DTO fields are `overtimeStatus`, `overtimeDecisionReason`, `overtimePayrollRunId`, and `overtimePayrollLineId`.


Browser validation of `https://erp.newworldcargo.com/hrm/time/timesheets` in live-only mode succeeded. Custom range 1–30 Sep 2026 rendered 2 live records, 2 present days, and 2 overtime entries: UAT Eunice on Sep 4 with 2h at 1.50x and `OT rejected`, and Sep 3 with 3h at 1.50x and `OT paid`. The page displayed the live PostgreSQL badge, real date controls, table controls, and no mock/preview content.


The live Timesheets Week layout projected the September range into a calendar grid and placed UAT Eunice's Sep 3 and Sep 4 attendance in their day cells. Clicking the Sep 3 cell opened the shared detail drawer showing 8:00 AM–8:00 PM, 11h worked, 3h overtime, `OT paid`, `Paid in payroll`, source `device-import`, and the exact run and line IDs. This confirms the weekly projection and detail evidence use the same live record.


The live Month layout rendered Sep 3 and Sep 4 as `OT` cells for UAT Eunice and showed monthly totals of 5h overtime and 21h total. Clicking the Sep 3 monthly cell opened the same live drawer with the paid payroll linkage. Month projection and cell-to-detail behavior therefore passed.


The rejected Sep 4 row opened successfully from the live Table view. Its drawer showed `OT rejected`, 10h worked, 2h overtime, source `device-import`, payroll state `Excluded from payroll`, decision note `M1 UAT rejection path`, and the deciding subject/timestamp. No payroll run or line linkage was shown.


Quick Access opened from the top navigation, displayed real role-aware route links across Time & attendance, People, Payroll & benefits, Performance & recruitment, and Reports & setup, and filtering for `overtime` reduced the list to the real `Overtime review` route. Escape closed the dialog successfully. The categorized sidebar remained visible with the New World Cargo navigation groups.


Following the filtered Quick Access link navigated to `/hrm/time/operations` without a crash. The live Overtime review page showed the workflow steps Import → Review → Approve → Payroll, automatic organisation/branch scope messaging, and tabs for Needs review, Approved, Rejected, Paid, and All records. The Approved tab showed 1 eligible record (9.50h); the Rejected tab showed UAT Eunice Sep 4, 2.00h ×1.50, `Excluded from payroll`, and `M1 UAT rejection path`. Needs review correctly showed 0 for the current persisted dataset.


The live browser console had no output/errors during the Timesheets, detail-drawer, Quick Access, and Overtime review navigation checks.
