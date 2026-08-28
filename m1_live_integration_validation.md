# M1 live Timesheets integration validation

Date: 2026-08-23
URL: https://erp.newworldcargo.com/hrm/time/timesheets

## Live API and backend

Authenticated browser request to `/api/hrm/time/attendance?from=2026-08-01&to=2026-09-30` returned HTTP 200 with real PostgreSQL attendance rows. The returned records included UAT Eunice on 3 and 4 September 2026 and UAT Alice on 15 August 2026, with clock times, total hours, regular hours, overtime hours, multipliers, overtime lifecycle statuses, decision metadata, and payroll run/line references where applicable.

The new authenticated `GET /api/hrm/time/attendance?from=YYYY-MM-DD&to=YYYY-MM-DD` route is scope-aware. It applies selected location/org-unit scope and branch allowed-location confinement. No database migration was needed because the endpoint reads the existing AttendanceRecord data model.

The containerized backend suite passed **312/312** after the new endpoint, additive employee-number DTO field, and export implementation.

## Browser result

The deployed Timesheets page now loads in `Live PostgreSQL attendance` mode. No preview/sample controls or hard-coded UI rows remain on this route. The default 23 August 2026 view correctly displayed zero rows because there were no records for that date.

The date control is a real date input. Selecting 15 August 2026 loaded the real UAT Alice record. The table showed UAT Alice, clock-in 8:00 AM, clock-out 5:30 PM, worked 09h 30m, overtime 09h 30m ×2.00x, `Active`, and `OT approved`.

Opening the real row showed the live detail drawer with source `device-import`, overtime status `OT approved`, payroll state `Approved for payroll`, decision note `test`, decision actor subject, and decision timestamp. This proves the drawer is mapped to persisted API fields rather than UI-only sample fields.

The live pending overtime query for the August–September UAT range returned HTTP 200 with an empty list. No live decision or write operation was performed during this validation because there was no pending record and the closed UAT records were not changed. The existing `/hrm/time/operations` page remains the tested live approval surface for pending records.

## Shared export

The Timesheets toolbar uses the shared `ExportButton` and now supports real attendance export. CSV export returned the success toast `attendance-timesheet CSV export downloaded.` Excel export returned `attendance-timesheet XLSX export downloaded.` The previous `attendance does not support export yet` error was removed by implementing the attendance schema's server-side export rows with date filtering and overtime lifecycle fields.

## Defect caught and fixed

The first post-deployment browser check caught a missing `ChevronDown` icon import that caused the route error boundary to render. The runtime exception was captured as `ReferenceError: ChevronDown is not defined`, fixed, rebuilt, redeployed, and revalidated successfully.

A malformed browser automation edit to the native date input also exposed a robustness gap. The date change handler now accepts only `YYYY-MM-DD`, preventing invalid date strings from reaching the API and causing a 500 response.

## Range-aware Timesheets validation

The deployed page now supports Today, Week, Month, and Custom periods on the same route, plus Table, Week layout, and Month layout projections of the same live attendance records.

Today mode loaded the honest empty state for 23 August 2026. Week mode queried 17–23 August 2026 and correctly showed no rows, then Previous period moved to 10–16 August 2026 and loaded the real UAT Alice record under Sat 15 Aug. Clicking that weekly cell opened the persisted attendance detail drawer.

Month mode queried August 2026, rendered the employee-by-day matrix, displayed UAT Alice's real 15 Aug overtime cell, and clicking that cell opened the same live detail drawer. The Month period header was corrected to `Aug 2026` rather than an incorrectly sliced label.

Custom mode exposed real start/end date controls and starts in the detailed Table layout. The range and layout controls are live UI state; no mock or preview fallback is present.

The final web build succeeded locally and in the VPS container. The web and proxy containers were restarted successfully while the API and healthy PostgreSQL container remained running.
