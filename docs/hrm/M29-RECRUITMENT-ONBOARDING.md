# M29 — Recruitment and onboarding completion

M29 completes the operational path from an approved hiring need to an active worker record.

## Delivered

- Vacancy draft, publish, and close controls, with applications restricted to published vacancies.
- A validated candidate state machine with an immutable stage-event history.
- Interview scheduling and persisted score/recommendation decisions.
- Offer drafting, HR-admin approval, issue, acceptance, decline, expiry, and response timestamps.
- Candidate document upload/download before a worker record exists.
- Transactional, duplicate-safe conversion of an accepted candidate into a pre-hire worker and assignment.
- A persisted preboarding case with required tasks for contract, identity, statutory data, bank data, and induction.
- Worker activation blocked until every required preboarding task is complete.
- A live Hiring operations UI covering the complete journey and operational offer/preboarding queues.

## Lifecycle

`applied → screening → shortlisted → interviewing → interviewed → offered → preboarding → hired`

Candidates may be rejected from any selection stage. `hired` and `rejected` are terminal. Offer acceptance creates a `pre-hire` worker; only successful preboarding activation changes the worker to `active`.

## Verification

- Backend integration tests cover the full candidate-to-worker journey, tenant scoping, invalid transitions, approval order, and activation gating.
- The frontend production build and targeted lint must pass.
- Playwright exercises the hiring operations journey through activation against the deployed application.
