# M22 — HR Requests Inbox, Worker Self-Service Requests and Onboarding Wiring

Date: 2026-08-14 · Commits: `710a358`, `0584498` on fork
`georgemungamba/erp` main · Deployed to production
(`erp.mightyfinance.co.zm`) and mirrored to `Mightyfin/erp` main via the
server commit-tree merge.

## Goal

Move the HR module from its mocked employee-experience shell to the real
backend. HR administrators now see a genuine **requests inbox**, can open a
thread, reply (including internal notes) and resolve a case; workers raise
their own requests through a guided four-step flow; and the **onboarding**
section reports real per-worker statutory readiness. The **approvals hub**
consolidates the four decision queues — leave, attendance corrections, HR
requests and workflow items — into one list of things waiting on a decision.

## Findings

The backend already exposed the request surface (`hr_requests` and
`hr_request_messages` in the `hrm` schema, listed by
`GET /experience/requests`, created by `POST /experience/requests`, threaded
by `POST /experience/requests/{id}/messages`), but every page that consumed
them was pure mock data. `hr_requests.worker_id` was **NOT NULL** with a
foreign key, which made it impossible for HR to raise a request that was not
attached to a specific worker — the inbox must allow organisation-level
cases. In addition, the approvals hub was pointing at route paths that do
not exist (`/leave/leave-requests`, `/time/time-corrections`,
`/workflows/queue`), so it rendered an empty queue.

## Backend changes

`ResolveWorkerId` now honors only the `worker_id` claim — a raw `sub` UUID
is no longer silently accepted as a worker identity, which closes an
identity-confusion gap where a Keycloak subject could impersonate a worker.
`hr_requests.worker_id` became nullable (`Guid?`) in the domain, DTO and
database (migration `M22RequestWorkerNullable`, applied to production), so
HR-initiated organisation cases no longer violate the FK. The create
endpoint accepts the nullable worker id and falls back to the caller's own
subject identity when none is supplied.

A real production bug surfaced and was fixed: EF Core 10 **demotes
navigation-tracked children** of a `Modified` parent during `SaveChanges`,
so the repository's message update path either updated zero rows (throwing
`DbUpdateConcurrencyException`) or inserted duplicates. The fix, already
established as the safe pattern in M20, is to insert new messages
top-level via `db.Set<HrRequestMessage>().Add()` instead of through the
parent's navigation collection, and `UpdateRequestAsync` pins existing
tracked messages to `Unchanged`. A new `AddMessageAsync` on
`IExperienceRepository` encapsulates this, and `ExperienceServiceImpl`
uses it directly. Three new tests were added (120 passing total), covering
creation without a worker, the nullable-worker round-trip and the message
insert pattern.

| Change | File |
| --- | --- |
| `worker_id` nullable + migration | `Workflows.cs`, `Migrations/…M22RequestWorkerNullable.cs` |
| Identity resolution hardened | `ApiRoutesClean.cs` (`ResolveWorkerId`) |
| Nullable create payload + subject fallback | `ExperienceServices.cs` |
| Safe message insert, pinned children | `Repositories.cs` (`UpdateRequestAsync`, `AddMessageAsync`), `Interfaces.cs` |
| Round-trip coverage | `ExperienceServiceTests.cs` |

## Frontend changes

Six pages in `module-connect` were rewritten against the real API, and
`use-api.ts` gained the matching helpers (`experienceRequests`,
`createExperienceRequest`, `addExperienceRequestMessage`,
`resolveExperienceRequest`). The release scope gate (`scope.ts`) now
enables the **Lifecycle** and **Employee experience** navigation sections
and the `/hrm/requests` and `/hrm/lifecycle/onboarding` prefixes.

| Page | Behaviour |
| --- | --- |
| `/hrm/requests` | Real inbox: open/awaiting/resolved tabs, category and confidentiality filters, 8-character reference links, employee names instead of raw IDs |
| `/hrm/requests/new` | Four-step guided flow — category, subject, details, confidentiality — posting to the create endpoint |
| `/hrm/requests/[id]` | Live thread: original request, chronological messages, HR-only internal notes (role-gated to `hr_ops`/`hr_admin`), reply, and resolve/close |
| `/hrm/lifecycle/onboarding` | Active workers with a derived statutory progress bar and status groups (first-day, pending documents, cleared) |
| `/hrm/lifecycle/onboarding/[id]` | Per-worker statutory readiness: NRC, TPIN, NAPSA, bank account and contract assignment, with an outstanding-items alert |
| `/hrm/approvals` | Unified decision queue pulling `GET /time/leave`, `GET /time/corrections`, `GET /experience/requests` and `GET /workflow/queue`, filtering to decidable statuses (pending/submitted/in-review/returned/open/in-progress/awaiting-employee) and grouping by type |

During integration the approvals hub revealed that the workflow queue row
uses `subjectName` for the worker and `currentApproverName` for the
decision-maker, and that raw statuses arrive lowercase — both were
normalized to title-case labels before the decidable-status filter so rows
actually appear in the queue.

## Verification on production

All checks were performed against the live API and the deployed UI on
`erp.mightyfinance.co.zm`: the inbox lists three requests with correct
statuses and readable references; a new request raised through the guided
flow appears in the inbox immediately; a reply posted in the thread is
persisted and visible to both sides of the conversation; resolving a case
transitions it through `resolved` to `closed`; the onboarding list shows
all workers with accurate statutory progress (EMP-0003 at 1/5, DEV-001 and
EMP-0002 at 0/5); and the approvals hub's four queues return decidable
rows (leave `approved`/`rejected`, one `submitted` correction, two
`submitted` workflow items) which the hub renders once the session is
valid. The E2E test requests were resolved afterwards and left in a clean
closed state.

## Notes for M23

Two candidate directions emerge from the gap analysis. The stronger one is
**statutory compliance reporting** — ZRA PAYE return files and
NAPSA/NHIMA remittance files — which is the last payroll prerequisite
before any real pay run can be signed off. A secondary option is the
**employee self-service layer** (leave requests and profile edits from the
employee side), but the employer-side focus remains the priority.
