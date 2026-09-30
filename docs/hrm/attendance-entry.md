# Attendance entry

Timesheets provides **Add attendance**, a modal for one employee, and **Add bulk attendance**, a table at `/hrm/time/attendance/bulk`. Both require HR administrator or HR operations access.

The table filters by work date, branch, department, employee name/number, employee status and recorded attendance. Only filled rows currently visible are submitted. Hidden drafts remain on the page and are counted separately. Existing records are read-only; use attendance corrections to change them.

Enter both times using the date the shift started. A clock-out earlier than clock-in is treated as the following day. Hours and overtime use existing shift rules; overtime requires review.

The API validates the entire submitted batch before writing and saves it in one transaction. It rejects duplicate employees, existing attendance, future dates, inactive/out-of-scope employees and locked payroll dates. A filtered unique index prevents concurrent duplicate attendance for the same tenant, employee and date. Each saved row has an audit entry.

Validation covers shift rules, overnight attendance, all-or-nothing batch saves, duplicate prevention, role and branch restrictions, and payroll locks.
