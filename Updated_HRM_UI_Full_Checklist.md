# New World Cargo HRM — Updated Full UI Validation Checklist

**Purpose:** Execute the complete requester-supplied payroll readiness and UAT checklist through the New World Cargo user interface, using the authenticated API results as the baseline and recording UI-specific evidence.

**Source checklist:** `HRM_Payroll_Readiness_and_UAT_Checklist(1).md`  
**Stakeholder source:** `Stakeholder_Comments_HRM_Monthly_Reports_2026-08-21.md`  
**Baseline:** August 2026 synthetic payroll is calculated, closed, and reconciled through the API; the live readiness endpoint currently reports 10/17 gates passed.

## UI execution status

For every item, record one of **Not tested**, **Passed**, **Failed — defect logged**, **Not applicable — reason documented**, or **Retest passed**. A UI pass requires the action to be reachable through the intended role and interface, produce the expected result, and leave the expected audit/evidence trail.

**UI validation closure note (21 August 2026):** A targeted authenticated browser pass covered the public sign-in, home, employee master/profile, payroll run list/detail, Reports and twelve downloads, payslips, leave, attendance, Time operations, documents, analytics, Go-live readiness, Talent/reviews, timesheets, and report builder routes. The targeted results and evidence are recorded in `ui_validation_initial_findings.md`. The 778 source items not directly exercised remain **Not tested**; the 18 stakeholder rows below have been updated with the observed route/capability outcome. Custom qualifiers such as “capability unavailable” or “policy decision pending” are recorded in the defect/evidence columns, while the status column retains the permitted checklist vocabulary. The targeted export-format validation directly evidenced **1 Passed**, **14 Failed — defect logged**, and **781 Not tested** items; no other source item is represented as Passed without direct UI evidence.

Each source checklist item below has a stable `UI-####` ID. Record the screen or route, test data, evidence filename, defect ID/severity, owner, and retest result in the corresponding execution register at the end of this document.

---

# HR Management System — Payroll Readiness and UAT Checklist

**Primary objective:** Confirm that the HRM contains all information, workflows, controls, calculations, outputs, integrations, and reports required to prepare, approve, pay, account for, report, and audit payroll accurately.

**Priority:** Payroll is the highest-priority workstream. Employee records, organisation setup, attendance, leave, benefits, loans, and exits are tested primarily as upstream inputs into payroll.

**Country context:** Zambia. Statutory rules, rates, thresholds, ceilings, deadlines, and file formats must be configurable, effective-dated, and verified against the current requirements of the relevant authorities before production use. They must never be permanently hard-coded.

---

## 1. How to use this checklist

Use the following status values:

- [ ] **UI-0001** Not tested
- [ ] **UI-0002** Passed
- [ ] **UI-0003** Failed — defect logged
- [ ] **UI-0004** Not applicable — reason documented
- [ ] **UI-0005** Retest passed

Record the following for every failed test:

| Field | Required information |
|---|---|
| Test ID | Unique checklist or UAT reference |
| Module/page | Where the issue occurred |
| Test data | Employee, period, and scenario used |
| Expected result | What the system should have done |
| Actual result | What the system did |
| Evidence | Screenshot, export, calculation, or log |
| Severity | Critical, High, Medium, or Low |
| Owner | Person/team responsible |
| Target date | Agreed correction date |
| Retest result | Pass/fail and date |

### Priority definitions

| Priority | Meaning |
|---|---|
| P0 — Payroll blocker | Payroll cannot be calculated, approved, paid, reported, reconciled, or audited safely |
| P1 — Required | Needed for controlled production use but may have a documented temporary workaround |
| P2 — Enhancement | Improves efficiency, usability, or analysis without compromising payroll correctness |

### Mandatory go-live gates

Payroll must not go live until all the following are true:

- [ ] **UI-0006** All P0 tests have passed.
- [ ] **UI-0007** No unresolved Critical or High payroll defects remain.
- [ ] **UI-0008** Opening employee balances and year-to-date values reconcile to the legacy/source system.
- [ ] **UI-0009** At least two parallel payroll runs reconcile to approved expected results.
- [ ] **UI-0010** Statutory calculations and returns have been independently verified.
- [ ] **UI-0011** Bank/payment output has been validated by Finance and, where possible, the bank/payment provider.
- [ ] **UI-0012** Payroll journals reconcile to the payroll register and have been accepted by Finance.
- [ ] **UI-0013** Payslips reconcile to the payroll register and payment file.
- [ ] **UI-0014** Access, segregation of duties, approvals, audit logs, backup, and recovery have been tested.
- [ ] **UI-0015** HR, Payroll, Finance, IT, and Management have signed off.

---

# PART A — PAYROLL FOUNDATION AND READINESS

## 2. Organisation and employer setup — P0

### 2.1 Legal employer information

- [ ] **UI-0016** Legal entity name is captured exactly as registered.
- [ ] **UI-0017** Trading name, registration number, TPIN, employer statutory identifiers, and contact information are captured.
- [ ] **UI-0018** Registered address and operational addresses are maintained.
- [ ] **UI-0019** Employer bank/payment accounts are captured securely.
- [ ] **UI-0020** Payroll signatories and authorised approvers are recorded.
- [ ] **UI-0021** Multiple legal entities can be separated where applicable.
- [ ] **UI-0022** Employees cannot accidentally be paid or reported under the wrong legal entity.
- [ ] **UI-0023** Employer statutory registrations can be effective-dated and changed without rewriting historical payroll.

### 2.2 Organisation structure

- [ ] **UI-0024** Branches, departments, divisions, units, teams, cost centres, projects, and work locations can be configured.
- [ ] **UI-0025** Each employee is assigned to the correct payroll-relevant organisation dimensions.
- [ ] **UI-0026** Transfers are effective-dated and do not alter historical reporting.
- [ ] **UI-0027** Payroll can be processed, filtered, reviewed, and reported by entity, branch, department, location, project, and cost centre.
- [ ] **UI-0028** Inactive structures cannot be used for new transactions but remain visible historically.

### 2.3 Payroll calendars and periods

- [ ] **UI-0029** Monthly, fortnightly, weekly, and other required pay frequencies are supported or explicitly ruled out.
- [ ] **UI-0030** Payroll groups are configurable by entity, employment category, branch, or frequency.
- [ ] **UI-0031** Each pay calendar defines period start, period end, cut-off date, pay date, and statutory period.
- [ ] **UI-0032** Weekends and public holidays are considered when setting payment dates.
- [ ] **UI-0033** Payroll periods move through controlled statuses: Draft → Input → Calculation → Validation → Approval → Payment → Posting → Closed.
- [ ] **UI-0034** Only authorised users can reopen a closed period.
- [ ] **UI-0035** Reopening requires a reason, approval, and audit trail.
- [ ] **UI-0036** Future periods can be prepared without changing a closed period.
- [ ] **UI-0037** Overlapping or missing pay periods are prevented.

### 2.4 Currency and precision

- [ ] **UI-0038** Base/payroll currency is configured as ZMW where applicable.
- [ ] **UI-0039** Foreign currency earnings or deductions are supported where required.
- [ ] **UI-0040** Exchange-rate source, date, approval, and rounding rules are controlled.
- [ ] **UI-0041** Currency precision and rounding are applied consistently at employee, component, register, journal, return, and payment levels.
- [ ] **UI-0042** The system documents how one-ngwee rounding differences are handled.

---

## 3. Employee payroll master data — P0

### 3.1 Identity and statutory KYC

- [ ] **UI-0043** Unique employee number is automatically or controllably assigned.
- [ ] **UI-0044** Full legal names match the employee's identity documents.
- [ ] **UI-0045** NRC/passport number, nationality, date of birth, sex/gender where legally required, and identity expiry date are captured.
- [ ] **UI-0046** TPIN/tax identifier is captured and validated where required.
- [ ] **UI-0047** NAPSA/social security membership number is captured and validated.
- [ ] **UI-0048** NHIMA/health insurance identifier is captured where required.
- [ ] **UI-0049** Employee KYC completeness is checked before payroll activation.
- [ ] **UI-0050** Duplicate employees are detected using employee number, NRC/passport, tax number, pension number, bank account, phone, or email.
- [ ] **UI-0051** Changes to legal names and identifiers are audited and require appropriate approval.
- [ ] **UI-0052** Missing or invalid statutory data appears on a payroll-readiness exception report.

### 3.2 Employment information

- [ ] **UI-0053** Employment type is captured: permanent, fixed-term, temporary, casual, intern, expatriate, consultant, or other configured type.
- [ ] **UI-0054** Payroll eligibility is clearly distinguished from worker/employee status.
- [ ] **UI-0055** Hire date, confirmation date, contract start/end dates, probation dates, and expected retirement date are captured.
- [ ] **UI-0056** Position, job title, grade, step, department, branch, location, supervisor, and cost centre are effective-dated.
- [ ] **UI-0057** Full-time equivalent, scheduled hours, workdays, and standard hours are recorded.
- [ ] **UI-0058** Employee status is controlled: Pre-hire, Active, Suspended, On Leave, Terminating, Terminated, Retired, or Deceased.
- [ ] **UI-0059** Status changes trigger the correct payroll action and do not erase history.
- [ ] **UI-0060** Backdated employment changes are flagged for recalculation or arrears.

### 3.3 Contract and compensation information

- [ ] **UI-0061** Signed contract and compensation approval documents can be attached or referenced.
- [ ] **UI-0062** Base salary, wage rate, hourly/daily rate, grade rate, and effective date are captured.
- [ ] **UI-0063** Salary basis clearly identifies monthly, hourly, daily, annual, piece-rate, commission, or other method.
- [ ] **UI-0064** Gross-up arrangements are supported where applicable.
- [ ] **UI-0065** Recurring allowances, benefits, deductions, employer contributions, and reimbursements are assigned with start/end dates.
- [ ] **UI-0066** Salary changes require effective date, reason, initiator, approval, and supporting evidence.
- [ ] **UI-0067** Future-dated salary changes are supported.
- [ ] **UI-0068** The system prevents conflicting or overlapping salary records.
- [ ] **UI-0069** Salary history is immutable and reportable.

### 3.4 Payment instructions

- [ ] **UI-0070** Payment method supports bank transfer, mobile money, cash, cheque, or other approved method.
- [ ] **UI-0071** Bank name, branch, account name, account number, account type, and payment reference are captured.
- [ ] **UI-0072** Mobile money provider and verified mobile number are captured where used.
- [ ] **UI-0073** Split payments to multiple accounts are supported only if the organisation permits them.
- [ ] **UI-0074** Payment details are masked from unauthorised users.
- [ ] **UI-0075** Changes to payment details require re-authentication, independent approval, and notification to the employee.
- [ ] **UI-0076** Duplicate bank/mobile accounts across employees are flagged for review.
- [ ] **UI-0077** Invalid, missing, inactive, or unverified payment details block or hold payment as configured.
- [ ] **UI-0078** Cash/cheque payroll produces a controlled payment list and acknowledgement record.

### 3.5 Dependants, beneficiaries, and emergency contacts

- [ ] **UI-0079** Dependants and beneficiaries can be recorded where needed for benefits or statutory reporting.
- [ ] **UI-0080** Relationship, date of birth, allocation percentage, and supporting documents are maintained.
- [ ] **UI-0081** Beneficiary allocations are validated where applicable.
- [ ] **UI-0082** Sensitive dependant information is access-controlled.

### 3.6 Payroll readiness status

- [ ] **UI-0083** Every employee has a visible payroll-readiness indicator.
- [ ] **UI-0084** Readiness identifies missing identity, employment, compensation, tax, statutory, attendance, and payment data.
- [ ] **UI-0085** Employees with unresolved blocking errors cannot silently enter payroll.
- [ ] **UI-0086** Authorised overrides require reason, evidence, approval, and audit trail.
- [ ] **UI-0087** A payroll population report shows included, excluded, held, new, terminated, and changed employees.

---

## 4. Payroll configuration and rules — P0

### 4.1 Earning components

- [ ] **UI-0088** Basic salary/wage is configured.
- [ ] **UI-0089** Overtime, shift, acting, leave, housing, transport, meal, airtime, hardship, responsibility, travel, commission, bonus, incentive, gratuity, severance, notice, arrears, back pay, and other required earnings can be configured.
- [ ] **UI-0090** Each earning defines whether it is recurring or once-off.
- [ ] **UI-0091** Each earning defines taxable, pensionable, health-insurance, workers-compensation, and other statutory treatment.
- [ ] **UI-0092** Each earning defines whether it affects gross pay, net pay, employer cost, or information-only totals.
- [ ] **UI-0093** Each earning has eligibility rules, formula, rate/unit, effective dates, proration, rounding, GL mapping, and reporting category.
- [ ] **UI-0094** Negative earnings are prevented unless an approved correction process is used.
- [ ] **UI-0095** Earnings can be limited by grade, job, location, employment type, or policy.

### 4.2 Deduction components

- [ ] **UI-0096** PAYE, employee NAPSA, employee NHIMA, salary advances, employee loans, pension, union dues, insurance, medical aid, savings, garnishments/court orders, staff purchases, absence deductions, and other required deductions can be configured.
- [ ] **UI-0097** Each deduction defines pre-tax or post-tax treatment and statutory priority.
- [ ] **UI-0098** Fixed amount, percentage, balance-based, instalment-based, and formula deductions are supported.
- [ ] **UI-0099** Start date, end date, total balance, instalment, frequency, and priority are captured.
- [ ] **UI-0100** Protected-pay/minimum-net-pay rules are configurable.
- [ ] **UI-0101** Deduction caps and insufficient-net-pay rules are configurable.
- [ ] **UI-0102** Deferred deductions automatically carry forward where policy permits.
- [ ] **UI-0103** The system distinguishes employee deductions from employer liabilities.
- [ ] **UI-0104** Deduction beneficiaries and remittance accounts are maintained.
- [ ] **UI-0105** A deduction cannot continue after its end date or fully recovered balance.

### 4.3 Employer contributions and payroll costs

- [ ] **UI-0106** Employer NAPSA, employer NHIMA where applicable, workers' compensation, pension, insurance, levy, benefit, and other employer costs can be configured.
- [ ] **UI-0107** Employer cost does not incorrectly reduce employee net pay.
- [ ] **UI-0108** Employer costs are reportable by employee and organisation dimension.
- [ ] **UI-0109** Employer liabilities are mapped separately in accounting outputs.

### 4.4 Formula engine and rule governance

- [ ] **UI-0110** Formulas support brackets, percentages, caps, thresholds, minimums, maximums, quantities, rates, dates, and conditional rules.
- [ ] **UI-0111** Rules are effective-dated and historical versions are preserved.
- [ ] **UI-0112** Configuration changes require maker-checker approval.
- [ ] **UI-0113** Rule changes cannot alter previously closed payroll results.
- [ ] **UI-0114** Formula dependencies and order of calculation are explicit.
- [ ] **UI-0115** Circular formula dependencies are detected.
- [ ] **UI-0116** Administrators can test formulas in a sandbox before activation.
- [ ] **UI-0117** A configuration comparison report shows what changed between versions.
- [ ] **UI-0118** Every active rule has an owner, legal/policy source, approval, effective date, and review date.

### 4.5 Proration and day-count rules

- [ ] **UI-0119** New hires, terminations, unpaid leave, suspension, and salary changes can be prorated.
- [ ] **UI-0120** Calendar-day, working-day, fixed-30-day, hourly, and policy-specific methods are supported.
- [ ] **UI-0121** The chosen method is consistent and documented by component.
- [ ] **UI-0122** Mid-period transfers and salary increases calculate correctly.
- [ ] **UI-0123** Leap years, short months, weekends, holidays, and overnight shifts are handled.
- [ ] **UI-0124** Proration produces transparent calculation details on the audit report.

---

## 5. Zambia statutory configuration and compliance — P0

> Rates and thresholds must be confirmed for the relevant tax/contribution year. The checklist intentionally does not freeze current values into the system specification.

### 5.1 PAYE

- [ ] **UI-0125** PAYE tax bands, rates, tax-free threshold, credits/reliefs, and effective dates are configurable.
- [ ] **UI-0126** Taxable and non-taxable treatment is defined for every earning and benefit.
- [ ] **UI-0127** Statutory deductions/allowances applied before PAYE are correctly ordered.
- [ ] **UI-0128** Regular earnings, irregular earnings, bonuses, arrears, terminal payments, benefits in kind, and expatriate scenarios are correctly treated.
- [ ] **UI-0129** Cumulative or period-based calculation method follows approved Zambian rules and organisational requirements.
- [ ] **UI-0130** Mid-year hire and opening year-to-date values calculate correctly.
- [ ] **UI-0131** Tax adjustments and prior-period corrections require approval and remain auditable.
- [ ] **UI-0132** PAYE return/export contains the required employer, employee, period, emolument, deduction, and tax fields.
- [ ] **UI-0133** PAYE liability report reconciles to employee deductions, return, payment, and GL liability.
- [ ] **UI-0134** Due-date reminders, return status, submission reference, payment reference, and proof of payment can be tracked.

### 5.2 NAPSA

- [ ] **UI-0135** Employee and employer contribution rates are separately configurable.
- [ ] **UI-0136** Contribution ceiling/base and effective dates are configurable by statutory year.
- [ ] **UI-0137** Pensionable earnings classification is defined for every component.
- [ ] **UI-0138** New hires, terminated staff, employees at/above ceilings, arrears, top-ups, and nil periods calculate correctly.
- [ ] **UI-0139** Employee identity/KYC required for NAPSA submission is validated before payroll closure.
- [ ] **UI-0140** NAPSA return/export supports the current accepted format or integration.
- [ ] **UI-0141** Return validation errors can be imported or recorded and assigned for resolution.
- [ ] **UI-0142** Original returns, top-up/correction returns, nil returns, tracking references, status, and payment are traceable.
- [ ] **UI-0143** Employee contribution + employer contribution = return total = payment total = GL liability movement.

### 5.3 NHIMA / national health insurance

- [ ] **UI-0144** Employee and employer rules, contribution bases, rates, ceilings, exemptions, and effective dates are configurable.
- [ ] **UI-0145** Contributory earnings are classified correctly.
- [ ] **UI-0146** Employee and employer portions are reported separately.
- [ ] **UI-0147** Required return/export is generated in the accepted format.
- [ ] **UI-0148** Registration/member exceptions are reported.
- [ ] **UI-0149** Contribution register, return, payment, and GL liability reconcile.

### 5.4 Workers' compensation and other employer obligations

- [ ] **UI-0150** Workers' compensation classification, assessable earnings, industry/risk rate, period, and employer cost are configurable.
- [ ] **UI-0151** Annual or periodic payroll earnings summaries required for returns can be produced.
- [ ] **UI-0152** Skills development levy or other applicable employer levy can be configured without affecting employee net pay incorrectly.
- [ ] **UI-0153** Pension, medical, union, court-ordered, or sector-specific obligations can be configured.
- [ ] **UI-0154** Statutory changes can be loaded prospectively and tested before the effective date.
- [ ] **UI-0155** Compliance dashboard shows due, prepared, approved, submitted, paid, accepted, rejected, overdue, corrected, and reconciled statuses.

---

# PART B — UPSTREAM HR INPUTS THAT FEED PAYROLL

## 6. Time, attendance, shifts, and overtime — P0 where used

- [ ] **UI-0156** Work schedules and shift rosters are assigned with effective dates.
- [ ] **UI-0157** Clock-in/out supports the approved source: biometric, device, web, mobile, QR, geolocation, or manual entry.
- [ ] **UI-0158** Missing punches, duplicates, late arrival, early departure, absence, and excess hours are detected.
- [ ] **UI-0159** Overnight, weekend, holiday, split, and cross-period shifts are handled correctly.
- [ ] **UI-0160** Grace periods, breaks, rounding, and paid/unpaid time rules are configured.
- [ ] **UI-0161** Overtime eligibility, rate multipliers, caps, and authorisation rules are configured.
- [ ] **UI-0162** Overtime must be approved before payroll cut-off.
- [ ] **UI-0163** Approved hours/units flow to payroll once and cannot be duplicated.
- [ ] **UI-0164** Rejected or unapproved time does not silently enter payroll.
- [ ] **UI-0165** Manual adjustments require reason, evidence, and approval.
- [ ] **UI-0166** Attendance is locked when imported into a payroll run.
- [ ] **UI-0167** Corrections after cut-off follow arrears/adjustment workflow.
- [ ] **UI-0168** Payroll can trace each time-based amount back to source shifts/timesheets.
- [ ] **UI-0169** Attendance-to-payroll reconciliation reports scheduled, worked, approved, paid, and exception hours.

## 7. Leave and absence — P0

- [ ] **UI-0170** Leave types define paid, partially paid, unpaid, encashable, and non-payroll-impacting treatment.
- [ ] **UI-0171** Accrual, carry-forward, expiry, maximum balance, eligibility, and service rules are configured.
- [ ] **UI-0172** Leave requests follow required approvals.
- [ ] **UI-0173** Approved paid leave feeds correct leave pay where applicable.
- [ ] **UI-0174** Unpaid leave creates correct payroll deductions and proration.
- [ ] **UI-0175** Half-days, hourly leave, cross-period leave, and leave spanning termination are handled.
- [ ] **UI-0176** Leave cancellation after payroll cut-off creates a controlled adjustment.
- [ ] **UI-0177** Leave encashment and final leave settlement calculate correctly.
- [ ] **UI-0178** Leave balances reconcile before and after payroll.
- [ ] **UI-0179** Payroll can trace leave-related amounts back to approved leave records.

## 8. Benefits, expenses, commissions, and variable pay — P0/P1

- [ ] **UI-0180** Benefits have eligibility, start/end date, employee amount, employer amount, tax/statutory treatment, and GL mapping.
- [ ] **UI-0181** Benefits in kind are supported where required.
- [ ] **UI-0182** Expense reimbursements are separated from taxable earnings unless policy/law requires otherwise.
- [ ] **UI-0183** Approved claims enter the intended pay period once only.
- [ ] **UI-0184** Commission and incentive formulas use approved source data and versioned rules.
- [ ] **UI-0185** Bonus pools, eligibility, performance result, approval, and payment timing are controlled.
- [ ] **UI-0186** Variable-pay uploads validate duplicates, employee eligibility, component, period, amount, and authorisation.
- [ ] **UI-0187** Mass uploads show validation errors before committing.
- [ ] **UI-0188** Imported values are traceable to file, user, timestamp, and approval.

## 9. Employee loans, advances, savings, and third-party deductions — P0

- [ ] **UI-0189** Loan principal, disbursement, interest/fees, instalment, start date, end date, outstanding balance, and recovery priority are maintained.
- [ ] **UI-0190** Payroll receives only approved and disbursed loans/advances.
- [ ] **UI-0191** Recovery starts in the correct period.
- [ ] **UI-0192** Partial recovery, missed recovery, overpayment, early settlement, write-off, restructure, and termination are handled.
- [ ] **UI-0193** Payroll cannot deduct more than the outstanding balance.
- [ ] **UI-0194** Insufficient net pay follows configured priority and carry-forward rules.
- [ ] **UI-0195** New disbursements and payroll deductions reconcile to the loan/subledger.
- [ ] **UI-0196** Third-party deductions produce beneficiary remittance schedules.
- [ ] **UI-0197** Deduction balances and employee statements are available.

## 10. Employee movements and lifecycle events — P0

### New hires

- [ ] **UI-0198** New hires enter the correct payroll based on effective hire date and readiness status.
- [ ] **UI-0199** First salary is prorated according to policy.
- [ ] **UI-0200** Opening balances and prior earnings are captured where required.
- [ ] **UI-0201** New-hire report shows compensation, payment, statutory, and document completeness.

### Changes and transfers

- [ ] **UI-0202** Promotions, salary reviews, acting appointments, transfers, grade changes, and cost-centre changes are effective-dated.
- [ ] **UI-0203** Backdated changes calculate arrears/differences transparently.
- [ ] **UI-0204** Approval occurs before payroll inclusion.
- [ ] **UI-0205** Historical payroll retains the old organisation and compensation values.

### Suspension and unpaid status

- [ ] **UI-0206** Paid and unpaid suspension rules are distinct.
- [ ] **UI-0207** Suspension dates affect pay only as authorised.
- [ ] **UI-0208** Reinstatement and back pay are supported.

### Termination and final pay

