# Initial UI Validation Findings

**Source URL:** https://erp.newworldcargo.com/  
**Validation date:** 21 August 2026

## Public entry and sign-in

The public ERP landing page loads with New World Cargo branding and a Human resources module card. Entering Human resources routes to `https://erp.newworldcargo.com/sign-in`. The sign-in page displays the New World Cargo HRM logo, local email/password fields, a local-account message, and no Mightyfin/OIDC redirect. The page also exposes a speak-up route without signing in.

## Authenticated entry

The confirmed local administrator account successfully authenticated and reached the HRM workspace. The header and navigation use New World Cargo branding. Visible top-level surfaces include Home, My HR, People, Lifecycle, Recruitment, Time and leave, Payroll, Performance, Offboarding, Employee experience, Relations and safety, Approvals, Reports, Configuration, and Add employee.

## Setup-wizard behavior

After authentication the application redirected to `https://erp.newworldcargo.com/hrm/setup`, showing **Step 4 of 9**, **78% done**, with working-time controls and a visible **Skip to dashboard** link. This is unexpected because the backend setup wizard had previously been completed through all nine steps for UAT. The link was visible but did not navigate during the first click attempts, so this is a UI defect candidate: setup completion state and/or skip-to-dashboard interaction must be retested before marking the setup/onboarding UI requirements passed.

## Current UI validation status

No payroll or report workflow has been marked passed yet. The next UI test should establish whether direct route navigation can bypass the setup screen for the same authenticated account, then validate payroll runs and reports against the API baseline.

## Setup navigation retest

Direct navigation to `/hrm/payroll/runs` was redirected back to `/hrm/setup`, confirming a client/server setup gate blocks operational routes for this account. Clicking `I'll do this later` advanced the wizard from Step 4 (working time) to Step 7 (policies), changing the completion indicator from 78% to 89%, rather than leaving the setup flow. The visible `Skip to dashboard` link remained present; a second click caused a loading state (`Preparing the setup wizard...`) but did not yet expose the dashboard. This is now a confirmed **High UI blocker** for full UI validation because authenticated users cannot reach payroll/report screens through normal navigation until the wizard state is resolved.

A second retry of **Skip to dashboard** left the browser on Step 7 of 9 at 89% completion. The control remains visually present but does not produce a dashboard transition. This confirms the setup-wizard escape path is not functioning in the live UI for the authenticated administrator.

## Setup completion result

The visible **Finish setup** control successfully completed the remaining optional wizard steps. The UI then displayed **You are all set up — The HRM is ready to use** and showed `Go to home` and `Make changes`. The prior blocked-route behavior was therefore caused by the incomplete UI setup state, not by API authentication failure. This UAT interaction changed only the setup completion state for the existing UAT organisation and did not alter payroll records.

## Home and payroll-run UI

After setup completion, the home dashboard loaded successfully and displayed the configured UAT organisation/branch, the closed **Aug 2026 payroll** deadline, zero open leave requests, zero attendance exceptions, zero HR cases, and **5 active employees**.

Opening the payroll run from the dashboard reached `/hrm/payroll/runs/01a02338-c700-7905-a370-f9a00424367f`. The rendered screenshot shows the Aug 2026 Monthly ZMW run as **Closed**, with five employees, period/entity/pay-group/currency summary, `Calculate run`, `Send for review`, `Lock inputs`, `Edit this run`, five employee rows, and export controls for audit CSV, JV summary/detailed CSV/PDF, department CSV/PDF, and bank CSV. The visible first rows show UAT Alice gross ZMW 7,000.00, deductions ZMW 810.00, net ZMW 6,190.00 and UAT Brian gross ZMW 5,200.00, deductions ZMW 330.00, net ZMW 4,870.00.

The browser text extractor simultaneously reported `This page didn't load / Something went wrong`, although the screenshot and visible controls rendered. This is a UI hydration/error-state defect candidate and needs a refresh/retest plus console inspection before the payroll detail screen is marked passed.

## Payroll detail reload result

