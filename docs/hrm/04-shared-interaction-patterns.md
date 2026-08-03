# Shared Interaction Patterns

## 1. Home and work queue

The home page prioritizes action over analytics:

1. Urgent exceptions
2. Approvals due
3. Assigned tasks
4. Upcoming deadlines
5. Informational metrics

Each queue item shows subject, action, age, due date and confidentiality indicator.

## 2. List page

Standard structure:

- Page title and one primary action
- Saved views
- Search
- Essential filters
- Results
- Bulk actions shown only after selection
- Column picker
- Export shown only when permitted

Lists preserve filters and position when a user returns from a record.

## 3. Record detail

Standard structure:

- Identity and status header
- Primary action
- Summary
- Relevant sections
- Timeline
- Related records
- Permission-controlled audit details

Do not repeat the same actions in the header, sidebar and footer.

## 4. Guided create or change flow

Use for onboarding, leave, employee changes, expenses and separation:

1. Choose purpose
2. Enter essential details
3. Review policy or calculation
4. Add evidence if required
5. Review submission
6. Submit and show next steps

Long flows autosave and provide a clear exit-to-draft action.

## 5. Approval

Approvers receive:

- Decision summary
- Policy and rule results
- Relevant history
- Conflicts or exceptions
- Supporting evidence
- Approve, return, reject or delegate actions when permitted
- Mandatory reason for negative or exceptional decisions

Approval UI must not expose unrelated sensitive fields.

## 6. Status timeline

Timeline events use:

- Human-readable event
- Actor or system source
- Timestamp and time zone
- Reason or comment
- Before/after summary when permitted
- Link to evidence or related record

Technical delivery logs belong in an advanced operations view.

## 7. Exception resolution

Exceptions show:

- What failed
- Business impact
- Affected records
- Safe recommended action
- Retry or reconcile option
- Escalation path
- Correlation identifier in advanced details

Never display raw stack traces to HR users.

## 8. Empty states

An empty state explains:

- What belongs here
- Why it may be empty
- The permitted next action
- Whether filters are hiding results

## 9. Notifications

Notifications are actionable but not a substitute for work queues.

- Group repeated events.
- Avoid exposing sensitive details on lock screens or email subjects.
- Deep-link to the relevant record.
- Show delivery preference and confidentiality policy where relevant.

## 10. Calculations

Payroll, leave and compensation calculations provide:

- Result
- Inputs
- Rule or configuration version
- Effective date
- Explanation
- Difference from prior result
- Permission-controlled technical details

## 11. Destructive and historical actions

Prefer cancel, reverse, supersede or close over delete. If deletion is permitted:

- Explain impact
- Identify related records
- Require an appropriate reason
- Record the action
- Never silently remove released payroll, protected evidence or required audit history