- [ ] **UI-0209** Resignation, dismissal, redundancy, retirement, death, contract expiry, and other exit reasons are supported.
- [ ] **UI-0210** Last working day, payroll end date, benefits end date, and payment date are distinguished.
- [ ] **UI-0211** Final pay includes applicable salary, leave, overtime, commission, gratuity, severance, notice, arrears, deductions, loans, tax, and statutory contributions.
- [ ] **UI-0212** Recoverable assets or staff obligations can be included only through approved deductions.
- [ ] **UI-0213** Exit clearance can place final pay on hold without losing the calculation.
- [ ] **UI-0214** Final payment requires HR and Finance approval.
- [ ] **UI-0215** Former employees cannot re-enter regular payroll accidentally.
- [ ] **UI-0216** Final payslip, tax/statutory outputs, payment record, and certificate/statement requirements are available.

---

# PART C — END-TO-END PAYROLL PROCESS

## 11. Payroll input and cut-off — P0

- [ ] **UI-0217** Payroll dashboard shows current period, group, cut-off, pay date, status, owners, and outstanding tasks.
- [ ] **UI-0218** A pre-payroll checklist identifies incomplete employee data and missing approvals.
- [ ] **UI-0219** New hires, leavers, salary changes, bank changes, overtime, leave, bonuses, loans, and one-off inputs are summarised.
- [ ] **UI-0220** Payroll input supports authorised manual entry and controlled bulk import.
- [ ] **UI-0221** Templates contain only valid employees and components.
- [ ] **UI-0222** Upload rejects unknown employees, invalid components, duplicate rows, invalid dates, wrong periods, and malformed amounts.
- [ ] **UI-0223** Upload preview displays additions, changes, warnings, and errors before posting.
- [ ] **UI-0224** Duplicate source transactions cannot be paid twice.
- [ ] **UI-0225** Cut-off locks upstream transactions for the payroll period or moves late items into an adjustment queue.
- [ ] **UI-0226** Exceptions are assigned to owners and tracked to resolution.

## 12. Payroll calculation — P0

- [ ] **UI-0227** Payroll can calculate a full group, selected employees, or changed employees.
- [ ] **UI-0228** Calculation order is correct: earnings → statutory bases → employee deductions → employer contributions → net pay → employer cost.
- [ ] **UI-0229** Gross pay, taxable pay, pensionable pay, assessable pay, total deductions, net pay, and employer cost are separately available.
- [ ] **UI-0230** Recurring items, one-off items, proration, arrears, retroactive changes, and balances calculate correctly.
- [ ] **UI-0231** Calculating repeatedly without input changes produces identical results.
- [ ] **UI-0232** Recalculation does not duplicate inputs or deductions.
- [ ] **UI-0233** Calculation errors identify affected employees and reasons without hiding successful employees.
- [ ] **UI-0234** Calculation detail explains every amount, formula, rate, base, cap, and rounding result.
- [ ] **UI-0235** Zero-pay and negative-net-pay employees are flagged.
- [ ] **UI-0236** Unexpected gross/net changes above configurable thresholds are flagged.
- [ ] **UI-0237** Employees missing from or unexpectedly added to payroll are flagged.
- [ ] **UI-0238** Test/simulation runs do not create accounting, payment, statutory, or employee-facing transactions.

## 13. Payroll validation and variance review — P0

- [ ] **UI-0239** Current payroll can be compared with prior period, budget, expected payroll, and headcount.
- [ ] **UI-0240** Variances are shown by employee, component, branch, department, cost centre, and total.
- [ ] **UI-0241** Configurable thresholds flag significant value or percentage changes.
- [ ] **UI-0242** Joiners, leavers, zero-pay, negative-pay, unusually high pay, unusual overtime, missing bank details, duplicated accounts, and stopped deductions are reported.
- [ ] **UI-0243** Gross-to-net reconciliation is available for every employee and total payroll.
- [ ] **UI-0244** Payroll control totals reconcile employee results, payment totals, statutory totals, third-party remittances, employer costs, and journals.
- [ ] **UI-0245** Validation exceptions require resolution, accepted explanation, or authorised override.
- [ ] **UI-0246** Review comments and supporting documents are stored with the run.
- [ ] **UI-0247** Recalculation after correction creates a new result version and comparison.

## 14. Payroll approval — P0

- [ ] **UI-0248** Payroll follows maker-checker or multi-level approval.
- [ ] **UI-0249** The preparer cannot be the sole final approver.
- [ ] **UI-0250** Approvers see control totals, variances, exceptions, employee changes, statutory totals, payment total, and journal total.
- [ ] **UI-0251** Approval can be rejected with comments and returned for correction.
- [ ] **UI-0252** Any material change after approval invalidates approval and requires reapproval.
- [ ] **UI-0253** Approval records user, timestamp, decision, comments, and result version.
- [ ] **UI-0254** Emergency override is restricted, justified, independently approved, and audited.
- [ ] **UI-0255** Final approval locks calculation inputs and results.

## 15. Payment processing — P0

- [ ] **UI-0256** Payment file/list is generated only from the finally approved payroll version.
- [ ] **UI-0257** Payment total exactly matches approved net-pay total, excluding documented holds.
- [ ] **UI-0258** Bank/payment output meets the provider's required format, field length, account validation, currency, reference, and control-total rules.
- [ ] **UI-0259** Payment filename, batch number, period, hash/control total, creator, approver, and creation time are recorded.
- [ ] **UI-0260** Payment file is encrypted or securely transmitted and cannot be accessed by unauthorised users.
- [ ] **UI-0261** Manual editing of generated payment files is prevented or detected.
- [ ] **UI-0262** Duplicate payment batches are prevented.
- [ ] **UI-0263** Held, rejected, returned, failed, cancelled, and reissued payments are handled without duplicating payroll expense or liability.
- [ ] **UI-0264** Payment status is tracked by employee and batch.
- [ ] **UI-0265** Bank confirmation/statement can be reconciled to the payment batch.
- [ ] **UI-0266** Cash/cheque payments require acknowledgement and outstanding-payment tracking.
- [ ] **UI-0267** Unclaimed or unpaid wages remain a controlled liability.

## 16. Payslips and employee communication — P0

- [ ] **UI-0268** Payslip shows employer, employee, employee number, period, pay date, earnings, deductions, statutory deductions, net pay, and year-to-date totals as required.
- [ ] **UI-0269** Payslip values reconcile exactly to the approved payroll register.
- [ ] **UI-0270** Employer-only costs are shown only where policy permits and are clearly labelled.
- [ ] **UI-0271** Leave, loan, or benefit balances are shown only if accurate and approved for display.
- [ ] **UI-0272** Payslips are generated only after the appropriate approval/status.
- [ ] **UI-0273** Employees see only their own payslips.
- [ ] **UI-0274** Portal access and downloaded documents are protected.
- [ ] **UI-0275** Email delivery avoids exposing salary in an insecure message; secure link or protected attachment is used.
- [ ] **UI-0276** Delivery status and employee access/download can be logged appropriately.
- [ ] **UI-0277** Corrected payslips are versioned; superseded versions remain auditable.
- [ ] **UI-0278** Bulk download is restricted and audited.
- [ ] **UI-0279** Employee payroll queries can be logged, assigned, resolved, and linked to the relevant payslip/run.

## 17. Statutory filing and remittance — P0

- [ ] **UI-0280** Statutory returns are generated from the same approved payroll version.
- [ ] **UI-0281** Each return has period, version, preparer, reviewer, approval, submission date, reference, acceptance status, payment date, amount, and proof.
- [ ] **UI-0282** Rejected submissions and validation errors are tracked to resolution.
- [ ] **UI-0283** Corrected or top-up returns do not overwrite original submissions.
- [ ] **UI-0284** Payment amount reconciles to the accepted return and payroll liability.
- [ ] **UI-0285** Due-date reminders and escalation are configured.
- [ ] **UI-0286** Nil return process exists where applicable.
- [ ] **UI-0287** Compliance calendar and dashboard show all required employer obligations.
- [ ] **UI-0288** Statutory reports can be regenerated exactly for a closed historical period.

## 18. Accounting and finance integration — P0

- [ ] **UI-0289** Every earning, deduction, employer cost, reimbursement, liability, and payment component has effective-dated GL mapping.
- [ ] **UI-0290** Journal dimensions include legal entity, branch, department, cost centre, project, and other required segments.
- [ ] **UI-0291** Payroll expense, employer contributions, employee liabilities, statutory liabilities, third-party liabilities, net-pay liability, and cash/bank entries are separated.
- [ ] **UI-0292** Journal debits equal credits.
- [ ] **UI-0293** Payroll register totals reconcile to journal lines.
- [ ] **UI-0294** Payment clears the net-pay liability correctly.
- [ ] **UI-0295** Statutory and third-party remittances clear the correct liabilities.
- [ ] **UI-0296** Loan/advance deductions reconcile to the relevant receivable/subledger.
- [ ] **UI-0297** Rounding differences are posted to an approved account and explained.
- [ ] **UI-0298** Journal can be reviewed before posting.
- [ ] **UI-0299** Posting requires Finance approval and produces a reference/status.
- [ ] **UI-0300** Duplicate journal posting is prevented.
- [ ] **UI-0301** Failed postings can be corrected and retried safely.
- [ ] **UI-0302** Reversals preserve the original reference and require approval.
- [ ] **UI-0303** Closed accounting periods are respected.
- [ ] **UI-0304** Payroll-to-GL reconciliation remains available by period and run.

## 19. Payroll closure and corrections — P0

- [ ] **UI-0305** Closure requires approved payroll, payment output, payslips, statutory outputs, journal, and completed reconciliations as configured.
- [ ] **UI-0306** Closed payroll is read-only.
- [ ] **UI-0307** Close action requires approval and records a final control pack.
- [ ] **UI-0308** Post-close corrections use off-cycle payroll, supplementary payroll, arrears, reversal, or next-period adjustment.
- [ ] **UI-0309** Corrections preserve the original result and create linked adjustment records.
- [ ] **UI-0310** Off-cycle payroll has separate approvals, payment batch, payslip, statutory treatment, and journal.
- [ ] **UI-0311** A closed payroll can be reproduced exactly from stored configuration, inputs, and result versions.

---

# PART D — REPORTING REQUIREMENTS

## 20. Payroll operations reports — P0

- [ ] **UI-0312** Payroll register — employee-level and summarised earnings, deductions, net pay, and employer costs.
- [ ] **UI-0313** Gross-to-net report — calculation bridge for each employee and total payroll.
- [ ] **UI-0314** Payroll summary — total gross, taxable, statutory bases, deductions, net pay, employer cost, and headcount.
- [ ] **UI-0315** Payroll input report — recurring and once-off inputs by source.
- [ ] **UI-0316** Payroll change report — all changes since prior run/period.
- [ ] **UI-0317** Variance report — current versus prior, expected, and budget.
- [ ] **UI-0318** New-hire report.
- [ ] **UI-0319** Termination/final-pay report.
- [ ] **UI-0320** Zero-pay, negative-net, minimum-net, and held-payment report.
- [ ] **UI-0321** Missing/invalid payroll data report.
- [ ] **UI-0322** Manual adjustment and override report.
- [ ] **UI-0323** Retroactive pay/arrears report.
- [ ] **UI-0324** Overtime and time-based earnings report.
- [ ] **UI-0325** Unpaid leave and absence deduction report.
- [ ] **UI-0326** Bonus, commission, and incentive report.
- [ ] **UI-0327** Employee loan/advance deduction and balance report.
- [ ] **UI-0328** Third-party deduction and remittance report.
- [ ] **UI-0329** Payment instruction and payment status report.
- [ ] **UI-0330** Payroll reconciliation/control totals report.
- [ ] **UI-0331** Payroll processing status and outstanding task report.

## 21. Statutory and compliance reports — P0

The system must not produce only a total statutory deduction report. It must produce the filing return or accepted upload file, employee/member schedule, liability report, reconciliation, correction report, payment support, and submission history for each authority.

### 21.1 Statutory report register

| Authority/obligation | Minimum system output | Normal reporting basis | Required control |
|---|---|---|---|
| ZRA — PAYE | Monthly PAYE return/export, including the applicable ITF/P16 format; employee tax schedule | Monthly and annual/YTD where required | Return = payroll PAYE liability = payment = GL movement |
| NAPSA | Monthly employee contribution return in the current iCARE-compatible format; employee and employer schedule | Monthly | Employee + employer contributions = return = payment = GL liability |
| NHIMA | Monthly employee/employer contribution schedule and accepted return/export | Monthly | Employee + employer contributions = return = payment = GL liability |
| Workers' Compensation | Assessable payroll/earnings schedule and applicable employer return | Required assessment/return period | Assessable earnings × approved rate reconciles to employer liability/payment |
| Skills development levy | Applicable return/export and levy calculation schedule | Required tax/return period | Levy basis × effective rate reconciles to return, payment, and GL |
| Occupational/sector pension | Member and employer contribution schedule and remittance file | Monthly or scheme period | Contribution schedule = remittance = payroll/GL liability |
| Union and third-party bodies | Member deduction and employer contribution schedule | Monthly or agreed period | Beneficiary schedule = payroll deduction/liability = remittance |
| Court/garnishment authority | Employee deduction and case/reference schedule | Per order/remittance period | Authorised order balance = deduction history = remittance |

### 21.2 ZRA PAYE reports and returns

- [ ] **UI-0332** Monthly PAYE return/export is generated in the currently accepted ZRA format, including the applicable ITF/P16 fields.
- [ ] **UI-0333** Return identifies legal employer, TPIN, tax period, return type, currency, and authorised declaration details.
- [ ] **UI-0334** Employee schedule includes employee number, full legal name, employee TPIN, NRC/passport where required, gross emoluments, taxable emoluments, allowable/statutory deductions, tax relief where applicable, and PAYE deducted.
- [ ] **UI-0335** Employees with missing or invalid TPINs are reported before return generation.
- [ ] **UI-0336** Taxable and non-taxable earnings are separately reportable by component.
- [ ] **UI-0337** Regular earnings, bonuses, commissions, benefits in kind, arrears, leave payments, gratuity, terminal benefits, and other taxable emoluments can be separately disclosed where required.
- [ ] **UI-0338** Employee-level PAYE calculation report shows tax bands, rate, base, relief/deduction, tax before adjustment, adjustment, and final PAYE.
- [ ] **UI-0339** PAYE exception report identifies missing identifiers, negative tax, manual adjustments, unusual tax movements, and employees with taxable pay but zero PAYE.
- [ ] **UI-0340** Monthly PAYE liability report shows opening liability, current payroll deduction, adjustments, payments, penalties/interest recorded, and closing liability.
- [ ] **UI-0341** PAYE reconciliation compares payroll register, monthly return, payment/PRN or receipt, and GL control account.
- [ ] **UI-0342** Original, amended, replacement, or correction returns are versioned and linked.
- [ ] **UI-0343** PAYE submission reference, PRN/payment reference, acceptance status, submission date, payment date, proof, and preparer/approver are stored.
- [ ] **UI-0344** Late, missing, rejected, unpaid, underpaid, and overpaid periods appear on the compliance dashboard.
- [ ] **UI-0345** Annual/YTD employee earnings and PAYE statement can be produced where required.
- [ ] **UI-0346** Annual PAYE reconciliation confirms the sum of monthly returns equals payroll YTD and GL totals.

### 21.3 NAPSA reports and iCARE return support

- [ ] **UI-0347** NAPSA monthly return/export follows the current iCARE template or approved API specification.
- [ ] **UI-0348** Return identifies employer account, contribution month/year, employee NAPSA/social security number or NRC as permitted, employee identity fields, gross/pensionable earnings, employee contribution, employer contribution, and total contribution.
- [ ] **UI-0349** Employee KYC exception report checks legal names, date of birth, NRC/passport, and membership/social security number against payroll requirements.
- [ ] **UI-0350** Contribution ceiling report identifies employees below, at, and above the applicable ceiling.
- [ ] **UI-0351** Pensionable earnings report explains which payroll components are included or excluded.
- [ ] **UI-0352** NAPSA employee contribution schedule is available by employee, branch, department, and period.
- [ ] **UI-0353** NAPSA employer contribution schedule is available separately from employee deductions.
- [ ] **UI-0354** NAPSA reconciliation proves employee contribution + employer contribution = return total = payment total = GL liability movement.
- [ ] **UI-0355** Normal monthly return, top-up return, correction return, contribution-without-return reconciliation, and nil return are supported where applicable.
- [ ] **UI-0356** Return validation summary shows valid records, invalid records, reasons, and correction status.
- [ ] **UI-0357** Tracking number/reference and statuses such as pending, failed verification, pending payment, completed, cancelled, or authority resolution are recordable.
- [ ] **UI-0358** Underpayment, overpayment, unmatched payment, undeclared month, and unpaid period reports are available.
- [ ] **UI-0359** Submission date, acceptance status, payment reference, payment date, proof, preparer, reviewer, and approver are stored.
- [ ] **UI-0360** Employee contribution history can be produced for member queries and audit support.

### 21.4 NHIMA reports and returns

- [ ] **UI-0361** NHIMA monthly return/export follows the current accepted portal, template, or API format.
- [ ] **UI-0362** Return identifies employer, contribution period, employee/member identifier, contributory earnings, employee contribution, employer contribution, and total.
- [ ] **UI-0363** Employee and employer contribution schedules are available separately.
- [ ] **UI-0364** Contributory-earnings report shows included and excluded components.
- [ ] **UI-0365** Contribution ceiling, threshold, exemption, or special-category report is available where applicable.
- [ ] **UI-0366** Missing or invalid member/identity data appears on an exception report.
- [ ] **UI-0367** New registration, leaver, inactive member, and correction exceptions are identified.
- [ ] **UI-0368** NHIMA reconciliation proves employee contribution + employer contribution = return = payment = GL liability movement.
- [ ] **UI-0369** Submission reference, acceptance/rejection status, error details, payment reference, payment date, proof, preparer, reviewer, and approver are stored.
- [ ] **UI-0370** Corrected, replacement, top-up, and nil returns are versioned where applicable.

### 21.5 Workers' Compensation reports

- [ ] **UI-0371** Assessable payroll/earnings schedule is produced for the required return or assessment period.
- [ ] **UI-0372** Schedule can group employees and earnings by legal entity, branch, location, occupation, work category, and risk classification where required.
- [ ] **UI-0373** Report separates assessable and non-assessable payroll components.
- [ ] **UI-0374** Employer assessment calculation shows assessable earnings, approved rate, adjustments, and resulting employer liability.
- [ ] **UI-0375** Employee joiners, leavers, headcount, and remuneration totals supporting the return are available.
- [ ] **UI-0376** Workers' Compensation return/export follows the currently accepted format.
- [ ] **UI-0377** Return amount reconciles to payroll earnings, employer-cost report, payment, and GL liability/expense.
- [ ] **UI-0378** Assessment notice, submission reference, payment reference, proof, and any adjustment are stored.

### 21.6 Skills levy, pension, union, and other statutory/third-party reports

- [ ] **UI-0379** Skills development levy report shows the applicable emolument/levy base, exclusions, rate, adjustment, and liability.
- [ ] **UI-0380** Applicable levy return/export reconciles to payroll, payment, and GL.
- [ ] **UI-0381** Occupational pension schedule separates employee and employer portions and includes member/scheme identifiers.
- [ ] **UI-0382** Pension remittance report reconciles to payroll deductions, employer cost, remittance, and GL liability.
- [ ] **UI-0383** Union dues schedule identifies employee, union/member number, deduction, arrears, and remittance total.
- [ ] **UI-0384** Court order/garnishment report identifies protected case/reference data, ordered amount, deduction, balance, and remittance status.
- [ ] **UI-0385** Sector-specific return requirements can be configured without changing historical reports.
- [ ] **UI-0386** Every third-party schedule names the beneficiary, bank/remittance details, period, preparer, approver, payment status, and reference.

### 21.7 Statutory compliance dashboard

- [ ] **UI-0387** Dashboard shows each authority/obligation and every expected return period.
- [ ] **UI-0388** Status supports Not due, Due, Preparing, Awaiting approval, Submitted, Accepted, Rejected, Payment pending, Paid, Reconciled, Corrected, Overdue, and Not applicable.
- [ ] **UI-0389** Each obligation shows statutory due date, internal due date, responsible owner, reviewer, and escalation contact.
- [ ] **UI-0390** Automated reminders and escalation are generated before and after due dates.
- [ ] **UI-0391** Dashboard distinguishes return submission from payment; neither can imply the other is complete.
- [ ] **UI-0392** Missing return, missing payment, unpaid period, unmatched payment, rejected return, underpayment, overpayment, and outstanding correction are visible.
- [ ] **UI-0393** Compliance certificate, clearance, authority correspondence, assessment, penalty, waiver, or dispute documents can be attached.
- [ ] **UI-0394** Penalties and interest can be recorded separately from employee payroll deductions.
- [ ] **UI-0395** Management can view compliance by legal entity, authority, period, status, value, and risk.

### 21.8 Statutory reporting controls

- [ ] **UI-0396** Statutory reports are generated only from the approved payroll result version.
- [ ] **UI-0397** Every report shows employer, period, payroll run ID, version, generation timestamp, user, currency, and status.
- [ ] **UI-0398** Return/export control totals include employee count, record count, gross/contributory earnings, employee contribution/deduction, employer contribution, and total liability as applicable.
- [ ] **UI-0399** Report totals reconcile from employee detail to summary without spreadsheet manipulation.
- [ ] **UI-0400** The system validates mandatory fields, accepted formats, duplicate employees, invalid identifiers, negative amounts, and control-total differences before export.
- [ ] **UI-0401** Historical reports use the statutory rules and employee data effective in that historical period.
- [ ] **UI-0402** Regeneration of a closed-period report produces the same result unless it is clearly marked as a new corrected version.
- [ ] **UI-0403** Original and corrected returns remain available with reasons, approvals, and submission references.
- [ ] **UI-0404** Manual statutory adjustments require reason, supporting evidence, maker-checker approval, and separate reporting.
- [ ] **UI-0405** Sensitive statutory reports follow role-based access, export logging, retention, and data-protection rules.
- [ ] **UI-0406** Statutory audit pack contains calculation detail, employee schedule, return/export, validation report, approvals, submission response, payment proof, GL reconciliation, corrections, and correspondence.

## 22. Finance and accounting reports — P0

- [ ] **UI-0407** Payroll journal detail and summary.
- [ ] **UI-0408** Payroll-to-GL reconciliation.
- [ ] **UI-0409** Net-pay liability reconciliation.
- [ ] **UI-0410** Statutory liability reconciliation by authority and period.
- [ ] **UI-0411** Third-party liability reconciliation.
- [ ] **UI-0412** Employer-cost report.
- [ ] **UI-0413** Payroll expense by entity, branch, department, cost centre, location, project, grade, and employee type.
- [ ] **UI-0414** Actual payroll versus budget/forecast.
- [ ] **UI-0415** Accrued payroll, bonus, leave, gratuity, or other provision report where configured.
- [ ] **UI-0416** Loan/advance receivable reconciliation.
- [ ] **UI-0417** Payment batch versus bank confirmation reconciliation.
- [ ] **UI-0418** Unpaid, rejected, returned, or unclaimed wages report.

## 23. HR and management reports — P1

- [ ] **UI-0419** Headcount and full-time-equivalent report reconciled to paid employees.
- [ ] **UI-0420** Compensation report by grade, job, department, gender where lawful, and location.
- [ ] **UI-0421** Salary history and salary-change report.
- [ ] **UI-0422** New hire, movement, promotion, and termination trends.
- [ ] **UI-0423** Overtime cost and utilisation trends.
- [ ] **UI-0424** Leave cost, leave balance, and absence trends.
- [ ] **UI-0425** Benefit enrolment and employer-cost report.
- [ ] **UI-0426** Payroll cost per employee and organisational unit.
- [ ] **UI-0427** Average pay, median pay, pay range, and compa-ratio where grade ranges exist.
- [ ] **UI-0428** Workforce cost trend and forecast.
- [ ] **UI-0429** Contract-expiry, probation, retirement, and recurring-payment expiry alerts.

## 24. Employee-facing reports — P1

- [ ] **UI-0430** Current and historical payslips.
- [ ] **UI-0431** Year-to-date earnings and deduction statement.
- [ ] **UI-0432** Loan/advance balance and deduction statement.
- [ ] **UI-0433** Leave balance and transaction statement.
- [ ] **UI-0434** Benefit and deduction summary.
- [ ] **UI-0435** Tax/statutory statement where required.
- [ ] **UI-0436** Final-pay statement for terminated employees.

## 25. Report functionality and controls — P0