A refresh/review of the payroll detail route rendered the full-page error state **This page didn't load — Something went wrong on our end** with `Try again` and `Go home`. Browser console inspection returned no console output, so the issue is not yet attributable to a logged frontend exception. The prior screenshot had rendered the page once, but the route is not reliably loadable; payroll detail UI is currently **not passed** until this is fixed or explained.

## Payroll detail route diagnosis — 2026-08-21

The authenticated browser session successfully queried the live endpoints for the reconciled run `01a02338-c700-7905-a370-f9a00424367f`:

- `GET /api/hrm/payroll/runs/{id}` returned HTTP 200 with a run object.
- `GET /api/hrm/payroll/runs/{id}/audit` returned HTTP 200 with an array of 12 events.
- `GET /api/hrm/payroll/runs/{id}/lines` returned HTTP 200 with an envelope containing 5 items.

Despite successful API responses, the rendered route `/hrm/payroll/runs/{id}` reproducibly displays `This page didn't load`. The frontend RunDetail loader calls the run and audit endpoints together, then adapts them; lines and statutory-readiness errors are separately caught. The likely defect is in the route component/adaptation/rendering layer rather than API availability. No payroll data was changed.

Source review shows `adaptRun` maps the closed status and expects an audit array, while the live endpoints provide those shapes. Further isolation is required, including checking the built frontend bundle/version and route-level runtime errors.

A temporary `window.error` / `unhandledrejection` capture was installed before a reload, but the browser session became unavailable during the subsequent wait. No exception payload was captured. The next check should reopen the public route and use the persisted session, if available.

## Payroll detail UI retest after frontend fix — 2026-08-21

The missing `Download` icon import was patched in the VPS repository, the web image rebuilt, and the web/proxy containers restarted. The public payroll detail route now renders successfully.

The UI displays the closed August 2026 Monthly ZMW run for five employees, with the same API-baseline totals: gross ZMW 35,700.00, deductions ZMW 5,512.48, net ZMW 30,187.52, and employer cost ZMW 37,842.00. It displays all five employee rows, final payslips with Open/PDF actions, the reconciled payment reference, audit trail, statutory/report export controls, and the closed-run message. The previous `This page didn't load` error is resolved.

The page also exposes workflow buttons `Calculate run`, `Send for review`, and `Lock inputs` even though the run is closed; the page explains that changes require a correction run. This should be checked as a usability/control presentation item, but no action was submitted.

## Reports UI validation — 2026-08-21

The authenticated `/hrm/reports` page loads with date filters, legal entity/department/location filters, a released-period selector, KPI cards, workforce movement, payroll cost trend, department cost table, leave, attendance, recruitment, statutory liability, authority filing, and certified CSV export sections.

The page exposes CSV exports for headcount, payroll by department, employee payroll detail, journal voucher, statutory liability, leave/attendance, recruitment, and workforce movements. It also displays the reconciliation-control descriptions and a `Statutory filings` navigation link.

However, despite the closed/reconciled August payroll being visible in the payroll detail UI, the Reports page currently displays employer cost, net pay, department payroll, and all statutory liability totals as `K 0.00`, and says no released payroll periods are available. This is a confirmed cross-screen UI/reporting defect or filter mismatch. The reports page likely filters only `released` runs while the valid completed UAT run is `closed`; the backend statutory fix already includes closed runs, but the frontend report API or its response adapter may still exclude them. No export was submitted or downloaded yet.

The Reports UI initially received a malformed date through the date-input automation; the field was corrected in-page to `2026-08-31` with normal input/change events. The filter has not yet been applied at this point.

## Reports UI retest with full August period — 2026-08-21

After setting the reporting end date to 2026-08-31 and applying filters, the Reports UI correctly displayed the reconciled payroll data. Employer cost is ZMW 37,842.00, net pay ZMW 30,187.52, active headcount 5, and new hires 5. Department rows show UAT HR & Payroll gross ZMW 19,000.00 / net ZMW 15,360.01 / employer cost ZMW 20,140.00 and UAT Operations gross ZMW 16,700.00 / net ZMW 14,827.51 / employer cost ZMW 17,702.00.

