# Quick Access and navigation validation

Date: 2026-08-23

## Deployed route

The live HRM shell at https://erp.newworldcargo.com/hrm loaded successfully after the final frontend restart.

## Sidebar validation

The left rail visibly presents the grouped top-level categories Home, My HR, People, Time & attendance, Recruitment, Payroll & benefits, Performance, Approvals, Reports & exports, and Configuration. Expanding Time & attendance displayed real links for Timesheets, Clock in and out, Corrections, Raise a correction, Schedules and rosters, Import attendance, Overtime review, My leave, Request leave, and Leave approvals.

## Quick Access validation

The top navigation exposes Quick access with the displayed shortcut Ctrl/Cmd+J. Clicking it opened the Quick access dialog. The dialog displayed grouped real links for Time & attendance, People, Payroll & benefits, Performance & recruitment, and Reports & setup.

Typing Timesheets filtered the dialog to one real Timesheets route result. Clicking that result closed the dialog and navigated to /hrm/time/timesheets. The Timesheets page loaded with its live attendance controls. Escape closed the Quick access dialog without leaving the current route.

The Ctrl/Cmd+J keyboard shortcut opened the dialog from the HRM home page. The browser automation inserted the triggering J into the focused search field after opening; the dialog itself opened successfully and this does not affect route behavior.

## Production-safety checks

The shortcut tiles are route links, not decorative buttons. Role-restricted items are filtered using the current role. No API, database, or mock-data behavior was added or changed in this navigation pass. The frontend build succeeded locally and in the VPS web container.
