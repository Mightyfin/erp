# Leave pay treatment

Leave types have a controlled pay treatment: paid, unpaid, or half-pay.
Approved leave requests and HR-recorded leave are matched to the configured
leave type when payroll is calculated. Paid leave leaves the scheduled salary
unchanged. Unpaid leave removes the full scheduled workday equivalent. Half-pay
leave removes half the scheduled workday equivalent; for example, two half-pay
workdays reduce a 23-day month to 22 paid-day equivalents. Salary proration
uses this fractional value, which is stored on the payroll line.

The type's default days per year is its entitlement/accrual policy. It does not
create an immediate 14-day balance for every worker. Administrators must check
or allocate balance before backdating a leave record when accrual has not yet
provided enough days. A type's effective date controls which absence dates can
use it, and existing leave-type categories are read at payroll calculation
rather than snapshotted on each leave record. Changing a category before
recalculation can therefore change an open run's result.