The statutory liability card displays PAYE ZMW 3,477.48, NAPSA employee ZMW 1,785.00, NAPSA employer ZMW 1,785.00, NHIMA employee ZMW 250.00, NHIMA employer ZMW 357.00, total liability ZMW 7,654.48. Charts populate with the August payroll cost trend. This matches the API baseline.

The Authority filing section still says `No released payroll periods are available` even though the completed run is closed/reconciled and the report card includes closed runs. This is a separate UI/API selector defect or lifecycle-label mismatch and remains to be investigated. The initial zero totals were caused by the default To date of 2026-08-21 excluding the period ending 2026-08-31; after selecting the full period, totals are correct.

## Reports UI revalidation after selector/runtime fix — 2026-08-21

The Reports route initially hit the generic error boundary after the web-container restart. Source inspection identified the cause: the selector patch had removed the local `periods` hook but left a `periods.loading` reference in the empty-state condition, producing a runtime exception. The component was corrected to load the first configured pay group and its pay periods, match closed/released payroll-run labels, and use the existing period IDs. The local frontend build passed, the fix was copied to `/opt/erp-hrm/repo`, and the web/proxy services were rebuilt and restarted.

Browser verification now shows the Reports route renders normally. The Authority filing period selector displays **Aug 2026**, and the controls for ZRA PAYE return, ZRA schedule, NAPSA remittance, and NHIMA remittance are enabled. With the reporting end date set to `2026-08-31`, the page displays active headcount 5, employer cost ZMW 37,842.00, net pay ZMW 30,187.52, PAYE ZMW 3,477.48, NAPSA employee/employer ZMW 1,785.00 each, NHIMA employee ZMW 250.00, NHIMA employer ZMW 357.00, and total statutory liability ZMW 7,654.48. Department totals are UAT HR & Payroll gross ZMW 19,000.00 / net ZMW 15,360.01 / employer cost ZMW 20,140.00 and UAT Operations gross ZMW 16,700.00 / net ZMW 14,827.51 / employer cost ZMW 17,702.00.

The remaining Reports checks are to activate each certified CSV export and each statutory filing button, confirm the browser downloads, and record file evidence. Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-21_10-25-30_8703.webp`.

**Repository status after this fix:** the VPS source-of-truth repository has two uncommitted frontend files: the payroll detail Download import fix and the Reports selector/runtime fix. Commit and push remain pending.

## Reports export validation — 2026-08-21

The Reports page remained rendered with the full August filter and populated totals while all eight certified CSV controls were activated. The browser Downloads view confirmed completed files: `workforce-summary.csv`, `payroll-department.csv`, `payroll-detail.csv`, `payroll-journal.csv`, `statutory-liability.csv`, `leave-attendance.csv`, `recruitment-funnel.csv`, and `workforce-movements.csv`. The four authority filing controls also produced completed files: `paye-return-01a02337-b97a-7119-8891-791eefab5291.csv`, `zra-01a02337-b97a-7119-8891-791eefab5291.csv`, `napsa-01a02337-b97a-7119-8891-791eefab5291.csv`, and `nhima-01a02337-b97a-7119-8891-791eefab5291.csv`. All twelve downloads were shown as completed and originated from `https://erp.newworldcargo.com`.

## Employee management UI validation — 2026-08-21

The authenticated `/hrm/employees` screen loads successfully with New World Cargo branding, Import, Export, Add employee, All employees/Active only/Archived tabs, search, status/type/entity filters, column controls, and five active UAT workers. Each row exposes an Open action and shows employee number, job title, department, legal entity, employment type, and Active status.

