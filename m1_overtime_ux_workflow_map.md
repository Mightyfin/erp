# Milestone 1 Overtime UX Workflow Map

## Why the current page feels robotic

The current Time Operations page is functionally connected to the real API, but its information architecture is implementation-led rather than work-led. It presents a large collection of unrelated cards—shift rule creation, shift assignment, attendance import, leave accrual, balance adjustment, encashment, escalation, and overtime—at the same visual weight. The user has to understand internal IDs, API concepts, and configuration mechanics before they can complete a daily review task.

The overtime reviewer also receives insufficient context. The page has one global decision-reason field instead of a reason attached to the row being reviewed, no status tabs or queue counts, no clear period/filter context, no distinction between daily review and setup work, no visible payroll handoff state, and no direct explanation of what approving the row will do. The existing last-operation JSON block is useful for debugging but not for an operational user.

## Users and jobs to be done

| User | Primary job | Decision they need to make | Evidence they need on screen |
|---|---|---|---|
| HR/time administrator | Bring attendance into a trustworthy review queue | Is the import complete and are there exceptions to resolve? | Import status, imported/rejected counts, date range, source file, exception link |
| Line manager or HR reviewer | Review overtime for their team/branch | Is the recorded overtime justified and within my scope? | Employee, work date, scheduled vs actual hours, overtime hours, multiplier, source, reason, scope |
| Payroll preparer | Prepare a payroll period using approved time | Which approved overtime records will enter this pay run? | Period, approved hours, estimated amount, pending blockers, payroll allocation state |
| Payroll approver/releaser | Confirm payroll control totals | Does the payroll total include the correct approved overtime exactly once? | Overtime total, source attendance reference, payroll line, release status, reconciliation state |
| Employee | Understand their own time outcome | Was my overtime approved, rejected, or paid? | Date, hours, status, reason, payroll period/reference |

## Core journeys

### Journey A — Daily reviewer queue

The reviewer enters **Time operations** and immediately sees the current queue summary. The page tells them how many records are waiting, how many hours are pending, what has already been approved, and what has been paid. The default view is **Needs review**, not an undifferentiated activity list.

The reviewer filters by period, employee, branch, or status. Each row shows a plain-language comparison such as `8 scheduled → 11 worked`, `3 overtime hours`, and `1.5× weekday rule`. Selecting a row opens an inline detail panel with the attendance source, shift rule, calculation, and any existing reason. The reviewer approves or rejects from that row. Reject requires a reason; approve requires an explicit confirmation that the hours will become eligible for payroll. The row updates immediately, then reconciles with the server response.

### Journey B — Attendance-to-queue handoff

The time administrator uses a separate **Import attendance** action. The import form is progressive: first choose the source and period, then paste/upload rows, then review a validation summary before committing. After commit, the page offers a direct `Review overtime` action scoped to the imported batch and tells the user whether there are rejected rows or pending overtime records.

Shift rules and assignments are secondary configuration tasks. They should be reachable from a clearly labelled **Setup** area or an expandable section, not compete with the daily overtime queue for primary attention.

### Journey C — Approved overtime to payroll

The payroll preparer opens the **Payroll handoff** view for a pay period. It displays approved hours and estimated cost, pending records that are not yet eligible, rejected records excluded from payroll, and already-paid records linked to a released run. The user can open the relevant payroll run and source attendance record from the handoff. Recalculation should show stable totals and never duplicate the earning.

### Journey D — Employee explanation

An employee-facing view is not the first redesign target, but the underlying status language must be user-readable and consistent: **Needs review**, **Approved for payroll**, **Rejected**, and **Paid in payroll**. Technical values such as `pending`, `approved`, and `overtimePayrollLineId` can remain available in a detail drawer or audit view but should not be the primary language.

## Proposed information architecture

| Layer | Proposed UI | Purpose |
|---|---|---|
| Page header | `Overtime & attendance` with period context and one primary action `Import attendance` | Establishes what the screen is for and where the user is operating. |
| Workflow strip | `1 Import → 2 Review → 3 Approve → 4 Payroll` with the current stage highlighted | Makes the end-to-end process visible without pretending every stage is complete. |
| Queue summary | Needs review, approved, rejected, paid, and total hours/cost cards | Gives the reviewer orientation and prioritisation. |
| Queue controls | Period selector, status tabs, employee search, branch/scope, refresh | Reduces scanning and makes the queue actionable. |
| Queue list | Dense but readable rows with decision actions attached to each row | Turns a backend list into a review workspace. |
| Detail drawer/panel | Calculation breakdown, source import, shift rule, audit trail, payroll link | Preserves context and supports safe decisions without leaving the queue. |
| Secondary tasks | Collapsible `Attendance tools` and `Leave tools` sections | Keeps configuration and infrequent actions available without overwhelming the daily workflow. |
| Feedback | Inline success state, optimistic row transition, server reconciliation, meaningful errors | Makes action consequences clear and reduces uncertainty. |

## Interaction and visual principles

The redesign should use a calm operations-console hierarchy: a strong page title, a short explanation, a yellow primary action, navy text and navigation, soft neutral surfaces, restrained status colors, tabular numeric alignment, and clear whitespace between the daily queue and secondary setup tools. It should avoid presenting every control as an equal card and should not expose raw JSON as the main confirmation mechanism.

Actions must be local to the record they affect. A reviewer should never have to guess which row a global reason field applies to. The default queue should prioritise pending work, while approved, rejected, and paid records remain available through tabs. Every mutation needs an explicit pending state, an immediate visible result, and a server-confirmed final state. The layout must remain usable at mobile widths with stacked row actions and thumb-sized buttons.

## Acceptance criteria for the UI redesign

| Criterion | Definition of done |
|---|---|
| Workflow clarity | A new reviewer can explain the four-step flow and identify what needs attention without reading implementation notes. |
| Queue prioritisation | The first screen makes pending records and pending hours obvious and opens on the review queue. |
| Decision safety | Approve/reject actions are attached to the selected record; rejection reason is local and required; paid rows have no decision controls. |
| Payroll transparency | Approved, paid, and excluded states are visible, with payroll handoff/reference where returned by the API. |
| Real-data integrity | Loading, empty, error, and degraded states never show mock/demo rows. |
| Accessibility | Labels, focus states, keyboard reachability, readable contrast, and responsive action layouts are preserved. |
| Operational efficiency | A reviewer can process several pending records without navigating away or re-entering global context. |
| Visual quality | The page reads as a purposeful workflow workspace rather than a collection of API forms. |
