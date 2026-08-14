# M20 — Payroll foundations (pay groups, ZRA PAYE slabs, NAPSA/NHIMA, salary components)

Date: 2026-08-14 · Commit: `e71b331` on fork `georgemungamba/erp` main ·
Deployed to production.

## Goal
Give HR the employer-side surface to manage the payroll configuration the pay
runs depend on: pay groups, the ZRA PAYE tax scale, NAPSA and NHIMA
contribution rules, and the standard salary components. Before M20 the backend
only read these values; HR had no way to update them. Employee self-service is
deliberately read-only here — writes are roles-gated to `hr_ops`/`hr_admin`.

## Findings
The M5/M6 payroll foundation already had complete **read** surfaces
(`GET /components`, `/pay-groups`, `/pay-groups/{id}/periods`,
`/tax-slabs?taxYear`, `/contribution-rules`) plus the seeded Zambian statutory
pack, but **zero write endpoints** for pay groups, tax slabs, contribution
rules, or components. M20 adds the full write surface while keeping every read
role-gated to `hr_ops`/`hr_admin`.

## Backend changes

### New endpoints (`ApiRoutesClean.cs`)
| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/payroll/pay-groups/full` | Pay groups with status (active/archived) for the admin table |
| PATCH | `/payroll/pay-groups/{groupId}` | Name, frequency, currency, payday day, input cutoff, is-default |
| PATCH | `/payroll/tax-slabs/{slabId}` | Rate and top-of-band for a PAYE slab |
| PATCH | `/payroll/contribution-rules/{ruleId}` | Rate, ceiling, floor for NAPSA/NHIMA rules |
| PATCH | `/payroll/components/{componentId}` | Rate/fixed/ceiling, IsTaxable, archive on standard components |

### Rules enforced in the service layer (`PayrollServiceImpl`)
- Setting `isDefault = true` on a pay group automatically clears the old
  default (`UnsetDefaultPayGroupsAsync`); exactly one default is kept.
- Archived pay groups cannot be edited (`422 archived-pay-group`).
- Tax-slab rate must be inside `0..100`; the top band's ceiling is left
  untouched (it is the open band).
- Statutory components (`paye`, `napsa-ee/er`, `nhima-ee/er`) are protected:
  their rates live on the slab/rule screens, so component updates on them are
  rejected with `statutory-component-protected`; archive is allowed.
- All writes roles-gated to `hr_ops`/`hr_admin` on the server.

### Tests
New file `PayrollSetupTests.cs` adds **6 tests**: tax-slab round-trip, rate
out-of-range (x2), contribution-rule ceiling update, default pay-group swap,
archived-group rejection, and component archive + statutory protection.
Backend suite now at **110 passing**.

## Frontend changes
New page **`/hrm/configuration/payroll`** ("Payroll setup", in the
Payroll → Configuration nav), tabbed in four sections:

| Tab | Reads | Edits via |
| --- | --- | --- |
| Pay groups | `GET /pay-groups/full` | dialog → `PATCH /pay-groups/{id}` |
| ZRA PAYE slabs | `GET /tax-slabs?taxYear=2026` | dialog → `PATCH /tax-slabs/{id}` |
| Contribution rules | `GET /contribution-rules` | dialog → `PATCH /contribution-rules/{id}` |
| Salary components | `GET /components` | dialog → `PATCH /components/{id}`; statutory rows show a STATUTORY badge and the editor disables rate/ceiling inputs |

Supporting changes: `use-api.ts` gained `payGroupsFull`, `updatePayGroup`,
`updateTaxSlab`, `updateContributionRule`, `updateSalaryComponent`; nav.ts
gained the Payroll setup item and a Configuration-hub row. The page follows
the M19 patterns: `USE_REAL` guard, `feedback.saved()` toasts, and the
`hr_ops`/`hr_admin` client-side role gate (server-enforced regardless).

## Bugs fixed en route
1. **Test harness tenant mismatch**: `PayrollSetupTests.cs` initially called
   `ctx.GetTenantId()`, which does not exist on `HrmDbContext`; tests now use
   the harness constant `"test-tenant"` (`TestDbContextFactory.Create`).
2. **Minimal-API lambda arrow placement**: the four `MapPatch` registrations
   first used a parameter-list form that produced CS1003/CS1061 compile
   errors; switching to the block-lambda `async (...) => { ... }` form used
   elsewhere in `ApiRoutesClean.cs` resolved all 52 errors.
3. **Initial UI 401**: the page first rendered before the SPA's OIDC session
   token had settled; the endpoints were healthy (verified with a console
   fetch against the stored token) and a reload renders everything.

## Verification in production
- `https://erp.mightyfinance.co.zm/hrm/configuration/payroll` → 200, all four
  tabs render live data: MONTHLY-ZMW (monthly, ZMW, payday day 28, 3-day
  cutoff, default, active), ZRA PAYE 2026 scale (0–5,100 @ 0%, 5,100–7,100 @
  20%, 7,100–9,200 @ 30%, 9,200+ @ 37%), NAPSA 5%/5% ceiling K1,861.80 and
  NHIMA 1%/1%, 8 components with statutory badges.
- **End-to-end write via the UI**: top PAYE band edited 37% → 37.5% (saved,
  toast confirms) and reverted to 37% through the dialog; the API round-trip
  was also confirmed with curl against the production API.
- Statutory component dialog renders with disabled inputs and the mandate
  note ("This component exists because ZRA or the pension authority mandates
  it…").
- Both `hrn-api` (28911) and `hrn-web` (3000) rebuilt and redeployed —
  backend and frontend both changed. The Go ERP on 28910 was untouched.

## Notes
- No create endpoints were added this milestone: the seeded Zambian pack
  (one pay group, 2026 PAYE scale, NAPSA/NHIMA rules, 8 components) is
  considered the starting catalogue. A finance-act-driven new tax-year scale
  or extra components would be the trigger for create flows (M21 candidate).
- Archive is soft-delete: past pay runs keep their component references
  intact; archived components are simply hidden from new configuration.