Opening UAT Eunice at `/hrm/employees/01a02337-ba48-7772-9c6b-ac0cbeb69834` renders the profile summary, Personal, Contact and next of kin, Employment terms, Pay and statutory, and Background tabs, plus Edit details and Request leave actions. The Personal tab presents the legal name, employment summary, contact information, masked NRC/date of birth controls, and account-linking state. The page correctly masks restricted values and explains that revealing is recorded. Two data-quality/UI issues were observed: the employment-history entry renders `Invalid Date` instead of a formatted hire date, and the synthetic employee is not linked to a self-service identity, so leave, documents, letters, and payslips are reported unavailable from the profile. No sensitive value was revealed and no record was edited. The Pay and statutory tab displays the bank payment method and bank/branch while masking the account number and all three statutory registrations. It shows `Pay group: Not recorded`, although the payroll run is configured through a pay group, so the profile-to-pay-group linkage should be clarified or completed before production. The Employment terms tab displays the permanent contract and start date but has no probation end/confirmation date, reporting line, legal entity, branch, work location, cost centre, shift pattern, holiday calendar, leave policy, or attendance-device ID; the profile also says no leave balance could be calculated. These are UAT data-completeness observations rather than a rendering failure.

No sensitive value was revealed and no record was edited.

## Payroll UI validation — 2026-08-21

The default `/hrm/payroll/runs` view loads but correctly shows zero in-progress records. Selecting `Paid or closed` exposes one record: `Aug 2026 · Monthly ZMW`, five employees, `ZMW 35,700.00` gross, `ZMW 30,187.52` net, and status `Closed`. Opening the run renders its locked lifecycle state, period/entity/pay-group summary, five employee pay lines, five final payslip rows, control totals, release status, payment/reconciliation state, audit trail, and accounting exports (JV summary/detailed CSV/PDF, department CSV/PDF, and bank CSV). The UI states `Reconciled and closed · UAT-BANK-RECON-20260821-001`. The prior payroll-detail `Download` crash was not reproduced; the detail page loaded successfully with all export controls visible.

The `/hrm/payslips` route loads cleanly but shows `0 of 0 records` for the authenticated administrator because the account is not linked to an employee identity. This matches the profile and leave-page self-service messaging, but means employee-facing payslip preview/download could not be validated from the current account. The `/hrm/leave` route also loads cleanly and exposes Request leave, but reports that no employee record is linked, no leave types are configured, and no balances are available. This is a configuration/data blocker for self-service UAT rather than a route-rendering failure. The `/hrm/attendance` route loads with Import and Raise a correction controls, All corrections/Awaiting action tabs, search, status filter, and columns, but has no seeded correction records. As a result, attendance exception and overtime correction workflows could not be exercised from the UI.

The `/hrm/configuration/go-live` route loads with the release decision, Evidence, Formal sign-off, and Runbooks and training tabs. It correctly shows `Blocked` and `10 of 17 readiness gates passed`, listing monitoring/alerts, incident runbook, rollback, HR UAT, payroll UAT, HR administrator training, and payroll operator training as blockers. The Evidence tab exposes a control selector, evidence reference/notes fields, and Record passed evidence action, while the Runbooks and training tab explains migration/release rehearsal, incident response, HR/payroll UAT, and rollback. No evidence was fabricated or recorded during validation.

## Time operations UI validation — 2026-08-21

The first authenticated load of `/hrm/time/operations` reached the generic error boundary. Browser-authenticated API probes showed the history request returned HTTP 200 with empty imports/accruals/adjustments/encashments, while the encashments list endpoint returned a JSON array. The route assumed an object envelope and dereferenced `encashments.data.items.length`, causing the runtime failure when the array response was rendered.

The route was corrected to normalize either the deployed array response or the documented `{ items: [...] }` envelope. The local frontend build and `git diff --check` passed; the fix was copied to the VPS source-of-truth and the web/proxy containers were rebuilt and restarted. Retest of `/hrm/time/operations` now renders Shift rule, Shift assignment, Attendance import, Monthly leave accrual, Leave balance adjustment, Leave encashment, Approval escalation, and a safe empty operational-history state. No time-operation records were seeded. **UI route rendering: Passed after fix. Stakeholder overtime/attendance detail: Not proven by this operations screen.** Defect classification: Medium, fixed and retested.

## Stakeholder addendum — probation and sick-note/document surfaces — 2026-08-21

The `/hrm/talent/reviews` route renders a branded **Not in this release** page. It explicitly lists Talent goals/reviews/learning/succession as coming next, with no probation due list, days-remaining view, reviewer/objectives status, reminder history, or Confirm/Extend/End Employment workflow. This is a product-scope gap, not a missing UAT record.