- [ ] **UI-0437** Reports can filter by period, pay group, entity, branch, department, cost centre, location, grade, employment type, component, and employee.
- [ ] **UI-0438** Totals remain consistent between screen, PDF, Excel/CSV, return file, journal, and payment file.
- [ ] **UI-0439** Drill-down moves from summary to employee to calculation/source transaction.
- [ ] **UI-0440** Reports show run ID, version, period, generation time, user, filters, currency, and status.
- [ ] **UI-0441** Draft reports are clearly watermarked or labelled.
- [ ] **UI-0442** Closed-period reports cannot silently change.
- [ ] **UI-0443** Exports preserve leading zeros in identifiers and account numbers.
- [ ] **UI-0444** Large reports complete reliably and do not omit rows.
- [ ] **UI-0445** Access respects entity, branch, department, and salary-data restrictions.
- [ ] **UI-0446** Sensitive exports are logged and expire or are protected where possible.
- [ ] **UI-0447** Scheduled reports are delivered only to authorised recipients.
- [ ] **UI-0448** Empty, zero, negative, very large, and special-character values render correctly.
- [ ] **UI-0449** Report totals include explicit inclusion/exclusion rules.

---

# PART E — SECURITY, CONTROL, AUDIT, AND RELIABILITY

## 26. Roles, permissions, and segregation of duties — P0

- [ ] **UI-0450** Roles exist for HR Administrator, HR Manager, Payroll Preparer, Payroll Reviewer, Payroll Approver, Finance Reviewer, Payment Approver, Auditor, Employee, Manager, and System Administrator as needed.
- [ ] **UI-0451** Permissions distinguish view, create, edit, delete, import, calculate, approve, pay, post, reopen, export, and administer.
- [ ] **UI-0452** Salary, bank, tax, identity, disciplinary, and medical information have appropriate field-level restrictions.
- [ ] **UI-0453** Users see only authorised legal entities, branches, departments, or employees.
- [ ] **UI-0454** System administrators cannot silently change payroll results.
- [ ] **UI-0455** Payroll preparer cannot be the sole approver or payment releaser.
- [ ] **UI-0456** Bank-detail changes are independently approved.
- [ ] **UI-0457** Role assignment and privileged access require approval and periodic review.
- [ ] **UI-0458** Terminated/transferred users lose access promptly.
- [ ] **UI-0459** Emergency access is time-limited, approved, monitored, and reviewed.

## 27. Audit trail — P0

- [ ] **UI-0460** Audit logs capture create, edit, delete/deactivate, import, calculation, approval, rejection, reopen, payment generation, payslip publication, return generation, journal posting, and export.
- [ ] **UI-0461** Log includes user, timestamp, action, record, before value, after value, reason, source, and approval where applicable.
- [ ] **UI-0462** Audit logs cannot be altered by ordinary administrators.
- [ ] **UI-0463** Salary, bank, statutory identifier, formula, rate, role, and payroll-result changes are easy to report.
- [ ] **UI-0464** Bulk imports identify every affected record.
- [ ] **UI-0465** API/integration changes record the calling system and request/reference ID.
- [ ] **UI-0466** Failed access and failed privileged actions are logged.
- [ ] **UI-0467** Audit logs follow approved retention policy.

## 28. Data protection and privacy — P0

- [ ] **UI-0468** Sensitive data is encrypted in transit and at rest.
- [ ] **UI-0469** Passwords, secrets, payment credentials, and integration keys are securely managed.
- [ ] **UI-0470** Multi-factor authentication is available for privileged/payroll users.
- [ ] **UI-0471** Sessions expire appropriately and sensitive actions require re-authentication.
- [ ] **UI-0472** Downloaded reports and payslips are protected from unauthorised access.
- [ ] **UI-0473** Non-production environments use masked or approved test data.
- [ ] **UI-0474** Data retention, archive, legal hold, and secure deletion rules are defined.
- [ ] **UI-0475** Employee consent/notice and access/correction processes are supported where required.
- [ ] **UI-0476** Security testing covers unauthorised salary access, IDOR, privilege escalation, export abuse, and file exposure.

## 29. Interfaces and integrations — P0 where applicable

- [ ] **UI-0477** Employee master, attendance, leave, LMS/loan, accounting, bank/payment, identity provider, notification, and statutory interfaces have defined ownership and contracts.
- [ ] **UI-0478** Integrations authenticate securely and use least privilege.
- [ ] **UI-0479** Every transaction has a unique idempotency/reference key.
- [ ] **UI-0480** Retries do not create duplicate employees, inputs, payments, returns, or journals.
- [ ] **UI-0481** Failed records enter a visible exception queue.
- [ ] **UI-0482** Partial batch success is reported accurately.
- [ ] **UI-0483** Interface totals and record counts reconcile source to destination.
- [ ] **UI-0484** Cut-off timing, time zones, date formats, encoding, and decimal precision are tested.
- [ ] **UI-0485** Integration changes are versioned and backward compatibility is managed.
- [ ] **UI-0486** Manual fallback procedures are documented and controlled.

## 30. Performance, availability, backup, and recovery — P0

- [ ] **UI-0487** Expected employee volume and five-year growth assumptions are documented.
- [ ] **UI-0488** Full payroll calculation completes within the agreed service level.
- [ ] **UI-0489** Recalculation, reports, bulk upload, payslip generation, and payment output perform acceptably at peak volume.
- [ ] **UI-0490** Simultaneous HR, payroll, manager, and employee users do not corrupt or lose data.
- [ ] **UI-0491** Long-running jobs show status and cannot be accidentally launched twice.
- [ ] **UI-0492** Failed jobs resume or restart safely.
- [ ] **UI-0493** Automated backups cover database, documents, configuration, audit logs, and required encryption keys.
- [ ] **UI-0494** Restore test proves a payroll period, employee documents, and audit history can be recovered.
- [ ] **UI-0495** Recovery point and recovery time objectives are documented and tested.
- [ ] **UI-0496** Payroll cut-off and payday continuity plan is documented for system, internet, bank, or integration outage.
- [ ] **UI-0497** Monitoring alerts on failed calculations, imports, integrations, report jobs, payment generation, and backups.

---

# PART F — CRITICAL UAT SCENARIOS

## 31. Core calculation scenarios — P0

Test each scenario manually with independently calculated expected results:

- [ ] **UI-0498** Normal employee on fixed monthly salary.
- [ ] **UI-0499** Employee below, within, and above each PAYE band boundary.
- [ ] **UI-0500** Employee below, exactly at, and above each statutory contribution ceiling/threshold.
- [ ] **UI-0501** New hire on first day, mid-period, and last day.
- [ ] **UI-0502** Termination on first day, mid-period, and last day.
- [ ] **UI-0503** Salary increase effective first day and mid-period.
- [ ] **UI-0504** Backdated salary increase producing arrears.
- [ ] **UI-0505** Paid leave, unpaid leave, and mixed leave in one period.
- [ ] **UI-0506** Standard, weekend, holiday, and overnight overtime.
- [ ] **UI-0507** Bonus/commission with correct statutory treatment.
- [ ] **UI-0508** Taxable and non-taxable allowance.
- [ ] **UI-0509** Benefit in kind where applicable.
- [ ] **UI-0510** Expense reimbursement separated from earnings.
- [ ] **UI-0511** Loan deduction with normal, final, partial, and skipped instalment.
- [ ] **UI-0512** Multiple deductions with insufficient net pay and priority rules.
- [ ] **UI-0513** Employee with zero gross pay.
- [ ] **UI-0514** Employee whose deductions would create negative net pay.
- [ ] **UI-0515** Employee on suspension and subsequent reinstatement/back pay.
- [ ] **UI-0516** Employee transferring branch/cost centre mid-period.
- [ ] **UI-0517** Final pay with leave, gratuity/severance, loan balance, and statutory deductions.
- [ ] **UI-0518** Off-cycle/supplementary payroll.
- [ ] **UI-0519** Correction to a prior closed period.
- [ ] **UI-0520** One employee with two payment accounts, if supported.
- [ ] **UI-0521** Rejected/returned payment and controlled reissue.
- [ ] **UI-0522** Expatriate or non-resident employee if applicable.
- [ ] **UI-0523** Casual/hourly/daily employee if applicable.
- [ ] **UI-0524** Employee with foreign-currency compensation if applicable.

## 32. Control and negative scenarios — P0

- [ ] **UI-0525** Duplicate employee is detected.
- [ ] **UI-0526** Duplicate variable-pay upload is detected.
- [ ] **UI-0527** Invalid bank details prevent or hold payment.
- [ ] **UI-0528** Unapproved overtime/leave/bonus does not enter payroll.
- [ ] **UI-0529** Expired recurring allowance stops correctly.
- [ ] **UI-0530** Fully repaid loan deduction stops correctly.
- [ ] **UI-0531** Unauthorised user cannot view salary or generate payment file.
- [ ] **UI-0532** Preparer cannot approve own payroll where segregation is required.
- [ ] **UI-0533** Change after approval forces reapproval.
- [ ] **UI-0534** Closed period cannot be edited directly.
- [ ] **UI-0535** Recalculation without changes produces the same results.
- [ ] **UI-0536** Repeated interface message does not duplicate a transaction.
- [ ] **UI-0537** Bank file cannot be generated from a draft or rejected payroll.
- [ ] **UI-0538** Payment file total mismatch blocks release.
- [ ] **UI-0539** Journal imbalance blocks posting.
- [ ] **UI-0540** Statutory return mismatch is flagged.
- [ ] **UI-0541** Missing statutory identifier appears as a blocking exception.
- [ ] **UI-0542** Unauthorised report export is blocked and logged.
- [ ] **UI-0543** Backup restoration reproduces the selected closed payroll.

## 33. Parallel run and reconciliation — P0

- [ ] **UI-0544** Import opening balances for leave, loans, deductions, arrears, and year-to-date payroll/statutory values.
- [ ] **UI-0545** Reconcile employee population to source HR/payroll records.
- [ ] **UI-0546** Reconcile employee master and bank/payment details.
- [ ] **UI-0547** Run the same period in old and new systems using frozen inputs.
- [ ] **UI-0548** Compare gross, each earning, taxable pay, each statutory value, each deduction, net pay, employer cost, and year-to-date totals employee by employee.
- [ ] **UI-0549** Investigate every difference; distinguish configuration, source-data, rounding, and software defects.
- [ ] **UI-0550** Agree materiality/tolerance only for legitimate rounding, never for unexplained differences.
- [ ] **UI-0551** Reconcile payment totals and bank output.
- [ ] **UI-0552** Reconcile statutory returns and liabilities.
- [ ] **UI-0553** Reconcile payroll journal and cost-centre distribution.
- [ ] **UI-0554** Complete at least two successful parallel periods, including one with joiners, leavers, changes, overtime, leave, and variable pay.
- [ ] **UI-0555** Obtain written approval of final reconciliation.

---

# PART G — PROCESS OWNERSHIP AND SIGN-OFF

## 34. Required procedures and user guidance — P1

- [ ] **UI-0556** Employee setup and payroll activation procedure.
- [ ] **UI-0557** Salary and bank-detail change procedure.
- [ ] **UI-0558** Payroll calendar, cut-off, and input procedure.
- [ ] **UI-0559** Variable-pay and bulk-upload procedure.
- [ ] **UI-0560** Payroll calculation, validation, approval, and closure procedure.
- [ ] **UI-0561** Payment file generation, release, confirmation, rejection, and reissue procedure.
- [ ] **UI-0562** Payslip publication and payroll-query procedure.
- [ ] **UI-0563** PAYE, NAPSA, NHIMA, workers' compensation, and other filing/remittance procedures.
- [ ] **UI-0564** Payroll-to-GL posting and reconciliation procedure.
- [ ] **UI-0565** Off-cycle, reversal, correction, and back-pay procedure.
- [ ] **UI-0566** Joiner, change, transfer, suspension, and leaver payroll procedure.
- [ ] **UI-0567** Access review, privileged access, and incident response procedure.
- [ ] **UI-0568** Backup, restore, outage, and payday continuity procedure.
- [ ] **UI-0569** Month-end, year-end, and annual statutory rollover procedure.

## 35. Training and operational readiness — P1

- [ ] **UI-0570** HR administrators are trained on payroll-critical master data.
- [ ] **UI-0571** Managers are trained on time, leave, overtime, and variable-pay approvals.
- [ ] **UI-0572** Payroll preparers are trained on inputs, calculations, exceptions, reconciliation, and corrections.
- [ ] **UI-0573** Payroll approvers are trained on control totals, variance review, and sign-off responsibilities.
- [ ] **UI-0574** Finance users are trained on payments, journals, liabilities, and reconciliations.
- [ ] **UI-0575** Employees are trained on payslip access and payroll queries.
- [ ] **UI-0576** Role-based quick guides and escalation contacts are available.
- [ ] **UI-0577** Production support ownership and severity-based response times are agreed.
- [ ] **UI-0578** First three production payrolls have enhanced support and daily issue review.

## 36. Final sign-off

| Area | Owner | Name | Decision | Date | Outstanding conditions |
|---|---|---|---|---|---|
| HR master data | HR |  | Approve / Reject |  |  |
| Time, leave, and variable inputs | HR/Operations |  | Approve / Reject |  |  |
| Payroll calculation and controls | Payroll |  | Approve / Reject |  |  |
| Statutory compliance | Payroll/Tax/Legal |  | Approve / Reject |  |  |
| Payments and reconciliations | Finance |  | Approve / Reject |  |  |
| Accounting and GL | Finance |  | Approve / Reject |  |  |
| Access and security | IT/Security |  | Approve / Reject |  |  |
| Performance and recovery | IT |  | Approve / Reject |  |  |
| Overall business acceptance | Management |  | Approve / Reject |  |  |

### Final go-live decision

- [ ] **UI-0579** Approved for production.
- [ ] **UI-0580** Conditionally approved — conditions documented, owned, and dated.
- [ ] **UI-0581** Not approved — blocking items documented.

**Approved production payroll start period:** ____________________

**Final accountable owner:** ____________________

**Approval date:** ____________________

---

# Appendix A — Minimum payroll control pack for every pay period

The system should retain or generate one complete control pack containing:

- [ ] **UI-0582** Payroll calendar and cut-off confirmation.
- [ ] **UI-0583** Payroll population and readiness report.
- [ ] **UI-0584** Approved joiners, leavers, salary changes, bank changes, overtime, leave, bonuses, and one-off inputs.
- [ ] **UI-0585** Import validation and exception reports.
- [ ] **UI-0586** Payroll register and gross-to-net report.
- [ ] **UI-0587** Current-versus-prior variance report with explanations.
- [ ] **UI-0588** Zero/negative/unusual pay and duplicate-payment-detail reports.
- [ ] **UI-0589** Payroll reconciliation and signed approval.
- [ ] **UI-0590** Payment file/list, control total, approval, bank confirmation, and rejected payment report.
- [ ] **UI-0591** Payslip generation/publication confirmation.
- [ ] **UI-0592** PAYE, NAPSA, NHIMA, and other applicable returns, approvals, submission references, payments, and reconciliations.
- [ ] **UI-0593** Payroll journal, posting reference, and payroll-to-GL reconciliation.
- [ ] **UI-0594** Third-party remittance schedules and proofs.
- [ ] **UI-0595** Adjustments, overrides, reopened periods, and off-cycle runs.
- [ ] **UI-0596** Audit log extract for material payroll actions.
- [ ] **UI-0597** Final closure confirmation.

# Appendix B — Payroll dashboard minimum information

- [ ] **UI-0598** Current pay period and days to cut-off/payday.
- [ ] **UI-0599** Payroll status and responsible owner.
- [ ] **UI-0600** Employee population: expected, ready, blocked, held, new, and terminating.
- [ ] **UI-0601** Outstanding HR inputs and approvals.
- [ ] **UI-0602** Calculation errors and validation exceptions.
- [ ] **UI-0603** Gross, deductions, net pay, employer cost, and prior-period variance.
- [ ] **UI-0604** Approval stage and pending approver.
- [ ] **UI-0605** Payment batch/status and rejected payments.
- [ ] **UI-0606** Statutory returns, due dates, submission, payment, and reconciliation status.
- [ ] **UI-0607** Journal posting and reconciliation status.
- [ ] **UI-0608** Critical alerts, overdue tasks, and unresolved payroll queries.

# Appendix C — Recommended defect severity

| Severity | Definition | Example |
|---|---|---|
| Critical | Incorrect or unauthorised payment, statutory non-compliance, material data exposure, unrecoverable corruption, or inability to process payroll | Wrong net pay for many employees; payment file duplicates; salary data exposed |
| High | Material payroll/control failure with no acceptable safe workaround | Incorrect PAYE ceiling; approval bypass; journal does not reconcile |
| Medium | Limited error with controlled workaround and no material compliance/payment risk | One report filter fails; manual correction is safe and documented |
| Low | Cosmetic, wording, or minor usability issue | Label alignment or non-blocking display issue |

# Appendix D — Mandatory Monthly HR Management Report Pack

The HRM must produce a complete monthly HR report pack, not only payroll and statutory reports. The pack should combine a one-page management summary with detailed schedules that HR can drill into or export.

## D1. Monthly reporting period and governance

- [ ] **UI-0609** HR can select the reporting month, legal entity, branch, department, cost centre, location, and employee category.
- [ ] **UI-0610** The report uses a controlled month-end employee snapshot so historical headcount does not change when records are edited later.
- [ ] **UI-0611** Opening balance, additions, reductions, and closing balance reconcile for all movement-based metrics.
- [ ] **UI-0612** Current month is compared with prior month, same month prior year where available, year-to-date, target, and budget where relevant.
- [ ] **UI-0613** Each metric has a definition, owner, source, calculation, inclusion/exclusion rule, and refresh date.
- [ ] **UI-0614** Draft, Reviewed, Approved, and Published statuses are supported.
- [ ] **UI-0615** HR prepares, HR management reviews, and the authorised executive approves the monthly pack.
- [ ] **UI-0616** Comments explain material movements, risks, causes, and corrective actions.
- [ ] **UI-0617** Action items identify owner, deadline, status, and follow-up result.
- [ ] **UI-0618** Published packs are versioned and cannot be silently changed.
- [ ] **UI-0619** Distribution is restricted because the report contains personal and compensation information.
- [ ] **UI-0620** Summary figures drill down to the supporting employee-level schedule for authorised users.

## D2. Executive HR dashboard — mandatory

- [ ] **UI-0621** Opening and closing headcount.
- [ ] **UI-0622** Joiners, leavers, net movement, and turnover rate.
- [ ] **UI-0623** Permanent, fixed-term, temporary, casual, intern, and other employee totals.
- [ ] **UI-0624** Active, suspended, on-leave, terminating, and inactive totals.
- [ ] **UI-0625** Total payroll cost, gross pay, net pay, employer statutory cost, and month-on-month variance.
- [ ] **UI-0626** Attendance rate, absence rate, lateness, and overtime hours/cost.
- [ ] **UI-0627** Leave taken, leave liability/balance, and overdue/excess leave.
- [ ] **UI-0628** Vacancies, hires, time-to-fill, offers, and outstanding onboarding actions.
- [ ] **UI-0629** Probations and contracts due for action.
- [ ] **UI-0630** Performance reviews due, completed, overdue, and outcome distribution.
- [ ] **UI-0631** Training completed, training hours, cost, and compliance completion.
- [ ] **UI-0632** Disciplinary, grievance, employee-relations, and health-and-safety cases.
- [ ] **UI-0633** Statutory filing/payment compliance status.
- [ ] **UI-0634** Critical employee-data, document, approval, and payroll-readiness exceptions.
- [ ] **UI-0635** Top five HR risks, decisions required from management, and next-month priorities.

## D3. Workforce and headcount report

- [ ] **UI-0636** Opening headcount + joiners + rehires - leavers = closing headcount.
- [ ] **UI-0637** Headcount and full-time equivalents are reported separately.
- [ ] **UI-0638** Headcount is analysed by entity, branch, department, cost centre, location, grade, job, position, supervisor, employment type, and employee status.
- [ ] **UI-0639** Filled positions, vacant positions, frozen positions, and approved establishment are compared.
- [ ] **UI-0640** Actual headcount is compared with approved establishment and budget.
- [ ] **UI-0641** Permanent, fixed-term, temporary, casual, intern, expatriate, and consultant populations are distinguishable.
- [ ] **UI-0642** Gender, age band, nationality, disability, or other workforce demographics are reported only where lawful and appropriate.
- [ ] **UI-0643** Span of control and employees without an assigned supervisor are reported.
- [ ] **UI-0644** Employees without valid positions, grades, departments, branches, or cost centres appear as data exceptions.
- [ ] **UI-0645** Month-end workforce list is exportable for audit support.

## D4. Joiners, movements, and leavers report

- [ ] **UI-0646** New hires and rehires are listed with start date, job, grade, branch, department, employment type, and readiness status.
- [ ] **UI-0647** Promotions, transfers, acting appointments, salary changes, grade changes, supervisor changes, and location changes are reported.
- [ ] **UI-0648** Leavers are listed by exit date, reason, department, tenure, final-pay status, clearance status, and replacement requirement.
- [ ] **UI-0649** Voluntary, involuntary, retirement, death, contract expiry, redundancy, and dismissal exits are separated.
- [ ] **UI-0650** Employee turnover, voluntary turnover, involuntary turnover, and regrettable turnover are calculated using documented definitions.
- [ ] **UI-0651** Turnover is analysed by branch, department, job, grade, supervisor, employment type, tenure, and reason.
- [ ] **UI-0652** Exit interviews due, completed, declined, and key themes are reported.
- [ ] **UI-0653** Offboarding, asset return, system-access removal, clearance, final pay, and document completion exceptions are shown.
- [ ] **UI-0654** Future-dated hires, transfers, and exits for the next reporting period are listed.

## D5. Attendance, time, shifts, and overtime report

- [ ] **UI-0655** Scheduled days/hours, worked days/hours, paid hours, and unpaid hours are reported.
- [ ] **UI-0656** Attendance rate and absence rate use documented formulas.
- [ ] **UI-0657** Late arrivals, early departures, missed punches, no-shows, and unauthorised absences are reported.
- [ ] **UI-0658** Attendance report explicitly lists employees who reported late and employees who missed work, with date, scheduled time, actual time, minutes late/absent, reason, approval status, and manager action.
- [ ] **UI-0659** Overtime hours and cost are analysed by employee, supervisor, department, branch, shift type, and reason.
- [ ] **UI-0660** Overtime report compares the current month's hours and ZMW cost with the previous month, showing amount variance, percentage variance, and a clear Increasing/Decreasing/No change indicator.
- [ ] **UI-0661** Approved, rejected, pending, and paid overtime are separately shown.
- [ ] **UI-0662** Excessive overtime, repeated lateness, consecutive workdays, rest-day, and policy-limit exceptions are flagged.
- [ ] **UI-0663** Shift coverage gaps, understaffed shifts, and unassigned employees are reported where shifts are used.
- [ ] **UI-0664** Timesheets outstanding, rejected, corrected, and approved after cut-off are reported.
- [ ] **UI-0665** Attendance-to-payroll reconciliation confirms approved payable hours equal payroll hours/units.
- [ ] **UI-0666** Monthly trend identifies departments with deteriorating attendance or abnormal overtime.

## D6. Leave and absence report

- [ ] **UI-0667** Opening leave balance + accrual + adjustment - leave taken/encashed/expired = closing balance.
- [ ] **UI-0668** A consolidated sick-note register lists every sick note received during the selected month, linked to the employee and related sick-leave/absence record.
- [ ] **UI-0669** Sick-note register shows employee, date reported sick, absence start/end date, days/hours absent, date note received, document status, verification/approval status, and authorised reviewer.
- [ ] **UI-0670** Monthly sick-note report identifies employees with one or more sick notes and shows occurrence count, total sick days/hours, and repeat occurrences for the month and year to date.
- [ ] **UI-0671** Leave taken is analysed by type, employee, department, branch, and period.
- [ ] **UI-0672** Leave requests submitted, approved, rejected, cancelled, and pending are reported.
- [ ] **UI-0673** Paid, unpaid, partially paid, sick, maternity, paternity, annual, compassionate, study, and other configured leave are separated.
- [ ] **UI-0674** Employees with negative, excessive, expired, or unusually high leave balances are flagged.
- [ ] **UI-0675** Employees who have not taken minimum/rest leave within policy periods are identified.
- [ ] **UI-0676** Long-term absence and return-to-work cases are tracked.
- [ ] **UI-0677** Sick leave frequency, duration, repeated patterns, and supporting-document exceptions are reported with restricted access.
- [ ] **UI-0678** Leave provision/liability is reported where Finance requires it.
- [ ] **UI-0679** Approved unpaid leave reconciles to payroll deductions.
- [ ] **UI-0680** Upcoming team/department leave calendar and coverage risks are available.

