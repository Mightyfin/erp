# Frontend-first Timesheets Validation

## Date

2026-08-23

## Live route

`https://erp.newworldcargo.com/hrm/time/timesheets?ui=timesheet-v2`

## Browser result

The Timesheets page now loads as a focused work-record workspace instead of the prior release-gated Coming Soon screen. It shows the New World Cargo shell, a Timesheets page header, the purpose statement “See your working time by day, keep the week complete, and resolve anything that needs attention,” a primary **Add time entry** action, previous/next week controls, a Weekly/Daily view selector, and a Download control.

Because the live timesheet API has not yet been connected, the production mock guard correctly shows the honest empty state: **Timesheets are ready for live connection**. The page explicitly states that no rows are shown until the live timesheet service is connected and will not invent hours or projects. No demo hours, projects, or fake timesheet rows were rendered.

The release scope was also corrected so `/hrm/time/timesheets` is no longer intercepted by the global Coming Soon screen. The standalone Attendance Import page was verified separately and renders with the shared Import/Export workflow and a direct **Review overtime** handoff.

## UX elements confirmed

| Element | Result |
|---|---|
| One-task page purpose | Passed — Timesheets is clearly a work-record page. |
| Date/period context | Passed — selected week is shown with previous/next controls. |
| Primary action | Passed — Add time entry is visible. |
| View control | Passed — Weekly/Daily selector is visible. |
| Export entry point | Passed — Download action is visible. |
| Real-data safety | Passed — no mock rows in production mode. |
| Empty state | Passed — live connection requirement is explained in plain language. |
| Release gating | Passed — Timesheets is no longer shown as Not in this release. |
| API changes | None — this pass was frontend-only. |

Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_06-17-57_5931.webp`.

## Final interaction test

After deployment, the **Add time entry** action opened a focused inline form with Work item, Date, Hours, Add draft, and Cancel controls. The page explicitly disclosed: “This frontend-first draft interaction is intentionally not connected to an API yet; no production record is written.” The interaction was therefore usable for frontend review without fabricating or persisting production data.

The final screenshot shows the weekly toolbar, four zero-value summary cards, and the honest connection state in one calm workspace. Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_06-20-53_4092.webp`.

## Navigation interaction validation

The next-week control changed the period context from `23 August 2026 – 29 August 2026` to `30 August 2026 – 5 September 2026` without leaving the page. The Weekly/Daily selector changed to **Daily** without navigation or runtime errors. The inline draft form remained visible and continued to disclose that no production record is written.

Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_06-21-13_1408.webp`.

## Navigation and shared Import/Export validation

The expanded Time and leave navigation now presents separate task entry points: **Overtime review**, **Import attendance**, **Schedules and rosters**, and **Timesheets**. Benefits and claims no longer contains the unrelated Time operations/schedules/timesheets card collection.

The live `/hrm/data/import-export` hub loaded with the real server schema registry and showed Employees, Attendance logs, and Payroll profiles. Each data type uses the same Import and Export controls, with plain-language safeguards explaining that schema mapping, server preview, and accepted-row confirmation happen before writing. The page also links to the task-specific Attendance Import workflow. No API changes were made in this pass.

Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_06-22-34_6195.webp`.

## Focused refinement validation

The refined live page now shows a clear `Live connection pending` status beside the period, a `This week` reset action, and a compact search/status toolbar under the summary cards. The status filter successfully changed from **All entries** to **Needs attention** without navigation or runtime errors. With no live rows connected, the page remains truthful and shows zero attention items rather than fabricated exceptions.

Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_06-31-52_2836.webp`.

## Opt-in UI sample preview — 2026-08-23

The deployed Timesheets page now includes an explicit **Preview sample** control. Activating it loads the existing local Timesheets fixture only for visual review. The page changes its header and period badges to **Sample UI preview · not saved** / **Sample preview** and shows a warning banner stating that rows are not from PostgreSQL and nothing is saved.

The preview rendered a complete weekly layout with 43.50 total hours, 40.00 regular hours, 3.50 overtime hours, two attention items, daily sections from Monday through Sunday, billable/non-billable work items, recorded statuses, and “Needs overtime review” rows. Row-level Edit and More actions were visible in the day groups. This confirms the intended UI hierarchy can be reviewed without changing production API behavior or persistence.

Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_06-35-57_9246.webp`.

## Preview interaction validation

In sample mode, selecting **Needs attention** reduced the visible list to the two overtime entries while preserving the period totals. The row-level **Edit** action produced a scoped informational message for the selected entry and did not write data. This demonstrates the intended exception-review behavior and keeps the API boundary explicit.

Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_06-36-23_6269.webp`.

## Preview exit validation

Selecting **Return to real data** restored the header label **Real data only · preview off**, reset the period to the real production context, returned all summary values to zero, and removed the sample rows. The informational row-action toast remained visible only as transient UI feedback and did not create a record.

Screenshot: `/home/ubuntu/screenshots/erp_newworldcargo_2026-08-23_06-36-38_1544.webp`.