The `/hrm/people/documents` route renders the employee-file UI for UAT Eunice with Upload document, Generate from template, All/Expiring/Awaiting signature/Restricted tabs, search, classification/category filters, and restricted-document messaging. The current employee file contains 0 documents; templates are available. No sick-note record, absence-linked document, receipt/verification/approval field, or supporting-document linkage was available to validate. The route itself rendered successfully and exposes classification-based access controls, but the sick-note register requirement remains unproven.

## Stakeholder addendum — overtime and attendance analytics — 2026-08-21

The `/hrm/time/timesheets` route renders the same **Not in this release** page as the Talent route. There is no employee-level timesheet table, overtime approval-state breakdown, current/prior month comparison, ZMW overtime cost, variance amount/percentage, direction indicator, abnormal-pattern view, or payroll-paid reconciliation control available in the live UI.

The `/hrm/analytics` route renders successfully and shows aggregate HR indicators: five active workers, payroll gross/employer cost for the closed August run, zero leave requests, zero cycles/assessments, zero open vacancies, zero average daily hours, zero overtime hours, and no attendance records in the trailing 30 days. It does not expose employee-level lateness/absence exceptions, scheduled-versus-actual times, reasons, approval status, manager actions, repeated patterns, or month-on-month attendance/overtime variance. **Aggregate analytics route: Passed. Stakeholder detail requirements: Not passed / product capability gap.**

## Report builder and stakeholder decisions — 2026-08-21

The `/hrm/reports/builder` route renders **Not in this release**. Custom report column selection, preview, saved reports, export/scheduling, approval/versioning, and distribution controls were not available to test.

Stakeholder configuration items STAKE-16 through STAKE-18 remain **Awaiting stakeholder confirmation**, not UI failures: the alert schedule, overtime budget/approved-limit comparison requirement, and repeated-sickness threshold must be confirmed by policy owners before implementation or acceptance criteria can be finalized.


## Selectable report formats — 2026-08-21

The Reports route now provides an explicit format selector for certified management reports: **PDF · print-ready**, **Excel · editable**, and **CSV · data**. The selector changes each catalogue action to the chosen format. The live browser produced `workforce-summary.xlsx` and `workforce-summary.pdf`; file inspection confirmed valid Microsoft Excel 2007+ and PDF files. The PDF was rendered and visually checked: it includes New World Cargo branding, a navy table header, yellow brand rule, report title, reporting window and generation timestamp, summary cards, aligned tabular data, source note, and page numbering. Existing CSV exports remain available and previously completed successfully. The Reports page remained stable and did not crash after the API/web rebuild.

The implementation was committed in the VPS source-of-truth and pushed to GitHub as commit `8def93a` (`Add selectable PDF Excel and CSV management report exports`).

The generated management-report PDF is a formatted summary/report output. Employee payslips and statutory filing files remain separate document/output types and should continue to receive their own template and authority-format validation.


## Milestone 1 — real overtime validation update (2026-08-22)

The production Time Operations page was rebuilt and loaded successfully at `https://erp.newworldcargo.com/hrm/time/operations`. It now presents a real overtime review queue, decision-reason input, and approve/reject controls. The page is backed by `GET /api/hrm/time/overtime` and `POST /api/hrm/time/overtime/{id}/decide`; it does not invoke mock loaders in production mode.

Browser validation before inserting the UAT fixture showed the honest live empty state, `No derived overtime records found.`, with no demo rows and no runtime error. The functional API UAT then imported two persisted September attendance rows, derived pending overtime, approved/rejected the rows, and validated payroll allocation and paid linkage. Detailed evidence is recorded in `m1_overtime_uat_evidence.md`; browser availability evidence is in `m1_browser_validation.md`.

This closes the Milestone 1 time/overtime implementation gap but does not imply overall production readiness. The page remains an operational HR/payroll workflow and requires real role configuration, policy sign-off, and broader readiness gates before production payroll approval.