## D7. Recruitment and onboarding report

- [ ] **UI-0681** Approved vacancies, open vacancies, applicants, shortlisted candidates, interviews, offers, acceptances, declines, and hires are reported.
- [ ] **UI-0682** Recruitment funnel conversion is calculated at each stage.
- [ ] **UI-0683** Vacancy age, time-to-shortlist, time-to-offer, time-to-fill, and time-to-start are reported.
- [ ] **UI-0684** Source of hire and recruitment cost are reportable.
- [ ] **UI-0685** Vacancies are analysed by entity, department, branch, position, grade, recruiter, and priority.
- [ ] **UI-0686** Planned versus actual hiring and budgeted versus unbudgeted positions are compared.
- [ ] **UI-0687** Offer status, background/reference checks, medical checks where applicable, and document collection are tracked.
- [ ] **UI-0688** New-hire onboarding completion covers contract, policies, KYC, statutory registration, payroll, bank details, orientation, equipment, system access, and supervisor actions.
- [ ] **UI-0689** Overdue onboarding tasks and employees not payroll-ready are highlighted.
- [ ] **UI-0690** Probation goals and review dates are created during onboarding.

## D8. Payroll, compensation, and benefits monthly report

- [ ] **UI-0691** Payroll population, gross pay, taxable pay, deductions, net pay, employer contributions, and total employer cost are reported.
- [ ] **UI-0692** Current payroll is compared with previous month, budget, and headcount movement.
- [ ] **UI-0693** Payroll variance is explained by joiners, leavers, salary changes, overtime, leave, bonus, commission, arrears, deductions, and corrections.
- [ ] **UI-0694** Payroll cost is analysed by entity, branch, department, cost centre, grade, employment type, and component.
- [ ] **UI-0695** Basic pay, allowances, overtime, bonus, commission, benefits, and employer statutory cost are separately reported.
- [ ] **UI-0696** Salary increases, promotions, acting allowances, new recurring items, expired items, and manual adjustments are reported.
- [ ] **UI-0697** Zero pay, negative net pay, unusually high/low pay, held pay, returned payments, and unpaid employees are reported.
- [ ] **UI-0698** Salary advance, employee loan, savings, union, garnishment, and third-party deduction totals and balances are reported.
- [ ] **UI-0699** Benefit enrolment, eligible-not-enrolled employees, employee cost, employer cost, additions, removals, and expiries are reported.
- [ ] **UI-0700** Payroll register, payment file, payslips, statutory returns, and GL journal reconciliation status is shown.
- [ ] **UI-0701** Payroll queries opened, resolved, overdue, and recurring causes are reported.

## D9. Performance management report

- [ ] **UI-0702** Active performance cycles, eligible employees, reviews launched, completed, pending, and overdue are reported.
- [ ] **UI-0703** Goal/KPI setting completion and approval are reported.
- [ ] **UI-0704** Mid-year, annual, probation, and other review types are separated.
- [ ] **UI-0705** Rating distribution is shown by department, grade, and reviewer with appropriate privacy controls.
- [ ] **UI-0706** Missing ratings, unapproved ratings, inconsistent calibration, and rating bias indicators are flagged for authorised review.
- [ ] **UI-0707** Performance improvement plans show start date, milestone, owner, status, and outcome.
- [ ] **UI-0708** Probation confirmations, extensions, and overdue probation decisions are reported.
- [ ] **UI-0709** For a standard three-month probation, the system calculates the expected end date from the employment start date and notifies HR and the responsible manager before the decision is due.
- [ ] **UI-0710** Probation workflow records the final decision as Confirm employment, Extend probation, or End employment, together with reason, approval, effective date, and supporting documents.
- [ ] **UI-0711** Approved performance outcomes feeding salary, bonus, promotion, training, or succession actions are tracked.
- [ ] **UI-0712** Manager completion and overdue-action rates are reported.

## D10. Learning, development, and compliance training report

- [ ] **UI-0713** Training planned, scheduled, attended, completed, failed, cancelled, and overdue are reported.
- [ ] **UI-0714** Mandatory/compliance training is separated from developmental training.
- [ ] **UI-0715** Completion rate is analysed by course, department, branch, job, and employee.
- [ ] **UI-0716** Training hours, direct cost, cost per employee, provider, and budget variance are reported.
- [ ] **UI-0717** Certificates, licences, and professional memberships due to expire are reported.
- [ ] **UI-0718** Training needs from performance reviews, role requirements, incidents, or succession plans are tracked.
- [ ] **UI-0719** Training effectiveness, assessment results, and post-training evaluation are reportable.
- [ ] **UI-0720** Employees performing regulated/safety-sensitive work without valid training or certification are flagged.

## D11. Employee relations, discipline, grievance, and wellbeing report

- [ ] **UI-0721** Disciplinary cases opened, active, awaiting action, closed, appealed, and overdue are reported.
- [ ] **UI-0722** Grievances opened, active, resolved, escalated, and overdue are reported.
- [ ] **UI-0723** Cases are analysed by type, department, branch, severity, age, and outcome without exposing unnecessary personal details.
- [ ] **UI-0724** Warnings and sanctions due to expire or requiring review are reported.
- [ ] **UI-0725** Investigations, hearings, appeal deadlines, and responsible officers are tracked.
- [ ] **UI-0726** Recurring case themes and high-risk departments are highlighted.
- [ ] **UI-0727** Employee assistance, wellbeing, or engagement indicators are included where collected lawfully.
- [ ] **UI-0728** Sensitive case details are restricted; management receives aggregated information unless individual detail is authorised.

## D12. Health, safety, and workplace incident report

- [ ] **UI-0729** Workplace accidents, incidents, near misses, occupational illness, and fatalities are reported.
- [ ] **UI-0730** Incident date, location, employee/contractor category, severity, lost time, cause, action, and status are tracked.
- [ ] **UI-0731** Lost-time injury, days lost, and other approved safety metrics are calculated consistently.
- [ ] **UI-0732** Workers' Compensation notifications, claims, supporting documents, and status are tracked.
- [ ] **UI-0733** Medical/safety checks due, completed, expired, or failed are reported with restricted access.
- [ ] **UI-0734** Corrective actions, responsible owners, deadlines, and overdue items are reported.
- [ ] **UI-0735** Safety training and protective-equipment compliance are reported where applicable.

## D13. Contracts, documents, and HR compliance report

- [ ] **UI-0736** Contracts expiring in 30, 60, and 90 days are reported.
- [ ] **UI-0737** Probation reviews, confirmations, or extensions due in 30, 60, and 90 days are reported.
- [ ] **UI-0738** NRC/passport, work permit, visa, licence, certificate, medical, and other required documents nearing expiry are reported.
- [ ] **UI-0739** Employees with missing contracts, policies, KYC, statutory identifiers, bank details, beneficiary details, or required documents are reported.
- [ ] **UI-0740** Employee files are scored for completeness and listed by missing requirement.
- [ ] **UI-0741** Policy acknowledgements and mandatory declarations outstanding are reported.
- [ ] **UI-0742** Statutory return, payment, registration, compliance certificate, assessment, and correction statuses are summarised.
- [ ] **UI-0743** Access reviews, conflict-of-interest declarations, and other recurring HR compliance actions are tracked where applicable.

## D14. HR service delivery and workflow report

- [ ] **UI-0744** Employee requests opened, resolved, pending, rejected, escalated, and overdue are reported.
- [ ] **UI-0745** Requests are analysed by category: leave, payroll, personal-data change, document, benefits, grievance, recruitment, onboarding, transfer, or exit.
- [ ] **UI-0746** Average response time, resolution time, SLA compliance, backlog, and ageing are reported.
- [ ] **UI-0747** Approval requests pending by manager/approver and ageing are reported.
- [ ] **UI-0748** Reopened requests, recurring requests, and root causes are highlighted.
- [ ] **UI-0749** HR team workload and case ownership are visible.
- [ ] **UI-0750** Employee satisfaction or service feedback is reported where captured.

## D15. Employee data quality report

- [ ] **UI-0751** Duplicate employee, NRC/passport, TPIN, NAPSA number, NHIMA number, bank account, phone, and email records are flagged.
- [ ] **UI-0752** Missing required identity, contact, employment, organisation, compensation, statutory, payment, supervisor, and document fields are reported.
- [ ] **UI-0753** Invalid dates, overlapping contracts, overlapping salary records, impossible ages, and termination before hire are detected.
- [ ] **UI-0754** Active employees assigned to inactive branches, departments, positions, grades, or cost centres are reported.
- [ ] **UI-0755** Employees with inconsistent status across HR, payroll, attendance, leave, benefits, loan, and access systems are reported.
- [ ] **UI-0756** Data corrections show owner, due date, status, and completion evidence.
- [ ] **UI-0757** Data-quality score and unresolved P0/P1 exceptions are included in the monthly executive dashboard.

## D16. Next-month HR action calendar

- [ ] **UI-0758** Expected joiners, leavers, transfers, promotions, and salary changes.
- [ ] **UI-0759** Contracts, probation reviews, permits, licences, certificates, and documents due to expire.
- [ ] **UI-0760** Planned recruitment milestones and vacancies requiring decisions.
- [ ] **UI-0761** Upcoming performance-cycle activities and overdue reviews.
- [ ] **UI-0762** Planned training and mandatory compliance deadlines.
- [ ] **UI-0763** Upcoming payroll cut-off, pay date, statutory deadlines, and annual rule changes.
- [ ] **UI-0764** Planned leave and workforce coverage risks.
- [ ] **UI-0765** Disciplinary, grievance, investigation, hearing, appeal, or case deadlines.
- [ ] **UI-0766** Management decisions required, accountable owner, and decision due date.

## D17. Monthly HR report acceptance tests

- [ ] **UI-0767** Closing headcount reconciles to the employee master at month end.
- [ ] **UI-0768** Opening headcount plus net employee movement equals closing headcount.
- [ ] **UI-0769** Paid headcount reconciles to payroll population, with every difference explained.
- [ ] **UI-0770** Payroll totals reconcile to the approved payroll register and Finance journal.
- [ ] **UI-0771** Attendance, overtime, unpaid leave, loans, and other HR inputs reconcile to payroll outputs.
- [ ] **UI-0772** Statutory compliance status reconciles to return submissions, payments, and authority references.
- [ ] **UI-0773** All dashboard figures drill down to accurate supporting schedules.
- [ ] **UI-0774** Historical monthly reports reproduce the approved snapshot and version.
- [ ] **UI-0775** Confidential reports cannot be viewed or exported by unauthorised users.
- [ ] **UI-0776** PDF, Excel/CSV, and on-screen totals are consistent.
- [ ] **UI-0777** Report generation completes within the agreed service level at production data volume.
- [ ] **UI-0778** HR and Management can sign off the monthly pack electronically with comments and actions.

---

# Stakeholder Monthly-Report Addendum

These requirements are mandatory UI acceptance items derived from the stakeholder comments and the clarified Appendix D requirements. They must be tested through the UI with real fixture records, role restrictions, expected outputs, and evidence.

## Sick-note and recurring sickness register

- [ ] **STAKE-01** Select a month and display a consolidated sick-note register linked to employee and absence records.
- [ ] **STAKE-02** Show employee number, employee name, organisation dimensions, sickness dates, days/hours, note receipt date, document status, verification status, approval status, and authorised reviewer.
- [ ] **STAKE-03** Show monthly and YTD occurrence counts, total sick days/hours, repeat occurrences, trends, and configured threshold exceptions.
- [ ] **STAKE-04** Verify restricted sensitive-detail access and supporting-document linkage.

## Probation monitoring and notifications

- [ ] **STAKE-05** Display employees approaching three-month probation end, days remaining, reviewer, objectives/review status, and overdue decisions.
- [ ] **STAKE-06** Trigger and display reminder history using the stakeholder-approved schedule.
- [ ] **STAKE-07** Complete Confirm, Extend, and End Employment outcomes with reason, approval, effective date, extension period, and new end date.
- [ ] **STAKE-08** Verify escalation and access control for probation records.

## Monthly overtime comparison

- [ ] **STAKE-09** Display current and prior month overtime hours and ZMW cost by employee and organisation dimension.
- [ ] **STAKE-10** Display amount variance, percentage variance, and Increasing/Decreasing/No change indicator.
- [ ] **STAKE-11** Separate approved, rejected, pending, and paid overtime and reconcile approved overtime to payroll-paid overtime.
- [ ] **STAKE-12** Display highest overtime employees/departments, repeated or abnormal patterns, and budget/approved-limit comparison if confirmed.

## Attendance exceptions

- [ ] **STAKE-13** Display late arrivals and early departures with date, scheduled time, actual time, minutes/hours, reason, approval status, and manager action.
- [ ] **STAKE-14** Display missed clock records, no-shows, unauthorised absences, approved leave, and authorised absences separately.
- [ ] **STAKE-15** Display repeated lateness/absence patterns, organisation dimensions, month-on-month trends, and outstanding manager actions.

## Stakeholder configuration decisions

- [ ] **STAKE-16** Confirm the probation alert schedule; recommended baseline is 30, 14, and 7 days before expiry, then daily after expiry.
- [ ] **STAKE-17** Confirm whether overtime must also be compared with budget or approved limits.
- [ ] **STAKE-18** Confirm the policy threshold for repeated sickness or sick leave.

---

# UI Execution Register

