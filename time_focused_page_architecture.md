# Time and Leave Focused-Page Architecture

## Design decision

The HRM will use a **one-task-per-page** interaction model. A page should answer one operational question and provide the controls needed to complete that task. Configuration, imports, approvals, and reporting are different jobs and should not be combined merely because they share a backend module.

> A user should be able to land on a page and know what they are here to do, what is waiting for them, and what the next safe action is.

## Time and leave route map

| Page | Route | Primary user | Single job | Primary action |
|---|---|---|---|---|
| Overtime review | `/hrm/time/operations` | HR reviewer / manager | Decide which derived overtime rows are valid | Approve or reject a row |
| Attendance import | `/hrm/time/attendance/import` | HR/time administrator | Bring attendance from a spreadsheet/device export into the system | Import and reconcile |
| Attendance corrections | `/hrm/attendance` | Employee / manager / HR | Resolve a missed or incorrect attendance record | Raise or decide correction |
| Shift rules | `/hrm/time/shifts` | HR administrator | Maintain working-hour and overtime rules | Create or edit a shift rule |
| Shift assignments | `/hrm/time/shifts/assignments` | HR administrator | Assign an effective-dated shift to a worker | Assign shift |
| Leave accrual | `/hrm/time/leave/accruals` | HR/payroll operations | Run and review a period accrual | Run accrual |
| Leave balances | `/hrm/time/leave/balances` | HR operations | Correct a worker’s leave balance | Post adjustment |
| Leave encashment | `/hrm/time/leave/encashments` | HR/payroll operations | Quote and submit controlled leave payout | Submit request |
| Approval escalation | `/hrm/time/approvals/escalation` | HR operations | Escalate overdue time and leave approvals | Escalate |
| Shared Import/Export | `/hrm/data/import-export` | HR operations / administrators | Select a data type, import spreadsheet rows, or export a filtered dataset | Import or export |

The existing mixed Time Operations page becomes the focused overtime review page. Its secondary forms should move to these routes incrementally. The first refactor keeps existing backend endpoints intact and changes navigation and page composition before adding new server capabilities.

## Shared Import/Export contract

The existing `ImportDialog` already provides the reusable interaction: file selection, CSV/XLSX parsing, server schema lookup, column mapping, server preview, accepted-row application, and downloadable error outcomes. It should be treated as the canonical import surface rather than copied into module pages.

The reusable component should support two modes:

| Mode | Required inputs | Result |
|---|---|---|
| Import | `typeKey`, optional scope/period, `onDone` | Schema → mapping → server preview → confirm → per-row outcome |
| Export | `typeKey`, current filters, format (`pdf`, `xlsx`, `csv`) | Downloaded file with the same filter context and a clear completion message |

Each importable domain registers a schema in one backend import-schema registry. The frontend passes only the `typeKey`; it does not maintain a second field list in every page. Attendance therefore uses `typeKey="attendance"`, just like employees use `typeKey="workers"`.

## Import/export UX rules

The shared surface must show the data type, scope, period, accepted/rejected counts, and a preview before writing. It must never silently use demo rows when the production real-API flag is enabled. The page that launches an import should link back to the resulting queue, such as `Review derived overtime` after attendance import. Export controls should use the same filter bar as the page and offer PDF, Excel, and CSV without module-specific duplicate controls.

## Incremental implementation sequence

1. Keep `/hrm/time/operations` focused on overtime review only.
2. Add a standalone attendance import page using the existing shared `ImportDialog` with `typeKey="attendance"`.
3. Add navigation links for Attendance import and Shift rules; keep existing routes available while links migrate.
4. Move shift setup, shift assignment, leave accrual, balance adjustment, encashment, and escalation one page at a time, reusing existing APIs and shared layout primitives.
5. Add a shared Import/Export hub and migrate other list pages to it without changing each page’s domain API.
6. Validate each page’s primary action, loading/empty/error states, real-data behavior, and permissions before moving to the next one.

## Frontend-first Timesheets implementation

The supplied TimeSheet.io reference clarified that the user-facing task is a focused weekly work-record workspace, not a mixed administration dashboard. The new Timesheets route therefore uses a period toolbar, weekly/daily view selector, truthful summary tiles, an inline frontend-only draft-entry interaction, and a day-grouped record area reserved for live rows. It intentionally does not connect a new API in this pass.

The production release scope now allows `/hrm/time/timesheets` and `/hrm/data/import-export`. The Timesheets route uses the production mock guard, so no fabricated rows are rendered while the live timesheet service is absent. The Attendance Import route is separate and uses the shared server-schema ImportDialog and shared ExportButton. Overtime review remains a separate decision page. Shift rules, schedules, corrections, leave operations, and other configuration work remain separate entry points rather than being embedded into Timesheets.

Live browser validation on 2026-08-23 confirmed the weekly toolbar, date navigation, Daily view selector, zero-value summaries, clear connection empty state, Add time entry disclosure, and standalone Attendance Import handoff. No API or database changes were made for this frontend-first pass.

## Opt-in UI sample preview

Timesheets now has an explicit **Preview sample** control for design review. It loads the existing local Timesheets fixture only after the user activates the control, labels the page **Sample UI preview · not saved**, and displays a warning that the rows are not from PostgreSQL. Returning to real data restores the zero-value live-connection state. This is a frontend review aid only and is not a production data source, seed, or API fallback.
