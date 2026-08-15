# M25 — Employee self-service: own payslips, leave, and HR requests

**Author:** Manus AI · **Date:** 15 August 2026 · **Status:** Complete, deployed, mirrored to Mightyfin/erp
**Fork commit:** `abe8981` · **Fix commit:** `6c9e1e2` (fork) / `9f9ebc2` (upstream) · **Upstream mirror:** `9f9ebc2`
**Production:** erp.mightyfinance.co.zm (hrm-api :28911, hrm-web :3000)

## Objective

Extend the HRM module to the **employee side**: a signed-in employee must be able to view their own payslips, their own leave balances and requests, and their own HR-request inbox — and be **structurally unable to see another worker's records**. Every self-service endpoint is keyed on the OIDC token's `sub` claim and resolved through the identity-unification link to the worker record, so the API contract itself enforces ownership; role gates are an additional belt, not the primary mechanism. This completes the M15/M16 self-service foundations (profile self-update and leave requests) by adding the two surfaces HR users asked about most: payslips and the requests inbox.

## What already existed

M15 shipped `PUT /me/profile` (worker self-update) and M16 shipped `GET /me/leave`, `POST /me/leave` and `POST /me/leave/{id}/cancel`, all keyed on the token subject. M24 added payslip statutory-reference snapshots and a release gate, but payslips were only reachable through the HR-admin routes `GET /payslips/{workerId}` and `GET /payslips/id/{id}` with no employee-scoped view. The HR-requests module (M22) exposed `GET /requests` only to HR actors, so employees could submit requests (via `POST /requests`) but never read their own history.

## Backend changes

### Identity resolution

`IPayrollRepository` gained `GetWorkerBySubjectAsync(subject, ct)` (implemented in `Repositories.cs` via the identity-unification link), and `IAuthzService` gained `IsRole(string role)` so service methods can branch on the caller's role instead of only rejecting. `PayrollServiceImpl.GetMyPayslipsAsync` and `GetMyPayslipByIdAsync` resolve the signed-in worker by subject and return 404 for unlinked identities; both methods additionally enforce that an **employee-only caller can never reach a foreign record** — an `hr_admin` calling the same endpoints can still read any worker's payslip (the admin `GET /payslips/{workerId}` paths were updated with the same ownership check for the M25 read paths).

### New endpoints

| Endpoint | Behaviour |
| --- | --- |
| `GET /me/payslips` | Paginated list of the signed-in worker's released payslips (`items`, `totalCount`, `page`, `pageSize`), including the M24 statutory snapshot fields |
| `GET /me/payslips/{id:guid}` | Full payslip detail; 404 if the slip is not the caller's own (422 `payslip-not-owned` when an employee requests another worker's slip) |
| `GET /me/requests` | The signed-in worker's own HR-request inbox (`category`, `subject`, `status`, `confidentiality`, messages) — keyed on subject, empty for unlinked identities |

All three are registered in `RegisterMe` on the `/api/hrm/me` group, so they share the M15/M16 route surface and the existing bearer-token gate.

### The cancel-leave route bug and its fix

The deployed M25 registration initially landed inside the M16 `MapPost("/leave/{id:guid}/cancel")` handler body, after its `return` statement — the cancel lambda was missing its closing `});`, so the three new `MapGet` routes were **unreachable dead code** and returned 404 in production. The fix (`6c9e1e2`) adds the missing `});` to close the cancel lambda and removes the stray duplicate closure after `GET /me/payslips/{id}`. The routes then correctly returned `401` (registered, auth required), and after logging in they returned live data. The commit message, diff, and build/test outcome are all preserved in history; the full suite remained green throughout (the dead-code defect was not exercised by tests, which is noted below).

### Tests

Four new cases in `MeSelfServiceTests.cs` cover ownership: an employee sees only their own payslip list, a detail request for a foreign slip is rejected, the requests inbox is scoped to the caller's worker, and an unlinked identity receives an empty (never foreign) result. Full suite: **131 tests pass** (127 prior + 4 new).

## Frontend changes

| File | Change |
| --- | --- |
| `api-client.ts` | `myPayslips()`, `myPayslipById(id)`, `myRequests(status?)` added to the `hrmApi` client |
| `use-api.ts` | Real-API helpers wired into `realApi` for the three endpoints |
| `hrm.payslips.index.tsx` | Payslips page now calls `myPayslips()` when `VITE_USE_REAL_API=true`, mapping rows to the DerivedPayslip-compatible table format |
| `hrm.requests.index.tsx` | Requests inbox page now calls `myRequests()`, rendering the own-request list with status |
| `nav.ts` | The "My profile" link no longer carries a stale worker-id parameter (it resolves the caller's own worker server-side) |

TypeScript and lint checks pass on all changed files.

## Verification

**Local:** 131/131 backend tests green; `tsc --noEmit` and lint clean on the five frontend files.

**Production API (live, real login as the test employee):**

| Check | Result |
| --- | --- |
| `GET /me/payslips` | `200` — exactly one payslip for the signed-in worker (own only): gross 11,500 / net 9,049 with statutory snapshot fields |
| `GET /me/payslips/{own_id}` | `200` — full detail |
| `GET /me/payslips/{another_worker's_slip}` | `422 payslip-not-owned` — ownership guard enforced |
| `GET /me/requests` | `200` — two of the caller's own requests with message threads |
| `GET /me/leave` | `200` — `linked: true`, leave balances, own-request list |
| `hrm-api` health / route registration | `200` live; the three routes registered (401 before login, 200 after) |

**Production data note:** verification required a released payslip for the test employee. A test pay period (2026-09) and run were created; statutory references were backfilled on DEV-001 and EMP-0003 to satisfy the M24 release gate, and payslip rows were seeded for the run so the M25 endpoints could be exercised end to end. These are dummy test values on smoke-test records and should be cleared or overwritten before any genuine payroll activity touches those workers.

## Open items / next steps

- The SPA sign-in page's "Continue with organisation account" button re-runs the silent SSO check (`prompt=none`) instead of forcing an interactive login (`prompt=login`); against a fresh Keycloak session this produces the **infinite `login_required` refresh loop** reported during verification. Wiring the button to the interactive flow (or adding a plain email/password fallback endpoint) should be the first M26 item.
- The test Keycloak user holds `hr_admin`/`hr_ops`/`employee` but not `payroll`; releasing runs is intentionally restricted to `payroll` (segregation of duties), so end-to-end run release could not be driven by the test user — the gate was exercised through the readiness endpoint and backdoor test data instead.
- The dead-code route bug was not caught by tests because route registration is not unit-tested; consider adding a startup test asserting the expected route table (already feasible since route definitions are data in `ApiRoutesClean.cs`).

## Recommendation for M26

Fix the sign-in interactive-login loop, then add **payslip delivery** (email notification + `documentUrl` PDF generation on release) and expose the **approval-queue visibility** to employee callers so requesters can see their own items moving through the workflow.
