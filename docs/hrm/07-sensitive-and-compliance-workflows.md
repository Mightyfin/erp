# Sensitive and Compliance Workflows

## General sensitive-data rules

- Do not reveal record existence to unauthorized users.
- Mask values in lists, notifications and shared-screen contexts.
- Require deliberate reveal for highly sensitive fields.
- Log sensitive views where policy requires it.
- Keep case access separate from ordinary HR administration.
- Use neutral browser titles and notification text.

## UI-PRV-001 / UI-PRV-002 — Personal-data consent and subject rights

### Experience

1. Employee opens a personal-data summary grouped by purpose (payroll, benefits, biometric attendance, background checks, AI-assisted screening where used).
2. Each purpose shows what is collected, why, and its current consent status.
3. Employee withdraws consent where withdrawal is legally and operationally permitted; the UI explains any resulting loss of capability before confirming.
4. Employee requests an export or correction of their own data, or a right-to-erasure request where retention allows it.
5. Confirmation shows a reference, expected response deadline and where to track status.

### Simplicity requirements

- Group by purpose, not by underlying data table.
- Explain consequences of withdrawal in plain language before the destructive step.
- Never silently deny a request; always show the reason and any escalation path.

## UI-PRV-003 — Subject-rights request triage

1. Authorized HR/privacy admin opens a restricted request queue.
2. Each request shows type (access, correction, erasure, consent withdrawal), deadline and affected systems.
3. Admin reviews what data exists, cross-border transfer implications and any legal-hold conflicts before fulfilling.
4. Fulfilment or refusal is recorded with reason; erasure requests preserve legally-retained records (payroll, audit) rather than silently deleting them.
5. Closure notifies the employee and preserves an audit trail of the request and response.

## UI-CMP-004 — Anonymous protected disclosure

### Entry

An organization may publish a configurable public, private, workforce-only or organization-code reporting link.

### Reporter experience

1. Landing page explains purpose, emergency limitations, privacy and available reporting modes.
2. Reporter chooses anonymous or identified reporting where permitted.
3. Reporter selects a broad concern category or starts with free text.
4. Guided questions adapt without forcing the reporter into an incorrect category.
5. Reporter adds narrative, dates, involved parties and evidence.
6. Before submission, the UI explains metadata handling and any limitation to anonymity.
7. Submission returns an anonymous reference and secure access code.
8. Reporter can return to communicate, add evidence and view safe status updates.

### Safety requirements

- No account is required for anonymous reporting.
- Do not request name, employee number or contact details unless the reporter chooses identification.
- Minimize IP, device, location and attachment metadata.
- Never place case details in email subjects or browser-history labels.
- Provide a fast safe-exit action without misleading claims about clearing network or employer logs.
- Accessible and multilingual presentation is required.

## UI-CMP-005 — Protected disclosure triage

1. Authorized investigator opens a restricted queue.
2. The system requires conflict declaration before revealing full details.
3. Conflicted users are removed from the case and cannot discover later activity.
4. Investigator assesses urgency, safeguarding, jurisdiction and routing.
5. Case plan defines restricted team, deadlines and evidence controls.
6. Reporter communication uses the protected channel.
7. Findings, remediation and disclosure decisions are recorded separately.
8. Closure preserves evidence, retention and retaliation follow-up.

### Non-disclosure behavior

- Ordinary HR administrators cannot search, count or infer protected cases.
- Reports suppress small groups and rare attributes.
- Exports require explicit permission and are audited.

## Payroll work centre

### Payroll home

Show:

- Current period and cutoff
- Readiness score
- Blocking exceptions
- Pending approvals
- Headcount and control totals
- Payment and accounting status

Do not begin with a dense employee payroll table.

### Payroll run

1. Confirm period and population.
2. Review readiness blockers.
3. Calculate with visible progress and resumable status.
4. Review material variances and exceptions.
5. Resolve, waive with authority, or exclude with reason.
6. Approve using segregation of duties.
7. Release payments, accounting and payslips as separate controlled stages.
8. Reconcile totals and close the period.

Every result exposes calculation version, inputs, adjustments and historical lineage to permitted users.

## Employee-relations and disciplinary cases

- Use restricted case queues.
- Separate allegations, evidence, findings and outcomes.
- Display procedural deadlines.
- Record representation, appeals and acknowledgements.
- Avoid prejudicial labels before findings.
- Protect medical, witness and protected-characteristic data.
- Preserve effective and historical outcomes without placing unnecessary details on the general employee profile.

## Health and safety

Worker reporting should prioritize:

1. Immediate safety instructions
2. Location and incident essentials
3. Injury or exposure needs
4. Evidence
5. Follow-up contact

Emergency reporting must not be delayed by a long form.

