# M24 — Statutory identity release gate + payslip statutory references

**Author:** Manus AI · **Date:** 15 August 2026 · **Status:** Complete, deployed, mirrored to Mightyfin/erp
**Fork commit:** `3ba82cc` · **Upstream mirror:** `6e4755a`
**Production:** erp.mightyfinance.co.zm (hrm-api :28911, hrm-web :3000)

## Objective

Extend the payroll control model so that a pay run **cannot be released** while any worker included in it is missing a statutory identity reference (Zambian context: **NRC, TPIN, NAPSA number, NHIMA number**). The goal is to stop incomplete worker records from ever reaching employees' payslips and downstream statutory filings, and to make the reason for a blocked release immediately visible instead of a cryptic failure after clicking. Two things move together:

1. A hard **release gate**: `POST /runs/{id}/release` rejects with `409 run-statutory-readiness` while any worker in the run is missing a reference, listing exactly who is missing what.
2. A **snapshot**: every payslip created by the release carries the worker's four statutory references copied from the worker record at release time, exposed in the API and printed on the payslip PDF.

## What already existed

`Worker` entities already carry `Nrc`, `Tpin`, `NapsaNumber` and `NhimaNumber` (all nullable). M22's onboarding readiness checklist surfaces the same fields per worker, but it is frontend-derived and is never consulted by payroll. `PayrollServiceImpl.ReleaseRunAsync` gates on status `approved` then calls `FinalizePayslipsAsync`, which materialises `Payslip` records from run lines without any worker statutory data. The seed worker **SMK001** (Smoke M3Worker) in production has none of the four references — a natural negative test case.

## Backend changes

### Payslip snapshot

The `Payslip` entity gained four nullable columns (`WorkerNrc`, `WorkerTpin`, `WorkerNapsaNumber`, `WorkerNhimaNumber`), mapped to `worker_nrc` / `worker_tpin` / `worker_napsa_number` / `worker_nhima_number`. `FinalizePayslipsAsync` now copies each reference from the run line's worker when it creates a payslip, so the payslip record is a faithful snapshot of what was true at release time — subsequent changes to the worker record cannot retroactively change a payslip. `PayslipDto` exposes the fields as positional constructor arguments appended to the end (preserving existing callers, including the record's positional usage throughout the mapper), and `MapPayslip` passes them through. The `M24PayslipStatutoryRefs` EF migration (`20260815174920`) was generated and applies cleanly on PostgreSQL.

### The release gate

A new service method `GetRunStatutoryReadinessAsync` iterates every run line's worker and reports per-worker `hasNrc/hasTpin/hasNapsaNumber/hasNhimaNumber` plus an overall `IsReady` flag. `ReleaseRunAsync` now calls it **before** the status check and throws `DomainException("run-statutory-readiness", …)` with a per-worker detail string when a blocker exists — the message names each worker by employee number and full name and each missing reference. The order matters for the frontend: with readiness checked first, a user whose role permits release still gets a meaningful, actionable error rather than only a role denial.

### New endpoint

`GET /api/hrm/payroll/runs/{id}/statutory-readiness` returns the same readiness payload as JSON. Payroll officers can see who blocks a release **before** clicking the button, which is exactly where the information is useful.

### PDF

`PayslipDocumentServiceImpl`'s HTML template now renders a **Statutory references** table (NRC, TPIN, NAPSA no., NHIMA no.) on every generated payslip, so the PDF matches the record and the API.

### Tests

New `PayslipStatutoryTests` class (five cases): release is blocked with the correct error code when a run worker misses a reference; release succeeds when every worker carries the full pack; the readiness endpoint reports each worker's per-reference state; and released payslips carry the snapshot values. The existing `PayrollEngineTests` harness seed worker was extended with a complete statutory pack so the lifecycle tests remain valid under the new gate. Full suite: **127 tests pass** (122 prior + 5 new).

## Frontend changes

| File | Change |
| --- | --- |
| `hrm.payroll.runs.$id.tsx` | New `StatutoryReadinessCard` (danger banner listing each worker and each missing reference); `ReleaseActions` now calls the real `POST /runs/{id}/release` when `VITE_USE_REAL_API=true`, with a dedicated "blocked by statutory identity check" message on `run-statutory-readiness`; readiness loaded on the same page and reflected in the button state; `released` status mapped to the Paid stage |
| `hrm.payslips.$id.tsx` | Real-API branch using `GET /payslips/id/{id}`; new **Statutory references** card (values snapshotted at release); PDF button downloads `documentUrl` when present or triggers `POST /payslips/{id}/generate` |
| `use-api.ts` | `payrollRunStatutoryReadiness`, `payslipById`, `payslipGenerate` helpers |

TypeScript and ESLint checks pass on all changed files.

## Verification

**Local:** 127/127 backend tests green; `tsc --noEmit` and eslint clean on the three frontend files.

**Production API (live):**

| Check | Result |
| --- | --- |
| `GET /runs/…/statutory-readiness` (Aug 2026 run, SMK001 missing all four) | `isReady: false`, worker listed with `hasNrc/hasTpin/hasNapsaNumber/hasNhimaNumber: false`, `ready: false` |
| Same call after giving SMK001 a test statutory pack | `isReady: true`, all `has*` true — both branches exercised |
| `GET /payslips/id/…` | `workerNrc/workerTpin/workerNapsaNumber/workerNhimaNumber` present in the DTO (null for the pre-M24 snapshot, as expected — those payslips were created before the snapshot existed) |
| `POST /runs/…/release` (test user) | `403 Requires one of roles: payroll` — correct, the test user holds `hr_admin` not `payroll`; the readiness gate itself is exercised by the passing tests |
| `hrm-api` health | `200`; `hrm-migrate` applied the migration and exited `0`; `hrm-web` rebuilt and healthy |

**Production data note:** the test statutory values written to SMK001 for verification were deliberately dummy values (`123456/78/1`, `1009876543210`, `700000001`, `NH00001`). Replace them with the real references (or clear them) before SMK001 participates in a genuine filing — they are only used by queries and payslip display, and SMK001 is a smoke-test worker.

## Open items / next steps

- The dummy statutory pack on SMK001 should be replaced or cleared in production (see note above).
- Pre-M24 payslips will always show blank statutory references — by design, those records predate the snapshot. A one-off backfill is possible later if desired.
- The legal entity's employer TPIN/NAPSA/NHIMA references (M23 open item) remain blank; filing readiness still needs them.

## Recommendation for M25

**Employee self-service (first non-HR milestone):** leave requests and balances for the employee's own record, payslip viewing (reusing the new real-API payslip endpoint with an `employee`-role gate), and profile update requests. The HRM module now has strong HR-side control surfaces; M25 extends value to the other side of the employer/employee divide.
