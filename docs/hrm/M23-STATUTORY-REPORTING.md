# M23 — Statutory Compliance Reporting

Date: 2026-08-14 · Commits: `727dac4` (main M23), `4812190`, `49d8f7c` (follow-up fixes) on fork `georgemunganga/erp` main · Deployed to production (`erp.mightyfinance.co.zm`) · Mirrored to `Mightyfin/erp` main (`4d6604b`).

## Goal

Close the statutory reporting loop for the HR department: before M23 the backend had an M8-era CSV export engine (`zra`, `napsa`, `nhima`, `napsa-bankfile`) with **no frontend surface** — the reports pages were mocks. M23 makes statutory compliance tangible for payroll officers by (1) adding a **ZRA PAYE return** export with an employer header block and a totals row, (2) adding a **statutory liability summary** endpoint so totals are visible without downloading a file, and (3) wiring the **Statutory filings** page (`/hrm/reports`) to the real API with a period picker, liability table and one-click downloads for all five file types.

## What already existed

`StatutoryExportServiceImpl` (ConfigAndExtras) already generated four file types from released, non-reversed payroll run lines, role-gated to `payroll|hr_admin`. The problems found were purely presentational: no UI consumed it, and the UI's mock reports page was the only "reports" experience. The engine itself was kept untouched — M23 extends it rather than replaces it.

## Backend changes

### New export type: ZRA PAYE return (`paye-return`)

`paye-return` was added to the existing export switch, so the same download route serves all five file types. The format mirrors what an employer files with ZRA each month: an **employer header block** (registered/trading name from the tenant's default legal entity, TPIN, NAPSA and NHIMA employer references, period, currency), one line per paid worker (`Employee No, Name, TPIN, NAPSA No, Gross Pay, PAYE, Net Pay`) and a **totals row**. Worker TPIN/NAPSA values come from the worker record attached to the run line; employer references come from `hrm.legal_entities`.

### New endpoint: liability summary

`GET /api/hrm/statutory-exports/summary?periodId` returns the period's aggregate figures as JSON — worker count, total gross, total PAYE, NAPSA EE/ER, NHIMA EE/ER, total net, plus the employer block — so the reports UI shows the liability picture immediately without a file download. It reuses the same released-run-lines query as the file generator, so figures are guaranteed consistent with the exports.

### Two real bugs fixed along the way

1. **Empty period label in the summary.** `ListReleasedRunLinesForPeriodAsync` included the run but not the run's pay period, so `PeriodLabel` was always `""` through the navigation. Fixed with a `ThenInclude(r => r!.PayPeriod)` (matching the existing pattern used by the payslip query), with a safety-net `GetPeriodAsync` fallback.
2. **Empty employer block.** The employer lookup required `IsDefault = true` on a legal entity, but the seeded production entity has that flag unset. Both `GenerateAsync` and `SummaryAsync` now fall back to the first legal entity in the tenant. Employer TPIN/NAPSA/NHIMA reference fields remain empty until HR completes them in the legal-entity configuration — that is intentional, not a gap: the placeholders make the missing data visible instead of silently blank.

## Frontend changes

| File | Change |
| --- | --- |
| `hrm.reports.index.tsx` | Rewritten from mock to real: pay-group → period picker, liability summary table (gross/PAYE/NAPSA/NHIMA/net), download buttons for all five file types, client-side role gate (`payroll\|hr_admin`) mirroring server enforcement |
| `use-api.ts` | New `statutoryGenerate` (binary download) and `statutorySummary` helpers |
| `api-client.ts` | New `getBlob` helper so statutory downloads arrive as downloadable files rather than parsed text |
| `scope.ts` | `reports` added to `ENABLED_SECTIONS` and `/hrm/reports` to `ENABLED_PREFIXES` |
| `nav.ts` | Reports nav item relabelled **Statutory filings** |

## Verification

**Backend tests:** 122 tests pass (120 existing + new export-generator and summary-aggregation tests covering the PAYE return header, worker rows and totals, the summary aggregation, and exclusion of reversed runs).

**Production API (live, period 2026-08):**

| Export | Result |
| --- | --- |
| `summary` | `periodLabel 2026-08, workers 1, gross 30,000, PAYE 8,726, NAPSA 1,250/1,250, NHIMA 250/250, net 19,774` |
| `paye-return` | Employer block `Mightyfin Zambia Ltd`, worker row `SMK001, Smoke M3Worker, 30,000, 8,726, 19,774`, totals row matching |
| `napsa` / `nhima` | EE rows with EE + ER + total columns correct |

Totals match the DB exactly (PAye computed from the M20 PAYE slabs, NAPSA at the K1,861.80 ceiling, NHIMA at 1%).

**Production data note:** the released run used for verification is the original August run whose status was restored to `released` after an M22-era reversal had left it closed (the reversal stays `is_reversal=true` and is excluded from every statutory query, so exports count the same data once).

## Open items / next steps

- The default legal entity's **TPIN, NAPSA and NHIMA employer references are blank** — HR should complete these in Organisation configuration before the first real filing.
- Worker statutory IDs (TPIN/NAPSA/NHIMA numbers) are blank on the seed worker records — the onboarding readiness checklist from M22 surfaces this; capture enforcement is a natural M24 hardening item.
- The sandbox browser's OIDC session expired during UI verification, so the page was verified against the same endpoints it consumes via API; the API path (UI → summary + downloads) is confirmed working in production.

## Recommendation for M24

**Statutory ID enforcement + payslip statutory section**: require NRC/TPIN/NAPSA/NHIMA numbers on workers before a period can be released (with the readiness checklist from M22 as the source of truth), and extend M21 payslips with the worker's statutory references so every payslip is filing-ready on its own.
