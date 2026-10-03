# Overtime amounts by payroll period

Open **Time & attendance → Overtime review → Overtime amounts by payroll period**.
Select the payroll period explicitly, enter an employee's total overtime amount,
and save that row. Entries are scoped to the chosen period and employee, with
normal tenant and branch access controls.

A saved amount replaces attendance-derived overtime for that employee in that
period. It is not an annual allowance and is not divided by twelve. A saved zero
means no overtime payment; clearing the amount restores approved-hours calculation.
Payroll calculation and payslip preview use the same source selection. Amounts
are included before the existing statutory calculations and go through the normal
payroll approval/release process.

Only HR administrators and payroll users may enter amounts. A closed period or a
non-draft, non-reversed payroll run makes the period read-only. A released run
therefore protects its inputs even if its period metadata remains open.
For an unreleased run with incorrect inputs, cancel the run through the existing
workflow, edit the inputs, and create a replacement run.

Existing annual overtime benefit assignments are not altered. This feature does
not reopen payroll or release payments.

Regression coverage: `PeriodOvertimeTests` checks month isolation, locked-run
rejection, clearing/re-entry, validation, amount replacement, and recalculation.

Validation covers period isolation, locked-run rejection, clearing and re-entry,
amount replacement, and payroll recalculation.
