# M21 — Salary Structures and Compensation Administration

Date: 2026-08-14 · Commits: `ff838fb`, `f508eb4`, `3602149`, `41f6ecb`
on fork `georgemungamba/erp` main · Deployed to production
(`erp.mightyfinance.co.zm`). Mirrored to `Mightyfin/erp` main via the server
commit-tree merge (`e71b331`-series mirror history).

## Goal

Close the per-worker payroll loop that M20 started at the configuration level.
HR admins can now (1) define **salary structures** — named sets of salary
components shared across workers (e.g. `ZMW-STANDARD` with three editable
earnings plus five statutory components) — and (2) assign a **pay profile** to
any worker from the Compensation and benefits page: a pay group, an effective
date and per-component opening amounts. These profiles are exactly what a
future pay run posts to (`hrm.worker_payroll_profiles` + `hrm.worker_component_values`).

## Findings

The M20 backend exposed the full configuration ledger (pay groups, ZRA PAYE
slabs, NAPSA/NHIMA rules, components) but nothing tied components to workers.
`GET /payroll/profiles` existed and returned the seeded `DEV-001` profile, yet
there was **no write surface** for profiles and **no structure admin at all** —
`salary_structures` and `salary_structure_items` were seeded but immutable.
M21 adds both, keeping every write role-gated to `hr_ops`/`hr_admin`.

## Backend changes

### New endpoints (`ApiRoutesClean.cs`)

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/payroll/structures` | List salary structures |
| GET | `/payroll/structures/{id}` | One structure with its items and components |
| POST | `/payroll/structures` | Create a structure with items |
| PATCH | `/payroll/structures/{id}` | Name, description, archive |
| PATCH | `/payroll/structures/{id}/items` | Replace the item set explicitly |
| POST | `/payroll/profiles/{workerId}` | Upsert the worker's pay profile (replaces any open profile) |

### Rules enforced in the service layer (`PayrollServiceImpl` / `PayrollRepository`)

A structure's items must reference **active, non-archived** components; at
least one earning component must be present; duplicate component codes are
rejected. `SetStructureItemsExplicitlyAsync` archives or deletes only items
removed by name, so statutory rows that the seed pack provides are kept alive.
The profile upsert closes the previous open profile (`effective_to`) before
creating the new one — a worker always carries exactly one open profile.
Statutory component amounts are accepted as zero (they recompute at run time);
non-statutory rows without an amount are still persisted as `0` so the ledger
is complete.

The repository's `ListStructuresAsync` also had to eager-load **both**
`Items` and `Items.Component` — the initial implementation included only
`Items`, which rendered structures with empty component names in the UI.

### Tests

New file `StructureTests.cs` adds **7 tests**: structure create + item round-trip,
duplicate-code rejection, no-earning rejection, archived-component rejection,
explicit-item replacement (removal archives orphaned items), structure archive,
and profile upsert replacing the open profile. Backend suite now at
**117 passing** (110 from M20 + 7).

### Bugs fixed en route

1. **EF Core 10 + SQLite Guid-V7 child-insert failure**: inserting
   `WorkerComponentValue` children through the parent graph in tests threw a
   `Guid-V7` conversion error. The fix is the explicit two-phase pattern now
   used everywhere child rows are created in tests:
   `db.Set<WorkerComponentValue>().AddRange(children)` followed by
   `SaveChangesAsync`, with **no** `Update` call for the second phase.
2. **Self-healing dialog (commit `3602149`)**: the compensation dialog's
   initialization `useEffect` could re-run after the operator started editing,
   overwriting in-flight selections when the async fetch resolved late. A
   `useRef` guard (`initializedRef`) flips on first apply; the synchronous
   `onOpenChange` handler never re-triggers the reset while the effect has
   fired.
3. **Resilient default pay group (commit `41f6ecb`)**: when the page's group
   snapshot had not resolved before the dialog opened, the pay group combobox
   rendered empty and the operator had to re-select. The dialog now fetches
   pay groups itself and falls back to the default (or first) group, so new
   profiles always start pre-selected. The same commit refreshes the page's
   profile snapshot in place after a save so the workers table reflects the
   change immediately.

## Frontend changes

The **`/hrm/pay/compensation`** page ("Compensation and benefits", under the
Payroll nav) replaces the mock pay-and-bands surface. It renders all active
workers with their open pay profile (pay group name and effective date,
resolved from the profile's `payGroupId`) and an Edit pay action gated to
`hr_ops`/`hr_admin`/`payroll` (server-enforced regardless of the client gate).

The edit-pay dialog shows: a pay group selector, an effective-from date, and
one row per active component — the three editable earnings with amount inputs
and the five statutory components (NAPSA employee/employer, NHIMA
employee/employer, ZRA PAYE 2026) shown read-only with statutory badges and
the note that they compute from basic at run time. Basic pay is mandatory; the
submit button reads **Assign pay structure** for new profiles and **Update pay
structure** when one exists, with the dialog description telling the operator
which case they are in. Amounts stay off the worker table by design — only the
dialog reveals them.

Supporting changes: `use-api.ts` gained `payrollStructures`, `getStructure`,
`createStructure`, `updateStructure`, `setStructureItems` and
`createPayrollProfile`; the Structures tab was added to
`/hrm/configuration/payroll` (list, create with code+name, item editor with
remove/restore chips, archive). Both pages follow the M19/M20 patterns:
`USE_REAL` guard, `feedback.saved()` toasts, and server-side role gates.

## Verification in production

- `https://erp.mightyfinance.co.zm/hrm/pay/compensation` → 200, live table:
  Mutale Kabwe `EMP-0003`, Grace Musonda `EMP-0002`, Dev Operator `DEV-001`.
- **End-to-end write via the UI**: opened `EMP-0003`, selected MONTHLY-ZMW,
  entered basic 9500 + housing 2000, clicked Assign pay structure → toast
  "Mutale Kabwe's pay structure saved for the 2026-08-14 start date."
- **Database truth**: `hrm.worker_payroll_profiles` row created at
  `2026-08-14 12:16:52 UTC` (pay group MONTHLY-ZMW, `effective_from`
  2026-08-14, not archived) with `hrm.worker_component_values` basic = 9500,
  housing-allowance = 2000, transport-allowance = 0.
- **Re-open verification**: reopening the dialog pre-selects Monthly ZMW,
  pre-populates 9500/2000/0, shows "Updating the open profile effective from
  08/14/2026…" and the button reads "Update pay structure" — both create and
  update paths behave correctly on production build `3602149`.
- Both `hrn-api` (28911) and `hrn-web` (3000) rebuilt and redeployed on the
  production server (`docker compose -f docker-compose.prod.yml`); the Go ERP
  on 28910 untouched.

## What is still open

The benefits, insurance, review-cycle and pay-gap tabs on the compensation
page are intentional placeholders ("coming in a later milestone") — the
backend holds no data model for them yet. PAYE/NAPSA/NHIMA statutory rows are
read-only per-worker by design; they compute at run time in the (still to be
built) pay-run engine. Next milestone candidates, per the HR capability gap
analysis: **M22 — HR requests inbox and onboarding wiring** (APIs exist, pages
are mock) or **M23 — statutory compliance reports** (ZRA PAYE returns,
NAPSA/NHIMA remittance files).
