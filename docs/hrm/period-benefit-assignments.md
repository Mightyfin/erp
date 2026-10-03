# Benefits: once-off and recurring assignments in Claims

Open Benefits → Claims and choose:
- **Once-off payroll**: select an active Add-to-payslip benefit, payroll period and employee amounts. Search, branch and department filters narrow the employee table. Save commits only changed visible rows; hidden drafts are counted and retained.
- **Recurring payroll**: the existing annual allowance assignment, exposed here for payroll benefits. Select employee/type/year and enter either annual or monthly amount. Annual ÷ 12 is paid in each applicable payroll period in the year, subject to existing proration.
- **Reimbursement claim**: existing claim-only benefit workflow. Evidence must actually be confirmed when required.

Once-off assignments are full monetary earnings for the selected pay period only; they are not prorated or divided by twelve. A period assignment replaces any recurring allowance of the same type for that employee/period to prevent double payment. Zero suppresses that period's benefit; clearing the amount archives the assignment and restores the recurring amount, if any. Other periods are unchanged.

The period input table has a unique active tenant/period/worker/type key and stores the amount and note. Saving a batch is transactional and validates role, scope, employee eligibility, precision and the annual cap across period assignments. Run state controls whether edits are allowed, as detailed below. Entries are audited and benefit-type deletion recognizes their usage. Payroll calculation and fresh employee previews include these inputs and the benefit's taxable/exempt setting; recalculation replaces run lines rather than accumulating benefits. The employee preview selects the newest usable payroll period and labels calculated runs as unreleased.

Validation covers both assignment choices, hidden drafts, persistence, closed-period protection, exact inclusion in the preview, recurring assignments and retaining the reimbursement form.

### Direct payroll claims from the Claims form

The Claims tab opens the standard form. Payroll benefits, including Trip Allowance,
keep Employee, Benefit type, Amount claimed, Currency, Note and Evidence attached
visible. They also require an explicit Payroll month. HR/admin submissions are
recorded as approved claims and added to the employee's monthly assignment in one
serializable transaction. Multiple claims add together; payroll calculation reads
the resulting monthly amount once. The existing recurring override rule applies.
Non-payroll claims retain their submit/decide/pay workflow.

The API accepts optional `payPeriodId` on `POST /api/hrm/benefits/claims`.
Payroll claims require HR/payroll access, employee scope and period eligibility,
matching payroll currency, required evidence, a positive two-decimal amount,
annual cap compliance and an unlocked payroll period. Payroll claims cannot be
marked paid separately: payment is released through payroll. The Claims list
identifies them as Assigned to payroll and shows the selected period.

Benefit input corrections are allowed while an open period has only draft,
locked/calculation-ready, calculated, cancelled, void or reversed runs. Saving
monthly assignments or direct payroll claims moves calculated runs back to
calculation-ready (`locked`) and records a `benefit-inputs-updated` run event.
Payroll must be calculated again before approval. Calculating, in-review,
approved, released and closed runs continue to block benefit input changes.

### Claim corrections

The Claims list uses the shared edit dialog and destructive confirmation dialog.
HR/payroll can edit or delete direct payroll claims while the selected period
accepts input. Edits adjust only that claim's contribution to the employee's
monthly total; deletion archives the claim and removes that contribution. Both
actions are audited and move calculated runs back to calculation-ready for a
fresh calculation. Workers and months cannot be changed on an existing claim.
Monthly grid rows backed by claims are read-only so manual changes cannot make
the claim records disagree with payroll. Reimbursement claims can be edited or
deleted while submitted, returned or rejected. Approved and paid reimbursements
cannot be edited or deleted.
