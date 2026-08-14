# M19 — Organisation configuration (legal entities, locations, departments)

Date: 2026-08-14 · Commits: `b9599f8` (M19), `ad66b68` (roles fix), `644f747` (toast fix)
on fork `georgemunganga/erp` main · Deployed to production.

## Goal
Give HR the employer-side tooling to structure the company: legal entities,
work locations, and the effective-dated department/cost-centre spine. Every
structural change carries an effective date, so history is never rewritten.

## Findings
The backend organisation-config surface was already complete from the M1
foundation: legal entities, work locations, and org units with full CRUD and
effective-dated closures, all roles-gated to `hr_ops`/`hr_admin` on writes.
M19 therefore adds **+3 backend tests** (location CRUD lifecycle, duplicate-code
guard, org-unit update) — suite at **103 passing** — and converts the existing
Organisation setup page from its mock-driven rendering to the real API.

## Backend tests added
| File | Change |
| --- | --- |
| `Mightyfin.Erp.Hrm.Tests/ConfigAdminServiceTests.cs` | +3 tests: location create/update lifecycle, org-unit create validation (duplicate code), org-unit update rename |

## Frontend changes
`hrm.configuration.organisation.tsx` now drives all three tables from the real
API when `VITE_USE_REAL_API=true`:

| Surface | Real-API behaviour |
| --- | --- |
| Legal entities | Live table + Edit dialog (PATCH), Add legal entity dialog (POST) |
| Work locations | Live table + Add work location dialog + Edit dialog |
| Departments & cost centres | Live table with search/filters, unit close form (POST `/{id}/close`), Delete where safe |
| Roles gating | All write actions gated to hr_ops/hr_admin (client-side, server-enforced) |

Supporting changes: `api-client.ts` gained `hrmApi.patch`; `use-api.ts` gained
the org-config CRUD helpers (`createLocation`, `updateLocation`,
`createOrgUnit`, `updateOrgUnit`, `closeOrgUnit`, `createEntity`, `updateEntity`);
navigation already pointed at `/hrm/configuration/organisation`.

## Bugs fixed en route (two latent production issues surfaced by this page)
1. **Roles decoded from the wrong token** (`ad66b68`): Keycloak only places
   `realm_access.roles` in the **access token**, not the id token, so the
   client-side role gates hid Edit/Decide actions for admin users. `oidc.ts`
   now merges access-token claims into the session user via
   `decodeSessionUser`; `auth.tsx` uses it. Verified in production: admin
   write buttons now render.
2. **Misleading demo toast** (`644f747`): `feedback.saved/removed` always
   showed "Demonstration build — nothing is saved", even in real-API builds.
   The note is now conditional on `VITE_USE_REAL_API`.

## Verification in production
- `https://erp.mightyfinance.co.zm/hrm/configuration/organisation` → 200, all
  three tables render real data (Mightyfin Zambia Ltd, M3 Test HQ, M3 Test
  Dept, Operations)
- **Create location end-to-end via the UI**: "M19 Test Office" (M19OFF, Ndola)
  created through the dialog and confirmed persisted through the API (2 rows:
  M3LOC, M19OFF)
- **Edit end-to-end**: PATCH rename round-trip HTTP 200
- DELETE on locations is deliberately 405 — lifecycle runs through effective
  dates, not hard deletion
- `hrn-web` rebuilt and redeployed (no backend changes required)
- Test artefact left in prod: location M19OFF "M19 Test Office" (unused, harmless)

## Notes
- `WorkLocationDtoFull` exposes `createdAt` but no `effectiveFrom`; the UI
  adapts locations from creation time, so a freshly created location shows
  its insert date as its effective date. Minor, accepted.
- Org units: `/hrm/admin/org-units` returns a plain array; locations and
  entities return `{items, totalCount}`.
