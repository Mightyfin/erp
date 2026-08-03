# Personas and Role Experiences

## Experience model

One HRM application provides different workspaces. Permissions control data and actions; workspaces control presentation and task priority.

## Employee workspace

### Primary goals

- View and maintain personal information
- Complete onboarding and assigned tasks
- Request leave, attendance correction, shifts, travel, expenses and letters
- Check schedules, balances, payslips and benefits
- Set goals, complete reviews and access learning
- Ask HR for help
- Submit a protected disclosure safely
- Manage consent for sensitive processing and request a personal-data export or correction

### Default navigation

Home, My Profile, Time & Leave, Pay & Benefits, Growth, Documents, Help

## Manager workspace

### Primary goals

- Understand team status
- Review and approve requests
- Plan schedules and capacity
- Support onboarding, performance and development
- Resolve attendance and leave exceptions
- Start permitted employee lifecycle actions

### Default navigation

Home, My Team, Approvals, Time & Scheduling, Talent, Team Reports, Help

Managers must not automatically receive payroll, medical, disciplinary or whistleblowing access.

## HR operations workspace

### Primary goals

- Maintain workforce records and organization assignments
- Run lifecycle processes
- Manage recruitment and onboarding
- Resolve operational exceptions
- Coordinate employee relations, cases, safety and compliance
- Publish documents, policies and communications
- Monitor deadlines and service levels

### Default navigation

Home, People, Lifecycle, Recruitment, Time Operations, Talent, Experience, Relations & Safety, Reports

## Payroll workspace

Payroll remains a capability inside HRM but has a restricted workspace.

### Primary goals

- Confirm payroll readiness
- Manage periods, inputs, exceptions and approvals
- Explain calculations
- Reconcile payments, accounting and statutory outputs
- Release and preserve payslips

### Default navigation

Payroll Home, Setup, Inputs, Runs, Exceptions, Payments, Statutory, Reports

"Setup" is a contextual shortcut into Configuration's payroll-specific settings (pay groups,
salary components, tax and contribution rules), not a separate configuration store — the same
data is reachable from the central Configuration entry point.

## HR administrator workspace

### Primary goals

- Configure organization terminology and HR policies
- Configure roles, permissions and data scopes
- Configure forms, workflows and automation
- Manage templates and sender references
- Enable supported HRM capabilities
- Review audit, integrations and data quality

### Default navigation

One Configuration entry point, not a row of parallel top-level items — see
[03-information-architecture.md](03-information-architecture.md)'s Configuration section for the
four sub-groups (Business setup, Process design, Security and compliance, Technical) it opens
into.

## Investigator and protected-case workspace

This workspace is isolated from ordinary HR administration.

### Primary goals

- Triage assigned protected disclosures
- Communicate safely with reporters
- Preserve evidence
- Manage conflicts and restricted access
- Record findings and remediation
- Monitor retaliation safeguards

Users cannot discover this workspace or case existence unless explicitly authorized.

## Multi-role behavior

- A user may switch workspaces if assigned multiple roles.
- The active workspace is always visible.
- Switching workspace does not expand authorization.
- Cross-workspace search returns only permitted data.
- Actions opened from notifications retain the correct workspace and scope.

