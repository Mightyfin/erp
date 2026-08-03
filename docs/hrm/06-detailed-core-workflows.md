# Detailed Core Workflows

## UI-FND-007 — Initial HR admin setup

The first real user of the system is almost always an HR administrator setting it up from empty,
not an employee or manager. Everything else in this catalogue assumes an organization, roles,
policies and a workforce already exist — this workflow is what gets from empty to that point, and
its absence would be the single biggest simplicity failure in the whole product: an empty Home
page, an 11-item nav rail and a 13-item Configuration hub with no suggested order.

### Experience

A short, linear, resumable setup guide replaces the ordinary flat navigation until it is
completed or deliberately dismissed:

1. **Organization** — confirm legal entity, branches/departments and default work calendar.
2. **Access** — confirm the admin's own account, then invite additional HR staff and assign
   roles (sensible default roles are pre-selected; deep permission tuning is deferred to
   Configuration).
3. **Capabilities** — choose which HRM capability tiers to enable now. Essentials are
   preselected and recommended; Advanced and Enterprise capabilities are opt-in, explained in one
   line each, and can be enabled later without reconfiguring anything already set up.
4. **Core policies** — set the essentials only: leave types and entitlements, probation length,
   approval routing default. Everything else keeps a safe default until changed.
5. **Workforce** — add the first workers, either one at a time or via bulk import, with a preview
   before committing.
6. **Review and go live** — a summary of every choice made, with a single confirmation. After
   this step, Home switches from setup mode to its normal work-queue view.

### Simplicity requirements

- One step, one screen, one clear "what happens if I skip this" note per step.
- Progress is saved automatically; the admin can leave and resume exactly where they stopped.
- Every step shows a working default so the admin is never blocked by an unanswered question.
- Skipping a step is always allowed except where it would leave the system unusable (e.g. no
  organization identity); skipped items reappear as a visible, dismissible Home banner, not a
  blocking wall.
- Nothing chosen here is a one-time, irreversible decision — every choice is reachable and
  changeable later from the normal Configuration entry point.

### Key states

Not started, In progress (per step), Skipped (per step, reappears as a banner), Complete

## UI-PPL-004 — Onboarding

### Entry points

- Accepted recruitment offer
- HR quick action
- Worker record action

### Experience

1. HR selects worker, employment and start-date context.
2. The system proposes a configurable onboarding plan.
3. HR reviews responsible owners, dates and required evidence.
4. The manager confirms role-specific tasks, equipment and access requests.
5. The employee receives a simple checklist ordered by due date.
6. Each participant completes assigned tasks without seeing restricted tasks.
7. HR monitors blockers and overdue items from an exception view.
8. Completion shows outstanding risks, acknowledgements and probation start.

### Simplicity requirements

- Employees see one checklist, not the underlying workflow graph.
- Required-now tasks appear before future tasks.
- Tasks explain why they matter and who can help.
- External provisioning status is summarized; technical logs remain hidden.

### Key states

Draft, Ready, Active, Blocked, Completed, Cancelled

## UI-TIM-007 — Request leave

### Experience

1. Employee selects leave type.
2. Calendar shows balance, eligible dates, holidays, team conflicts and blocked dates.
3. Employee selects dates or hours.
4. The system explains the calculated duration and projected balance.
5. Required evidence appears only when triggered by policy.
6. Employee reviews approver route and submits.
7. Confirmation shows status, balance reservation and next step.

### Exceptions

- Insufficient balance
- Ineligible leave type
- Blocked date
- Overlapping request
- Missing evidence
- No approver
- Payroll cutoff impact

The UI explains the problem and permitted next action; it does not expose rule-engine internals.

## UI-TIM-003 — Attendance correction

### Experience

1. Employee opens the affected day.
2. Timeline shows captured punches, source and derived attendance.
3. Employee selects the issue: missing punch, wrong time, wrong shift, duty, leave or other.
4. Employee proposes a correction and supplies a reason/evidence when required.
5. The UI previews the corrected hours and possible overtime impact.
6. Submission displays the approval route and payroll-cutoff warning.

### Manager review

- Compare original and proposed values.
- See schedule, approved duty and related leave.
- Approve, return or reject with reason.
- Avoid exposure of unrelated location history.

## UI-PPL-005 — Employee movement

### Experience

1. Authorized user selects movement purpose.
2. Current assignment appears as a read-only baseline.
3. User enters effective-dated changes.
4. Impact summary covers reporting line, access, payroll, benefits, position and documents.
5. Required approvals and conflicts are displayed.
6. Submission creates a pending future change; it does not overwrite current history.
7. Completion shows downstream status and unresolved exceptions.

## UI-EXP-003 — Expense claim

### Experience

1. Employee selects trip, advance or standalone claim.
2. Employee adds receipt using camera, upload or file picker.
3. The UI proposes amount, date, currency and category when extraction is available.
4. Employee confirms business purpose and cost allocation.
5. Policy warnings appear beside the affected item.
6. Summary reconciles claim, advance and amount due.
7. Submission shows approver and expected settlement route.

## UI-XPR-001 — HR request

### Experience

1. Employee describes the need in plain language or selects a category.
2. Suggested knowledge answers appear without blocking case creation.
3. The form requests only category-relevant information.
4. Employee selects confidentiality and preferred contact where permitted.
5. Confirmation provides case reference, service target and next update.
6. Conversation and status remain in one thread.

## UI-XPR-002 — HR letter

### Experience

1. Employee selects letter purpose.
2. The UI shows eligibility, data that will appear and expected delivery.
3. Employee confirms configurable details.
4. If approval is required, the request enters the standard approval pattern.
5. The released document appears in Documents with version and verification information.

## UI-PAY-001 — View payslip

### Experience

1. Employee selects a pay period.
2. Summary shows gross, deductions, net and payment status.
3. Expandable sections explain each component.
4. Year-to-date and comparison values are shown when permitted.
5. Employee can open the released document, verify it or raise a payroll query.
6. Corrected versions are linked; the original remains historically visible.

## UI-PPL-007 — Offboarding

### Experience

1. Authorized user selects separation reason and proposed last working date.
2. The system previews notice, leave, payroll, benefits, assets and access impacts.
3. Configured approval occurs before irreversible actions.
4. Employee, manager, HR, payroll and asset owners receive scoped checklists.
5. Blockers are visible in a consolidated clearance view.
6. Final review identifies unresolved balances, evidence and legal documents.
7. Completion preserves the worker record and rehire eligibility.