| ID | Source line | Requirement | UI status | Screen/route and role | Test data / expected result | Evidence | Defect / severity / owner / retest |
|---|---:|---|---|---|---|---|---|
| UI-0001 | 15 | Not tested | Not tested |  |  |  |  |
| UI-0002 | 16 | Passed | Not tested |  |  |  |  |
| UI-0003 | 17 | Failed — defect logged | Not tested |  |  |  |  |
| UI-0004 | 18 | Not applicable — reason documented | Not tested |  |  |  |  |
| UI-0005 | 19 | Retest passed | Not tested |  |  |  |  |
| UI-0006 | 48 | All P0 tests have passed. | Not tested |  |  |  |  |
| UI-0007 | 49 | No unresolved Critical or High payroll defects remain. | Not tested |  |  |  |  |
| UI-0008 | 50 | Opening employee balances and year-to-date values reconcile to the legacy/source system. | Not tested |  |  |  |  |
| UI-0009 | 51 | At least two parallel payroll runs reconcile to approved expected results. | Not tested |  |  |  |  |
| UI-0010 | 52 | Statutory calculations and returns have been independently verified. | Not tested |  |  |  |  |
| UI-0011 | 53 | Bank/payment output has been validated by Finance and, where possible, the bank/payment provider. | Not tested |  |  |  |  |
| UI-0012 | 54 | Payroll journals reconcile to the payroll register and have been accepted by Finance. | Not tested |  |  |  |  |
| UI-0013 | 55 | Payslips reconcile to the payroll register and payment file. | Not tested |  |  |  |  |
| UI-0014 | 56 | Access, segregation of duties, approvals, audit logs, backup, and recovery have been tested. | Not tested |  |  |  |  |
| UI-0015 | 57 | HR, Payroll, Finance, IT, and Management have signed off. | Not tested |  |  |  |  |
| UI-0016 | 67 | Legal entity name is captured exactly as registered. | Not tested |  |  |  |  |
| UI-0017 | 68 | Trading name, registration number, TPIN, employer statutory identifiers, and contact information are captured. | Not tested |  |  |  |  |
| UI-0018 | 69 | Registered address and operational addresses are maintained. | Not tested |  |  |  |  |
| UI-0019 | 70 | Employer bank/payment accounts are captured securely. | Not tested |  |  |  |  |
| UI-0020 | 71 | Payroll signatories and authorised approvers are recorded. | Not tested |  |  |  |  |
| UI-0021 | 72 | Multiple legal entities can be separated where applicable. | Not tested |  |  |  |  |
| UI-0022 | 73 | Employees cannot accidentally be paid or reported under the wrong legal entity. | Not tested |  |  |  |  |
| UI-0023 | 74 | Employer statutory registrations can be effective-dated and changed without rewriting historical payroll. | Not tested |  |  |  |  |
| UI-0024 | 78 | Branches, departments, divisions, units, teams, cost centres, projects, and work locations can be configured. | Not tested |  |  |  |  |
| UI-0025 | 79 | Each employee is assigned to the correct payroll-relevant organisation dimensions. | Not tested |  |  |  |  |
| UI-0026 | 80 | Transfers are effective-dated and do not alter historical reporting. | Not tested |  |  |  |  |
| UI-0027 | 81 | Payroll can be processed, filtered, reviewed, and reported by entity, branch, department, location, project, and cost centre. | Not tested |  |  |  |  |
| UI-0028 | 82 | Inactive structures cannot be used for new transactions but remain visible historically. | Not tested |  |  |  |  |
| UI-0029 | 86 | Monthly, fortnightly, weekly, and other required pay frequencies are supported or explicitly ruled out. | Not tested |  |  |  |  |
| UI-0030 | 87 | Payroll groups are configurable by entity, employment category, branch, or frequency. | Not tested |  |  |  |  |
| UI-0031 | 88 | Each pay calendar defines period start, period end, cut-off date, pay date, and statutory period. | Not tested |  |  |  |  |
| UI-0032 | 89 | Weekends and public holidays are considered when setting payment dates. | Not tested |  |  |  |  |
| UI-0033 | 90 | Payroll periods move through controlled statuses: Draft → Input → Calculation → Validation → Approval → Payment → Posting → Closed. | Not tested |  |  |  |  |
| UI-0034 | 91 | Only authorised users can reopen a closed period. | Not tested |  |  |  |  |
| UI-0035 | 92 | Reopening requires a reason, approval, and audit trail. | Not tested |  |  |  |  |
| UI-0036 | 93 | Future periods can be prepared without changing a closed period. | Not tested |  |  |  |  |
| UI-0037 | 94 | Overlapping or missing pay periods are prevented. | Not tested |  |  |  |  |
| UI-0038 | 98 | Base/payroll currency is configured as ZMW where applicable. | Not tested |  |  |  |  |
| UI-0039 | 99 | Foreign currency earnings or deductions are supported where required. | Not tested |  |  |  |  |
| UI-0040 | 100 | Exchange-rate source, date, approval, and rounding rules are controlled. | Not tested |  |  |  |  |
| UI-0041 | 101 | Currency precision and rounding are applied consistently at employee, component, register, journal, return, and payment levels. | Not tested |  |  |  |  |
| UI-0042 | 102 | The system documents how one-ngwee rounding differences are handled. | Not tested |  |  |  |  |
| UI-0043 | 110 | Unique employee number is automatically or controllably assigned. | Not tested |  |  |  |  |
| UI-0044 | 111 | Full legal names match the employee's identity documents. | Not tested |  |  |  |  |
| UI-0045 | 112 | NRC/passport number, nationality, date of birth, sex/gender where legally required, and identity expiry date are captured. | Not tested |  |  |  |  |
| UI-0046 | 113 | TPIN/tax identifier is captured and validated where required. | Not tested |  |  |  |  |
| UI-0047 | 114 | NAPSA/social security membership number is captured and validated. | Not tested |  |  |  |  |
| UI-0048 | 115 | NHIMA/health insurance identifier is captured where required. | Not tested |  |  |  |  |
| UI-0049 | 116 | Employee KYC completeness is checked before payroll activation. | Not tested |  |  |  |  |
| UI-0050 | 117 | Duplicate employees are detected using employee number, NRC/passport, tax number, pension number, bank account, phone, or email. | Not tested |  |  |  |  |
| UI-0051 | 118 | Changes to legal names and identifiers are audited and require appropriate approval. | Not tested |  |  |  |  |
| UI-0052 | 119 | Missing or invalid statutory data appears on a payroll-readiness exception report. | Not tested |  |  |  |  |
| UI-0053 | 123 | Employment type is captured: permanent, fixed-term, temporary, casual, intern, expatriate, consultant, or other configured type. | Not tested |  |  |  |  |
| UI-0054 | 124 | Payroll eligibility is clearly distinguished from worker/employee status. | Not tested |  |  |  |  |
| UI-0055 | 125 | Hire date, confirmation date, contract start/end dates, probation dates, and expected retirement date are captured. | Not tested |  |  |  |  |
| UI-0056 | 126 | Position, job title, grade, step, department, branch, location, supervisor, and cost centre are effective-dated. | Not tested |  |  |  |  |
| UI-0057 | 127 | Full-time equivalent, scheduled hours, workdays, and standard hours are recorded. | Not tested |  |  |  |  |
| UI-0058 | 128 | Employee status is controlled: Pre-hire, Active, Suspended, On Leave, Terminating, Terminated, Retired, or Deceased. | Not tested |  |  |  |  |
| UI-0059 | 129 | Status changes trigger the correct payroll action and do not erase history. | Not tested |  |  |  |  |
| UI-0060 | 130 | Backdated employment changes are flagged for recalculation or arrears. | Not tested |  |  |  |  |
| UI-0061 | 134 | Signed contract and compensation approval documents can be attached or referenced. | Not tested |  |  |  |  |
| UI-0062 | 135 | Base salary, wage rate, hourly/daily rate, grade rate, and effective date are captured. | Not tested |  |  |  |  |
| UI-0063 | 136 | Salary basis clearly identifies monthly, hourly, daily, annual, piece-rate, commission, or other method. | Not tested |  |  |  |  |
| UI-0064 | 137 | Gross-up arrangements are supported where applicable. | Not tested |  |  |  |  |
| UI-0065 | 138 | Recurring allowances, benefits, deductions, employer contributions, and reimbursements are assigned with start/end dates. | Not tested |  |  |  |  |
| UI-0066 | 139 | Salary changes require effective date, reason, initiator, approval, and supporting evidence. | Not tested |  |  |  |  |
| UI-0067 | 140 | Future-dated salary changes are supported. | Not tested |  |  |  |  |
| UI-0068 | 141 | The system prevents conflicting or overlapping salary records. | Not tested |  |  |  |  |
| UI-0069 | 142 | Salary history is immutable and reportable. | Not tested |  |  |  |  |
| UI-0070 | 146 | Payment method supports bank transfer, mobile money, cash, cheque, or other approved method. | Not tested |  |  |  |  |
| UI-0071 | 147 | Bank name, branch, account name, account number, account type, and payment reference are captured. | Not tested |  |  |  |  |
| UI-0072 | 148 | Mobile money provider and verified mobile number are captured where used. | Not tested |  |  |  |  |
| UI-0073 | 149 | Split payments to multiple accounts are supported only if the organisation permits them. | Not tested |  |  |  |  |
| UI-0074 | 150 | Payment details are masked from unauthorised users. | Not tested |  |  |  |  |
| UI-0075 | 151 | Changes to payment details require re-authentication, independent approval, and notification to the employee. | Not tested |  |  |  |  |
| UI-0076 | 152 | Duplicate bank/mobile accounts across employees are flagged for review. | Not tested |  |  |  |  |
| UI-0077 | 153 | Invalid, missing, inactive, or unverified payment details block or hold payment as configured. | Not tested |  |  |  |  |
| UI-0078 | 154 | Cash/cheque payroll produces a controlled payment list and acknowledgement record. | Not tested |  |  |  |  |
| UI-0079 | 158 | Dependants and beneficiaries can be recorded where needed for benefits or statutory reporting. | Not tested |  |  |  |  |
| UI-0080 | 159 | Relationship, date of birth, allocation percentage, and supporting documents are maintained. | Not tested |  |  |  |  |
| UI-0081 | 160 | Beneficiary allocations are validated where applicable. | Not tested |  |  |  |  |
| UI-0082 | 161 | Sensitive dependant information is access-controlled. | Not tested |  |  |  |  |
| UI-0083 | 165 | Every employee has a visible payroll-readiness indicator. | Not tested |  |  |  |  |
| UI-0084 | 166 | Readiness identifies missing identity, employment, compensation, tax, statutory, attendance, and payment data. | Not tested |  |  |  |  |
| UI-0085 | 167 | Employees with unresolved blocking errors cannot silently enter payroll. | Not tested |  |  |  |  |
| UI-0086 | 168 | Authorised overrides require reason, evidence, approval, and audit trail. | Not tested |  |  |  |  |
| UI-0087 | 169 | A payroll population report shows included, excluded, held, new, terminated, and changed employees. | Not tested |  |  |  |  |
| UI-0088 | 177 | Basic salary/wage is configured. | Not tested |  |  |  |  |
| UI-0089 | 178 | Overtime, shift, acting, leave, housing, transport, meal, airtime, hardship, responsibility, travel, commission, bonus, incentive, gratuity, severance, notice, arrears, back pay, and other required earnings can be configured. | Not tested |  |  |  |  |
| UI-0090 | 179 | Each earning defines whether it is recurring or once-off. | Not tested |  |  |  |  |
| UI-0091 | 180 | Each earning defines taxable, pensionable, health-insurance, workers-compensation, and other statutory treatment. | Not tested |  |  |  |  |
| UI-0092 | 181 | Each earning defines whether it affects gross pay, net pay, employer cost, or information-only totals. | Not tested |  |  |  |  |
| UI-0093 | 182 | Each earning has eligibility rules, formula, rate/unit, effective dates, proration, rounding, GL mapping, and reporting category. | Not tested |  |  |  |  |
| UI-0094 | 183 | Negative earnings are prevented unless an approved correction process is used. | Not tested |  |  |  |  |
| UI-0095 | 184 | Earnings can be limited by grade, job, location, employment type, or policy. | Not tested |  |  |  |  |
| UI-0096 | 188 | PAYE, employee NAPSA, employee NHIMA, salary advances, employee loans, pension, union dues, insurance, medical aid, savings, garnishments/court orders, staff purchases, absence deductions, and other required deductions can be configured. | Not tested |  |  |  |  |
| UI-0097 | 189 | Each deduction defines pre-tax or post-tax treatment and statutory priority. | Not tested |  |  |  |  |
| UI-0098 | 190 | Fixed amount, percentage, balance-based, instalment-based, and formula deductions are supported. | Not tested |  |  |  |  |
| UI-0099 | 191 | Start date, end date, total balance, instalment, frequency, and priority are captured. | Not tested |  |  |  |  |
| UI-0100 | 192 | Protected-pay/minimum-net-pay rules are configurable. | Not tested |  |  |  |  |
| UI-0101 | 193 | Deduction caps and insufficient-net-pay rules are configurable. | Not tested |  |  |  |  |
| UI-0102 | 194 | Deferred deductions automatically carry forward where policy permits. | Not tested |  |  |  |  |
| UI-0103 | 195 | The system distinguishes employee deductions from employer liabilities. | Not tested |  |  |  |  |
| UI-0104 | 196 | Deduction beneficiaries and remittance accounts are maintained. | Not tested |  |  |  |  |
| UI-0105 | 197 | A deduction cannot continue after its end date or fully recovered balance. | Not tested |  |  |  |  |
| UI-0106 | 201 | Employer NAPSA, employer NHIMA where applicable, workers' compensation, pension, insurance, levy, benefit, and other employer costs can be configured. | Not tested |  |  |  |  |
| UI-0107 | 202 | Employer cost does not incorrectly reduce employee net pay. | Not tested |  |  |  |  |
| UI-0108 | 203 | Employer costs are reportable by employee and organisation dimension. | Not tested |  |  |  |  |
| UI-0109 | 204 | Employer liabilities are mapped separately in accounting outputs. | Not tested |  |  |  |  |
| UI-0110 | 208 | Formulas support brackets, percentages, caps, thresholds, minimums, maximums, quantities, rates, dates, and conditional rules. | Not tested |  |  |  |  |
| UI-0111 | 209 | Rules are effective-dated and historical versions are preserved. | Not tested |  |  |  |  |
| UI-0112 | 210 | Configuration changes require maker-checker approval. | Not tested |  |  |  |  |
| UI-0113 | 211 | Rule changes cannot alter previously closed payroll results. | Not tested |  |  |  |  |
| UI-0114 | 212 | Formula dependencies and order of calculation are explicit. | Not tested |  |  |  |  |
| UI-0115 | 213 | Circular formula dependencies are detected. | Not tested |  |  |  |  |
| UI-0116 | 214 | Administrators can test formulas in a sandbox before activation. | Not tested |  |  |  |  |
| UI-0117 | 215 | A configuration comparison report shows what changed between versions. | Not tested |  |  |  |  |
| UI-0118 | 216 | Every active rule has an owner, legal/policy source, approval, effective date, and review date. | Not tested |  |  |  |  |
| UI-0119 | 220 | New hires, terminations, unpaid leave, suspension, and salary changes can be prorated. | Not tested |  |  |  |  |
| UI-0120 | 221 | Calendar-day, working-day, fixed-30-day, hourly, and policy-specific methods are supported. | Not tested |  |  |  |  |
| UI-0121 | 222 | The chosen method is consistent and documented by component. | Not tested |  |  |  |  |
| UI-0122 | 223 | Mid-period transfers and salary increases calculate correctly. | Not tested |  |  |  |  |
| UI-0123 | 224 | Leap years, short months, weekends, holidays, and overnight shifts are handled. | Not tested |  |  |  |  |
| UI-0124 | 225 | Proration produces transparent calculation details on the audit report. | Not tested |  |  |  |  |
| UI-0125 | 235 | PAYE tax bands, rates, tax-free threshold, credits/reliefs, and effective dates are configurable. | Not tested |  |  |  |  |
| UI-0126 | 236 | Taxable and non-taxable treatment is defined for every earning and benefit. | Not tested |  |  |  |  |
| UI-0127 | 237 | Statutory deductions/allowances applied before PAYE are correctly ordered. | Not tested |  |  |  |  |
| UI-0128 | 238 | Regular earnings, irregular earnings, bonuses, arrears, terminal payments, benefits in kind, and expatriate scenarios are correctly treated. | Not tested |  |  |  |  |
| UI-0129 | 239 | Cumulative or period-based calculation method follows approved Zambian rules and organisational requirements. | Not tested |  |  |  |  |
| UI-0130 | 240 | Mid-year hire and opening year-to-date values calculate correctly. | Not tested |  |  |  |  |
| UI-0131 | 241 | Tax adjustments and prior-period corrections require approval and remain auditable. | Not tested |  |  |  |  |
| UI-0132 | 242 | PAYE return/export contains the required employer, employee, period, emolument, deduction, and tax fields. | Not tested |  |  |  |  |
| UI-0133 | 243 | PAYE liability report reconciles to employee deductions, return, payment, and GL liability. | Not tested |  |  |  |  |
| UI-0134 | 244 | Due-date reminders, return status, submission reference, payment reference, and proof of payment can be tracked. | Not tested |  |  |  |  |
| UI-0135 | 248 | Employee and employer contribution rates are separately configurable. | Not tested |  |  |  |  |
| UI-0136 | 249 | Contribution ceiling/base and effective dates are configurable by statutory year. | Not tested |  |  |  |  |
| UI-0137 | 250 | Pensionable earnings classification is defined for every component. | Not tested |  |  |  |  |
| UI-0138 | 251 | New hires, terminated staff, employees at/above ceilings, arrears, top-ups, and nil periods calculate correctly. | Not tested |  |  |  |  |
| UI-0139 | 252 | Employee identity/KYC required for NAPSA submission is validated before payroll closure. | Not tested |  |  |  |  |
| UI-0140 | 253 | NAPSA return/export supports the current accepted format or integration. | Not tested |  |  |  |  |
| UI-0141 | 254 | Return validation errors can be imported or recorded and assigned for resolution. | Not tested |  |  |  |  |
| UI-0142 | 255 | Original returns, top-up/correction returns, nil returns, tracking references, status, and payment are traceable. | Not tested |  |  |  |  |
| UI-0143 | 256 | Employee contribution + employer contribution = return total = payment total = GL liability movement. | Not tested |  |  |  |  |
| UI-0144 | 260 | Employee and employer rules, contribution bases, rates, ceilings, exemptions, and effective dates are configurable. | Not tested |  |  |  |  |
| UI-0145 | 261 | Contributory earnings are classified correctly. | Not tested |  |  |  |  |
| UI-0146 | 262 | Employee and employer portions are reported separately. | Not tested |  |  |  |  |
| UI-0147 | 263 | Required return/export is generated in the accepted format. | Not tested |  |  |  |  |
| UI-0148 | 264 | Registration/member exceptions are reported. | Not tested |  |  |  |  |
| UI-0149 | 265 | Contribution register, return, payment, and GL liability reconcile. | Not tested |  |  |  |  |
| UI-0150 | 269 | Workers' compensation classification, assessable earnings, industry/risk rate, period, and employer cost are configurable. | Not tested |  |  |  |  |
| UI-0151 | 270 | Annual or periodic payroll earnings summaries required for returns can be produced. | Not tested |  |  |  |  |
| UI-0152 | 271 | Skills development levy or other applicable employer levy can be configured without affecting employee net pay incorrectly. | Not tested |  |  |  |  |
| UI-0153 | 272 | Pension, medical, union, court-ordered, or sector-specific obligations can be configured. | Not tested |  |  |  |  |
| UI-0154 | 273 | Statutory changes can be loaded prospectively and tested before the effective date. | Not tested |  |  |  |  |
| UI-0155 | 274 | Compliance dashboard shows due, prepared, approved, submitted, paid, accepted, rejected, overdue, corrected, and reconciled statuses. | Not tested |  |  |  |  |
| UI-0156 | 282 | Work schedules and shift rosters are assigned with effective dates. | Not tested |  |  |  |  |
| UI-0157 | 283 | Clock-in/out supports the approved source: biometric, device, web, mobile, QR, geolocation, or manual entry. | Not tested |  |  |  |  |
| UI-0158 | 284 | Missing punches, duplicates, late arrival, early departure, absence, and excess hours are detected. | Not tested |  |  |  |  |
| UI-0159 | 285 | Overnight, weekend, holiday, split, and cross-period shifts are handled correctly. | Not tested |  |  |  |  |
| UI-0160 | 286 | Grace periods, breaks, rounding, and paid/unpaid time rules are configured. | Not tested |  |  |  |  |
| UI-0161 | 287 | Overtime eligibility, rate multipliers, caps, and authorisation rules are configured. | Not tested |  |  |  |  |
| UI-0162 | 288 | Overtime must be approved before payroll cut-off. | Not tested |  |  |  |  |
| UI-0163 | 289 | Approved hours/units flow to payroll once and cannot be duplicated. | Not tested |  |  |  |  |
| UI-0164 | 290 | Rejected or unapproved time does not silently enter payroll. | Not tested |  |  |  |  |
| UI-0165 | 291 | Manual adjustments require reason, evidence, and approval. | Not tested |  |  |  |  |
| UI-0166 | 292 | Attendance is locked when imported into a payroll run. | Not tested |  |  |  |  |
| UI-0167 | 293 | Corrections after cut-off follow arrears/adjustment workflow. | Not tested |  |  |  |  |
| UI-0168 | 294 | Payroll can trace each time-based amount back to source shifts/timesheets. | Not tested |  |  |  |  |
| UI-0169 | 295 | Attendance-to-payroll reconciliation reports scheduled, worked, approved, paid, and exception hours. | Not tested |  |  |  |  |
| UI-0170 | 299 | Leave types define paid, partially paid, unpaid, encashable, and non-payroll-impacting treatment. | Not tested |  |  |  |  |
| UI-0171 | 300 | Accrual, carry-forward, expiry, maximum balance, eligibility, and service rules are configured. | Not tested |  |  |  |  |
| UI-0172 | 301 | Leave requests follow required approvals. | Not tested |  |  |  |  |
| UI-0173 | 302 | Approved paid leave feeds correct leave pay where applicable. | Not tested |  |  |  |  |
| UI-0174 | 303 | Unpaid leave creates correct payroll deductions and proration. | Not tested |  |  |  |  |
| UI-0175 | 304 | Half-days, hourly leave, cross-period leave, and leave spanning termination are handled. | Not tested |  |  |  |  |
| UI-0176 | 305 | Leave cancellation after payroll cut-off creates a controlled adjustment. | Not tested |  |  |  |  |
| UI-0177 | 306 | Leave encashment and final leave settlement calculate correctly. | Not tested |  |  |  |  |
| UI-0178 | 307 | Leave balances reconcile before and after payroll. | Not tested |  |  |  |  |
| UI-0179 | 308 | Payroll can trace leave-related amounts back to approved leave records. | Not tested |  |  |  |  |
| UI-0180 | 312 | Benefits have eligibility, start/end date, employee amount, employer amount, tax/statutory treatment, and GL mapping. | Not tested |  |  |  |  |
| UI-0181 | 313 | Benefits in kind are supported where required. | Not tested |  |  |  |  |
| UI-0182 | 314 | Expense reimbursements are separated from taxable earnings unless policy/law requires otherwise. | Not tested |  |  |  |  |
| UI-0183 | 315 | Approved claims enter the intended pay period once only. | Not tested |  |  |  |  |
| UI-0184 | 316 | Commission and incentive formulas use approved source data and versioned rules. | Not tested |  |  |  |  |
| UI-0185 | 317 | Bonus pools, eligibility, performance result, approval, and payment timing are controlled. | Not tested |  |  |  |  |
| UI-0186 | 318 | Variable-pay uploads validate duplicates, employee eligibility, component, period, amount, and authorisation. | Not tested |  |  |  |  |
| UI-0187 | 319 | Mass uploads show validation errors before committing. | Not tested |  |  |  |  |
| UI-0188 | 320 | Imported values are traceable to file, user, timestamp, and approval. | Not tested |  |  |  |  |
| UI-0189 | 324 | Loan principal, disbursement, interest/fees, instalment, start date, end date, outstanding balance, and recovery priority are maintained. | Not tested |  |  |  |  |
| UI-0190 | 325 | Payroll receives only approved and disbursed loans/advances. | Not tested |  |  |  |  |
| UI-0191 | 326 | Recovery starts in the correct period. | Not tested |  |  |  |  |
| UI-0192 | 327 | Partial recovery, missed recovery, overpayment, early settlement, write-off, restructure, and termination are handled. | Not tested |  |  |  |  |
| UI-0193 | 328 | Payroll cannot deduct more than the outstanding balance. | Not tested |  |  |  |  |
| UI-0194 | 329 | Insufficient net pay follows configured priority and carry-forward rules. | Not tested |  |  |  |  |
| UI-0195 | 330 | New disbursements and payroll deductions reconcile to the loan/subledger. | Not tested |  |  |  |  |
| UI-0196 | 331 | Third-party deductions produce beneficiary remittance schedules. | Not tested |  |  |  |  |
| UI-0197 | 332 | Deduction balances and employee statements are available. | Not tested |  |  |  |  |
| UI-0198 | 338 | New hires enter the correct payroll based on effective hire date and readiness status. | Not tested |  |  |  |  |
| UI-0199 | 339 | First salary is prorated according to policy. | Not tested |  |  |  |  |
| UI-0200 | 340 | Opening balances and prior earnings are captured where required. | Not tested |  |  |  |  |
| UI-0201 | 341 | New-hire report shows compensation, payment, statutory, and document completeness. | Not tested |  |  |  |  |
| UI-0202 | 345 | Promotions, salary reviews, acting appointments, transfers, grade changes, and cost-centre changes are effective-dated. | Not tested |  |  |  |  |
| UI-0203 | 346 | Backdated changes calculate arrears/differences transparently. | Not tested |  |  |  |  |
| UI-0204 | 347 | Approval occurs before payroll inclusion. | Not tested |  |  |  |  |
| UI-0205 | 348 | Historical payroll retains the old organisation and compensation values. | Not tested |  |  |  |  |
| UI-0206 | 352 | Paid and unpaid suspension rules are distinct. | Not tested |  |  |  |  |
| UI-0207 | 353 | Suspension dates affect pay only as authorised. | Not tested |  |  |  |  |
| UI-0208 | 354 | Reinstatement and back pay are supported. | Not tested |  |  |  |  |
| UI-0209 | 358 | Resignation, dismissal, redundancy, retirement, death, contract expiry, and other exit reasons are supported. | Not tested |  |  |  |  |
| UI-0210 | 359 | Last working day, payroll end date, benefits end date, and payment date are distinguished. | Not tested |  |  |  |  |
| UI-0211 | 360 | Final pay includes applicable salary, leave, overtime, commission, gratuity, severance, notice, arrears, deductions, loans, tax, and statutory contributions. | Not tested |  |  |  |  |
| UI-0212 | 361 | Recoverable assets or staff obligations can be included only through approved deductions. | Not tested |  |  |  |  |
| UI-0213 | 362 | Exit clearance can place final pay on hold without losing the calculation. | Not tested |  |  |  |  |
| UI-0214 | 363 | Final payment requires HR and Finance approval. | Not tested |  |  |  |  |
| UI-0215 | 364 | Former employees cannot re-enter regular payroll accidentally. | Not tested |  |  |  |  |
| UI-0216 | 365 | Final payslip, tax/statutory outputs, payment record, and certificate/statement requirements are available. | Not tested |  |  |  |  |
| UI-0217 | 373 | Payroll dashboard shows current period, group, cut-off, pay date, status, owners, and outstanding tasks. | Not tested |  |  |  |  |
| UI-0218 | 374 | A pre-payroll checklist identifies incomplete employee data and missing approvals. | Not tested |  |  |  |  |
| UI-0219 | 375 | New hires, leavers, salary changes, bank changes, overtime, leave, bonuses, loans, and one-off inputs are summarised. | Not tested |  |  |  |  |
| UI-0220 | 376 | Payroll input supports authorised manual entry and controlled bulk import. | Not tested |  |  |  |  |
| UI-0221 | 377 | Templates contain only valid employees and components. | Not tested |  |  |  |  |
| UI-0222 | 378 | Upload rejects unknown employees, invalid components, duplicate rows, invalid dates, wrong periods, and malformed amounts. | Not tested |  |  |  |  |
| UI-0223 | 379 | Upload preview displays additions, changes, warnings, and errors before posting. | Not tested |  |  |  |  |
| UI-0224 | 380 | Duplicate source transactions cannot be paid twice. | Not tested |  |  |  |  |
| UI-0225 | 381 | Cut-off locks upstream transactions for the payroll period or moves late items into an adjustment queue. | Not tested |  |  |  |  |
| UI-0226 | 382 | Exceptions are assigned to owners and tracked to resolution. | Not tested |  |  |  |  |
| UI-0227 | 386 | Payroll can calculate a full group, selected employees, or changed employees. | Not tested |  |  |  |  |
| UI-0228 | 387 | Calculation order is correct: earnings → statutory bases → employee deductions → employer contributions → net pay → employer cost. | Not tested |  |  |  |  |
| UI-0229 | 388 | Gross pay, taxable pay, pensionable pay, assessable pay, total deductions, net pay, and employer cost are separately available. | Not tested |  |  |  |  |
| UI-0230 | 389 | Recurring items, one-off items, proration, arrears, retroactive changes, and balances calculate correctly. | Not tested |  |  |  |  |
| UI-0231 | 390 | Calculating repeatedly without input changes produces identical results. | Not tested |  |  |  |  |
| UI-0232 | 391 | Recalculation does not duplicate inputs or deductions. | Not tested |  |  |  |  |
| UI-0233 | 392 | Calculation errors identify affected employees and reasons without hiding successful employees. | Not tested |  |  |  |  |
| UI-0234 | 393 | Calculation detail explains every amount, formula, rate, base, cap, and rounding result. | Not tested |  |  |  |  |
| UI-0235 | 394 | Zero-pay and negative-net-pay employees are flagged. | Not tested |  |  |  |  |
| UI-0236 | 395 | Unexpected gross/net changes above configurable thresholds are flagged. | Not tested |  |  |  |  |
| UI-0237 | 396 | Employees missing from or unexpectedly added to payroll are flagged. | Not tested |  |  |  |  |
| UI-0238 | 397 | Test/simulation runs do not create accounting, payment, statutory, or employee-facing transactions. | Not tested |  |  |  |  |
| UI-0239 | 401 | Current payroll can be compared with prior period, budget, expected payroll, and headcount. | Not tested |  |  |  |  |
| UI-0240 | 402 | Variances are shown by employee, component, branch, department, cost centre, and total. | Not tested |  |  |  |  |
| UI-0241 | 403 | Configurable thresholds flag significant value or percentage changes. | Not tested |  |  |  |  |
| UI-0242 | 404 | Joiners, leavers, zero-pay, negative-pay, unusually high pay, unusual overtime, missing bank details, duplicated accounts, and stopped deductions are reported. | Not tested |  |  |  |  |
| UI-0243 | 405 | Gross-to-net reconciliation is available for every employee and total payroll. | Not tested |  |  |  |  |
| UI-0244 | 406 | Payroll control totals reconcile employee results, payment totals, statutory totals, third-party remittances, employer costs, and journals. | Not tested |  |  |  |  |
| UI-0245 | 407 | Validation exceptions require resolution, accepted explanation, or authorised override. | Not tested |  |  |  |  |
| UI-0246 | 408 | Review comments and supporting documents are stored with the run. | Not tested |  |  |  |  |
| UI-0247 | 409 | Recalculation after correction creates a new result version and comparison. | Not tested |  |  |  |  |
| UI-0248 | 413 | Payroll follows maker-checker or multi-level approval. | Not tested |  |  |  |  |
| UI-0249 | 414 | The preparer cannot be the sole final approver. | Not tested |  |  |  |  |
| UI-0250 | 415 | Approvers see control totals, variances, exceptions, employee changes, statutory totals, payment total, and journal total. | Not tested |  |  |  |  |
| UI-0251 | 416 | Approval can be rejected with comments and returned for correction. | Not tested |  |  |  |  |
| UI-0252 | 417 | Any material change after approval invalidates approval and requires reapproval. | Not tested |  |  |  |  |
| UI-0253 | 418 | Approval records user, timestamp, decision, comments, and result version. | Not tested |  |  |  |  |
| UI-0254 | 419 | Emergency override is restricted, justified, independently approved, and audited. | Not tested |  |  |  |  |
| UI-0255 | 420 | Final approval locks calculation inputs and results. | Not tested |  |  |  |  |
| UI-0256 | 424 | Payment file/list is generated only from the finally approved payroll version. | Not tested |  |  |  |  |
| UI-0257 | 425 | Payment total exactly matches approved net-pay total, excluding documented holds. | Not tested |  |  |  |  |
| UI-0258 | 426 | Bank/payment output meets the provider's required format, field length, account validation, currency, reference, and control-total rules. | Not tested |  |  |  |  |
| UI-0259 | 427 | Payment filename, batch number, period, hash/control total, creator, approver, and creation time are recorded. | Not tested |  |  |  |  |
| UI-0260 | 428 | Payment file is encrypted or securely transmitted and cannot be accessed by unauthorised users. | Not tested |  |  |  |  |
| UI-0261 | 429 | Manual editing of generated payment files is prevented or detected. | Not tested |  |  |  |  |
| UI-0262 | 430 | Duplicate payment batches are prevented. | Not tested |  |  |  |  |
| UI-0263 | 431 | Held, rejected, returned, failed, cancelled, and reissued payments are handled without duplicating payroll expense or liability. | Not tested |  |  |  |  |
| UI-0264 | 432 | Payment status is tracked by employee and batch. | Not tested |  |  |  |  |
| UI-0265 | 433 | Bank confirmation/statement can be reconciled to the payment batch. | Not tested |  |  |  |  |
| UI-0266 | 434 | Cash/cheque payments require acknowledgement and outstanding-payment tracking. | Not tested |  |  |  |  |
| UI-0267 | 435 | Unclaimed or unpaid wages remain a controlled liability. | Not tested |  |  |  |  |
| UI-0268 | 439 | Payslip shows employer, employee, employee number, period, pay date, earnings, deductions, statutory deductions, net pay, and year-to-date totals as required. | Not tested |  |  |  |  |
| UI-0269 | 440 | Payslip values reconcile exactly to the approved payroll register. | Not tested |  |  |  |  |
| UI-0270 | 441 | Employer-only costs are shown only where policy permits and are clearly labelled. | Not tested |  |  |  |  |
| UI-0271 | 442 | Leave, loan, or benefit balances are shown only if accurate and approved for display. | Not tested |  |  |  |  |
| UI-0272 | 443 | Payslips are generated only after the appropriate approval/status. | Not tested |  |  |  |  |
| UI-0273 | 444 | Employees see only their own payslips. | Not tested |  |  |  |  |
| UI-0274 | 445 | Portal access and downloaded documents are protected. | Not tested |  |  |  |  |
| UI-0275 | 446 | Email delivery avoids exposing salary in an insecure message; secure link or protected attachment is used. | Not tested |  |  |  |  |
| UI-0276 | 447 | Delivery status and employee access/download can be logged appropriately. | Not tested |  |  |  |  |
| UI-0277 | 448 | Corrected payslips are versioned; superseded versions remain auditable. | Not tested |  |  |  |  |
| UI-0278 | 449 | Bulk download is restricted and audited. | Not tested |  |  |  |  |
| UI-0279 | 450 | Employee payroll queries can be logged, assigned, resolved, and linked to the relevant payslip/run. | Not tested |  |  |  |  |
| UI-0280 | 454 | Statutory returns are generated from the same approved payroll version. | Not tested |  |  |  |  |
| UI-0281 | 455 | Each return has period, version, preparer, reviewer, approval, submission date, reference, acceptance status, payment date, amount, and proof. | Not tested |  |  |  |  |
| UI-0282 | 456 | Rejected submissions and validation errors are tracked to resolution. | Not tested |  |  |  |  |
| UI-0283 | 457 | Corrected or top-up returns do not overwrite original submissions. | Not tested |  |  |  |  |
| UI-0284 | 458 | Payment amount reconciles to the accepted return and payroll liability. | Not tested |  |  |  |  |
| UI-0285 | 459 | Due-date reminders and escalation are configured. | Not tested |  |  |  |  |
| UI-0286 | 460 | Nil return process exists where applicable. | Not tested |  |  |  |  |
| UI-0287 | 461 | Compliance calendar and dashboard show all required employer obligations. | Not tested |  |  |  |  |
| UI-0288 | 462 | Statutory reports can be regenerated exactly for a closed historical period. | Not tested |  |  |  |  |
| UI-0289 | 466 | Every earning, deduction, employer cost, reimbursement, liability, and payment component has effective-dated GL mapping. | Not tested |  |  |  |  |
| UI-0290 | 467 | Journal dimensions include legal entity, branch, department, cost centre, project, and other required segments. | Not tested |  |  |  |  |
| UI-0291 | 468 | Payroll expense, employer contributions, employee liabilities, statutory liabilities, third-party liabilities, net-pay liability, and cash/bank entries are separated. | Not tested |  |  |  |  |
| UI-0292 | 469 | Journal debits equal credits. | Not tested |  |  |  |  |
| UI-0293 | 470 | Payroll register totals reconcile to journal lines. | Not tested |  |  |  |  |
| UI-0294 | 471 | Payment clears the net-pay liability correctly. | Not tested |  |  |  |  |
| UI-0295 | 472 | Statutory and third-party remittances clear the correct liabilities. | Not tested |  |  |  |  |
| UI-0296 | 473 | Loan/advance deductions reconcile to the relevant receivable/subledger. | Not tested |  |  |  |  |
| UI-0297 | 474 | Rounding differences are posted to an approved account and explained. | Not tested |  |  |  |  |
| UI-0298 | 475 | Journal can be reviewed before posting. | Not tested |  |  |  |  |
| UI-0299 | 476 | Posting requires Finance approval and produces a reference/status. | Not tested |  |  |  |  |
| UI-0300 | 477 | Duplicate journal posting is prevented. | Not tested |  |  |  |  |
| UI-0301 | 478 | Failed postings can be corrected and retried safely. | Not tested |  |  |  |  |
| UI-0302 | 479 | Reversals preserve the original reference and require approval. | Not tested |  |  |  |  |
| UI-0303 | 480 | Closed accounting periods are respected. | Not tested |  |  |  |  |
| UI-0304 | 481 | Payroll-to-GL reconciliation remains available by period and run. | Not tested |  |  |  |  |
| UI-0305 | 485 | Closure requires approved payroll, payment output, payslips, statutory outputs, journal, and completed reconciliations as configured. | Not tested |  |  |  |  |
| UI-0306 | 486 | Closed payroll is read-only. | Not tested |  |  |  |  |
| UI-0307 | 487 | Close action requires approval and records a final control pack. | Not tested |  |  |  |  |
| UI-0308 | 488 | Post-close corrections use off-cycle payroll, supplementary payroll, arrears, reversal, or next-period adjustment. | Not tested |  |  |  |  |
| UI-0309 | 489 | Corrections preserve the original result and create linked adjustment records. | Not tested |  |  |  |  |
| UI-0310 | 490 | Off-cycle payroll has separate approvals, payment batch, payslip, statutory treatment, and journal. | Not tested |  |  |  |  |
| UI-0311 | 491 | A closed payroll can be reproduced exactly from stored configuration, inputs, and result versions. | Not tested |  |  |  |  |
| UI-0312 | 499 | Payroll register — employee-level and summarised earnings, deductions, net pay, and employer costs. | Not tested |  |  |  |  |
| UI-0313 | 500 | Gross-to-net report — calculation bridge for each employee and total payroll. | Not tested |  |  |  |  |
| UI-0314 | 501 | Payroll summary — total gross, taxable, statutory bases, deductions, net pay, employer cost, and headcount. | Not tested |  |  |  |  |
| UI-0315 | 502 | Payroll input report — recurring and once-off inputs by source. | Not tested |  |  |  |  |
| UI-0316 | 503 | Payroll change report — all changes since prior run/period. | Not tested |  |  |  |  |
| UI-0317 | 504 | Variance report — current versus prior, expected, and budget. | Not tested |  |  |  |  |
| UI-0318 | 505 | New-hire report. | Not tested |  |  |  |  |
| UI-0319 | 506 | Termination/final-pay report. | Not tested |  |  |  |  |
| UI-0320 | 507 | Zero-pay, negative-net, minimum-net, and held-payment report. | Not tested |  |  |  |  |
| UI-0321 | 508 | Missing/invalid payroll data report. | Not tested |  |  |  |  |
| UI-0322 | 509 | Manual adjustment and override report. | Not tested |  |  |  |  |
| UI-0323 | 510 | Retroactive pay/arrears report. | Not tested |  |  |  |  |
| UI-0324 | 511 | Overtime and time-based earnings report. | Not tested |  |  |  |  |
| UI-0325 | 512 | Unpaid leave and absence deduction report. | Not tested |  |  |  |  |
| UI-0326 | 513 | Bonus, commission, and incentive report. | Not tested |  |  |  |  |
| UI-0327 | 514 | Employee loan/advance deduction and balance report. | Not tested |  |  |  |  |
| UI-0328 | 515 | Third-party deduction and remittance report. | Not tested |  |  |  |  |
| UI-0329 | 516 | Payment instruction and payment status report. | Not tested |  |  |  |  |
| UI-0330 | 517 | Payroll reconciliation/control totals report. | Not tested |  |  |  |  |
| UI-0331 | 518 | Payroll processing status and outstanding task report. | Not tested |  |  |  |  |
| UI-0332 | 539 | Monthly PAYE return/export is generated in the currently accepted ZRA format, including the applicable ITF/P16 fields. | Not tested |  |  |  |  |
| UI-0333 | 540 | Return identifies legal employer, TPIN, tax period, return type, currency, and authorised declaration details. | Not tested |  |  |  |  |
| UI-0334 | 541 | Employee schedule includes employee number, full legal name, employee TPIN, NRC/passport where required, gross emoluments, taxable emoluments, allowable/statutory deductions, tax relief where applicable, and PAYE deducted. | Not tested |  |  |  |  |
| UI-0335 | 542 | Employees with missing or invalid TPINs are reported before return generation. | Not tested |  |  |  |  |
| UI-0336 | 543 | Taxable and non-taxable earnings are separately reportable by component. | Not tested |  |  |  |  |
| UI-0337 | 544 | Regular earnings, bonuses, commissions, benefits in kind, arrears, leave payments, gratuity, terminal benefits, and other taxable emoluments can be separately disclosed where required. | Not tested |  |  |  |  |
| UI-0338 | 545 | Employee-level PAYE calculation report shows tax bands, rate, base, relief/deduction, tax before adjustment, adjustment, and final PAYE. | Not tested |  |  |  |  |
| UI-0339 | 546 | PAYE exception report identifies missing identifiers, negative tax, manual adjustments, unusual tax movements, and employees with taxable pay but zero PAYE. | Not tested |  |  |  |  |
| UI-0340 | 547 | Monthly PAYE liability report shows opening liability, current payroll deduction, adjustments, payments, penalties/interest recorded, and closing liability. | Not tested |  |  |  |  |
| UI-0341 | 548 | PAYE reconciliation compares payroll register, monthly return, payment/PRN or receipt, and GL control account. | Not tested |  |  |  |  |
| UI-0342 | 549 | Original, amended, replacement, or correction returns are versioned and linked. | Not tested |  |  |  |  |
| UI-0343 | 550 | PAYE submission reference, PRN/payment reference, acceptance status, submission date, payment date, proof, and preparer/approver are stored. | Not tested |  |  |  |  |
| UI-0344 | 551 | Late, missing, rejected, unpaid, underpaid, and overpaid periods appear on the compliance dashboard. | Not tested |  |  |  |  |
| UI-0345 | 552 | Annual/YTD employee earnings and PAYE statement can be produced where required. | Not tested |  |  |  |  |
| UI-0346 | 553 | Annual PAYE reconciliation confirms the sum of monthly returns equals payroll YTD and GL totals. | Not tested |  |  |  |  |
| UI-0347 | 557 | NAPSA monthly return/export follows the current iCARE template or approved API specification. | Not tested |  |  |  |  |
| UI-0348 | 558 | Return identifies employer account, contribution month/year, employee NAPSA/social security number or NRC as permitted, employee identity fields, gross/pensionable earnings, employee contribution, employer contribution, and total contribution. | Not tested |  |  |  |  |
| UI-0349 | 559 | Employee KYC exception report checks legal names, date of birth, NRC/passport, and membership/social security number against payroll requirements. | Not tested |  |  |  |  |
| UI-0350 | 560 | Contribution ceiling report identifies employees below, at, and above the applicable ceiling. | Not tested |  |  |  |  |
| UI-0351 | 561 | Pensionable earnings report explains which payroll components are included or excluded. | Not tested |  |  |  |  |
| UI-0352 | 562 | NAPSA employee contribution schedule is available by employee, branch, department, and period. | Not tested |  |  |  |  |
| UI-0353 | 563 | NAPSA employer contribution schedule is available separately from employee deductions. | Not tested |  |  |  |  |
| UI-0354 | 564 | NAPSA reconciliation proves employee contribution + employer contribution = return total = payment total = GL liability movement. | Not tested |  |  |  |  |
| UI-0355 | 565 | Normal monthly return, top-up return, correction return, contribution-without-return reconciliation, and nil return are supported where applicable. | Not tested |  |  |  |  |
| UI-0356 | 566 | Return validation summary shows valid records, invalid records, reasons, and correction status. | Not tested |  |  |  |  |
| UI-0357 | 567 | Tracking number/reference and statuses such as pending, failed verification, pending payment, completed, cancelled, or authority resolution are recordable. | Not tested |  |  |  |  |
| UI-0358 | 568 | Underpayment, overpayment, unmatched payment, undeclared month, and unpaid period reports are available. | Not tested |  |  |  |  |
| UI-0359 | 569 | Submission date, acceptance status, payment reference, payment date, proof, preparer, reviewer, and approver are stored. | Not tested |  |  |  |  |
| UI-0360 | 570 | Employee contribution history can be produced for member queries and audit support. | Not tested |  |  |  |  |
| UI-0361 | 574 | NHIMA monthly return/export follows the current accepted portal, template, or API format. | Not tested |  |  |  |  |
| UI-0362 | 575 | Return identifies employer, contribution period, employee/member identifier, contributory earnings, employee contribution, employer contribution, and total. | Not tested |  |  |  |  |
| UI-0363 | 576 | Employee and employer contribution schedules are available separately. | Not tested |  |  |  |  |
| UI-0364 | 577 | Contributory-earnings report shows included and excluded components. | Not tested |  |  |  |  |
| UI-0365 | 578 | Contribution ceiling, threshold, exemption, or special-category report is available where applicable. | Not tested |  |  |  |  |
| UI-0366 | 579 | Missing or invalid member/identity data appears on an exception report. | Not tested |  |  |  |  |
| UI-0367 | 580 | New registration, leaver, inactive member, and correction exceptions are identified. | Not tested |  |  |  |  |
| UI-0368 | 581 | NHIMA reconciliation proves employee contribution + employer contribution = return = payment = GL liability movement. | Not tested |  |  |  |  |
| UI-0369 | 582 | Submission reference, acceptance/rejection status, error details, payment reference, payment date, proof, preparer, reviewer, and approver are stored. | Not tested |  |  |  |  |
| UI-0370 | 583 | Corrected, replacement, top-up, and nil returns are versioned where applicable. | Not tested |  |  |  |  |
| UI-0371 | 587 | Assessable payroll/earnings schedule is produced for the required return or assessment period. | Not tested |  |  |  |  |
| UI-0372 | 588 | Schedule can group employees and earnings by legal entity, branch, location, occupation, work category, and risk classification where required. | Not tested |  |  |  |  |
| UI-0373 | 589 | Report separates assessable and non-assessable payroll components. | Not tested |  |  |  |  |
| UI-0374 | 590 | Employer assessment calculation shows assessable earnings, approved rate, adjustments, and resulting employer liability. | Not tested |  |  |  |  |
| UI-0375 | 591 | Employee joiners, leavers, headcount, and remuneration totals supporting the return are available. | Not tested |  |  |  |  |
| UI-0376 | 592 | Workers' Compensation return/export follows the currently accepted format. | Not tested |  |  |  |  |
| UI-0377 | 593 | Return amount reconciles to payroll earnings, employer-cost report, payment, and GL liability/expense. | Not tested |  |  |  |  |
| UI-0378 | 594 | Assessment notice, submission reference, payment reference, proof, and any adjustment are stored. | Not tested |  |  |  |  |
| UI-0379 | 598 | Skills development levy report shows the applicable emolument/levy base, exclusions, rate, adjustment, and liability. | Not tested |  |  |  |  |
| UI-0380 | 599 | Applicable levy return/export reconciles to payroll, payment, and GL. | Not tested |  |  |  |  |
| UI-0381 | 600 | Occupational pension schedule separates employee and employer portions and includes member/scheme identifiers. | Not tested |  |  |  |  |
| UI-0382 | 601 | Pension remittance report reconciles to payroll deductions, employer cost, remittance, and GL liability. | Not tested |  |  |  |  |
| UI-0383 | 602 | Union dues schedule identifies employee, union/member number, deduction, arrears, and remittance total. | Not tested |  |  |  |  |
| UI-0384 | 603 | Court order/garnishment report identifies protected case/reference data, ordered amount, deduction, balance, and remittance status. | Not tested |  |  |  |  |
| UI-0385 | 604 | Sector-specific return requirements can be configured without changing historical reports. | Not tested |  |  |  |  |
| UI-0386 | 605 | Every third-party schedule names the beneficiary, bank/remittance details, period, preparer, approver, payment status, and reference. | Not tested |  |  |  |  |
| UI-0387 | 609 | Dashboard shows each authority/obligation and every expected return period. | Not tested |  |  |  |  |
| UI-0388 | 610 | Status supports Not due, Due, Preparing, Awaiting approval, Submitted, Accepted, Rejected, Payment pending, Paid, Reconciled, Corrected, Overdue, and Not applicable. | Not tested |  |  |  |  |
| UI-0389 | 611 | Each obligation shows statutory due date, internal due date, responsible owner, reviewer, and escalation contact. | Not tested |  |  |  |  |
| UI-0390 | 612 | Automated reminders and escalation are generated before and after due dates. | Not tested |  |  |  |  |
| UI-0391 | 613 | Dashboard distinguishes return submission from payment; neither can imply the other is complete. | Not tested |  |  |  |  |
| UI-0392 | 614 | Missing return, missing payment, unpaid period, unmatched payment, rejected return, underpayment, overpayment, and outstanding correction are visible. | Not tested |  |  |  |  |
| UI-0393 | 615 | Compliance certificate, clearance, authority correspondence, assessment, penalty, waiver, or dispute documents can be attached. | Not tested |  |  |  |  |
| UI-0394 | 616 | Penalties and interest can be recorded separately from employee payroll deductions. | Not tested |  |  |  |  |
| UI-0395 | 617 | Management can view compliance by legal entity, authority, period, status, value, and risk. | Not tested |  |  |  |  |
| UI-0396 | 621 | Statutory reports are generated only from the approved payroll result version. | Not tested |  |  |  |  |
| UI-0397 | 622 | Every report shows employer, period, payroll run ID, version, generation timestamp, user, currency, and status. | Not tested |  |  |  |  |
| UI-0398 | 623 | Return/export control totals include employee count, record count, gross/contributory earnings, employee contribution/deduction, employer contribution, and total liability as applicable. | Not tested |  |  |  |  |
| UI-0399 | 624 | Report totals reconcile from employee detail to summary without spreadsheet manipulation. | Not tested |  |  |  |  |
| UI-0400 | 625 | The system validates mandatory fields, accepted formats, duplicate employees, invalid identifiers, negative amounts, and control-total differences before export. | Not tested |  |  |  |  |
| UI-0401 | 626 | Historical reports use the statutory rules and employee data effective in that historical period. | Not tested |  |  |  |  |
| UI-0402 | 627 | Regeneration of a closed-period report produces the same result unless it is clearly marked as a new corrected version. | Not tested |  |  |  |  |
| UI-0403 | 628 | Original and corrected returns remain available with reasons, approvals, and submission references. | Not tested |  |  |  |  |
| UI-0404 | 629 | Manual statutory adjustments require reason, supporting evidence, maker-checker approval, and separate reporting. | Not tested |  |  |  |  |
| UI-0405 | 630 | Sensitive statutory reports follow role-based access, export logging, retention, and data-protection rules. | Not tested |  |  |  |  |
| UI-0406 | 631 | Statutory audit pack contains calculation detail, employee schedule, return/export, validation report, approvals, submission response, payment proof, GL reconciliation, corrections, and correspondence. | Not tested |  |  |  |  |
| UI-0407 | 635 | Payroll journal detail and summary. | Not tested |  |  |  |  |
| UI-0408 | 636 | Payroll-to-GL reconciliation. | Not tested |  |  |  |  |
| UI-0409 | 637 | Net-pay liability reconciliation. | Not tested |  |  |  |  |
| UI-0410 | 638 | Statutory liability reconciliation by authority and period. | Not tested |  |  |  |  |
| UI-0411 | 639 | Third-party liability reconciliation. | Not tested |  |  |  |  |
| UI-0412 | 640 | Employer-cost report. | Not tested |  |  |  |  |
| UI-0413 | 641 | Payroll expense by entity, branch, department, cost centre, location, project, grade, and employee type. | Not tested |  |  |  |  |
| UI-0414 | 642 | Actual payroll versus budget/forecast. | Not tested |  |  |  |  |
| UI-0415 | 643 | Accrued payroll, bonus, leave, gratuity, or other provision report where configured. | Not tested |  |  |  |  |
| UI-0416 | 644 | Loan/advance receivable reconciliation. | Not tested |  |  |  |  |
| UI-0417 | 645 | Payment batch versus bank confirmation reconciliation. | Not tested |  |  |  |  |
| UI-0418 | 646 | Unpaid, rejected, returned, or unclaimed wages report. | Not tested |  |  |  |  |
| UI-0419 | 650 | Headcount and full-time-equivalent report reconciled to paid employees. | Not tested |  |  |  |  |
| UI-0420 | 651 | Compensation report by grade, job, department, gender where lawful, and location. | Not tested |  |  |  |  |
| UI-0421 | 652 | Salary history and salary-change report. | Not tested |  |  |  |  |
| UI-0422 | 653 | New hire, movement, promotion, and termination trends. | Not tested |  |  |  |  |
| UI-0423 | 654 | Overtime cost and utilisation trends. | Not tested |  |  |  |  |
| UI-0424 | 655 | Leave cost, leave balance, and absence trends. | Not tested |  |  |  |  |
| UI-0425 | 656 | Benefit enrolment and employer-cost report. | Not tested |  |  |  |  |
| UI-0426 | 657 | Payroll cost per employee and organisational unit. | Not tested |  |  |  |  |
| UI-0427 | 658 | Average pay, median pay, pay range, and compa-ratio where grade ranges exist. | Not tested |  |  |  |  |
| UI-0428 | 659 | Workforce cost trend and forecast. | Not tested |  |  |  |  |
| UI-0429 | 660 | Contract-expiry, probation, retirement, and recurring-payment expiry alerts. | Not tested |  |  |  |  |
| UI-0430 | 664 | Current and historical payslips. | Not tested |  |  |  |  |
| UI-0431 | 665 | Year-to-date earnings and deduction statement. | Not tested |  |  |  |  |
| UI-0432 | 666 | Loan/advance balance and deduction statement. | Not tested |  |  |  |  |
| UI-0433 | 667 | Leave balance and transaction statement. | Not tested |  |  |  |  |
| UI-0434 | 668 | Benefit and deduction summary. | Not tested |  |  |  |  |
| UI-0435 | 669 | Tax/statutory statement where required. | Not tested |  |  |  |  |
| UI-0436 | 670 | Final-pay statement for terminated employees. | Not tested |  |  |  |  |
| UI-0437 | 674 | Reports can filter by period, pay group, entity, branch, department, cost centre, location, grade, employment type, component, and employee. | Not tested |  |  |  |  |
| UI-0438 | 675 | Totals remain consistent between screen, PDF, Excel/CSV, return file, journal, and payment file. | Not tested |  |  |  |  |
| UI-0439 | 676 | Drill-down moves from summary to employee to calculation/source transaction. | Not tested |  |  |  |  |
| UI-0440 | 677 | Reports show run ID, version, period, generation time, user, filters, currency, and status. | Not tested |  |  |  |  |
| UI-0441 | 678 | Draft reports are clearly watermarked or labelled. | Not tested |  |  |  |  |
| UI-0442 | 679 | Closed-period reports cannot silently change. | Not tested |  |  |  |  |
| UI-0443 | 680 | Exports preserve leading zeros in identifiers and account numbers. | Not tested |  |  |  |  |
| UI-0444 | 681 | Large reports complete reliably and do not omit rows. | Not tested |  |  |  |  |
| UI-0445 | 682 | Access respects entity, branch, department, and salary-data restrictions. | Not tested |  |  |  |  |
| UI-0446 | 683 | Sensitive exports are logged and expire or are protected where possible. | Not tested |  |  |  |  |
| UI-0447 | 684 | Scheduled reports are delivered only to authorised recipients. | Not tested |  |  |  |  |
| UI-0448 | 685 | Empty, zero, negative, very large, and special-character values render correctly. | Not tested |  |  |  |  |
| UI-0449 | 686 | Report totals include explicit inclusion/exclusion rules. | Not tested |  |  |  |  |
| UI-0450 | 694 | Roles exist for HR Administrator, HR Manager, Payroll Preparer, Payroll Reviewer, Payroll Approver, Finance Reviewer, Payment Approver, Auditor, Employee, Manager, and System Administrator as needed. | Not tested |  |  |  |  |
| UI-0451 | 695 | Permissions distinguish view, create, edit, delete, import, calculate, approve, pay, post, reopen, export, and administer. | Not tested |  |  |  |  |
| UI-0452 | 696 | Salary, bank, tax, identity, disciplinary, and medical information have appropriate field-level restrictions. | Not tested |  |  |  |  |
| UI-0453 | 697 | Users see only authorised legal entities, branches, departments, or employees. | Not tested |  |  |  |  |
| UI-0454 | 698 | System administrators cannot silently change payroll results. | Not tested |  |  |  |  |
| UI-0455 | 699 | Payroll preparer cannot be the sole approver or payment releaser. | Not tested |  |  |  |  |
| UI-0456 | 700 | Bank-detail changes are independently approved. | Not tested |  |  |  |  |
| UI-0457 | 701 | Role assignment and privileged access require approval and periodic review. | Not tested |  |  |  |  |
| UI-0458 | 702 | Terminated/transferred users lose access promptly. | Not tested |  |  |  |  |
| UI-0459 | 703 | Emergency access is time-limited, approved, monitored, and reviewed. | Not tested |  |  |  |  |
| UI-0460 | 707 | Audit logs capture create, edit, delete/deactivate, import, calculation, approval, rejection, reopen, payment generation, payslip publication, return generation, journal posting, and export. | Not tested |  |  |  |  |
| UI-0461 | 708 | Log includes user, timestamp, action, record, before value, after value, reason, source, and approval where applicable. | Not tested |  |  |  |  |
| UI-0462 | 709 | Audit logs cannot be altered by ordinary administrators. | Not tested |  |  |  |  |
| UI-0463 | 710 | Salary, bank, statutory identifier, formula, rate, role, and payroll-result changes are easy to report. | Not tested |  |  |  |  |
| UI-0464 | 711 | Bulk imports identify every affected record. | Not tested |  |  |  |  |
| UI-0465 | 712 | API/integration changes record the calling system and request/reference ID. | Not tested |  |  |  |  |
| UI-0466 | 713 | Failed access and failed privileged actions are logged. | Not tested |  |  |  |  |
| UI-0467 | 714 | Audit logs follow approved retention policy. | Not tested |  |  |  |  |
| UI-0468 | 718 | Sensitive data is encrypted in transit and at rest. | Not tested |  |  |  |  |
| UI-0469 | 719 | Passwords, secrets, payment credentials, and integration keys are securely managed. | Not tested |  |  |  |  |
| UI-0470 | 720 | Multi-factor authentication is available for privileged/payroll users. | Not tested |  |  |  |  |
| UI-0471 | 721 | Sessions expire appropriately and sensitive actions require re-authentication. | Not tested |  |  |  |  |
| UI-0472 | 722 | Downloaded reports and payslips are protected from unauthorised access. | Not tested |  |  |  |  |
| UI-0473 | 723 | Non-production environments use masked or approved test data. | Not tested |  |  |  |  |
| UI-0474 | 724 | Data retention, archive, legal hold, and secure deletion rules are defined. | Not tested |  |  |  |  |
| UI-0475 | 725 | Employee consent/notice and access/correction processes are supported where required. | Not tested |  |  |  |  |
| UI-0476 | 726 | Security testing covers unauthorised salary access, IDOR, privilege escalation, export abuse, and file exposure. | Not tested |  |  |  |  |
| UI-0477 | 730 | Employee master, attendance, leave, LMS/loan, accounting, bank/payment, identity provider, notification, and statutory interfaces have defined ownership and contracts. | Not tested |  |  |  |  |
| UI-0478 | 731 | Integrations authenticate securely and use least privilege. | Not tested |  |  |  |  |
| UI-0479 | 732 | Every transaction has a unique idempotency/reference key. | Not tested |  |  |  |  |
| UI-0480 | 733 | Retries do not create duplicate employees, inputs, payments, returns, or journals. | Not tested |  |  |  |  |
| UI-0481 | 734 | Failed records enter a visible exception queue. | Not tested |  |  |  |  |
| UI-0482 | 735 | Partial batch success is reported accurately. | Not tested |  |  |  |  |
| UI-0483 | 736 | Interface totals and record counts reconcile source to destination. | Not tested |  |  |  |  |
| UI-0484 | 737 | Cut-off timing, time zones, date formats, encoding, and decimal precision are tested. | Not tested |  |  |  |  |
| UI-0485 | 738 | Integration changes are versioned and backward compatibility is managed. | Not tested |  |  |  |  |
| UI-0486 | 739 | Manual fallback procedures are documented and controlled. | Not tested |  |  |  |  |
| UI-0487 | 743 | Expected employee volume and five-year growth assumptions are documented. | Not tested |  |  |  |  |
| UI-0488 | 744 | Full payroll calculation completes within the agreed service level. | Not tested |  |  |  |  |
| UI-0489 | 745 | Recalculation, reports, bulk upload, payslip generation, and payment output perform acceptably at peak volume. | Not tested |  |  |  |  |
| UI-0490 | 746 | Simultaneous HR, payroll, manager, and employee users do not corrupt or lose data. | Not tested |  |  |  |  |
| UI-0491 | 747 | Long-running jobs show status and cannot be accidentally launched twice. | Not tested |  |  |  |  |
| UI-0492 | 748 | Failed jobs resume or restart safely. | Not tested |  |  |  |  |
| UI-0493 | 749 | Automated backups cover database, documents, configuration, audit logs, and required encryption keys. | Not tested |  |  |  |  |
| UI-0494 | 750 | Restore test proves a payroll period, employee documents, and audit history can be recovered. | Not tested |  |  |  |  |
| UI-0495 | 751 | Recovery point and recovery time objectives are documented and tested. | Not tested |  |  |  |  |
| UI-0496 | 752 | Payroll cut-off and payday continuity plan is documented for system, internet, bank, or integration outage. | Not tested |  |  |  |  |
| UI-0497 | 753 | Monitoring alerts on failed calculations, imports, integrations, report jobs, payment generation, and backups. | Not tested |  |  |  |  |
| UI-0498 | 763 | Normal employee on fixed monthly salary. | Not tested |  |  |  |  |
| UI-0499 | 764 | Employee below, within, and above each PAYE band boundary. | Not tested |  |  |  |  |
| UI-0500 | 765 | Employee below, exactly at, and above each statutory contribution ceiling/threshold. | Not tested |  |  |  |  |
| UI-0501 | 766 | New hire on first day, mid-period, and last day. | Not tested |  |  |  |  |
| UI-0502 | 767 | Termination on first day, mid-period, and last day. | Not tested |  |  |  |  |
| UI-0503 | 768 | Salary increase effective first day and mid-period. | Not tested |  |  |  |  |
| UI-0504 | 769 | Backdated salary increase producing arrears. | Not tested |  |  |  |  |
| UI-0505 | 770 | Paid leave, unpaid leave, and mixed leave in one period. | Not tested |  |  |  |  |
| UI-0506 | 771 | Standard, weekend, holiday, and overnight overtime. | Not tested |  |  |  |  |
| UI-0507 | 772 | Bonus/commission with correct statutory treatment. | Not tested |  |  |  |  |
| UI-0508 | 773 | Taxable and non-taxable allowance. | Not tested |  |  |  |  |
| UI-0509 | 774 | Benefit in kind where applicable. | Not tested |  |  |  |  |
| UI-0510 | 775 | Expense reimbursement separated from earnings. | Not tested |  |  |  |  |
| UI-0511 | 776 | Loan deduction with normal, final, partial, and skipped instalment. | Not tested |  |  |  |  |
| UI-0512 | 777 | Multiple deductions with insufficient net pay and priority rules. | Not tested |  |  |  |  |
| UI-0513 | 778 | Employee with zero gross pay. | Not tested |  |  |  |  |
| UI-0514 | 779 | Employee whose deductions would create negative net pay. | Not tested |  |  |  |  |
| UI-0515 | 780 | Employee on suspension and subsequent reinstatement/back pay. | Not tested |  |  |  |  |
| UI-0516 | 781 | Employee transferring branch/cost centre mid-period. | Not tested |  |  |  |  |
| UI-0517 | 782 | Final pay with leave, gratuity/severance, loan balance, and statutory deductions. | Not tested |  |  |  |  |
| UI-0518 | 783 | Off-cycle/supplementary payroll. | Not tested |  |  |  |  |
| UI-0519 | 784 | Correction to a prior closed period. | Not tested |  |  |  |  |
| UI-0520 | 785 | One employee with two payment accounts, if supported. | Not tested |  |  |  |  |
| UI-0521 | 786 | Rejected/returned payment and controlled reissue. | Not tested |  |  |  |  |
| UI-0522 | 787 | Expatriate or non-resident employee if applicable. | Not tested |  |  |  |  |
| UI-0523 | 788 | Casual/hourly/daily employee if applicable. | Not tested |  |  |  |  |
| UI-0524 | 789 | Employee with foreign-currency compensation if applicable. | Not tested |  |  |  |  |
| UI-0525 | 793 | Duplicate employee is detected. | Not tested |  |  |  |  |
| UI-0526 | 794 | Duplicate variable-pay upload is detected. | Not tested |  |  |  |  |
| UI-0527 | 795 | Invalid bank details prevent or hold payment. | Not tested |  |  |  |  |
| UI-0528 | 796 | Unapproved overtime/leave/bonus does not enter payroll. | Not tested |  |  |  |  |
| UI-0529 | 797 | Expired recurring allowance stops correctly. | Not tested |  |  |  |  |
| UI-0530 | 798 | Fully repaid loan deduction stops correctly. | Not tested |  |  |  |  |
| UI-0531 | 799 | Unauthorised user cannot view salary or generate payment file. | Not tested |  |  |  |  |
| UI-0532 | 800 | Preparer cannot approve own payroll where segregation is required. | Not tested |  |  |  |  |
| UI-0533 | 801 | Change after approval forces reapproval. | Not tested |  |  |  |  |
| UI-0534 | 802 | Closed period cannot be edited directly. | Not tested |  |  |  |  |
| UI-0535 | 803 | Recalculation without changes produces the same results. | Not tested |  |  |  |  |
| UI-0536 | 804 | Repeated interface message does not duplicate a transaction. | Not tested |  |  |  |  |
| UI-0537 | 805 | Bank file cannot be generated from a draft or rejected payroll. | Not tested |  |  |  |  |
| UI-0538 | 806 | Payment file total mismatch blocks release. | Not tested |  |  |  |  |
| UI-0539 | 807 | Journal imbalance blocks posting. | Not tested |  |  |  |  |
| UI-0540 | 808 | Statutory return mismatch is flagged. | Not tested |  |  |  |  |
| UI-0541 | 809 | Missing statutory identifier appears as a blocking exception. | Not tested |  |  |  |  |
| UI-0542 | 810 | Unauthorised report export is blocked and logged. | Not tested |  |  |  |  |
| UI-0543 | 811 | Backup restoration reproduces the selected closed payroll. | Not tested |  |  |  |  |
| UI-0544 | 815 | Import opening balances for leave, loans, deductions, arrears, and year-to-date payroll/statutory values. | Not tested |  |  |  |  |
| UI-0545 | 816 | Reconcile employee population to source HR/payroll records. | Not tested |  |  |  |  |
| UI-0546 | 817 | Reconcile employee master and bank/payment details. | Not tested |  |  |  |  |
| UI-0547 | 818 | Run the same period in old and new systems using frozen inputs. | Not tested |  |  |  |  |
| UI-0548 | 819 | Compare gross, each earning, taxable pay, each statutory value, each deduction, net pay, employer cost, and year-to-date totals employee by employee. | Not tested |  |  |  |  |
| UI-0549 | 820 | Investigate every difference; distinguish configuration, source-data, rounding, and software defects. | Not tested |  |  |  |  |
| UI-0550 | 821 | Agree materiality/tolerance only for legitimate rounding, never for unexplained differences. | Not tested |  |  |  |  |
| UI-0551 | 822 | Reconcile payment totals and bank output. | Not tested |  |  |  |  |
| UI-0552 | 823 | Reconcile statutory returns and liabilities. | Not tested |  |  |  |  |
| UI-0553 | 824 | Reconcile payroll journal and cost-centre distribution. | Not tested |  |  |  |  |
| UI-0554 | 825 | Complete at least two successful parallel periods, including one with joiners, leavers, changes, overtime, leave, and variable pay. | Not tested |  |  |  |  |
| UI-0555 | 826 | Obtain written approval of final reconciliation. | Not tested |  |  |  |  |
| UI-0556 | 834 | Employee setup and payroll activation procedure. | Not tested |  |  |  |  |
| UI-0557 | 835 | Salary and bank-detail change procedure. | Not tested |  |  |  |  |
| UI-0558 | 836 | Payroll calendar, cut-off, and input procedure. | Not tested |  |  |  |  |
| UI-0559 | 837 | Variable-pay and bulk-upload procedure. | Not tested |  |  |  |  |
| UI-0560 | 838 | Payroll calculation, validation, approval, and closure procedure. | Not tested |  |  |  |  |
| UI-0561 | 839 | Payment file generation, release, confirmation, rejection, and reissue procedure. | Not tested |  |  |  |  |
| UI-0562 | 840 | Payslip publication and payroll-query procedure. | Not tested |  |  |  |  |
| UI-0563 | 841 | PAYE, NAPSA, NHIMA, workers' compensation, and other filing/remittance procedures. | Not tested |  |  |  |  |
| UI-0564 | 842 | Payroll-to-GL posting and reconciliation procedure. | Not tested |  |  |  |  |
| UI-0565 | 843 | Off-cycle, reversal, correction, and back-pay procedure. | Not tested |  |  |  |  |
| UI-0566 | 844 | Joiner, change, transfer, suspension, and leaver payroll procedure. | Not tested |  |  |  |  |
| UI-0567 | 845 | Access review, privileged access, and incident response procedure. | Not tested |  |  |  |  |
| UI-0568 | 846 | Backup, restore, outage, and payday continuity procedure. | Not tested |  |  |  |  |
| UI-0569 | 847 | Month-end, year-end, and annual statutory rollover procedure. | Not tested |  |  |  |  |
| UI-0570 | 851 | HR administrators are trained on payroll-critical master data. | Not tested |  |  |  |  |
| UI-0571 | 852 | Managers are trained on time, leave, overtime, and variable-pay approvals. | Not tested |  |  |  |  |
| UI-0572 | 853 | Payroll preparers are trained on inputs, calculations, exceptions, reconciliation, and corrections. | Not tested |  |  |  |  |
| UI-0573 | 854 | Payroll approvers are trained on control totals, variance review, and sign-off responsibilities. | Not tested |  |  |  |  |
| UI-0574 | 855 | Finance users are trained on payments, journals, liabilities, and reconciliations. | Not tested |  |  |  |  |
| UI-0575 | 856 | Employees are trained on payslip access and payroll queries. | Not tested |  |  |  |  |
| UI-0576 | 857 | Role-based quick guides and escalation contacts are available. | Not tested |  |  |  |  |
| UI-0577 | 858 | Production support ownership and severity-based response times are agreed. | Not tested |  |  |  |  |
| UI-0578 | 859 | First three production payrolls have enhanced support and daily issue review. | Not tested |  |  |  |  |
| UI-0579 | 877 | Approved for production. | Not tested |  |  |  |  |
| UI-0580 | 878 | Conditionally approved — conditions documented, owned, and dated. | Not tested |  |  |  |  |
| UI-0581 | 879 | Not approved — blocking items documented. | Not tested |  |  |  |  |
| UI-0582 | 893 | Payroll calendar and cut-off confirmation. | Not tested |  |  |  |  |
| UI-0583 | 894 | Payroll population and readiness report. | Not tested |  |  |  |  |
| UI-0584 | 895 | Approved joiners, leavers, salary changes, bank changes, overtime, leave, bonuses, and one-off inputs. | Not tested |  |  |  |  |
| UI-0585 | 896 | Import validation and exception reports. | Not tested |  |  |  |  |
| UI-0586 | 897 | Payroll register and gross-to-net report. | Not tested |  |  |  |  |
| UI-0587 | 898 | Current-versus-prior variance report with explanations. | Not tested |  |  |  |  |
| UI-0588 | 899 | Zero/negative/unusual pay and duplicate-payment-detail reports. | Not tested |  |  |  |  |
| UI-0589 | 900 | Payroll reconciliation and signed approval. | Not tested |  |  |  |  |
| UI-0590 | 901 | Payment file/list, control total, approval, bank confirmation, and rejected payment report. | Not tested |  |  |  |  |
| UI-0591 | 902 | Payslip generation/publication confirmation. | Not tested |  |  |  |  |
| UI-0592 | 903 | PAYE, NAPSA, NHIMA, and other applicable returns, approvals, submission references, payments, and reconciliations. | Not tested |  |  |  |  |
| UI-0593 | 904 | Payroll journal, posting reference, and payroll-to-GL reconciliation. | Not tested |  |  |  |  |
| UI-0594 | 905 | Third-party remittance schedules and proofs. | Not tested |  |  |  |  |
| UI-0595 | 906 | Adjustments, overrides, reopened periods, and off-cycle runs. | Not tested |  |  |  |  |
| UI-0596 | 907 | Audit log extract for material payroll actions. | Not tested |  |  |  |  |
| UI-0597 | 908 | Final closure confirmation. | Not tested |  |  |  |  |
| UI-0598 | 912 | Current pay period and days to cut-off/payday. | Not tested |  |  |  |  |
| UI-0599 | 913 | Payroll status and responsible owner. | Not tested |  |  |  |  |
| UI-0600 | 914 | Employee population: expected, ready, blocked, held, new, and terminating. | Not tested |  |  |  |  |
| UI-0601 | 915 | Outstanding HR inputs and approvals. | Not tested |  |  |  |  |
| UI-0602 | 916 | Calculation errors and validation exceptions. | Not tested |  |  |  |  |
| UI-0603 | 917 | Gross, deductions, net pay, employer cost, and prior-period variance. | Not tested |  |  |  |  |
| UI-0604 | 918 | Approval stage and pending approver. | Not tested |  |  |  |  |
| UI-0605 | 919 | Payment batch/status and rejected payments. | Not tested |  |  |  |  |
| UI-0606 | 920 | Statutory returns, due dates, submission, payment, and reconciliation status. | Not tested |  |  |  |  |
| UI-0607 | 921 | Journal posting and reconciliation status. | Not tested |  |  |  |  |
| UI-0608 | 922 | Critical alerts, overdue tasks, and unresolved payroll queries. | Not tested |  |  |  |  |
| UI-0609 | 939 | HR can select the reporting month, legal entity, branch, department, cost centre, location, and employee category. | Not tested |  |  |  |  |
| UI-0610 | 940 | The report uses a controlled month-end employee snapshot so historical headcount does not change when records are edited later. | Not tested |  |  |  |  |
| UI-0611 | 941 | Opening balance, additions, reductions, and closing balance reconcile for all movement-based metrics. | Not tested |  |  |  |  |
| UI-0612 | 942 | Current month is compared with prior month, same month prior year where available, year-to-date, target, and budget where relevant. | Not tested |  |  |  |  |
| UI-0613 | 943 | Each metric has a definition, owner, source, calculation, inclusion/exclusion rule, and refresh date. | Not tested |  |  |  |  |
| UI-0614 | 944 | Draft, Reviewed, Approved, and Published statuses are supported. | Not tested |  |  |  |  |
| UI-0615 | 945 | HR prepares, HR management reviews, and the authorised executive approves the monthly pack. | Not tested |  |  |  |  |
| UI-0616 | 946 | Comments explain material movements, risks, causes, and corrective actions. | Not tested |  |  |  |  |
| UI-0617 | 947 | Action items identify owner, deadline, status, and follow-up result. | Not tested |  |  |  |  |
| UI-0618 | 948 | Published packs are versioned and cannot be silently changed. | Not tested |  |  |  |  |
| UI-0619 | 949 | Distribution is restricted because the report contains personal and compensation information. | Not tested |  |  |  |  |
| UI-0620 | 950 | Summary figures drill down to the supporting employee-level schedule for authorised users. | Not tested |  |  |  |  |
| UI-0621 | 954 | Opening and closing headcount. | Not tested |  |  |  |  |
| UI-0622 | 955 | Joiners, leavers, net movement, and turnover rate. | Not tested |  |  |  |  |
| UI-0623 | 956 | Permanent, fixed-term, temporary, casual, intern, and other employee totals. | Not tested |  |  |  |  |
| UI-0624 | 957 | Active, suspended, on-leave, terminating, and inactive totals. | Not tested |  |  |  |  |
| UI-0625 | 958 | Total payroll cost, gross pay, net pay, employer statutory cost, and month-on-month variance. | Not tested |  |  |  |  |
| UI-0626 | 959 | Attendance rate, absence rate, lateness, and overtime hours/cost. | Not tested |  |  |  |  |
| UI-0627 | 960 | Leave taken, leave liability/balance, and overdue/excess leave. | Not tested |  |  |  |  |
| UI-0628 | 961 | Vacancies, hires, time-to-fill, offers, and outstanding onboarding actions. | Not tested |  |  |  |  |
| UI-0629 | 962 | Probations and contracts due for action. | Not tested |  |  |  |  |
| UI-0630 | 963 | Performance reviews due, completed, overdue, and outcome distribution. | Not tested |  |  |  |  |
| UI-0631 | 964 | Training completed, training hours, cost, and compliance completion. | Not tested |  |  |  |  |
| UI-0632 | 965 | Disciplinary, grievance, employee-relations, and health-and-safety cases. | Not tested |  |  |  |  |
| UI-0633 | 966 | Statutory filing/payment compliance status. | Not tested |  |  |  |  |
| UI-0634 | 967 | Critical employee-data, document, approval, and payroll-readiness exceptions. | Not tested |  |  |  |  |
| UI-0635 | 968 | Top five HR risks, decisions required from management, and next-month priorities. | Not tested |  |  |  |  |
| UI-0636 | 972 | Opening headcount + joiners + rehires - leavers = closing headcount. | Not tested |  |  |  |  |
| UI-0637 | 973 | Headcount and full-time equivalents are reported separately. | Not tested |  |  |  |  |
| UI-0638 | 974 | Headcount is analysed by entity, branch, department, cost centre, location, grade, job, position, supervisor, employment type, and employee status. | Not tested |  |  |  |  |
| UI-0639 | 975 | Filled positions, vacant positions, frozen positions, and approved establishment are compared. | Not tested |  |  |  |  |
| UI-0640 | 976 | Actual headcount is compared with approved establishment and budget. | Not tested |  |  |  |  |
| UI-0641 | 977 | Permanent, fixed-term, temporary, casual, intern, expatriate, and consultant populations are distinguishable. | Not tested |  |  |  |  |
| UI-0642 | 978 | Gender, age band, nationality, disability, or other workforce demographics are reported only where lawful and appropriate. | Not tested |  |  |  |  |
| UI-0643 | 979 | Span of control and employees without an assigned supervisor are reported. | Not tested |  |  |  |  |
| UI-0644 | 980 | Employees without valid positions, grades, departments, branches, or cost centres appear as data exceptions. | Not tested |  |  |  |  |
| UI-0645 | 981 | Month-end workforce list is exportable for audit support. | Not tested |  |  |  |  |
| UI-0646 | 985 | New hires and rehires are listed with start date, job, grade, branch, department, employment type, and readiness status. | Not tested |  |  |  |  |
| UI-0647 | 986 | Promotions, transfers, acting appointments, salary changes, grade changes, supervisor changes, and location changes are reported. | Not tested |  |  |  |  |
| UI-0648 | 987 | Leavers are listed by exit date, reason, department, tenure, final-pay status, clearance status, and replacement requirement. | Not tested |  |  |  |  |
| UI-0649 | 988 | Voluntary, involuntary, retirement, death, contract expiry, redundancy, and dismissal exits are separated. | Not tested |  |  |  |  |
| UI-0650 | 989 | Employee turnover, voluntary turnover, involuntary turnover, and regrettable turnover are calculated using documented definitions. | Not tested |  |  |  |  |
| UI-0651 | 990 | Turnover is analysed by branch, department, job, grade, supervisor, employment type, tenure, and reason. | Not tested |  |  |  |  |
| UI-0652 | 991 | Exit interviews due, completed, declined, and key themes are reported. | Not tested |  |  |  |  |
| UI-0653 | 992 | Offboarding, asset return, system-access removal, clearance, final pay, and document completion exceptions are shown. | Not tested |  |  |  |  |
| UI-0654 | 993 | Future-dated hires, transfers, and exits for the next reporting period are listed. | Not tested |  |  |  |  |
| UI-0655 | 997 | Scheduled days/hours, worked days/hours, paid hours, and unpaid hours are reported. | Not tested |  |  |  |  |
| UI-0656 | 998 | Attendance rate and absence rate use documented formulas. | Not tested |  |  |  |  |
| UI-0657 | 999 | Late arrivals, early departures, missed punches, no-shows, and unauthorised absences are reported. | Not tested |  |  |  |  |
| UI-0658 | 1000 | Attendance report explicitly lists employees who reported late and employees who missed work, with date, scheduled time, actual time, minutes late/absent, reason, approval status, and manager action. | Not tested |  |  |  |  |
| UI-0659 | 1001 | Overtime hours and cost are analysed by employee, supervisor, department, branch, shift type, and reason. | Not tested |  |  |  |  |
| UI-0660 | 1002 | Overtime report compares the current month's hours and ZMW cost with the previous month, showing amount variance, percentage variance, and a clear Increasing/Decreasing/No change indicator. | Not tested |  |  |  |  |
| UI-0661 | 1003 | Approved, rejected, pending, and paid overtime are separately shown. | Not tested |  |  |  |  |
| UI-0662 | 1004 | Excessive overtime, repeated lateness, consecutive workdays, rest-day, and policy-limit exceptions are flagged. | Not tested |  |  |  |  |
| UI-0663 | 1005 | Shift coverage gaps, understaffed shifts, and unassigned employees are reported where shifts are used. | Not tested |  |  |  |  |
| UI-0664 | 1006 | Timesheets outstanding, rejected, corrected, and approved after cut-off are reported. | Not tested |  |  |  |  |
| UI-0665 | 1007 | Attendance-to-payroll reconciliation confirms approved payable hours equal payroll hours/units. | Not tested |  |  |  |  |
| UI-0666 | 1008 | Monthly trend identifies departments with deteriorating attendance or abnormal overtime. | Not tested |  |  |  |  |
| UI-0667 | 1012 | Opening leave balance + accrual + adjustment - leave taken/encashed/expired = closing balance. | Not tested |  |  |  |  |
| UI-0668 | 1013 | A consolidated sick-note register lists every sick note received during the selected month, linked to the employee and related sick-leave/absence record. | Not tested |  |  |  |  |
| UI-0669 | 1014 | Sick-note register shows employee, date reported sick, absence start/end date, days/hours absent, date note received, document status, verification/approval status, and authorised reviewer. | Not tested |  |  |  |  |
| UI-0670 | 1015 | Monthly sick-note report identifies employees with one or more sick notes and shows occurrence count, total sick days/hours, and repeat occurrences for the month and year to date. | Not tested |  |  |  |  |
| UI-0671 | 1016 | Leave taken is analysed by type, employee, department, branch, and period. | Not tested |  |  |  |  |
| UI-0672 | 1017 | Leave requests submitted, approved, rejected, cancelled, and pending are reported. | Not tested |  |  |  |  |
| UI-0673 | 1018 | Paid, unpaid, partially paid, sick, maternity, paternity, annual, compassionate, study, and other configured leave are separated. | Not tested |  |  |  |  |
| UI-0674 | 1019 | Employees with negative, excessive, expired, or unusually high leave balances are flagged. | Not tested |  |  |  |  |
| UI-0675 | 1020 | Employees who have not taken minimum/rest leave within policy periods are identified. | Not tested |  |  |  |  |
| UI-0676 | 1021 | Long-term absence and return-to-work cases are tracked. | Not tested |  |  |  |  |
| UI-0677 | 1022 | Sick leave frequency, duration, repeated patterns, and supporting-document exceptions are reported with restricted access. | Not tested |  |  |  |  |
| UI-0678 | 1023 | Leave provision/liability is reported where Finance requires it. | Not tested |  |  |  |  |
| UI-0679 | 1024 | Approved unpaid leave reconciles to payroll deductions. | Not tested |  |  |  |  |
| UI-0680 | 1025 | Upcoming team/department leave calendar and coverage risks are available. | Not tested |  |  |  |  |
| UI-0681 | 1029 | Approved vacancies, open vacancies, applicants, shortlisted candidates, interviews, offers, acceptances, declines, and hires are reported. | Not tested |  |  |  |  |
| UI-0682 | 1030 | Recruitment funnel conversion is calculated at each stage. | Not tested |  |  |  |  |
| UI-0683 | 1031 | Vacancy age, time-to-shortlist, time-to-offer, time-to-fill, and time-to-start are reported. | Not tested |  |  |  |  |
| UI-0684 | 1032 | Source of hire and recruitment cost are reportable. | Not tested |  |  |  |  |
| UI-0685 | 1033 | Vacancies are analysed by entity, department, branch, position, grade, recruiter, and priority. | Not tested |  |  |  |  |
| UI-0686 | 1034 | Planned versus actual hiring and budgeted versus unbudgeted positions are compared. | Not tested |  |  |  |  |
| UI-0687 | 1035 | Offer status, background/reference checks, medical checks where applicable, and document collection are tracked. | Not tested |  |  |  |  |
| UI-0688 | 1036 | New-hire onboarding completion covers contract, policies, KYC, statutory registration, payroll, bank details, orientation, equipment, system access, and supervisor actions. | Not tested |  |  |  |  |
| UI-0689 | 1037 | Overdue onboarding tasks and employees not payroll-ready are highlighted. | Not tested |  |  |  |  |
| UI-0690 | 1038 | Probation goals and review dates are created during onboarding. | Not tested |  |  |  |  |
| UI-0691 | 1042 | Payroll population, gross pay, taxable pay, deductions, net pay, employer contributions, and total employer cost are reported. | Not tested |  |  |  |  |
| UI-0692 | 1043 | Current payroll is compared with previous month, budget, and headcount movement. | Not tested |  |  |  |  |
| UI-0693 | 1044 | Payroll variance is explained by joiners, leavers, salary changes, overtime, leave, bonus, commission, arrears, deductions, and corrections. | Not tested |  |  |  |  |
| UI-0694 | 1045 | Payroll cost is analysed by entity, branch, department, cost centre, grade, employment type, and component. | Not tested |  |  |  |  |
| UI-0695 | 1046 | Basic pay, allowances, overtime, bonus, commission, benefits, and employer statutory cost are separately reported. | Not tested |  |  |  |  |
| UI-0696 | 1047 | Salary increases, promotions, acting allowances, new recurring items, expired items, and manual adjustments are reported. | Not tested |  |  |  |  |
| UI-0697 | 1048 | Zero pay, negative net pay, unusually high/low pay, held pay, returned payments, and unpaid employees are reported. | Not tested |  |  |  |  |
| UI-0698 | 1049 | Salary advance, employee loan, savings, union, garnishment, and third-party deduction totals and balances are reported. | Not tested |  |  |  |  |
| UI-0699 | 1050 | Benefit enrolment, eligible-not-enrolled employees, employee cost, employer cost, additions, removals, and expiries are reported. | Not tested |  |  |  |  |
| UI-0700 | 1051 | Payroll register, payment file, payslips, statutory returns, and GL journal reconciliation status is shown. | Not tested |  |  |  |  |
| UI-0701 | 1052 | Payroll queries opened, resolved, overdue, and recurring causes are reported. | Not tested |  |  |  |  |
| UI-0702 | 1056 | Active performance cycles, eligible employees, reviews launched, completed, pending, and overdue are reported. | Not tested |  |  |  |  |
| UI-0703 | 1057 | Goal/KPI setting completion and approval are reported. | Not tested |  |  |  |  |
| UI-0704 | 1058 | Mid-year, annual, probation, and other review types are separated. | Not tested |  |  |  |  |
| UI-0705 | 1059 | Rating distribution is shown by department, grade, and reviewer with appropriate privacy controls. | Not tested |  |  |  |  |
| UI-0706 | 1060 | Missing ratings, unapproved ratings, inconsistent calibration, and rating bias indicators are flagged for authorised review. | Not tested |  |  |  |  |
| UI-0707 | 1061 | Performance improvement plans show start date, milestone, owner, status, and outcome. | Not tested |  |  |  |  |
| UI-0708 | 1062 | Probation confirmations, extensions, and overdue probation decisions are reported. | Not tested |  |  |  |  |
| UI-0709 | 1063 | For a standard three-month probation, the system calculates the expected end date from the employment start date and notifies HR and the responsible manager before the decision is due. | Not tested |  |  |  |  |
| UI-0710 | 1064 | Probation workflow records the final decision as Confirm employment, Extend probation, or End employment, together with reason, approval, effective date, and supporting documents. | Not tested |  |  |  |  |
| UI-0711 | 1065 | Approved performance outcomes feeding salary, bonus, promotion, training, or succession actions are tracked. | Not tested |  |  |  |  |
| UI-0712 | 1066 | Manager completion and overdue-action rates are reported. | Not tested |  |  |  |  |
| UI-0713 | 1070 | Training planned, scheduled, attended, completed, failed, cancelled, and overdue are reported. | Not tested |  |  |  |  |
| UI-0714 | 1071 | Mandatory/compliance training is separated from developmental training. | Not tested |  |  |  |  |
| UI-0715 | 1072 | Completion rate is analysed by course, department, branch, job, and employee. | Not tested |  |  |  |  |
| UI-0716 | 1073 | Training hours, direct cost, cost per employee, provider, and budget variance are reported. | Not tested |  |  |  |  |
| UI-0717 | 1074 | Certificates, licences, and professional memberships due to expire are reported. | Not tested |  |  |  |  |
| UI-0718 | 1075 | Training needs from performance reviews, role requirements, incidents, or succession plans are tracked. | Not tested |  |  |  |  |
| UI-0719 | 1076 | Training effectiveness, assessment results, and post-training evaluation are reportable. | Not tested |  |  |  |  |
| UI-0720 | 1077 | Employees performing regulated/safety-sensitive work without valid training or certification are flagged. | Not tested |  |  |  |  |
| UI-0721 | 1081 | Disciplinary cases opened, active, awaiting action, closed, appealed, and overdue are reported. | Not tested |  |  |  |  |
| UI-0722 | 1082 | Grievances opened, active, resolved, escalated, and overdue are reported. | Not tested |  |  |  |  |
| UI-0723 | 1083 | Cases are analysed by type, department, branch, severity, age, and outcome without exposing unnecessary personal details. | Not tested |  |  |  |  |
| UI-0724 | 1084 | Warnings and sanctions due to expire or requiring review are reported. | Not tested |  |  |  |  |
| UI-0725 | 1085 | Investigations, hearings, appeal deadlines, and responsible officers are tracked. | Not tested |  |  |  |  |
| UI-0726 | 1086 | Recurring case themes and high-risk departments are highlighted. | Not tested |  |  |  |  |
| UI-0727 | 1087 | Employee assistance, wellbeing, or engagement indicators are included where collected lawfully. | Not tested |  |  |  |  |
| UI-0728 | 1088 | Sensitive case details are restricted; management receives aggregated information unless individual detail is authorised. | Not tested |  |  |  |  |
| UI-0729 | 1092 | Workplace accidents, incidents, near misses, occupational illness, and fatalities are reported. | Not tested |  |  |  |  |
| UI-0730 | 1093 | Incident date, location, employee/contractor category, severity, lost time, cause, action, and status are tracked. | Not tested |  |  |  |  |
| UI-0731 | 1094 | Lost-time injury, days lost, and other approved safety metrics are calculated consistently. | Not tested |  |  |  |  |
| UI-0732 | 1095 | Workers' Compensation notifications, claims, supporting documents, and status are tracked. | Not tested |  |  |  |  |
| UI-0733 | 1096 | Medical/safety checks due, completed, expired, or failed are reported with restricted access. | Not tested |  |  |  |  |
| UI-0734 | 1097 | Corrective actions, responsible owners, deadlines, and overdue items are reported. | Not tested |  |  |  |  |
| UI-0735 | 1098 | Safety training and protective-equipment compliance are reported where applicable. | Not tested |  |  |  |  |
| UI-0736 | 1102 | Contracts expiring in 30, 60, and 90 days are reported. | Not tested |  |  |  |  |
| UI-0737 | 1103 | Probation reviews, confirmations, or extensions due in 30, 60, and 90 days are reported. | Not tested |  |  |  |  |
| UI-0738 | 1104 | NRC/passport, work permit, visa, licence, certificate, medical, and other required documents nearing expiry are reported. | Not tested |  |  |  |  |
| UI-0739 | 1105 | Employees with missing contracts, policies, KYC, statutory identifiers, bank details, beneficiary details, or required documents are reported. | Not tested |  |  |  |  |
| UI-0740 | 1106 | Employee files are scored for completeness and listed by missing requirement. | Not tested |  |  |  |  |
| UI-0741 | 1107 | Policy acknowledgements and mandatory declarations outstanding are reported. | Not tested |  |  |  |  |
| UI-0742 | 1108 | Statutory return, payment, registration, compliance certificate, assessment, and correction statuses are summarised. | Not tested |  |  |  |  |
| UI-0743 | 1109 | Access reviews, conflict-of-interest declarations, and other recurring HR compliance actions are tracked where applicable. | Not tested |  |  |  |  |
| UI-0744 | 1113 | Employee requests opened, resolved, pending, rejected, escalated, and overdue are reported. | Not tested |  |  |  |  |
| UI-0745 | 1114 | Requests are analysed by category: leave, payroll, personal-data change, document, benefits, grievance, recruitment, onboarding, transfer, or exit. | Not tested |  |  |  |  |
| UI-0746 | 1115 | Average response time, resolution time, SLA compliance, backlog, and ageing are reported. | Not tested |  |  |  |  |
| UI-0747 | 1116 | Approval requests pending by manager/approver and ageing are reported. | Not tested |  |  |  |  |
| UI-0748 | 1117 | Reopened requests, recurring requests, and root causes are highlighted. | Not tested |  |  |  |  |
| UI-0749 | 1118 | HR team workload and case ownership are visible. | Not tested |  |  |  |  |
| UI-0750 | 1119 | Employee satisfaction or service feedback is reported where captured. | Not tested |  |  |  |  |
| UI-0751 | 1123 | Duplicate employee, NRC/passport, TPIN, NAPSA number, NHIMA number, bank account, phone, and email records are flagged. | Not tested |  |  |  |  |
| UI-0752 | 1124 | Missing required identity, contact, employment, organisation, compensation, statutory, payment, supervisor, and document fields are reported. | Not tested |  |  |  |  |
| UI-0753 | 1125 | Invalid dates, overlapping contracts, overlapping salary records, impossible ages, and termination before hire are detected. | Not tested |  |  |  |  |
| UI-0754 | 1126 | Active employees assigned to inactive branches, departments, positions, grades, or cost centres are reported. | Not tested |  |  |  |  |
| UI-0755 | 1127 | Employees with inconsistent status across HR, payroll, attendance, leave, benefits, loan, and access systems are reported. | Not tested |  |  |  |  |
| UI-0756 | 1128 | Data corrections show owner, due date, status, and completion evidence. | Not tested |  |  |  |  |
| UI-0757 | 1129 | Data-quality score and unresolved P0/P1 exceptions are included in the monthly executive dashboard. | Not tested |  |  |  |  |
| UI-0758 | 1133 | Expected joiners, leavers, transfers, promotions, and salary changes. | Not tested |  |  |  |  |
| UI-0759 | 1134 | Contracts, probation reviews, permits, licences, certificates, and documents due to expire. | Not tested |  |  |  |  |
| UI-0760 | 1135 | Planned recruitment milestones and vacancies requiring decisions. | Not tested |  |  |  |  |
| UI-0761 | 1136 | Upcoming performance-cycle activities and overdue reviews. | Not tested |  |  |  |  |
| UI-0762 | 1137 | Planned training and mandatory compliance deadlines. | Not tested |  |  |  |  |
| UI-0763 | 1138 | Upcoming payroll cut-off, pay date, statutory deadlines, and annual rule changes. | Not tested |  |  |  |  |
| UI-0764 | 1139 | Planned leave and workforce coverage risks. | Not tested |  |  |  |  |
| UI-0765 | 1140 | Disciplinary, grievance, investigation, hearing, appeal, or case deadlines. | Not tested |  |  |  |  |
| UI-0766 | 1141 | Management decisions required, accountable owner, and decision due date. | Not tested |  |  |  |  |
| UI-0767 | 1145 | Closing headcount reconciles to the employee master at month end. | Not tested |  |  |  |  |
| UI-0768 | 1146 | Opening headcount plus net employee movement equals closing headcount. | Not tested |  |  |  |  |
| UI-0769 | 1147 | Paid headcount reconciles to payroll population, with every difference explained. | Not tested |  |  |  |  |
| UI-0770 | 1148 | Payroll totals reconcile to the approved payroll register and Finance journal. | Not tested |  |  |  |  |
| UI-0771 | 1149 | Attendance, overtime, unpaid leave, loans, and other HR inputs reconcile to payroll outputs. | Not tested |  |  |  |  |
| UI-0772 | 1150 | Statutory compliance status reconciles to return submissions, payments, and authority references. | Not tested |  |  |  |  |
| UI-0773 | 1151 | All dashboard figures drill down to accurate supporting schedules. | Not tested |  |  |  |  |
| UI-0774 | 1152 | Historical monthly reports reproduce the approved snapshot and version. | Not tested |  |  |  |  |
| UI-0775 | 1153 | Confidential reports cannot be viewed or exported by unauthorised users. | Not tested |  |  |  |  |
| UI-0776 | 1154 | PDF, Excel/CSV, and on-screen totals are consistent. | Passed | `/hrm/reports` · HR admin | Headcount and workforce movements report with active headcount 5; on-screen value 5, PDF text value 5, and XLSX metric value 5.00 matched. The live selector offered PDF · print-ready, Excel · editable, and CSV · data. | Browser Downloads: `workforce-summary.pdf`, `workforce-summary.xlsx`; rendered PDF preview `/home/ubuntu/workforce-summary-preview.png`; findings log; commit `8def93a` | Targeted format-consistency check passed; broader payroll/statutory and large-volume cross-format reconciliation remains not tested |
| UI-0777 | 1155 | Report generation completes within the agreed service level at production data volume. | Not tested |  |  |  |  |
| UI-0778 | 1156 | HR and Management can sign off the monthly pack electronically with comments and actions. | Not tested |  |  |  |  |

