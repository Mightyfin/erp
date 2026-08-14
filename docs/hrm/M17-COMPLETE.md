- Verify prod: navigate https://erp.mightyfinance.co.zm/hrm/leave/approvals in browser; note test user may NOT have hr_admin role (if so decision buttons 403) — verify keycloak roles of georgemunganga.

## M17 COMPLETE (2026-08-14)
- Fork commits: `0b3dd1c` (M17: HR-admin leave approvals inbox) + `ccb0aae` (chore).
  Upstream Mightyfin/erp still blocked (ghu_ app token 403) — needs ghp_ PAT.
- Changes: NEW src/routes/hrm.leave.approvals.tsx (LeaveApprovals: summary cards
  awaiting-decision/returned/total-needing-action, status filter chips, search,
  ListPage, Decide per row); DecisionDialog (approve/return/reject, reason required
  for return+reject, cutoff warning, error display, list reload).
- Nav: Leave group now My leave / Request leave / **Leave approvals** (roles
  hr_ops, hr_admin, manager). use-api.ts leaveRequests typed; routeTree regen.
- Backend: 1 new test `ListLeave_CompanyWide_WhenWorkerIdIsNull` — 96 passing.
- Prod: git pull + build hrn-web + up — deployed. Live at
  https://erp.mightyfinance.co.zm/hrm/leave/approvals (200, 5 rows render).
- Roles verified for test user: hr_admin, hr_ops, manager, employee.
- Decide E2E: POST /hrm/time/leave/{id}/decide on approved row → 422
  leave-not-reviewable (correct guard); no submitted rows in prod to fully test
  approve end-to-end without a new employee submission.
- Frontend build clean; only TS errors in pre-existing leave.$id.tsx (M11 mock
  mismatch, outside M17 scope).
