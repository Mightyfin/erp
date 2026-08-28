# New World Cargo HRMS Implementation Map

## Product position

New World Cargo is being developed as a **full HRMS**. Payroll remains a central completion goal, but it is one consuming and control-heavy module inside the HRMS. The surrounding HR workflows must therefore be useful on their own and must not be reduced to payroll-only screens.

## Implementation standard

Every operational module must have a real PostgreSQL model or an explicitly justified existing equivalent, API contracts, tenant and branch scope enforcement, role checks, lifecycle validation, audit/outbox coverage where the action changes a business record, visible loading/error/empty states, and browser validation. Mock data must not silently appear in production.

## Module sequence

| Sequence | HRMS area | Main screens | Payroll relationship | Initial status |
|---|---|---|---|---|
| 1 | Organisation and people foundation | Company, Branch, Department, Designation, Employee, Group, Grade | Supplies worker identity, assignment, department, grade, bank and statutory data | Partly live; Designation CRUD enabled; Group/Grade UI still pending |
| 2 | Working time | Shift Type, Shift Location, Shift Schedule, assignments, shift requests | Supplies scheduled hours, attendance derivation and overtime | Attendance/overtime live; Shift Type create/assign live and update/archive API added |
| 3 | Leave administration | Holiday List, Leave Type, Period, Policy, Block List, Allocation, Policy Assignment, Control Panel | Approved unpaid/half-pay leave affects payment days; approved leave affects HR/payroll reporting | Leave application live; several administration contracts pending |
| 4 | Attendance | Attendance, Requests, Checkin, Import, Employee Attendance Tool | Direct payroll input for attendance-derived overtime and exceptions | Core live; checkin identity-link setup and some evidence UI remain |
| 5 | Time and overtime | Timesheet, Overtime, Overtime Type, Overtime Slip/equivalent | Approved overtime becomes an explainable payroll earning | Milestone 1 workflow live and UAT-validated |
| 6 | Expenses | Expense Claim Type, Expense Claim, receipts, advances, approval and payment status | Usually reimbursement/settlement feed; must remain separate from salary unless configured | Dedicated implementation pending |
| 7 | Travel | Purpose of Travel, Travel Request, itinerary, advance, settlement, approvals | May feed expense settlement and absence/availability; not a salary calculation by default | Dedicated implementation pending |
| 8 | Payroll and statutory | Pay groups, periods, profiles, components, PAYE/NAPSA/NHIMA, runs, payslips, reports | Completion goal and controlled downstream consumer | Real backend and UAT evidence exist; broader readiness remains not approved |
| 9 | Wider HRMS | Recruitment, onboarding, performance, employee relations, self-service, documents, compliance | Supplies lifecycle and governance context; not all modules alter payroll | Several real or partial areas already exist and require continued validation |

## Shared workflow decisions

Leave applications, attendance corrections, overtime, expense claims, travel requests, and other approvals should use the same lifecycle vocabulary where appropriate: draft, submitted, in review, approved, returned, rejected, cancelled, and paid or settled only where money has actually been processed. A payroll calculation must never silently treat a pending or rejected input as approved.

The system should distinguish **master data**, **transaction records**, **approval records**, and **derived records**. For example, Leave Type and Leave Policy are master data; Leave Application and Compensatory Leave Request are transactions; an approval decision is an immutable audit event; and a payroll overtime component is a derived explainable record linked back to its source.

## Payroll-safe integration rules

Attendance-derived overtime may feed payroll only after approval. Unpaid or half-pay leave may affect payment-day proration only after approval. Expense and travel records must not alter salary payroll merely because they exist; an explicit approved and configured settlement route is required. Changes to effective-dated policy, pay configuration, or statutory rules must be versioned or blocked after the relevant payroll period is locked.

## Immediate implementation backlog

The next implementation slice should create dedicated live screens and contracts for Leave Period, Leave Policy, Policy Assignment, Leave Block List, first-class Leave Allocation, Holiday List, Leave Type administration, Compensatory Leave Request, Leave Encashment UI, Expense Claim/Type, and Travel Request/Purpose of Travel. Non-payroll HRMS screens such as recruitment, performance, documents, relations, and self-service remain in scope and should continue to be improved independently.

## Honest readiness boundary

A completed HRMS module does not automatically mean payroll is approved for production. Payroll readiness still requires full end-to-end UAT, statutory verification, segregation of duties, operational rollback/backup evidence, training, business acceptance, and formal sign-off. The current broader payroll assessment remains **NOT APPROVED** until those gates are completed.

*Prepared by Manus AI for New World Cargo.*