| ID | Requirement | UI status | Screen/route and role | Test data / expected result | Evidence | Defect / severity / owner / retest |
|---|---|---|---|---|---|---|
| STAKE-01 | Sick-note register linked to employee and absence record | Failed — defect logged | `/hrm/leave`, `/hrm/reports`, `/hrm/people/documents` · HR admin | August 2026 UAT; expected consolidated monthly register linked to absence and employee | `ui_validation_initial_findings.md` — stakeholder addendum; live reports/document screens | High · missing stakeholder report capability · product owner · no retest until implemented |
| STAKE-02 | Sick-note fields, verification, approval, reviewer, and documents | Failed — defect logged | `/hrm/people/documents`, `/hrm/leave` · HR admin | Expected employee number/name, dates, days/hours, note receipt, document, verification, approval, reviewer | `ui_validation_initial_findings.md` — 0 documents and no sick-note fields | High · required fields/workflow absent · product owner · no retest until implemented |
| STAKE-03 | Sickness counts, trends, thresholds, and restricted detail | Failed — defect logged | `/hrm/reports`, `/hrm/analytics` · HR admin | Expected monthly/YTD occurrences, sick days/hours, repeat patterns, trends, threshold exceptions | `ui_validation_initial_findings.md` — aggregate analytics only; no sickness measures | High · required sickness analytics absent · product owner · no retest until implemented |
| STAKE-04 | Sensitive-document access control | Failed — defect logged | `/hrm/people/documents` · HR admin | Restricted-only tab and classification-based access messaging visible; separate unauthorised-role test not completed | Screenshot `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-21_10-37-31_6762.webp`; findings log | Medium · unauthorised-role export/access retest outstanding · HR admin · pending role test |
| STAKE-05 | Probation due list and review status | Failed — defect logged | `/hrm/talent/reviews`, `/hrm/performance` · HR admin | Expected due list, days remaining, reviewer, objectives/review status, overdue decisions | Screenshot `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-21_10-37-25_5686.webp`; findings log | High · Talent/probation surface explicitly disabled · product owner · no retest until enabled |
| STAKE-06 | Reminder and escalation history | Failed — defect logged | `/hrm/talent/reviews`, `/hrm/performance` · HR admin | Expected stakeholder-approved reminder schedule and history | Findings log — route displays Not in this release | High · reminder workflow absent · product owner · no retest until implemented |
| STAKE-07 | Confirm, Extend, and End Employment workflow | Failed — defect logged | `/hrm/talent/reviews`, `/hrm/performance` · HR admin | Expected Confirm/Extend/End actions with reason, approval, effective/extension dates | Findings log — no workflow controls rendered | Critical · probation employment-decision control absent · product owner · no retest until implemented |
| STAKE-08 | Probation access control | Not tested | `/hrm/talent/reviews`, `/hrm/performance` · HR admin | Expected role-restricted probation records and escalation | Findings log — route disabled in release | High · cannot perform access test until capability exists · product owner · pending |
| STAKE-09 | Overtime current/prior hours and cost | Failed — defect logged | `/hrm/time/timesheets`, `/hrm/reports` · HR admin | Expected current/prior month hours and ZMW cost by employee/dimension | Screenshot `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-21_10-37-44_4063.webp`; reports page has only aggregate overtime KPI/table | High · timesheet/comparison surface absent · product owner · no retest until implemented |
| STAKE-10 | Overtime variance and direction indicator | Failed — defect logged | `/hrm/reports`, `/hrm/analytics` · HR admin | Expected amount variance, percentage variance, and Increasing/Decreasing/No change | Findings log — no variance fields or indicator visible | High · required comparison calculation/UI absent · product owner · no retest until implemented |
| STAKE-11 | Overtime approval states and payroll reconciliation | Failed — defect logged | `/hrm/time/timesheets`, `/hrm/attendance`, `/hrm/reports` · HR admin | Expected approved/rejected/pending/paid states and payroll-paid reconciliation | Findings log — no timesheet route in release; attendance has no seeded records | High · workflow/output absent · product owner · no retest until implemented |
| STAKE-12 | Overtime abnormal patterns and budget comparison | Failed — defect logged | `/hrm/reports`, `/hrm/analytics` · HR admin | Expected highest employees/departments, abnormal patterns, and budget/approved-limit comparison if confirmed | Findings log; STAKE-17 requires stakeholder confirmation | High · policy decision and detailed analytics absent · product owner · pending policy confirmation/implementation |
| STAKE-13 | Late arrivals and early departures | Failed — defect logged | `/hrm/attendance`, `/hrm/reports` · HR admin | Expected employee list with date, scheduled/actual time, duration, reason, approval, manager action | Findings log — attendance route empty correction state; reports only aggregate status/overtime | High · detailed exception report absent · product owner · no retest until implemented |
| STAKE-14 | Missed clocks, no-shows, and absence categories | Failed — defect logged | `/hrm/attendance`, `/hrm/leave`, `/hrm/reports` · HR admin | Expected separate missed-clock/no-show/unauthorised/approved/authorised categories | Findings log — no seeded correction records and no employee-level exception output | High · detailed categories/report absent · product owner · no retest until implemented |
| STAKE-15 | Attendance trends and manager actions | Failed — defect logged | `/hrm/analytics`, `/hrm/reports` · HR admin | Expected repeated patterns, dimensions, month-on-month trends, and outstanding actions | Findings log — aggregate analytics only; no exception drill-down | High · required trend/action view absent · product owner · no retest until implemented |
| STAKE-16 | Confirm probation alert schedule | Not tested | Policy/configuration decision · stakeholder owner | Confirm 30/14/7 days before expiry and daily after expiry, or approved alternative | Findings log — no policy confirmation supplied | Medium · acceptance rule not final · stakeholder owner · pending decision |
| STAKE-17 | Confirm overtime budget comparison | Not tested | Policy/configuration decision · stakeholder owner | Confirm whether overtime must be compared with budget or approved limits | Findings log — no policy confirmation supplied | Medium · acceptance rule not final · stakeholder owner · pending decision |
| STAKE-18 | Confirm sickness threshold | Not tested | Policy/configuration decision · stakeholder owner | Confirm repeated-sickness/sick-leave threshold and escalation rule | Findings log — no policy confirmation supplied | Medium · acceptance rule not final · stakeholder owner · pending decision |

