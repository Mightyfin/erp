# Newworldcargo HRM Payroll Preparation Instructions

Use this checklist before generating payroll. The goal is to make payroll a controlled process: setup first, validate, generate drafts, review, approve, then post and pay.

## 1. Confirm Employees

- Open HRM > Employees.
- Confirm every employee who must be paid exists and is active.
- Check employee number, name, department, branch, job title, grade, start date, NRC, NAPSA, NHIMA, TPIN, email, and phone.
- Archive leavers only after confirming they should not be included in the next payroll.
- Fix missing departments, branches, or grades before importing payroll profiles.

## 2. Confirm Organisation Setup

- Open HRM > Configuration > Organisation.
- Confirm legal entities, branches, departments, teams, managers, and work locations are correct.
- Confirm each employee belongs to the correct department and branch.
- For branch payroll, make sure branch access assignments are correct so users only prepare payroll for their own branch.

## 3. Confirm Payroll Configurations

- Open HRM > Configuration > Payroll.
- Confirm pay groups and pay periods.
- Confirm salary components such as Basic, allowances, deductions, PAYE, NAPSA, and NHIMA.
- Confirm statutory rules and tax slabs are configured as configuration data, not typed manually per employee.
- Confirm payroll cut-off dates and pay dates.

## 4. Assign Compensation Profiles

- Open HRM > Pay > Compensation.
- Use filters for status, employment type, branch, department/entity, grade, pay group, and missing profiles.
- Every active employee who must be paid needs an assigned pay profile.
- Basic pay is mandatory.
- Statutory components should calculate from payroll configuration at run time.
- Use import when assigning many profiles, then review missing or rejected rows.

## 5. Confirm Bank And Payment Details

- Open each employee record where needed.
- Confirm bank name, branch code, account name, account number, payment method, and mobile money details if applicable.
- Mark one payment detail as primary.
- Do not proceed to final payment if employees are missing payment details.

## 6. Confirm Attendance, Leave, And Adjustments

- Confirm attendance, leave without pay, overtime, TOIL, unpaid absences, and approved changes for the payroll period.
- Confirm ad hoc additions or deductions before payroll generation.
- Resolve pending approvals before generating the final payroll.

## 7. Run Payroll Pre-check

- Open HRM > Payroll > New Run.
- Select the pay group and period.
- Run the pre-check/readiness checks.
- Fix blockers before generating payroll.
- Warnings can be reviewed, but blockers must be resolved.

## 8. Generate Draft Payroll

- Generate payroll as a draft first.
- Review totals by employee, department, branch, earnings, deductions, statutory amounts, and net pay.
- Preview payslips before final approval.
- Check unusual increases, missing deductions, duplicate profiles, and employees excluded from the run.

## 9. Approve And Post Payroll

- Branch payroll should be submitted for organisation-wide HR approval where branch controls apply.
- Top HR reviews the submitted draft before approval.
- Only approve when totals and payslips are correct.
- Posting payroll should create the controlled payroll result for payslips and payment records.

## 10. Pay, Print, And Archive

- Generate payment files or payment summaries after approval.
- Print or send payslips only after payroll is approved.
- Keep the payroll run, payslips, approval events, reports, payment file, and any exception notes for audit.

## Mistake Handling

- Do not delete payroll silently after approval.
- If payroll was generated incorrectly, use controlled reverse/cancel actions where available.
- Record a reason for reversals or corrections.
- Top admin or HR admin should be able to correct genuine mistakes, but the system must keep an audit trail.

## Minimum Payroll Readiness Rule

Payroll should not be generated until these are true:

- Employees are active and assigned correctly.
- Branch and department data is correct.
- Pay group and pay period exist.
- Salary components, statutory rules, and tax slabs are configured.
- Each payable employee has a pay profile with Basic pay.
- Bank/payment details are ready.
- Attendance, leave, overtime, and adjustments are complete.
- Payroll pre-check has no blockers.
