# HRM Frontend UI Workflow Documentation

## Purpose

This folder is the design authority for the frontend-only HRM experience. It translates the HRM feature catalogue into simple, role-based user journeys without hardcoding a customer's policies, approval chains, terminology, providers, or organization structure.

The frontend may initially run against mock data and simulated services. Backend contracts are represented as replaceable interfaces and must not leak infrastructure details into the user experience.

## Product promise

> A user should immediately understand what needs attention, what action to take, what will happen next, and where to find the result.

## Documentation map

| Document | Purpose |
|---|---|
| [01-product-principles.md](01-product-principles.md) | Simplicity, accessibility and interaction rules |
| [02-personas-and-role-experiences.md](02-personas-and-role-experiences.md) | Employee, manager, HR, payroll and administrator experiences |
| [03-information-architecture.md](03-information-architecture.md) | Navigation, routes and content grouping |
| [04-shared-interaction-patterns.md](04-shared-interaction-patterns.md) | Reusable page, form, approval and status patterns |
| [05-workflow-catalogue.md](05-workflow-catalogue.md) | Complete frontend workflow inventory and priorities |
| [06-detailed-core-workflows.md](06-detailed-core-workflows.md) | Step-by-step MVP workflows |
| [07-sensitive-and-compliance-workflows.md](07-sensitive-and-compliance-workflows.md) | Payroll, cases, discipline and whistleblowing UX |
| [08-frontend-build-contract.md](08-frontend-build-contract.md) | Frontend boundaries, mock services and acceptance gates |

## Governing rules

1. Navigation is organized around user goals, not database entities or backend services.
2. Users see only capabilities relevant to their role, permissions and organization scope.
3. A workflow is configured from reusable steps; examples are not mandatory business processes.
4. Every submitted item shows status, owner, next step and history.
5. Sensitive data is hidden, masked or omitted unless access is explicitly permitted.
6. Mobile and keyboard access are designed with the desktop experience, not added later.
7. The full HRM catalogue remains available through role-specific workspaces and search; it is not exposed as one enormous menu.

## Frontend delivery sequence

### Foundation

- Initial HR admin setup guide
- Application shell
- Role-aware navigation
- Home and work queues
- Search and command palette
- Shared list, detail, form, timeline and approval patterns
- Mock service layer

### Employee and manager core

- Employee profile
- Onboarding
- Leave
- Attendance and shifts
- Approvals
- Documents and letters
- Privacy and consent
- Help desk

### HR operations

- People directory and employee lifecycle
- Recruitment
- Scheduling and exceptions
- Performance
- Learning
- Cases and compliance

### Pay and administration

- Compensation
- Payroll work centre
- Reports
- Configuration centre
- Permissions
- Workflow builder
- Integration and audit views

