# M18 — Employer-side employee administration CRUD

Date: 2026-08-14 · Commit: `04c6af1` (fork `georgemunganga/erp` main) · Deployed to production.

## Goal
Give HR the immediate-launch capability to run the employee directory: search,
filter, and archive leavers — with the record history preserved for payroll and
reporting. Create/edit already worked end-to-end (M11/M14 real API paths), so
M18 closes the remaining gaps on the list surface and record lifecycle.

## Backend changes
| File | Change |
| --- | --- |
| `Mightyfin.Erp.Hrm.Application/Dtos.cs` | `WorkerListFilters` gained `IncludeArchived = false` |
| `Mightyfin.Erp.Hrm.Infrastructure/Repositories.cs` | `ListAsync` excludes `IsArchived` workers unless the filter is set |
| `Mightyfin.Erp.Hrm.Application/Workers/WorkerService.cs` | `UpdateAsync` now rejects archived workers (`worker-archived`, 422) |
| `Mightyfin.Erp.Hrm.Tests/WorkerServiceTests.cs` | +4 tests: archived exclusion, archive status, update-guard, route validation |

Backend test suite: **100 passing** (was 96 after M17).

## Frontend changes
`hrm.employees.index.tsx` rewritten for the real API:
- List driven by `GET /hrm/workers` with `search`, `status`, `workerType`,
  `includeArchived`, `pageSize` query parameters
- View chips: All employees / Active only / Archived (server-scoped)
- Status and Type selects (client refinement over server scope)
- Per-row Archive action (roles hr_ops/hr_admin, server-enforced) with a
  confirmation dialog and a saved toast; list reloads after archive
- Archived rows show an Archived badge; they stay searchable via the Archived filter

Mock mode remains untouched so the demo build still renders the seeded catalogue.
TypeScript check: 0 M18 errors (the single remaining error is the pre-existing
M11 mock mismatch in `hrm.leave.$id.tsx`).

## Verification in production
- `https://erp.mightyfinance.co.zm/hrm/employees` → 200, real records render
  (3 active employees live: Mutale Kabwe, Grace Musonda, Dev Operator)
- Archived Grace Musonda via the API end-to-end: she disappeared from the
  active roster, appeared under the Archived view with an Archived badge, and
  was unarchived afterwards so production data stays clean (7 archived records
  were visible there, mostly prior smoke-test workers).
- `hrn-api`, `hrn-web`, and the DB migration container were rebuilt and restarted
  via the production compose; the Go ERP on 28910 was not touched.

## Roles
List: hr_ops, hr_admin, payroll, manager, employee. Archive/update: hr_ops, hr_admin.

## Notes / next candidates
- No new routes → route tree unchanged.
- The archived view still mixes active rows in the same query response
  (server scopes via `includeArchived`); pagination remains single-page
  (`pageSize=100`) — fine for current roster sizes.
- M19 candidate: organization config — departments/locations management or
  employee detail enrichment (org placement, payroll identifiers form).
