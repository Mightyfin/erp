# New World Cargo HRM Mock-Integration Audit

**Date:** 21 August 2026  
**Scope:** Frontend route source, shared API adapters, backend route/service source, and targeted live UI verification of the deployed HRM at `https://erp.newworldcargo.com`.

## Executive conclusion

The audit confirms that the application is **not uniformly mock-backed**, but it is also **not fully integrated with live APIs across the whole navigation surface**. The production build uses `VITE_USE_REAL_API=true`, and the principal first-release surfaces—employees, payroll, reports, readiness, configuration payroll/integrations, and several operational workflows—call live API adapters. However, the repository still contains a substantial set of mock-only route implementations and a second group of mixed routes that retain mock imports or instantiate mock hooks alongside live API calls.

The most important distinction is that the confirmed mock-only features are generally **release-gated or visibly marked “Not in this release”**, rather than silently presenting demo records on the tested production paths. That is safer operationally, but it does not mean those features are integrated. They remain planned UI shells or demo implementations and should not be represented as production capabilities.

## Route-level inventory

The saved route map contains **101 frontend routes**. Their source-level integration classification is:

| Classification | Count | Interpretation |
|---|---:|---|
| Real API only | 34 | Route source uses live API adapters and no mock service/data import was detected. |
| Mixed real and mock | 36 | Route has a live API path but also retains mock imports, adapters, fallback state, or demo-only behavior. Visible data must be checked against the `USE_REAL` branch. |
| Mock only | 31 | No live API adapter was detected in the route source; the route is demo-backed, planned, or unavailable in the current release. |

These figures are source-level counts, not claims that every mixed route displays mock data. A mock import by itself is not proof of a production data leak.

## Confirmed live API coverage

The strongest live integrations are the areas already used during UAT: employee directory and worker profiles, employee creation and updates, payroll runs and payroll-line workflow, payslips, payroll configuration, statutory/integration handoffs, Reports, go-live readiness, data-quality checks, organisation/chart functions, recruitment operations, onboarding, offboarding, performance-cycle management, relations cases, protected disclosures, self-service preferences/documents, and Time operations after the compatibility fix.

The Reports route was specifically revalidated after its earlier runtime defect. It loaded successfully with the closed August 2026 period, displayed populated totals, and supported CSV, Excel, and print-ready PDF exports. This route is therefore a confirmed live integration, not a mock-only report surface.

## Confirmed mock-only or unavailable feature families

The following route families are mock-only or have no corresponding live API path in the current source map:

| Feature family | Representative routes | Finding | Production implication |
|---|---|---|---|
| Report builder | `/hrm/reports/builder` | Live UI shows **Not in this release**. Source retains a mock saved-report builder. | No live custom report creation, save, schedule, preview, or approval workflow. |
| Timesheets and timesheet pay | `/hrm/time/timesheets`, `/hrm/time/schedules`, `/hrm/time/toil`, `/hrm/time/utilisation` | Timesheets are release-gated. Backend comments explicitly describe timesheet pay as a planning flag, not an implemented payroll mode. | Overtime or timesheet hours cannot currently be assumed to flow automatically into payroll. |
| Talent submodules | `/hrm/talent/goals`, `/hrm/talent/feedback`, `/hrm/talent/learning`, `/hrm/talent/succession`, review routes | Mock-backed source; the tested reviews surface is not a proven live workflow. | No production-grade performance/talent data integration across these modules. |
| Lifecycle submodules | alumni, assets, journeys, mobility, movements, offboarding list | Several routes are mock-only; some detail workflows have partial live API support. | Do not treat lifecycle navigation as full live employee-lifecycle processing. |
| Employee experience | `/hrm/experience/announcements`, `/hrm/experience/knowledge` | Mock-only source; no matching live API contract was found. | Announcements and knowledge content are demo/planned capabilities. |
| Relations and safety submodules | discipline, ethics, labour, safety | Mock-only source; relations cases and operations are separate live surfaces. | These submodules are not equivalent to the live relations case workflow. |
| Configuration business/process/technical | `/hrm/configuration/business`, `/hrm/configuration/process`, `/hrm/configuration/technical` | Mock-only or mixed source depending on route; only selected configuration areas are live. | Configuration pages must be validated individually; navigation alone does not prove persistence. |

## Mixed-route findings

Mixed routes include mock code because the application was designed to preserve a demonstration mode while live APIs were being added. For example, the employee directory calls the live worker endpoint when `VITE_USE_REAL_API=true` and chooses live rows for rendering, while still instantiating a mock state for demo mode. This is not currently a displayed-data defect, but it creates unnecessary production work and makes future regressions easier if a component accidentally reads the mock state.

Other mixed routes include employee profile/edit, payroll run detail/edit, payslips, payroll exceptions, documents, recruitment, organisation, positions, privacy, attendance clock, and leave creation/detail. These require continued review because a route can be partly live while a secondary tab, empty state, label, or action remains mock-backed.

The leave index route is a good example of an explicit safe pattern: live mode calls `hrmApi.myLeave()` and renders the live response, while mock mode renders a clearly labeled mock preview. The source text states that balances and requests come from the real API in live mode. This should be the standard pattern for any feature that must retain demo support.

## Live UI confirmation

Two high-risk mock-only routes were opened in the deployed browser session:

| Route | Live result | Classification |
|---|---|---|
| `/hrm/reports/builder` | Shared **Not in this release** screen; no mock saved reports were shown. | Unavailable planned feature, not a live integration. |
| `/hrm/time/timesheets` | Shared **Not in this release** screen; no demo timesheet rows were shown. | Unavailable planned feature, not a live overtime integration. |