## UI evidence rules

A checklist item is not considered passed merely because its API route returns a successful response. The UI test must verify role visibility, field labels, validation, user feedback, state transitions, downloads/exports where required, audit history, and the expected result against the API baseline. Any API capability without a usable UI path is recorded as a UI gap.


## Milestone 1 targeted update — 2026-08-22

The real overtime capability is now available and validated through the production UI/API path. The Time Operations page exposes persisted attendance-derived overtime with employee, work date, total/regular/overtime hours, multiplier, lifecycle status, decision reason, and approve/reject actions. Approved overtime is included exactly once in a new payroll run as an explainable earning and becomes paid/linked only at final payroll release; rejected overtime remains excluded. See `m1_overtime_uat_evidence.md` and `m1_browser_validation.md`.

| Targeted requirement | Status | Evidence |
|---|---|---|
| Employee-level overtime capture and review | Passed for Milestone 1 | Real attendance import derived pending records; live overtime queue rendered and listed records. |
| Overtime approval/rejection with reason and actor | Passed for Milestone 1 | Approve/reject API decisions persisted actor, timestamp, reason where required; outbox events present. |
| Payroll inclusion and exact-once recalculation | Passed for Milestone 1 | September UAT run contained one ZMW 64.90 overtime component on both calculations with identical totals. |
| Rejected overtime excluded | Passed for Milestone 1 | Rejected 2026-09-04 row remained unlinked and absent from payroll lines. |
| Payroll-release reconciliation | Passed for Milestone 1 | Approved 2026-09-03 row became `paid` and carried run/line references after separate-role release. |
| Permission and lifecycle guards | Passed for Milestone 1 | Unauthenticated list 401; HR-admin release 403; paid-record decision blocked. |

This targeted milestone result does not change the overall checklist readiness: the broader register still contains untested items and stakeholder gaps, and the system remains not approved for production payroll.
