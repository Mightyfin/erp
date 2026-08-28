# Milestone 1 — Real overtime processing UAT evidence

**Environment:** New World Cargo HRM production deployment at `https://erp.newworldcargo.com`, tenant `local-tenant`, PostgreSQL-backed ASP.NET API, 2026-08-22.

**Test fixture:** A uniquely named persisted shift rule `M1-UAT-20260822` was created through the live API with 08:00–17:00 hours, a 60-minute unpaid break, an 8-hour daily overtime threshold, and a weekday multiplier of 1.5. It was assigned through the live API to worker `EMP-0005` / `UAT Eunice` for September 2026. A new open pay period `Sep 2026` was provisioned as a controlled UAT fixture because the current administrative API exposes period reads but not period creation; the closed August 2026 run was not changed.

## Test results

| Test | Evidence | Result |
|---|---|---|
| Attendance source and derivation | Live attendance import `m1-overtime-uat.csv` imported 2 rows with 0 rejects. 2026-09-03 08:00–20:00 produced 11.00 total hours, 8.00 regular hours, 3.00 overtime hours, multiplier 1.5, status `pending`. 2026-09-04 08:00–19:00 produced 10.00 total hours, 8.00 regular hours, 2.00 overtime hours, multiplier 1.5, status `pending`. | Passed |
| Approval path | The 3-hour record was approved through `POST /api/hrm/time/overtime/{id}/decide`; reviewer subject was the HR admin account and decision timestamp was persisted. | Passed |
| Rejection path | The 2-hour record was rejected through the same live API with required reason `M1 UAT rejection path`; reason, reviewer, and timestamp were persisted. | Passed |
| Payroll inclusion | A new September payroll run was created, locked, and calculated through live APIs. The employee line contained exactly one dedicated `overtime` earning of **ZMW 64.90**, explained as `3.00 approved attendance overtime hour(s)` using basic salary ZMW 3,000 / 208 standard monthly hours and multiplier 1.5. The rejected 2026-09-04 record did not appear in payroll lines. | Passed |
| Recalculation idempotency | First and second calculations both returned gross **ZMW 35,764.90**, net **ZMW 30,252.42450**, and employer cost **ZMW 37,906.90**. Both contained one overtime component of ZMW 64.90; no duplicate component was produced. | Passed |
| Release allocation | Separate payroll-role accounts approved and released the run. The approved attendance row became `paid` and was linked to run `01a02b54-ba69-7db2-9682-7a57419f9e13` and its payroll line. The rejected row remained `rejected` and unlinked. | Passed |
| Role and lifecycle guards | Unauthenticated overtime list returned HTTP 401. HR-admin release attempt returned HTTP 403 because release requires `payroll`. A subsequent attempt to change the paid overtime record returned HTTP 422 with `overtime-already-paid`. | Passed |
| Audit persistence | Two `hrm.overtime.decided` outbox events were present in PostgreSQL, one for each decision. Privileged API audit rows were present for the overtime decision routes. | Passed |
| Live UI | Time Operations loaded at the production domain with an overtime review queue, decision-reason field, and no mock rows. Before the fixture was imported it correctly showed `No derived overtime records found.` | Passed for availability; functional queue interaction is covered by API UAT above |

## Scope and limitations

This evidence demonstrates the Milestone 1 workflow using controlled synthetic UAT records and does not constitute approval for production payroll. The UAT run was intentionally left in `released` state without creating or reconciling a bank payment file. No external payment occurred. The closed August 2026 payroll run remains unchanged.

The overtime rate conversion currently uses the documented standard monthly denominator of 208 hours (26 working days × 8 hours), while the multiplier comes from the persisted shift/calendar derivation. Future policy changes to the denominator or pay basis require a separately versioned policy/configuration milestone.