This confirms the release gate is preventing those demo screens from being mistaken for production functionality. It does not remove the underlying integration gap.

## Backend/API evidence

The backend search found no general timesheet-pay implementation. The payroll domain contains a `PayBasis` value of `salary` or `timesheet`, but comments and validation identify timesheet pay as a planning control rather than implemented timesheet-driven payroll. The backend does provide live services for several other areas, including worker movements, onboarding/offboarding, payroll, imports, performance, recruitment, relations operations, and time corrections.

Consequently, the correct current statement is: **attendance/time-operation controls exist, but employee-level overtime capture, approval, variance reporting, and payroll reconciliation are not proven as a complete live workflow.** No overtime payment should be expected solely because a route or pay-basis field exists.

## Defect classification

| Severity | Finding | Status |
|---|---|---|
| High | Timesheet-driven pay is not implemented; timesheet UI is release-gated. | Open product gap. |
| High | Report builder is mock-backed/planned and unavailable. | Open product gap. |
| High | Stakeholder sick-note, probation, overtime-variance, and detailed attendance-exception reports are not available as proven live reports. | Open product gap. |
| Medium | 31 routes have no live API adapter detected. | Open integration backlog; many are intentionally out of release. |
| Medium | 36 mixed routes retain mock imports or instantiate mock state alongside live paths. | Cleanup and regression-risk backlog; not all are visible-data defects. |
| Low | Some mock state is unnecessarily created even when real mode is enabled. | Safe performance/maintainability cleanup. |

No new code fix was applied in this audit because the confirmed mock-only routes are intentionally release-gated and the remaining work requires product/API scope rather than a safe one-line correction. Removing mock imports without implementing equivalent server contracts would create empty or broken screens rather than improve production behavior.

## Recommended remediation order

First, formalise the release boundary in navigation and documentation so mock-only routes are clearly labelled as planned and cannot be interpreted as live functionality. Second, implement a real time-and-overtime domain contract: employee-level time entries, approval state, rate/rule calculation, payroll-period linkage, audit history, and employee-level variance reporting. Third, implement the stakeholder report contracts for sick-note register, probation monitoring, overtime variance, and attendance exceptions. Fourth, replace mixed-route mock fallbacks with a shared adapter policy that never invokes mock services in production mode and makes live API failures visible instead of silently falling back to demo data. Finally, add automated route tests that fail if a production route renders mock rows when `VITE_USE_REAL_API=true`.

## References

[1]: `real_vs_mock_route_map.txt` — source-level route integration map.  
[2]: `mock_route_inventory.txt` — mock-import and mock-only route inventory.  
[3]: `mock_feature_trace.txt` — representative mock feature and backend capability trace.  
[4]: `modules/hrm/frontend/module-connect/src/platform/use-api.ts` — shared live API adapter.  
[5]: `modules/hrm/frontend/module-connect/src/platform/use-mock.ts` — mock-mode hook behavior.  
[6]: `modules/hrm/frontend/module-connect/src/routes/hrm.employees.index.tsx` — mixed live/mock employee route.  
[7]: `modules/hrm/frontend/module-connect/src/routes/hrm.leave.index.tsx` — explicit live/mock leave-mode split.  
[8]: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Domain/Entities/Payroll.cs` — timesheet pay-basis planning flag.  
[9]: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Application/Payroll/PayrollServices.cs` — payroll pay-basis comments and validation.  
[10]: `modules/hrm/frontend/module-connect/src/routes/hrm.reports.builder.tsx` — mock report-builder source.  
[11]: `modules/hrm/frontend/module-connect/src/routes/hrm.time.timesheets.tsx` — mock timesheet source.  
[12]: `mock_integration_audit_notes.md` — persisted audit evidence and findings log.

## Remediation completed after the audit

The shared frontend `useMock` hook was hardened in commit `4825db0` (`Prevent mock loaders from running in production mode`). When `VITE_USE_REAL_API=true`, mock loader functions are now skipped and the mock state is settled without loading or data; demo behavior remains available only when live API mode is disabled. This prevents mixed routes from executing mock loaders in the production deployment.

The frontend was rebuilt and deployed to the VPS. The live Reports route loaded successfully after restart, confirming that the guard does not produce a white screen or generic error. This remediation eliminates **silent mock execution in live mode**, but it does not implement the missing APIs for timesheets, overtime-driven payroll, report builder, talent, lifecycle, experience, or relations submodules. Those remain release-gated/open integration work.


## Milestone 1 remediation update — 2026-08-22

The time/overtime production gap has been remediated incrementally. `AttendanceRecord` now persists the overtime review and payroll-allocation lifecycle in PostgreSQL. The ASP.NET API exposes real overtime list and decision endpoints, and the React Time Operations page consumes those endpoints through `realApi`; no mock loader is used when `VITE_USE_REAL_API=true`.

A live correction-safe UAT imported two September attendance rows, derived 3 and 2 overtime hours from the persisted shift rule, approved one and rejected one, calculated a new September payroll run, verified one explainable ZMW 64.90 overtime earning on recalculation, released it through separate payroll-role accounts, and verified the approved source row became `paid` while the rejected row remained unallocated. Unauthenticated and wrong-role access checks also passed. Detailed evidence is in `m1_overtime_uat_evidence.md`.

The reports builder and other intentionally planned/mock-only feature families remain release-gated and were not silently converted into demo data. This milestone does not change the overall production readiness decision; the HRM remains not approved for production payroll until all remaining operational gates, training, sign-offs, and external evidence are complete.
