# New World Cargo HRM — CRUD Coverage Audit

**Environment:** [erp.newworldcargo.com](https://erp.newworldcargo.com)  
**Audit date:** 23 August 2026  
**Scope:** Requested Setup, Employee, Leave, Attendance, Expense, and Travel links.  
**Method:** Deployed browser inspection, live authenticated API read checks, and source-level route/adapter inspection. No business data was changed during this audit.

## Executive summary

The system does **not** currently have complete CRUD for every requested link. It has a solid live foundation for **Employee records, organisation structure, payroll setup, leave requests/approvals, attendance import/corrections, and employee clocking**, but several areas are either partial, release-gated, or still mock-only.

The most important distinction is between **backend capability** and **deployed UI capability**. In a few areas the ASP.NET/PostgreSQL API already exposes create/update operations, but the live React page only lists records or offers a limited action. Those should be treated as **partial CRUD**, not complete CRUD, until the UI exposes the supported action and browser validation confirms persistence.

## Coverage matrix

| Requested link | Current deployed page/resource | Live backend/API evidence | Deployed UI result | CRUD classification |
|---|---|---|---|---|
| **Setup** | `/hrm/setup` | Setup state, steps, saved step data, complete-step, finish, first-user claim, administrator provisioning, and guarded reset routes exist | Guided setup wizard; this is onboarding/configuration orchestration, not ordinary record CRUD | **Implemented wizard, not CRUD** |
| **Company** | Organisation setup → Legal entities | `/hrm/admin/legal-entities`: list, detail, create, update; no delete route | Live company/entity list renders, but action is read-only in real mode | **Partial: API CRUD-minus-delete; UI read-only** |
| **Branch** | Organisation setup → Branches/work locations | `/hrm/admin/locations`: list, create, update; no delete route | Live “Add work location” exists; edit is not exposed in real mode. Branch access assignments separately support create/delete | **Partial CRUD** |
| **Department** | Organisation setup → Departments/cost centres | `/hrm/admin/org-units`: list, tree, entity tree, create, update, close; no hard-delete route | Live list and safe close action are present; create/edit are not exposed in real mode | **Partial: API create/update/close; UI list/close** |
| **Designation** | Intended mapping: Jobs catalogue / job title | `/hrm/admin/jobs`: list, create, update, close | “Jobs, grades and pay ranges” page is release-gated in the deployed browser; no live designation CRUD screen | **Backend CRUD; UI missing/release-gated** |
| **Employee** | `/hrm/employees` and employee create/edit/profile | `/hrm/workers`: list, detail, create, update, archive; imports, account linking, assignments and several child-record CRUD routes | Live directory shows five PostgreSQL-backed workers; Add employee, edit subset, archive, import/export and child-record operations exist | **Mostly live CRUD, with partial field coverage; archive instead of hard delete** |
| **Employee Group** | No dedicated live page/resource found | No EmployeeGroup model, route, or live adapter found | No deployed CRUD surface | **Missing** |
| **Employee Grade** | Mentioned under Jobs/grades/pay ranges | No dedicated grade CRUD route/model found; grades are supplied from saved setup-step data/mock/reference options | Jobs/grades page is release-gated in the deployed browser | **Missing dedicated CRUD** |
| **Leave Application** | `/hrm/leave`, `/hrm/leave/new`, leave approvals | `/hrm/me/leave` create; `/hrm/time/leave` list/create/decide; leave balances and leave types routes exist | Live self-service inbox and guided five-step request form render. Current admin account is not linked to a worker, so submission is correctly blocked | **Real create/list/cancel/approve workflow; partial attachments and identity setup** |
| **Compensatory Leave Request** | `/hrm/time/toil` | No live compensatory-leave/toil API route found | Deployed page is “Not in this release” | **Missing/release-gated** |
| **HR Settings** | `/hrm/configuration/process` and configuration index | Only a generic `/hrm/admin/config` read endpoint plus selected specific resources such as leave types, jobs, roles, capabilities, retention rules, and calendars | Process-design/HR-settings page is “Not in this release” in the deployed browser | **Partial configuration APIs; no unified live HR Settings CRUD page** |
| **Payroll Settings** | `/hrm/configuration/payroll` | Pay groups read/update, salary structures read/create/update, PAYE slabs update, contribution rules update, salary components read/update, payroll profiles create/update | Live Payroll setup renders real pay group and tabs; existing records can be edited, but most resource creation/deletion is not exposed in the UI | **Live partial CRUD/configuration** |
| **Daily Work Summary Group** | No dedicated page/resource found | No matching live model, route, adapter, or deployed page found | No CRUD surface | **Missing** |
| **Attendance** | Timesheets, attendance corrections, import, clocking | Attendance summary, worker attendance history, clock-in/out, shifts, assignment, import, corrections, overtime lifecycle routes exist | Timesheets is live and supports day/week/month/custom projections. Raw attendance is derived/imported data rather than a general edit/delete register | **Live operational workflow, not full raw-record CRUD** |
| **Attendance Request** | `/hrm/attendance` and `/hrm/attendance/new` | `/hrm/time/corrections`: list/create/decide; self-service correction create route also exists | Live correction list and Raise a correction form exist; evidence upload is explicitly non-persistent/mock and recorded-time preview is not fully live | **Real partial workflow** |
| **Employee Checkin** | `/hrm/attendance/clock` and self-service dashboard | `/hrm/me/attendance/today`, `/hrm/me/attendance`, `/hrm/me/attendance/clock-in`, `/hrm/me/attendance/clock-out`; worker-scoped clock routes also exist | Page exists and uses live punch actions, but current administrator returned HTTP 422 `worker-not-linked`; it is self-service clocking, not an HR CRUD register | **Live clock workflow; partial/unavailable until account link** |
| **Expense Claim** | `/hrm/time/expenses` and `/hrm/time/expenses/new` | No live expense API route or `realApi` adapter found; `/api/hrm/time/expenses` returned HTTP 404 | Deployed route is “Not in this release”; source surface is mock/demo-oriented | **Missing live CRUD** |
| **Travel Request** | `/hrm/time/travel` | No live travel API route or `realApi` adapter found; `/api/hrm/time/travel` returned HTTP 404 | Deployed route is “Not in this release” | **Missing live CRUD** |

## What is genuinely live today

The strongest live CRUD/workflow areas are the employee directory, employee creation and selected employee profile/history records; legal entities, work locations and organisational units at API level; payroll setup records and profiles; leave request creation and approval; attendance import and correction workflows; shift definitions and assignment; and attendance-derived overtime review and payroll allocation.

Employee records are not a simple unrestricted CRUD table. The system intentionally uses **archive** rather than hard deletion for workers, and organisation records use effective dates and closure so historical payroll and reporting remain reproducible. That is the correct control model for HR data, but the UI must expose the supported create/update/close actions consistently.

The live employee directory currently renders five PostgreSQL-backed UAT workers. It provides list/filter, Add employee, employee detail/edit, archive, shared import/export, account linking, assignments, and several child-record CRUD surfaces. The edit page is deliberately limited to fields currently backed by the live worker contract; many richer profile sections remain not fully live-editable.

Leave Application is genuinely backend-wired. The guided form loads leave types and submits a real request, while the inbox and employer approval page use live endpoints. The current administrator identity is not linked to a worker, so the application correctly displays that leave cannot be submitted until HR links the account. Attachments/evidence are not yet persisted.

Attendance is also genuinely live, but it is divided by purpose. Timesheets and attendance import use real attendance records; Employee Checkin records the caller’s own punches; Attendance Request is an audited correction workflow. There is not yet a general HR register that permits arbitrary edit and delete of raw attendance rows, which is appropriate for an audit-sensitive system but should be described clearly in the UI.

## Main gaps to fix before Milestone 2

The highest-value pre-Milestone-2 work is to make the existing live APIs usable from the UI. Organisation setup should expose create/update/close actions for Company, Branch, and Department while preserving effective dating and audit history. A real Jobs/Designation page should be enabled and backed by the existing jobs API. Employee Group and Employee Grade need an explicit decision: either implement them as PostgreSQL-backed master data with CRUD, or remove/hide the links until implemented; setup-fed or mock grade options should not be presented as a complete master-data solution.

The second priority is leave and attendance completeness. Compensatory Leave Request needs a real domain model, API, permissions, approval lifecycle, balances, and UI. Attendance Request evidence upload should use the shared document/import storage contract rather than a non-persistent mock block. Employee Checkin should either be tested with a properly linked local user or provide an administrator-facing employee check-in register if that is the intended workflow.

The third priority is Expense Claim and Travel Request. Both are currently release-gated and have no live API resources. They should not be enabled for operational use until their PostgreSQL schema, create/edit/list/approve/reject/reimburse or advance workflows, permissions, audit, attachments, and browser validation exist.

## Recommended implementation order

| Priority | Work package | Reason |
|---:|---|---|
| 1 | Enable and complete live Organisation CRUD for Company, Branch, and Department | These are foundational foreign-key/master data used by employees, leave, attendance, and payroll |
| 2 | Implement and expose Designation plus decide Employee Grade and Employee Group contracts | Employee placement and payroll/profile selectors need authoritative master data |
| 3 | Complete HR Settings/Leave Settings page and persisted policy administration | Leave and attendance workflows depend on configurable policy data |
| 4 | Complete Attendance Request evidence persistence and validate Employee Checkin with linked users | Removes current operational blockers around attendance exceptions and self-service clocking |
| 5 | Implement Compensatory Leave Request | It is adjacent to attendance/overtime and should be a controlled workflow, not a mock page |
| 6 | Implement Expense Claim and Travel Request | These require separate financial-control workflows and should not be enabled as superficial CRUD |
| 7 | Add Daily Work Summary Group only after the required business definition is confirmed | No current domain/API contract exists, so premature implementation risks building the wrong model |

## Conclusion

The answer is **“we have meaningful live coverage, but not complete CRUD for the whole list.”** Employee, payroll setup, leave application/approval, attendance import/corrections, clocking, and parts of organisation configuration are real. Company, Branch, Department, and Payroll Settings have backend support but incomplete UI CRUD. Designation has backend support but no enabled live UI. Employee Group, Employee Grade as a dedicated master, Daily Work Summary Group, Compensatory Leave Request, Expense Claim, and Travel Request do not currently have complete live PostgreSQL-backed CRUD.

The pages that currently show **“Not in this release”** should remain disabled rather than silently using demo data. The next implementation step should be to fix the live organisation/master-data and HR-settings gaps first, then complete the adjacent attendance/leave workflows, before opening new finance-related modules.

## References

[1]: https://erp.newworldcargo.com/ "Live New World Cargo HRM deployment"

[2]: https://erp.newworldcargo.com/hrm/configuration "Live Configuration landing page"

[3]: https://erp.newworldcargo.com/hrm/configuration/organisation "Live Organisation setup page"

[4]: https://erp.newworldcargo.com/hrm/configuration/payroll "Live Payroll setup page"

[5]: https://erp.newworldcargo.com/hrm/employees "Live Employee directory"

[6]: https://erp.newworldcargo.com/hrm/leave "Live Leave inbox"

[7]: https://erp.newworldcargo.com/hrm/attendance "Live Attendance corrections page"

[8]: https://erp.newworldcargo.com/hrm/attendance/clock "Live Employee Checkin page"

[9]: https://github.com/georgemunganga/erp/tree/55fef11 "Authoritative deployed ERP/HRM source repository"
