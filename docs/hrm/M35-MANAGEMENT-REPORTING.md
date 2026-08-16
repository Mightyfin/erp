# M35 — Reporting and management dashboards

M35 replaces the reports-page mock catalogue with a tenant-scoped management reporting service and a live React dashboard. HR leadership, HR operations and payroll can now review workforce, cost, statutory, leave, attendance, recruitment and movement measures without joining database exports by hand.

## Source and metric controls

- Active headcount is point-in-time: a worker must have started by the report date and not ended before it.
- Hires and leavers use worker start/end dates. Turnover is leavers in the window divided by average opening and closing headcount.
- Organisation filters and payroll department attribution resolve the assignment effective at the relevant date. Payroll is attributed at pay-period end, not from the worker's current department.
- Payroll figures include released or closed, non-reversal runs only. Gross, deductions, net and employer contribution values come from stored run lines; the report engine does not recalculate payroll.
- PAYE, NAPSA and NHIMA liabilities come from immutable released line components.
- Leave, attendance, recruitment and workforce movements read their operational ledgers and use the same date/organisation filter contract.
- Every query remains subject to the database's tenant filters. Dashboard access requires `hr_ops`, `hr_admin` or `payroll`; employee-level payroll, journal and statutory exports additionally require `payroll` or `hr_admin`.

## Live surface and exports

`GET /api/hrm/reports/management` returns filter dimensions, metric definitions and sources, monthly trends, department controls, operational breakdowns, the statutory liability control, export catalogue and reconciliation notes.

`GET /api/hrm/reports/management/export/{reportType}` produces reproducible CSV for:

- headcount and workforce movements;
- payroll by department and employee detail;
- balanced payroll journal voucher;
- statutory liability summary;
- leave and attendance operations;
- recruitment funnel; and
- workforce movement register.

The React route `/hrm/reports` is summary-first: global period/entity/department/location filters, KPI cards with definitions, workforce and payroll trends, department reconciliation, operational tables, statutory controls and certified downloads. It never substitutes mock data when the live reporting source is unavailable.

## Comparison with the supplied legacy June pack

The reference ZIP contains a six-worker payroll, department summary/detail, PAYE and NAPSA schedules, and journal voucher controls. Its principal reconciliation values are gross/payments K17,348.35, deductions K1,298.35, net K16,050.00, PAYE K430.94, NAPSA K867.41 per side, and balanced journal totals of K18,215.76 debit and credit.

M35 now covers those control shapes from released payroll snapshots:

- `payroll-department` gives gross-to-net and employer cost by effective department;
- `payroll-detail` gives employee-period detail;
- `payroll-journal` emits a balanced control total;
- `statutory-liability` separates PAYE, NAPSA and NHIMA employee/employer liabilities; and
- the existing authority-specific ZRA/NAPSA/NHIMA exports remain available through the statutory export engine.

The ERP CSV formats are system-generated controls rather than pixel copies of third-party authority forms. Authority template changes should remain presentation adapters over these same released snapshots.

## Acceptance

Backend tests prove source reconciliation, organisation filtering, reversal exclusion, balanced journal export, payroll export role enforcement and invalid-date rejection. The frontend production build validates the routed dashboard. Playwright exercises live dashboard rendering, a department filter and a certified CSV download; the complete production browser suite remains the deployment gate.
