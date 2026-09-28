# Timesheet operator access

Assign only the **Timesheet operator** role (`timesheet_operator`) to an account
that records attendance for other employees. An employee record can be linked
without granting the Employee self-service permission.

The account can view attendance summaries and preview, import, and export
attendance spreadsheets. Imported overtime requires an existing approver.
Payroll, employee administration, users, roles, shifts, and leave administration
remain unavailable. Branch assignments constrain both viewing and imports.
Attendance already linked to payroll cannot be overwritten by an import.

The frontend directs this account to Timesheets and shows only Timesheets and
Import attendance. The API also enforces an explicit endpoint allowlist on both
`/api/hrm` and `/api/v1/hrm`. Import previews are bound to their creator and their
stored schema is checked again when applied.

Permissions are additive: assigning another workforce permission deliberately
grants its existing access. To keep an account restricted, assign this role alone.

Validation: `TimesheetAccessTests`, `TimeServiceTests`, and the browser test
`tests/e2e/timesheet-access.spec.ts` (frontend built with `VITE_USE_REAL_API=true`).
