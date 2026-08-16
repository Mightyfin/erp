# M30 — Employee relations and case management completion

M30 completes confidential employee-relations investigations while keeping anonymous protected disclosures in a separate, more tightly controlled workflow.

## Delivered

- Restricted case references and redacted triage queues that do not expose allegations.
- Persisted no-conflict/conflict declarations before case detail can be opened.
- HR-admin assignment controls and investigator-specific access enforcement.
- Validated case transitions from open through triage, investigation, resolution, and closure.
- Investigation actions with pending/completed/cancelled states and resolution blocked while actions remain open.
- Restricted evidence upload/download with content-type and 10 MB size validation, backed by a persistent production volume.
- Required findings and outcomes before case resolution or closure.
- Immutable audit events for creation, access, views, assignments, transitions, actions, and evidence custody.
- A separate protected-disclosure queue whose list response omits the anonymous narrative.
- Protected-disclosure assignment, triage, investigation, resolution/dismissal, and its own audit history.
- Live case-operations and protected-disclosure investigator workspaces.

## Lifecycles

Employee-relations cases follow:

`open → triage → investigating → action-pending → resolved → closed`

An investigator may return a resolved case to investigation. Closure is terminal. Existing `in-progress` records can enter the new investigation workflow.

Protected disclosures follow:

`new → triage → investigating → resolved | dismissed`

Protected disclosures never become ordinary relations cases or HR requests. Their anonymous intake reference and access code remain in the dedicated disclosure record.

## Verification

- Backend tests cover access declarations, conflict denial, queue redaction, action gating, investigation closure, disclosure separation, and protected audit history.
- The frontend production build and targeted lint must pass.
- Playwright exercises both the restricted case journey and the separate protected-disclosure workspace against the deployed application.
