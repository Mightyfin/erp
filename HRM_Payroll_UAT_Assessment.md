# New World Cargo HRMS Payroll Readiness and UAT Assessment

**System:** New World Cargo HRMS  
**Environment:** Standalone deployment at [erp.newworldcargo.com](https://erp.newworldcargo.com)  
**Assessment date:** 21 August 2026  
**Prepared by:** **Manus AI**  
**Test specification:** Requester-supplied `HRM_Payroll_Readiness_and_UAT_Checklist-3.md`

## Executive decision

> **Decision: NOT APPROVED for production payroll.**

The New World Cargo HRMS is deployed with local PostgreSQL authentication, New World Cargo branding, an operational payroll configuration, a successfully completed synthetic payroll workflow, and populated statutory aggregate reports for the reconciled UAT run. After API-based blocker remediation, the live readiness service reports **10 of 17 gates passed and 7 blocked**. The remaining readiness blockers are evidence, operational-process, training, and business-ownership gates rather than missing payroll master data; the revised stakeholder monthly-report requirements also remain only partially covered.

The synthetic August 2026 payroll run completed through calculation, approval, payslip release, payment-file generation, payment approval, payment release, and reconciliation. Five synthetic workers produced **ZMW 35,700.00 gross**, **ZMW 5,512.48 deductions**, **ZMW 30,187.52 net pay**, and **ZMW 37,842.00 employer cost**, with **zero payroll exceptions**. This validates the configured fixture path and workflow controls; it does **not** establish production statutory correctness or approval to pay real employees.

The supplied payroll checklist contains **546 items**, including **301 P0**, **41 P1**, and **204 without an explicit priority**. The updated UI checklist adds **778 stable UI source items** plus **18 stakeholder monthly-report items**. The completed work is a targeted authenticated browser validation of the highest-value live routes and a separate API baseline/remediation pass; it does not constitute completion of every checklist scenario, expected-result comparison, parallel run, training activity, or accountable-owner sign-off.

## Scope and method

The API baseline and remediation work was executed through the server’s private authenticated API binding. A subsequent authenticated browser pass covered the public sign-in, home, employee master/profile, payroll run list/detail, Reports and twelve downloads, payslips, leave, attendance, Time operations, documents, analytics, Go-live readiness, Talent/reviews, timesheets, and report-builder routes. Five clearly synthetic workers were created under **“New World Cargo UAT Test Employer (NOT FOR PRODUCTION)”**. Synthetic bank records, statutory identifiers, payroll profiles, and salary data were used; no real personal or banking data was introduced.

The pre-existing active development worker `DEV-001` was archived through the supported worker lifecycle API because it was outside the synthetic UAT population and was the sole remaining active record without a complete statutory identity pack. The active population now consists of the five synthetic workers, all of whom have the required placeholder statutory identifiers.

Three temporary UAT payroll accounts were created through the local user-management API with the `payroll` role: a preparer, an approver, and a releaser. Their credentials are not included in this report. The account separation was used to test maker-checker and payment-release controls without using the bootstrap administrator for every action.

During the earlier calculation pass, two defects in the synthetic setup/calculation path were isolated and corrected. The five worker profiles were dated **1 January 2026**, before the August period. The payroll engine was corrected so an explicit worker profile amount takes precedence over a component default, including an intentional zero override; recalculation now resets employee and exception counters before rebuilding run lines. A further diagnostics correction now reports current non-expiring evidence as current and passed instead of incorrectly describing it as expired. The relevant source commits are `375160d`, `aecd406`, `15258b1`, `d796e07`, and `25ae660`; `25ae660` is deployed in the live API image and pushed to the configured GitHub main branch.

## Executed test results

| Test area | Test performed | Result | Evidence |
|---|---|---:|---|
| Public availability and branding | Earlier deployment smoke test confirmed the public New World Cargo HRMS page and branded module selector | **PASS** | `erp.newworldcargo.com` smoke evidence |
| Local authentication | Logged in through the standalone local email/password API | **PASS** | Authenticated HTTP 200 response |
| Session lifecycle | Verified authenticated session, logout, and revoked-session rejection | **PASS** | Local session lifecycle evidence |
| User administration | Created and listed local accounts through the `hrm_admin` API | **PASS** | Three payroll-role UAT accounts created |
| API security controls | Unauthenticated payroll access denied; payroll role authorized for payroll reads; payroll role denied hr_admin-only evidence recording | **PASS** | `security_acceptance_api_test_20260821` |
| Database migration gate | Live database had no pending migrations | **PASS** | `automated:ef-migrations` |
| Backup and restore | Restored a custom-format PostgreSQL dump into an isolated temporary database, verified key counts, then removed the clone and dump | **PASS** | `backup_restore_rehearsal_20260821` |
| Migration rehearsal | Ran the deployed API migration command against an isolated restored clone and received “Migrations applied. Exiting.” | **PASS** | `migration_rehearsal_20260821` |
| Performance acceptance | Executed 100 authenticated `GET /payroll/runs` requests at concurrency 10 with zero failures and p95 below 1,000 ms | **PASS for bounded API acceptance** | `performance_acceptance_api_test_20260821` |
| Employer statutory references | Default legal entity has TPIN, NAPSA, and NHIMA references in the UAT configuration | **PASS for fixture configuration** | Readiness endpoint |
| Payroll reference data | Pay group, PAYE slabs, NAPSA rules, NHIMA rules, and salary structure are active | **PASS for fixture configuration** | Readiness endpoint |
| Worker statutory identity | All five active workers have the required synthetic statutory identity pack | **PASS for UAT population** | Readiness endpoint |
| Payroll workflow | Recalculated, approved, released, paid-file generated, payment approved, payment released, and reconciled the synthetic run | **PASS for synthetic workflow** | Payroll audit trail and reconciliation reference |
| Statutory aggregate reports | Rechecked statutory summary and employer-liability report after the closed-run fix; five workers and PAYE/NAPSA/NHIMA totals are populated and consistent with payroll lines | **PASS for UAT aggregate path** | `verify_report_run_consistency_after_fix.json` |
| Stakeholder monthly reports | Catalogue exposes `leave-attendance` but no dedicated sick-note register, probation monitoring, overtime variance, or attendance-exception report; UAT has no source records for those scenarios | **NOT COMPLETED** | `reassess_checklist_api_after_statfix.json`; stakeholder requirements |
| Monitoring and alert validation | No actual alerting or monitoring acceptance evidence recorded | **NOT COMPLETED** | Human/technical operations evidence required |
| Incident runbook walkthrough | No walkthrough evidence recorded | **NOT COMPLETED** | Human operational evidence required |
| Rollback rehearsal | No rollback rehearsal evidence recorded | **NOT COMPLETED** | Technical-owner evidence required |
| Full checklist scenario pack | All calculation, control, output, parallel-run, and expected-result scenarios were not executed | **NOT COMPLETED** | Independent UAT pack and sign-off required |

## Synthetic payroll result

| Employee | Synthetic worker | Gross pay (ZMW) | Deductions (ZMW) | Net pay (ZMW) | Exception |
|---|---|---:|---:|---:|---|
| EMP-0001 | UAT Alice | 7,000.00 | 810.00 | 6,190.00 | None |
| EMP-0002 | UAT Brian | 5,200.00 | 330.00 | 4,870.00 | None |
| EMP-0003 | UAT Chipo | 8,500.00 | 1,342.49 | 7,157.51 | None |
| EMP-0004 | UAT Daniel | 12,000.00 | 2,829.99 | 9,170.01 | None |
| EMP-0005 | UAT Eunice | 3,000.00 | 200.00 | 2,800.00 | None |
| **Total** | **5 workers** | **35,700.00** | **5,512.48** | **30,187.52** | **0 exceptions** |

**Run:** `01a02338-c700-7905-a370-f9a00424367f`  
**Period:** August 2026  
**Final status:** `closed`  
**Payment status:** `reconciled`  
**Payment file reference:** `PAY-20260821-01A02338`  
**Reconciliation reference:** `UAT-BANK-RECON-20260821-001`  
**Calculation version:** `engine-v1`

The audit trail proves separate subjects were used for preparation, run approval, release, payment approval, and payment release. The reconciliation note explicitly records that no external bank transfer occurred; this was a synthetic UAT operation only.

## Revised stakeholder monthly-report assessment

The stakeholder comments add four mandatory monthly-report capabilities. The live report catalogue exposes `leave-attendance`, but it does not expose dedicated report types or evidence for the following requirements:

| Stakeholder requirement | Live API finding | Assessment |
|---|---|---|
| Consolidated monthly/YTD sick-note and sick-leave register | A generic leave/attendance surface exists, but the reassessment returned zero UAT rows and did not demonstrate sick-note receipt, verification, approval, reviewer, trend, threshold, or supporting-document controls | **Not proven** |
| Three-month probation monitoring and notifications | No dedicated probation report or reminder-history output was identified in the live report catalogue; confirmation, extension, end-employment, overdue escalation, and notification history were not demonstrated | **Not proven** |
| Monthly overtime current-versus-prior variance | The management KPI payload includes an `overtime` key, but no dedicated overtime report with hours, ZMW cost, percentage variance, approval, budget comparison, or abnormal-pattern analysis was demonstrated | **Not proven** |
| Attendance exception reporting | A generic leave/attendance surface exists, but no UAT attendance exceptions or month-on-month trend were present in the tested data | **Not proven** |

The stakeholder comments also state that the exact probation alert schedule, overtime-versus-budget requirement, and repeated-sickness threshold require confirmation. These requirements therefore remain outside the passed readiness count until the report contracts, source data, expected outputs, and stakeholder acceptance are completed.

## Targeted UI validation result

| UI area | Result | Live evidence and conclusion |
|---|---:|---|
| Public entry, sign-in, branding, and local session | **PASS** | New World Cargo branding and local email/password authentication rendered; no Mightyfin/OIDC redirect observed. |
| Employee master and profile | **PASS with data-quality findings** | `/hrm/employees` and UAT Eunice profile rendered with personal, pay/statutory, employment, masking, bank, and account-linking controls. Findings: `Invalid Date` hire-date presentation, no employee-linked self-service identity, missing profile pay-group linkage, missing probation/organisation/leave fields. |
| Payroll run list and detail | **PASS after fix** | Closed/reconciled Aug 2026 run rendered with five lines, totals, payslips, audit trail, reconciliation reference, and accounting exports. The missing `Download` import crash was fixed and retested. |
| Reports and statutory filings | **PASS after fixes** | Full August filter populated the management/statutory totals. Authority selector displayed Aug 2026. All eight certified CSV exports and four authority filing downloads completed in browser Downloads. The certified-report catalogue now provides an explicit **PDF · print-ready / Excel · editable / CSV · data** selector; live XLSX and PDF files were downloaded and validated, and the PDF was rendered for visual layout review. |
| Payslips and leave self-service | **PARTIAL** | Routes rendered, but the admin account has no linked employee identity; payslips showed 0 records and leave showed no linked employee, leave types, or balances. Employee-facing preview/download was not proven. |
| Attendance | **PARTIAL** | Route rendered with Import/Raise correction controls and tabs, but no seeded correction records were available for exception/overtime workflow validation. |
| Time operations | **PASS after fix** | Route now renders shift, attendance import, accrual, adjustment, encashment, escalation, and safe empty-history controls. A response-shape crash was fixed and retested. |
| Documents | **PARTIAL** | Employee-file route rendered with upload/template, classification, restricted-only, expiry, and signature tabs; no seeded documents or sick-note register fields were present. |
| Analytics | **PASS for aggregate rendering; insufficient for stakeholder detail** | Active headcount, payroll cost, leave, recruitment, performance, and aggregate attendance/overtime cards rendered; employee-level exception and month-on-month stakeholder metrics were absent. |
| Go-live readiness | **PASS for gate rendering; decision remains blocked** | Evidence, sign-off, and runbook/training tabs rendered. The UI correctly displayed 10/17 passed and seven blocked gates; no unsupported evidence was recorded. |
| Talent/reviews, timesheets, and report builder | **FAILED — not in release** | These routes explicitly render “Not in this release”; probation monitoring, overtime comparison, custom reports, scheduling, and related workflows were not available. |

The browser findings are recorded in `ui_validation_initial_findings.md`. The updated execution register in `Updated_HRM_UI_Full_Checklist.md` records all 18 stakeholder rows: STAKE-01–03 and STAKE-05–07, STAKE-09–15 are failed capability gaps; STAKE-04 requires an unauthorised-role retest; STAKE-08 and STAKE-16–18 remain not tested because the required surface or policy decision is unavailable. The 778 source UI rows not directly exercised remain `Not tested` rather than being represented as passed.

Four frontend/reporting defects or capability gaps were addressed in the deployed source: payroll-detail `Download` import, Reports authority-period/runtime handling, Time operations encashment/leave-type response compatibility, and report export-format support. The export enhancement adds explicit PDF/Excel/CSV selection, dependency-free XLSX generation, and a print-ready branded PDF layout. The first three fixes are included in GitHub commit `35fec8a`; the export enhancement is included in commit `8def93a` (`Add selectable PDF Excel and CSV management report exports`).

## Live readiness-gate result

The final authenticated `GET /api/hrm/go-live/` response returned **`decision=blocked`**, with **10 passed gates out of 17**.

| Readiness gate | Status | Current evidence/detail |
|---|---:|---|
| Database migrations | **PASSED** | Database reachable; no pending migrations |
| Employer statutory references | **PASSED** | Default legal entity has TPIN, NAPSA, and NHIMA references |
| Payroll reference data | **PASSED** | Pay groups, PAYE slabs, NAPSA and NHIMA rules are active |
| Worker statutory identity | **PASSED** | All five active workers have the required synthetic identity pack |
| Reconciled payroll cycle | **PASSED** | Synthetic payroll reached closed/reconciled state |
| Delivery and integration backlog | **PASSED** | No failed notification or external-integration operations are waiting |
| Backup and restore rehearsal | **PASSED** | Current evidence recorded from isolated PostgreSQL restore rehearsal |
| Security acceptance test | **PASSED** | Current evidence recorded from authenticated API security checks |
| Production migration rehearsal | **PASSED** | Current evidence recorded from migration against restored clone |
| Performance acceptance test | **PASSED** | Current bounded API acceptance evidence; not a full capacity test |
| Monitoring and alert validation | **BLOCKED** | No evidence has been recorded |
| Incident runbook walkthrough | **BLOCKED** | No evidence has been recorded |
| Rollback rehearsal | **BLOCKED** | No evidence has been recorded |
| HR user acceptance testing | **BLOCKED** | No evidence has been recorded |
| Payroll user acceptance testing | **BLOCKED** | The synthetic workflow passed, but the full checklist and business acceptance evidence are absent |
| HR administrator training | **BLOCKED** | No evidence has been recorded |
| Payroll operator training | **BLOCKED** | No evidence has been recorded |

All five required sign-offs remain pending: **HR owner, Payroll owner, Finance owner, Technical owner, and Tenant executive owner**. The sign-off API correctly refuses an approval while any readiness gate remains blocked.

## Blocker classification and next actions

| Blocker | Classification | Required action | Can be solved by API/data entry alone? |
|---|---|---|---:|
| Monitoring and alert validation | Operational evidence | Configure or identify the monitoring/alerting path, execute an alert test, and have the technical owner accept the evidence | **No** |
| Incident runbook walkthrough | Operational evidence | Conduct and document a tabletop walkthrough with named responders and escalation contacts | **No** |
| Rollback rehearsal | Technical/operational evidence | Test rollback to an approved prior image or release in an isolated environment and document recovery | **No** |
| HR UAT | Business acceptance | Execute the HR scenario pack, record expected versus actual results, and obtain HR acceptance | **No** |
| Payroll UAT | Business acceptance | Execute the full payroll checklist, including boundary cases, outputs, accounting, statutory returns, and independent expected results | **No** |
| HR administrator training | Human process | Deliver training and record attendance and competency confirmation | **No** |
| Payroll operator training | Human process | Deliver payroll training and record attendance, competency, and escalation procedures | **No** |
| Owner sign-offs | Governance | Obtain five accountable-owner approvals after all gates pass | **No** |

## Final conclusion

The system **passes deployment, public availability, New World Cargo branding, local PostgreSQL authentication, local session management, basic user administration, payroll reference-data setup, the synthetic five-worker payroll calculation, the synthetic end-to-end payroll/payment/reconciliation workflow, the populated UAT statutory summary and employer-liability aggregate path, the targeted core UI routes after the three frontend fixes, the twelve browser downloads, the isolated backup/restore rehearsal, the isolated migration rehearsal, the API security acceptance, and the bounded API performance acceptance**.

It **does not pass the attached payroll readiness and UAT checklist or the full stakeholder monthly-report requirements** and remains **not approved for production payroll**. The UI pass was targeted, not a claim that all 778 source items passed. The remaining blockers include seven readiness gates, self-service identity/configuration gaps, employee-profile data completeness, absent sick-note/probation/overtime-variance/attendance-exception capabilities, one pending document-role retest, three pending stakeholder policy decisions, business acceptance, training, and accountable sign-off. No real employee data or external bank transfer was used. The blockers cannot be truthfully cleared by adding placeholder data or recording unsupported evidence; they require genuine operational execution, product completion where applicable, business acceptance, training, and accountable sign-off.

## Production mock-integration remediation

A source audit identified 34 real-API-only routes, 36 mixed routes that retain demo imports or fallback state, and 31 mock-only or API-unmapped routes. The shared frontend `useMock` hook was hardened so that `VITE_USE_REAL_API=true` prevents mock loader execution and returns a settled empty mock state; mock behavior remains available only when live API mode is disabled. This prevents mixed routes from silently executing mock loaders in production. The change was deployed and pushed in commit `4825db0` (`Prevent mock loaders from running in production mode`).

Post-deployment browser validation confirmed that the Reports page loaded normally, the Employee directory displayed all five seeded UAT workers from the live API, the Paid or closed Payroll view displayed the reconciled Aug 2026 run with its live totals, and the Leave page showed the real employee-linkage/configuration state rather than a mock preview. This remediation addresses silent mock execution, not the absence of APIs for planned features.

The remaining production product gaps are explicit: timesheet-driven pay and end-to-end overtime processing are not implemented; Report Builder, Timesheets, several talent/lifecycle/experience/relations submodules, and some configuration surfaces remain release-gated or mock-only. These must either be implemented with PostgreSQL-backed contracts and live UI workflows or remain disabled and excluded from the production scope. The system therefore remains **NOT APPROVED** until the seven readiness gates, full payroll UAT, stakeholder report capabilities, training, and owner sign-offs are completed.

## References

[1]: https://erp.newworldcargo.com/api/hrm/go-live/ "Live New World Cargo HRMS go-live readiness endpoint"

[2]: https://erp.newworldcargo.com/ "Live New World Cargo HRMS deployment"

[3]: https://github.com/georgemunganga/erp.git "Supplied ERP/HRM source repository"

[4]: HRM_Payroll_Readiness_and_UAT_Checklist-3.md "Requester-supplied HRM payroll readiness and UAT checklist"

[5]: Stakeholder_Comments_HRM_Monthly_Reports_2026-08-21.md "Stakeholder comments on HRM monthly reports"

**Supporting evidence files:** `uat_final_after_all_fixes.json`, `uat_payroll_cycle_result.json`, `verify_report_run_consistency_after_fix.json`, `reassess_checklist_api_after_statfix.json`, `backup_restore_rehearsal_result.txt`, `migration_rehearsal_result.txt`, `security_acceptance_api_result.json`, `performance_acceptance_api_result.json`, `technical_evidence_result.json`, `ui_validation_initial_findings.md`, and `Updated_HRM_UI_Full_Checklist.md`.


## Milestone 1 — real time/overtime completion update (2026-08-22)

The first production-critical feature milestone is complete for attendance-derived overtime. The live PostgreSQL schema, ASP.NET API, payroll engine, audit outbox, and React Time Operations UI are deployed and functioning. Overtime now follows an explicit `none → pending → approved/rejected → paid` lifecycle on the existing attendance record. Payroll reads only approved and unallocated overtime, presents it as a dedicated explainable earning, and links the source attendance to the run and line only at release.

Correction-safe live UAT used a new September 2026 period and two new attendance rows for `EMP-0005`; the closed August run was not changed. The 3-hour row was approved and produced one ZMW 64.90 overtime earning. The 2-hour row was rejected and was excluded. Recalculation returned the same gross ZMW 35,764.90, net ZMW 30,252.42450, and employer cost ZMW 37,906.90 with no duplicate overtime component. Separate payroll-role accounts approved and released the new run; the approved row became `paid` and the rejected row remained unallocated. Unauthenticated access, wrong-role release, and paid-record mutation guards passed. Two decision outbox events and privileged route audit rows were persisted.

The backend test suite passed **312 tests**, the frontend production build passed, the EF migration applied to PostgreSQL, and the deployed browser route loaded with the honest empty state before the UAT fixture was inserted. Detailed evidence is in `m1_overtime_uat_evidence.md` and `m1_browser_validation.md`.

This milestone result does **not** change the overall assessment. The HRM remains **NOT APPROVED for production payroll** because the readiness score, operational evidence, independent statutory verification, parallel payroll runs, Finance acceptance, training, backup/recovery sign-off, and stakeholder approvals remain incomplete. The September run was a synthetic UAT run and was released only to validate the feature; no external payment file was generated or reconciled.
