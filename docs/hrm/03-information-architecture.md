# Information Architecture

## Navigation strategy

Do not expose the 61 parent features as 61 navigation entries. Group them into stable user goals.

One rule applies specifically to administrative configuration: **the whole HRM module has exactly
one Configuration entry point**, not one per feature area. Everything an administrator sets up —
policies, roles, forms, workflows, templates, country/industry packs, localization, privacy,
vendors, integrations, audit, self-service settings, and Payroll's own setup — lives inside that
one entry point, organized internally into as many labeled sub-groups as needed (see the
Configuration section below). A configuration-sounding item is never given its own separate
top-level home elsewhere in the app just because it happens to live near a related operational
screen — if it's setup rather than day-to-day work, it belongs in Configuration, cross-linked from
context (e.g. a payroll admin jumping from the Payroll workspace into Configuration → Payroll
setup) rather than duplicated.

## Global application shell

- Workspace switcher
- Organization/entity context
- Global search
- Command palette
- Notifications
- Tasks and approvals
- Help
- User menu

## HR operations navigation

### Home

- Work queue
- Exceptions
- Deadlines
- Quick actions
- Workforce summary

### People

- Directory
- Worker records
- Employment and contracts
- Jobs and positions
- Workforce planning and establishment
- Contingent and external workers
- Organization structure
- Documents
- Data quality

### Lifecycle

- Preboarding
- Onboarding
- Movements and transfers
- Global mobility and assignments
- Employee journeys
- Separation and offboarding
- Assets and access
- Former workers and rehire

### Recruitment

- Workforce requests
- Requisitions
- Vacancies
- Candidates
- Interviews
- Offers
- Referrals
- Recruitment analytics

### Time Operations

- Attendance
- Shifts and rosters
- Leave
- Compensatory time
- Regularization
- Timesheets
- Travel
- Expenses and advances

### Pay & Benefits

- Payroll
- Compensation and rewards planning
- Benefits
- Insurance and claims
- Employee loans and recoveries

### Talent

- Performance
- Goals and OKRs/KRAs
- Learning
- Skills
- Career development
- Succession
- Internal opportunities

### Employee Experience

- HR requests
- Letters
- Knowledge
- Engagement
- Recognition
- Announcements

### Relations & Safety

- Employee relations
- Discipline
- Labour relations
- Health and safety
- Ethics and declarations
- Emergency accountability
- Protected disclosures, visible only to authorized users

### Reports

- My reports
- Operational reports
- Management dashboards
- Scheduled reports
- Certified metrics

### Configuration

Thirteen capabilities, four groups, so this stays scannable instead of becoming a flat junk drawer:

**Business setup**
- Organization and terminology
- Policies
- Country and industry packs
- Language and localization
- Payroll configuration (pay groups, salary components, tax and contribution rules —
  also reachable contextually from the Payroll workspace's "Setup")

**Process design**
- Forms and fields
- Workflows and approvals
- Automation
- Templates
- Self-service experience configuration

**Security and compliance**
- Access and permissions
- Privacy and consent administration
- Audit and data administration

**Technical**
- Integrations and developer tools
- Vendor and contract management

## Route principles

- Routes describe user goals: `/hr/people`, `/hr/leave/requests`, `/hr/payroll/runs`.
- Every operational record has a stable URL.
- List state may be shared through URL filters.
- Modal-only navigation is prohibited for complex records.
- Configuration routes are separate from operational routes.
- Sensitive routes return a neutral not-found experience when existence must not be disclosed.

## Search

Global search groups results by:

- People
- Actions
- Requests and cases
- Documents
- Reports
- Configuration

Search respects field masking, record scope and sensitive-case non-disclosure.

## Command palette

The command palette supports permitted actions such as:

- Add worker
- Start onboarding
- Request leave
- Record attendance correction
- Create requisition
- Start payroll run
- Generate letter
- Open configuration

Commands display the required workspace and organization context before execution.

