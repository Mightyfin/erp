# Procurement Phase 1 Feature Specifications

> **Superseded draft.** The current source of truth is [PROC-00](../PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md). Its Phase 1 scope and `PROC-01`/`PROC-02` identifiers differ from this provisional draft. Do not use these IDs as the current feature catalogue.

**Status:** Draft for product and stakeholder review<br>
**Version:** 0.1<br>
**Prepared:** 3 October 2026<br>
**Framework:** [ERP Feature Specification Framework](../../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx)<br>
**Business context:** [Procurement Enterprise Workflow Blueprint](ENTERPRISE-WORKFLOW-BLUEPRINT.md)

These specifications apply the required hierarchy **Module → Parent Feature → Child Feature → Operation or Scenario**. They define Phase 1 behaviour before design or development. All thresholds, legal rules, authorities, owners, and integration availability are draft assumptions until approved. A section marked as an open decision is not permission to silently choose a default in code.

## Module definition

**Module:** Procurement. **Purpose:** control business purchasing from request through supplier, order, receipt, invoice validation, and Finance/AP handoff. **Boundary:** Procurement owns purchase transactions and supplier eligibility; HRMS owns workforce and organization master data; Finance owns budgets, accounting, AP posting and payments; Inventory owns stock and valuation. **Deployment:** use the ERP's adopted module boundaries and versioned contracts; resolve Go versus ASP.NET Core runtime and migration ownership before implementation. **Shared requirements:** tenant and entity scope, effective-dated policy, server-side authorization, immutable business history, accessible task queues, idempotent integration, and synthetic-data sandbox. **Release gate:** no child feature below is implementation-ready while its named business and integration decisions remain unresolved.

| Parent feature | Purpose, shared actors and policy | Phase 1 child feature | Later candidates and dependency |
|---|---|---|---|
| Procurement foundation | Configure policy and consume workforce reference data; admin, HRMS and Finance stewards; changes audited | PROC-01 Policy and workforce context | More countries and entities after reference contracts |
| Supplier management | Distinguish proposed from orderable suppliers; supplier steward and Finance reviewer | PROC-02 Supplier activation and maintenance | Supplier portal and advanced qualification in Phase 2 |
| Buying and approvals | Record need, evaluate policy and budget, decide authority; requester, manager, budget owner | PROC-03 Purchase request; PROC-04 Approval and budget decision | Catalog and contract buying in Phase 2 |
| Purchase ordering | Create and issue a controlled commercial commitment; buyer and PO approver | PROC-05 Purchase order and revision | Blanket orders and schedules after policy approval |
| Fulfillment | Record accepted goods or services; receiver or service owner | PROC-06 Goods receipt and service acceptance | Inventory and asset events when those contracts exist |
| Invoice validation | Match cost evidence and hand off to Finance; AP processor and Finance | PROC-07 Invoice match and AP handoff | OCR and supplier invoice portal in Phase 2 |

| Parent feature | Shared risk and control | Measure |
|---|---|---|
| Procurement foundation | Stale worker status or changed policy could grant wrong authority; pin versions and fail closed | Stale projection age, orphan tasks, policy coverage |
| Supplier management | Duplicate or unverified supplier/payment change could misdirect spending; independent review | Onboarding time, duplicate backlog, suspended supplier orders blocked |
| Buying and approvals | Unapproved or unfunded demand could be ordered; enforce Finance decision and self-approval guard | Approval age, hard budget blocks, exceptions |
| Purchase ordering | Issued terms could drift from approval; snapshot and revise rather than overwrite | Issue cycle time, unacknowledged POs, revisions |
| Fulfillment | False or excess receipt could validate an invoice; enforce tolerance and evidence | Rejection rate, open receipts, reversal rate |
| Invoice validation | Duplicate or unmatched invoice could reach AP; match and use stable handoff key | First-pass match, duplicate blocks, AP rejections |

The following operation contracts make the final hierarchy level testable. The matching child sections below contain the full fields, policy, API, recovery and test details.

| Operation or scenario | Workflow and actor | Rule, error and event | Acceptance evidence |
|---|---|---|---|
| PROC-01-O1 Publish policy | Admin drafts; independent reviewer publishes | P01-R1/R3; overlap blocks; `policy.published` | Old decisions retain old version |
| PROC-01-O2 Sync worker | HRMS event updates minimal projection | P01-R2; stale/duplicate event quarantined or ignored; sync receipt logged | Inactive worker cannot act |
| PROC-01-O3 Reassign task | Workflow service moves task after worker/manager change | P01-R4; no fallback escalates; reassignment fact recorded | Prior actor and new owner visible |
| PROC-02-O1 Activate supplier | Steward reviews, Finance verifies sensitive details, approver activates | P02-R1/R2; duplicate blocks pending review; `supplier.activated` | Only active supplier is orderable |
| PROC-02-O2 Change supplier | Steward proposes version; independent verifier approves sensitive change | P02-R3; verification failure keeps prior version | New terms not active early |
| PROC-02-O3 Suspend supplier | Authorized steward records reason and affected orders | P02-R4; unauthorized actor denied; `supplier.suspended` | New PO issue blocked, history visible |
| PROC-03-O1 Submit PR | Requester saves, validates and submits version | P03-R1; invalid coding blocks; `pr.submitted` | One version and task after retry |
| PROC-03-O2 Resubmit PR | Approver returns; requester revises and submits | P03-R2; stale edit conflicts; `pr.submitted` for new version | Prior version and reason preserved |
| PROC-03-O3 Cancel or close | Requester/owner cancels eligible unused demand | P03-R3; ordered balance protected; `pr.cancelled` | Finance release reconciled if reserved |
| PROC-04-O1 Check funds | Service requests Finance result | P04-R2; outage leaves Pending; response logged | Hard failure cannot approve |
| PROC-04-O2 Decide approval | Assigned approver acts under pinned policy | P04-R1/R3/R4; self-approval denied; decision event | Decision has actor, reason if required, policy version |
| PROC-04-O3 Resolve exception | Named budget authority approves/denies with evidence | P04-R2/R6; no silent override; exception event | Commitment waits for Finance resolution |
| PROC-05-O1 Convert lines | Buyer creates PO from approved unconsumed PR lines | P05-R1/R2; insufficient balance blocks | Source balance and PO lines agree |
| PROC-05-O2 Issue PO | Approver authorizes; delivery adapter sends; supplier acknowledges | P05-R4; failed delivery remains visible; `po.issued` | One immutable issued version and dispatch log |
| PROC-05-O3 Revise or cancel | Buyer proposes delta; approver decides; supplier notified | P05-R3/R5; unapproved revision cannot issue; `po.revised`/`.cancelled` | Old version and commitment adjustment traceable |
| PROC-06-O1 Receive goods | Receiver inspects and accepts line quantity | P06-R1/R2; over-tolerance blocks; GRN fact | Remaining balance correct |
| PROC-06-O2 Certify service | Assigned owner confirms milestone evidence | P06-R2; missing evidence blocks; acceptance fact | Accepted value available for matching |
| PROC-06-O3 Reverse or return | Authorized receiver links correction to original | P06-R3/R4; unlinked deletion denied; reversal fact | Matching excludes reversed amount |
| PROC-07-O1 Detect and match | AP captures invoice and evaluates PO/receipt | P07-R1/R2; duplicate or variance routes exception | Match decision names rule and source lines |
| PROC-07-O2 Resolve exception | Assigned owner documents correction/approval and reruns match | P07-R3; unresolved hard variance blocks | Original and resolution versions retained |
| PROC-07-O3 Hand off and reconcile | AP sends validated snapshot; Finance acknowledges/statuses | P07-R4/R5; unknown outcome reconciled by key; AP event | One payable request; Paid only on Finance confirmation |

## PROC-01 Policy and workforce context

### 1. Feature Identification

Child **PROC-01**, parent **Procurement foundation**, Phase 1, version 0.1, draft. Proposed business owner: Procurement policy owner; data owners: HRMS for workforce, Finance for budget and coding, Procurement for policy; product and technical owners to assign.

### 2. Purpose and Business Outcome

Give all transactions the same effective policy and current workforce context. Success means decisions can be reconstructed from a policy version and stale employees cannot start work.

### 3. Scope and Exclusions

Includes policy versions, numbering, category and tolerance settings, delegated authority, minimal HRMS worker projection, sync health and task reassignment. Excludes editing HRMS records, defining tax law, and hosting a generic ERP identity service.

### 4. Parent and Related Features

Parent foundation; supplies PROC-02 through PROC-07. HRMS and workforce identity supply active status, subject ID, manager, organization, entity and location; Finance supplies cost-centre and budget contracts.

### 5. Actors and Participants

Procurement administrator, HRMS steward, Finance steward, delegated approver, worker, integration operator, sync job, workflow engine, auditor.

### 6. Entry Points and Triggers

Admin configuration page, approved policy publication, HRMS change event or scheduled refresh, employee deactivation, manager reassignment, and retry from operations queue.

### 7. Preconditions

Tenant and entity exist; admin has configuration permission; policy has effective date and approver; HRMS source identifier is stable; sandbox uses synthetic identities.

### 8. Workflow

PROC-01-O1: admin drafts rule set → validates overlaps and authority → second authorized reviewer approves → publish effective version. PROC-01-O2: sync receives HRMS change → validates tenant and version → updates minimal projection → disables new actions for inactive worker. PROC-01-O3: manager change or deactivation identifies pending tasks → applies approved fallback/delegation rule → records reassignment and notifies new owner. Failed sync enters reconciliation queue.

### 9. Status and State Transitions

Policy: Draft → Pending Review → Published → Superseded; rejected review returns to Draft. Projection: Current, Stale, Blocked. Task reassignment: Pending → Reassigned or Escalated. Published policy is never edited in place; historical decisions keep its version.

### 10. Permissions and Data Scope

Configuration is tenant/entity scoped and limited to procurement admins; publishing requires separate authority. Worker projection exposes only permitted work fields. Integration operator sees health and IDs, not unrelated HR data. A delegate cannot exceed the delegator's authority.

### 11. Business Rules

**P01-R1:** exactly one applicable published policy version per tenant, entity, feature, and effective instant. **P01-R2:** inactive workers cannot initiate or approve new transactions. **P01-R3:** a policy change never retroactively changes a recorded decision. **P01-R4:** overlapping delegation or manager gaps require a named escalation path, never auto-approval.

### 12. Data Fields and Ownership

Procurement stores policy ID/version, scope, effective period, rule values, approver subject, publication event, and a minimal `EmployeeRef` projection with HRMS ID, subject, active flag, manager ID, entity, org unit, location, source version and sync time. HRMS owns worker fields; Finance owns cost centres and budgets; Procurement owns policy. No payroll or personal identity numbers are copied.

### 13. Screens and Data Views

Policy list and version detail, rule editor with overlap validation, approval task, workforce-sync health, stale-record queue, and reassignment timeline. Hide configuration actions from requesters.

### 14. User Experience and Human Behaviour

Show the policy effective date and why an action is blocked. Avoid exposing HR details to buyers. A reassigned task names its new owner and reason, so people do not assume an approval was silently granted.

### 15. Notifications and Communications

Notify reviewer on draft publication request, operations on sync failure, and old/new task owners on reassignment. Messages contain record links and minimal identifying data; escalation timing is configurable.

### 16. Documents and Attachments

Policy approval evidence may be attached and versioned. Retention and legal hold follow approved company policy; no workforce documents are copied from HRMS.

### 17. Exceptions and Edge Cases

Handle absent manager, circular hierarchy, overlapping policies, late HRMS event, worker with multiple assignments, and deactivation during an open approval. Quarantine conflicting source changes for steward review.

### 18. Integrations, APIs, Events and Sandbox

Consume a versioned HRMS workforce projection and Finance reference contract; publish `procurement.policy.published` and task-reassignment facts only after commit. Specify tenant, entity, source ID/version, occurred time, correlation and idempotency keys. Sandbox must contain synthetic workers, manager changes, deactivation and stale-event replay. No direct HRMS table reads.

### 19. Audit, Security and Privacy

Append policy publication, field changes, sync source/version, task reassignment and actor. Restrict projection access; log privileged reads and exports. Apply SSO, tenant isolation, encryption and retention policy.

### 20. Reports, Dashboards and Metrics

Published policy coverage by entity, stale-projection count and age, failed syncs, orphan approvals and reassignment time. Metric definitions include time window and owner.

### 21. Configuration and Localization

Entity, category, currency, local date/time, threshold, delegation period and effective date are configurable. Store normalized values and display in user locale. Country-specific rules are configured, not coded as fixed Zambia values.

### 22. Non-Functional Requirements

Authorization applies to every API and search result; sync is idempotent and observable. Target sync latency, availability, load and recovery objectives await volume and operational decisions.

### 23. Postconditions

Published transactions reference a valid policy version; active worker projection is current or explicitly stale; reassigned tasks retain original history.

### 24. Failure and Recovery Behaviour

Reject invalid policy atomically. Queue failed sync with retry and dead-letter visibility; do not grant access from stale or missing active status. Reconcile HRMS source versions before replay.

### 25. Acceptance Criteria

Given two conflicting policy drafts, publication fails with the overlap. Given a deactivation event, the worker cannot start or approve a new item. Given a changed manager, an open approval is reassigned or escalated with its original decision history intact.

### 26. Test Scenarios

Test publication and supersession, overlapping rules, multi-entity scope, deactivation, out-of-order and duplicate HRMS events, missing manager, delegation expiry, unauthorized admin action, and recovery after sync outage.

### 27. Ownership and Dependencies

Procurement policy owner approves rules; HRMS steward owns source data; Finance steward owns financial dimensions; platform owner provides identity and notification contracts. Technical owner and service-level owners require assignment.

### 28. Open Decisions and Assumptions

Confirm HRMS projection transport and latency, multi-assignment resolution, task fallback owner, policy approval authority, entity scope, and backend host. Product owner tracks decisions before development.

## PROC-02 Supplier activation and maintenance

### 1. Feature Identification

Child **PROC-02**, parent **Supplier management**, Phase 1, version 0.1, draft. Proposed business owner: supplier management lead; sensitive payment-data owner: Finance; product and technical owners to assign.

### 2. Purpose and Business Outcome

Maintain a trustworthy, reusable, orderable supplier reference and preserve evidence for activation and changes. Success is an active supplier with traceable review and no unauthorized payment-detail change.

### 3. Scope and Exclusions

Includes internally initiated supplier request, basic profile, duplicate review, eligibility, activation, suspension and versioned changes. Portal, automated sanctions/tax checks, scoring and supplier performance are Phase 2 candidates.

### 4. Parent and Related Features

Parent Supplier management; PROC-05 requires active supplier, PROC-07 uses supplier identity. Finance owns verified remittance details and AP supplier mapping.

### 5. Actors and Participants

Requester, supplier steward, Procurement approver, Finance verifier, compliance reviewer when required, supplier contact via controlled communication, auditor.

### 6. Entry Points and Triggers

Supplier request form, buyer invitation, potential duplicate alert, qualification expiry, profile change, suspension decision, Finance verification callback.

### 7. Preconditions

Tenant/entity, category and supplier policy exist; initiator has scope; required identification and evidence fields are configured; sensitive payment channel is available if remittance details are needed.

### 8. Workflow

PROC-02-O1: create proposed profile → check duplicates → collect evidence → review eligibility → independently verify payment reference where needed → approve and activate. PROC-02-O2: submit material change → retain prior version → rerun required reviews → publish new version. PROC-02-O3: suspend with reason/effective date → block new orders → review existing obligations; return or reject keeps trace.

### 9. Status and State Transitions

Draft → Submitted → Due Diligence → Pending Approval → Active; alternatives More Information Required, Conditional, Rejected, Suspended, Expired, Inactive. A suspended profile cannot transition directly to Active without review. Versions remain immutable after approval.

### 10. Permissions and Data Scope

Supplier stewards edit profiles in assigned tenant/entity; Finance alone sees or verifies payment instruction detail; buyer reads orderable status and approved public fields. Requester cannot activate their own supplier. External supplier access is excluded from Phase 1.

### 11. Business Rules

**P02-R1:** only Active supplier can be issued a new PO absent a documented emergency exception. **P02-R2:** potential duplicate requires human disposition. **P02-R3:** legal, tax, payment, or risk changes trigger revalidation. **P02-R4:** supplier suspension does not erase existing liabilities or history.

### 12. Data Fields and Ownership

Procurement owns supplier ID, names, registration/tax references as permitted, site, category, contacts, status, evidence and version. Finance owns payment instructions and AP mapping; Procurement stores a restricted verification token/status, not bank details in general views. Classify contact and tax data as sensitive by policy.

### 13. Screens and Data Views

Supplier request form, duplicate review, evidence checklist, approval queue, active supplier list, detail/version timeline, suspension impact view. Buyer selector shows only orderable suppliers.

### 14. User Experience and Human Behaviour

Explain why a supplier is blocked and who can resolve it; avoid revealing confidential diligence or banking details. Warn before suspending a supplier with open POs.

### 15. Notifications and Communications

Notify reviewers on submission, requester on return/rejection, buyers on relevant suspension, owner before qualification expiry, Finance on sensitive change. Messages use approved supplier name and link, no bank detail.

### 16. Documents and Attachments

Registration, tax, insurance, declarations and verification evidence are access controlled, scanned, versioned and retained by approved schedule. The approved version references each evidence version.

### 17. Exceptions and Edge Cases

Potential duplicate, name change, multiple supplier sites or entities, expired qualification, emergency one-time supplier, suspension with open invoice, failed Finance verification. None causes automatic merge or payment-detail activation.

### 18. Integrations, APIs, Events and Sandbox

Versioned Finance verification and AP supplier mapping contract; publish `procurement.supplier.activated` or `.suspended` after commit. Idempotent callbacks carry tenant/entity, supplier ID/version and correlation. Sandbox uses synthetic supplier and payment tokens; test late verification and duplicate callback.

### 19. Audit, Security and Privacy

Log original and changed fields, evidence, reviewers, verification reference, activation and suspension. Restrict contact, tax and payment metadata; enforce tenant isolation and privileged-read logging.

### 20. Reports, Dashboards and Metrics

Supplier onboarding time, active/conditional/suspended count, duplicate review backlog, expiring qualifications and open obligations affected by suspension; each report filters by entity.

### 21. Configuration and Localization

Required fields and qualifications vary by category/country; identifiers, address formats, currencies, review intervals and approval path are effective dated. No assumed Zambia-only supplier rule is hardcoded.

### 22. Non-Functional Requirements

Search must respect field and entity permissions; evidence scanning and verification failures are observable. Define latency, availability and document limits from pilot volume.

### 23. Postconditions

Active supplier has an approved version and eligibility status; blocked supplier cannot appear as orderable; existing transaction references remain resolvable.

### 24. Failure and Recovery Behaviour

Verification outage leaves supplier Pending, not Active. Retry idempotently; reconcile divergent Finance and Procurement status. Document upload failure does not mark evidence complete.

### 25. Acceptance Criteria

Given a probable duplicate, activation waits for disposition. Given an unverified payment change, prior verified instructions remain authoritative. Given suspension, a new PO issue is blocked while existing POs remain readable.

### 26. Test Scenarios

Test normal activation, duplicate identifiers, multi-entity sites, expired document, payment-change segregation, unauthorized field read, suspension with open PO, callback replay and outage recovery.

### 27. Ownership and Dependencies

Supplier steward owns eligibility; Finance owns remittance verification and AP mapping; compliance owns diligence criteria if applicable. Depends on PROC-01, document service and Finance contract.

### 28. Open Decisions and Assumptions

Confirm required supplier fields, risk tiers, diligence authority, emergency supplier route, Finance verification mechanism, banking data ownership and retention before build.

## PROC-03 Purchase request

### 1. Feature Identification

Child **PROC-03**, parent **Buying and approvals**, Phase 1, version 0.1, draft. Proposed business owner: purchasing process owner; data owner: Procurement.

### 2. Purpose and Business Outcome

Capture a justified, coded need before commitment. Success is an approved demand record that can be traced to an order, with no bypass from an unapproved request.

### 3. Scope and Exclusions

Includes non-catalog PR lines, draft, submission, return, resubmission, withdrawal, cancellation, partial order and closure. Catalog shopping and contract releases are Phase 2; purchase approval decision is PROC-04.

### 4. Parent and Related Features

Parent Buying and approvals; PROC-01 supplies worker and policy context, PROC-04 decides authority, PROC-05 consumes approved lines. HRM recruitment requisitions remain separate.

### 5. Actors and Participants

Employee requester, on-behalf-of delegate if allowed, manager, buyer, workflow engine, Finance budget checker, auditor.

### 6. Entry Points and Triggers

Requester web form, permitted on-behalf-of entry, draft duplication, returned-request edit, API entry if explicitly enabled. No anonymous or inactive-worker submission.

### 7. Preconditions

Active HRMS worker and valid entity, location, category, unit, coding and effective policy; required evidence present; unique request number issued at configured point.

### 8. Workflow

PROC-03-O1: enter header and lines → save draft → validate fields and policy → submit immutable version → PROC-04. PROC-03-O2: approver returns with reason → requester edits version → revalidate and resubmit. PROC-03-O3: requester withdraws eligible item or authorized owner cancels/close unused demand → release any associated Finance reservation. Buyer converts only approved lines to PROC-05.

### 9. Status and State Transitions

Draft → Submitted → Pending Approval → Approved → Partially Ordered or Ordered → Closed. Submitted/Pending Approval → Returned → Submitted or Withdrawn; Pending Approval → Rejected; eligible states → Cancelled or On Hold with actor and reason. A submitted version cannot be overwritten.

### 10. Permissions and Data Scope

Requester edits own Draft/Returned request within assigned entity; manager sees assigned approvals; buyer sees approved demand within purchasing scope. On-behalf-of action records both actor and requester. Cross-tenant, entity and field checks apply to API and search.

### 11. Business Rules

**P03-R1:** lines require quantity/unit, need date, entity, location, category, coding and estimate/currency. **P03-R2:** material edit after submission creates a new version and re-evaluation. **P03-R3:** no cancelled, rejected or unapproved line creates a PO. **P03-R4:** partial line approval is disabled unless policy explicitly enables it.

### 12. Data Fields and Ownership

Procurement owns PR ID/number, requester subject and HRMS ID reference, entity, location, cost-centre/project references, justification, line descriptions/quantity/unit/estimate/currency, need date, attachment refs, source version, state and order balance. HRMS owns employee/org data; Finance owns valid financial dimensions.

### 13. Screens and Data Views

My requests, create/edit form, request detail and line status, returned-work queue, buyer approved-demand list, search and filters by owner/entity/status/need date. Show source PR links on PO.

### 14. User Experience and Human Behaviour

Autosave drafts, explain coding and policy failures in plain language, show current owner and next action. Similar-request warnings help avoid duplicates without blocking legitimate repeat orders unless policy says so.

### 15. Notifications and Communications

Notify approver on submission, requester on return/rejection/approval, buyer on approved demand, and owner on stale requests. Avoid placing justification or attachment content in email.

### 16. Documents and Attachments

Quote, specification and business-case evidence are scanned, versioned, access controlled and tied to the submitted PR version. Retention follows policy.

### 17. Exceptions and Edge Cases

Requester deactivated mid-draft, changed cost centre, multi-currency lines, partial approved lines, duplicate-looking request, need date in past, cancellation after partial order, unavailable Finance check. Each produces a named action or blocked status.

### 18. Integrations, APIs, Events and Sandbox

Consume workforce and coding references; request PROC-04 budget and approval decision. Publish `procurement.pr.submitted`, `.approved`, `.cancelled` only after committed transitions. Contracts include tenant/entity, PR ID/version, source line IDs, correlation and idempotency keys. Sandbox covers synthetic worker and coding changes.

### 19. Audit, Security and Privacy

Record each version, field change, actor, on-behalf-of identity, transition, evidence and policy result. Restrict request details to work scope; never expose HR private fields.

### 20. Reports, Dashboards and Metrics

PR volume, age by status/approver, returned rate, approved-but-unordered demand and duplicate warning rate. Define period and currency conversion before spend totals.

### 21. Configuration and Localization

Required fields, numbering, category rules, duplicate window, need-date policy, units, currency and locale are configurable by effective entity policy.

### 22. Non-Functional Requirements

Draft save is resilient to retry; list/search pagination enforces scope. Mobile and keyboard completion must be possible. Volume and latency targets await pilot figures.

### 23. Postconditions

Submitted PR has immutable version and pending owner; approved lines are available to ordering; cancelled unused balance is released through Finance if reserved.

### 24. Failure and Recovery Behaviour

Failed submit does not duplicate a PR version or approval task. Reservation-release failure leaves a visible reconciliation item; do not claim funds were released until Finance confirms.

### 25. Acceptance Criteria

Given an active worker and valid coding, submitting creates one immutable version and approval task. Given inactive worker or invalid entity, submit fails. Given a cancelled line, PO conversion fails. Given a returned request, resubmission preserves the prior version.

### 26. Test Scenarios

Test save/resume, validation, duplicate warning, return/resubmit, concurrent edit, unauthorized requester, deactivation, multi-line approval, partial order, cancellation and idempotent submit replay.

### 27. Ownership and Dependencies

Purchasing process owner and product owner sign off; Procurement owns record; HRMS and Finance own reference data. Depends on PROC-01 and PROC-04; PROC-05 consumes output.

### 28. Open Decisions and Assumptions

Confirm first-release line types, partial approval, on-behalf-of entry, multi-currency lines, numbering and cancellation authority. No hardcoded rule until approved.

## PROC-04 Approval and budget decision

### 1. Feature Identification

Child **PROC-04**, parent **Buying and approvals**, Phase 1, version 0.1, draft. Proposed owners: procurement policy owner and Finance budget owner; technical owner to assign.

### 2. Purpose and Business Outcome

Make every spending authorization attributable to the right people and an authoritative funding decision. Success is an approved request with traceable policy, budget evidence and segregation of duties.

### 3. Scope and Exclusions

Includes policy evaluation, sequential/parallel approval, delegation, return/reject, SLA escalation, budget checks and documented exception. Excludes creating a Finance ledger or silently reserving budget without Finance confirmation.

### 4. Parent and Related Features

Parent Buying and approvals; consumes PROC-01 policy and PROC-03 PR, gates PROC-05 ordering. Finance is source for available budget and reservation; HRMS provides hierarchy.

### 5. Actors and Participants

Requester, line manager, budget owner, procurement reviewer, authorized exception approver, delegated approver, Finance service/operator, workflow engine and auditor.

### 6. Entry Points and Triggers

PR submission or resubmission, material edit, Finance budget response, approver decision, delegation effective date, overdue SLA, cancelled demand and exception request.

### 7. Preconditions

Immutable PR version and applicable published policy; resolvable authority and current worker status; Finance budget source or approved manual-evidence path; no unresolved hard restriction.

### 8. Workflow

PROC-04-O1: evaluate policy and request Finance budget decision; record source, time and reservation ID only if Finance confirms one. PROC-04-O2: generate steps → notify owners → approve, reject or return with reason → enforce quorum and finalize. PROC-04-O3: hard failure blocks; eligible exception routes to named authority, captures evidence, obtains Finance transfer/reservation before commitment. On material PR change, invalidate affected pending approval and re-evaluate.

### 9. Status and State Transitions

Evaluation: Pending → Passed, Failed or Exception Pending. Approval: Pending → In Progress → Approved, Returned or Rejected. Exception Pending → Approved with evidence or Denied. Approved is invalidated by material revision, but prior decision remains in history; escalation never approves automatically.

### 10. Permissions and Data Scope

Only assigned active approver/delegate may decide within entity, amount, category and effective authority. Requester cannot approve own PR unless an explicitly approved compensating policy applies. Finance controls budget override; procurement cannot edit Finance result. Approvers see only needed fields.

### 11. Business Rules

**P04-R1:** one applicable policy version is pinned to each submission. **P04-R2:** hard budget failure blocks approval until authorized Finance resolution. **P04-R3:** delegates inherit no more authority than delegator. **P04-R4:** return/reject/exception requires reason. **P04-R5:** material value, coding or supplier change triggers reapproval. **P04-R6:** escalation changes owner/priority, never decision.

### 12. Data Fields and Ownership

Procurement owns policy evaluation ID/version, approval instance/steps, actor, decision, reason, effective delegation and timestamps. Finance owns budget amount, availability, reservation/release and resulting reference; Procurement stores immutable Finance response snapshot and correlation, not a second budget ledger. HRMS owns reporting line.

### 13. Screens and Data Views

My approvals queue, PR decision summary, budget check and evidence, policy explanation, pending quorum, exception form, delegation admin, approval timeline and reconciliation view.

### 14. User Experience and Human Behaviour

Show what is being approved, total and currency, source of budget result, conflicts, and effect of decision. Make return distinct from reject. Display missing authority plainly rather than leaving a request stranded.

### 15. Notifications and Communications

Notify new approver, requester on decision, budget owner on exception, operator on failed Finance exchange and escalation owner on overdue task. Avoid sensitive line details in email.

### 16. Documents and Attachments

Budget exception evidence, delegated authority, and material decision documents link to the exact PR/policy version, are scanned and permissioned, and follow retention policy.

### 17. Exceptions and Edge Cases

Manager is requester, vacant manager, approval cycle, delegated user inactive, currency conversion across threshold, concurrent approvals, Finance result stale, budget changes while pending, reservation release failure.

### 18. Integrations, APIs, Events and Sandbox

Versioned Finance budget check/reserve/release interface with idempotency and source timestamp. Publish approved/returned/rejected decision events after commit with tenant, entity, request version, policy version and correlation. Sandbox exercises hard failure, stale response, duplicated callback and Finance outage using synthetic budgets.

### 19. Audit, Security and Privacy

Append actor, role, scope, decision, reason, policy version, budget response, delegation chain and override. Enforce separation of duties server-side and protect budget and attachment views by scope.

### 20. Reports, Dashboards and Metrics

Approval turnaround and backlog by step/entity, budget failures, exception frequency, overdue escalations and reservation reconciliation. Define business-hours calculation and attribution.

### 21. Configuration and Localization

Effective thresholds, inclusive-value rule, category/risk steps, quorum, SLA, delegation and currency conversion source are configured by entity and country. No monetary default is embedded here.

### 22. Non-Functional Requirements

Concurrent decisions are serialized or conflict checked; retries are idempotent. The UI shows current decision and integration state. Target Finance response timeout and availability need agreement.

### 23. Postconditions

Approved PR version has required decisions and valid funding evidence; rejected/returned item cannot order; a reservation, if used, has one authoritative Finance reference.

### 24. Failure and Recovery Behaviour

Finance outage leaves check Pending or controlled manual review, never Passed by default. Duplicate callbacks do not duplicate approvals/reservations. Operators reconcile failed release before closure claims.

### 25. Acceptance Criteria

Given a hard budget failure, approval and PO creation are blocked. Given valid delegated authority, a decision records delegator and delegate. Given material PR revision, prior approval cannot authorize the new version. Given overdue task, escalation does not set Approved.

### 26. Test Scenarios

Test threshold boundaries, tax-inclusive amount, self-approval, circular hierarchy, parallel quorum, delegate expiry, concurrent decision, budget outage, retry, stale response, exception denial and reservation release.

### 27. Ownership and Dependencies

Procurement policy owner owns approval rule; Finance owns budget truth and reservation; HRMS owns hierarchy; workflow owner owns task service. Depends on PROC-01 and PROC-03; gates PROC-05.

### 28. Open Decisions and Assumptions

Confirm thresholds, authority matrix, budget-check point, reservation point, manual Finance procedure, currency conversion, delegation and exception owners before build.

## PROC-05 Purchase order and revision

### 1. Feature Identification

Child **PROC-05**, parent **Purchase ordering**, Phase 1, version 0.1, draft. Proposed business owner: purchasing lead; data owner: Procurement.

### 2. Purpose and Business Outcome

Create a controlled commercial instruction to a supplier from approved demand. Success is a traceable issued version whose value and terms match authority and whose changes are approved.

### 3. Scope and Exclusions

Includes conversion of approved PR lines, PO approval, dispatch, acknowledgment, revision, cancellation and closure. Blanket orders, contract releases and direct buy are deferred unless explicitly approved for Phase 1.

### 4. Parent and Related Features

Parent Purchase ordering; requires PROC-02 active supplier, PROC-03 approved lines and PROC-04 authority; feeds PROC-06 receiving and PROC-07 invoice matching.

### 5. Actors and Participants

Buyer, PO approver, supplier contact through controlled channel, budget owner, Finance commitment service, receiver and auditor.

### 6. Entry Points and Triggers

Buyer converts approved PR line, supplier acknowledges or requests change, buyer submits revision, delivery completes, remaining quantity is cancelled.

### 7. Preconditions

Approved unconsumed PR-line balance, orderable supplier, valid entity/currency/coding, approved funding and policy version, required PO approver. Supplier channel and delivery address are valid.

### 8. Workflow

PROC-05-O1: select approved lines → build supplier/terms snapshot → validate amounts and source balance → approve if policy requires → issue immutable numbered version. PROC-05-O2: record dispatch result and supplier acknowledgment or requested change. PROC-05-O3: draft numbered revision with delta → re-evaluate authority/funds → approve → send revised version; cancellation/closure releases unused commitment through Finance and keeps prior versions.

### 9. Status and State Transitions

Commercial state: Draft → Pending Approval → Approved → Issued → Acknowledged → Closed; alternatives Returned, Change Pending, On Hold, Cancelled, Expired and Disputed. Receiving and invoice progress are separate attributes. Issued version is immutable; a revision supersedes it after approval.

### 10. Permissions and Data Scope

Buyer creates in assigned entity/category; designated approver decides; supplier sees only addressed issued version if external access is enabled. Buyer cannot self-approve against policy. Receiver cannot change commercial terms.

### 11. Business Rules

**P05-R1:** no order line exceeds approved, unconsumed PR balance without reapproval. **P05-R2:** inactive supplier blocks issue. **P05-R3:** material change to price, quantity, supplier, scope, delivery or terms creates revision and reapproval. **P05-R4:** no dispatch before approval. **P05-R5:** closure requires fulfillment/invoice reconciliation or an authorized remainder cancellation.

### 12. Data Fields and Ownership

Procurement owns PO ID/number, PR line references, supplier and address snapshot, entity, currency, line quantity/unit/price/tax reference, coding, dates, terms, version, dispatch and acknowledgment history. Finance owns reservation/commitment and accounting dimensions; PO stores their references.

### 13. Screens and Data Views

Approved-demand conversion, PO editor, approval queue, issued PDF/detail, version comparison, dispatch log, acknowledgment status, open-balance and closure views.

### 14. User Experience and Human Behaviour

Show approved source values beside proposed PO and highlight deltas before approval. Supplier-facing document clearly displays version and supersession. Warn of open receipts/invoices before cancellation.

### 15. Notifications and Communications

Notify approver of draft, supplier on issue/revision/cancellation through controlled delivery, buyer on acknowledgment failure or rejection, receiver on expected delivery, Finance on commitment changes.

### 16. Documents and Attachments

Store issued version and terms as immutable document snapshot; attach acknowledgment and change rationale. Apply access, scanning, retention and legal hold rules.

### 17. Exceptions and Edge Cases

Supplier becomes suspended between draft and issue, PR line concurrently consumed, currency mismatch, partial supplier rejection, lost dispatch acknowledgment, revision after partial receipt, cancellation with invoice in flight.

### 18. Integrations, APIs, Events and Sandbox

Finance commitment/reservation adjustment and supplier delivery adapter; publish `procurement.po.issued`, `.revised`, `.cancelled` after commit. Versioned payload includes tenant/entity, PO ID/version, source line, supplier snapshot reference, correlation and idempotency. Sandbox simulates delivery failure and duplicate acknowledgments.

### 19. Audit, Security and Privacy

Append source authorization, every field delta, approver, document version, dispatch attempt and supplier response. Entity and supplier scope applies to all views and attachments.

### 20. Reports, Dashboards and Metrics

PO cycle time, issue backlog, supplier acknowledgment time, open commitment, overdue delivery and revision frequency; totals keep currency and as-of date.

### 21. Configuration and Localization

PO numbering, approval materiality, document template, locale, tax presentation, payment and delivery terms, dispatch channels and currency rules are effective dated by entity.

### 22. Non-Functional Requirements

PO conversion is transactional against source balance; issue/retry is idempotent. PDF generation and outbound delivery are observable. Performance and volume targets await pilot data.

### 23. Postconditions

Issued PO has one approved immutable version, supplier dispatch record, accurate open line balances and linked source approval/funding references.

### 24. Failure and Recovery Behaviour

Delivery failure keeps commercial state approved or issue-pending with visible retry, never claims supplier receipt. Commitment adjustment failure blocks issue or closure until reconciled. Duplicate issue retry sends same version/key.

### 25. Acceptance Criteria

Given an unapproved PR line or suspended supplier, issue fails. Given material value change, revised issue waits for reapproval. Given duplicate dispatch retry, one commercial PO version exists and attempts remain visible. Given partial receipt, cancellation affects only valid remainder.

### 26. Test Scenarios

Test conversion, concurrent line consumption, approval threshold, self-approval, supplier suspension, issue failure/retry, acknowledgment, multi-revision comparison, partial receipt revision and closure reconciliation.

### 27. Ownership and Dependencies

Purchasing lead owns process, Procurement owns PO, Finance owns commitment, platform owns delivery adapter. Depends on PROC-01 through PROC-04; PROC-06 and PROC-07 consume output.

### 28. Open Decisions and Assumptions

Confirm PO approval after PR, dispatch channel, legal document template, direct buy, cancellation authority, tax basis and Finance commitment point before build.

## PROC-06 Goods receipt and service acceptance

### 1. Feature Identification

Child **PROC-06**, parent **Fulfillment**, Phase 1, version 0.1, draft. Proposed business owner: receiving or service operations lead; data owner: Procurement for acceptance record.

### 2. Purpose and Business Outcome

Establish what was actually delivered or performed before invoice payment. Success is accurate accepted balance and traceable exception evidence.

### 3. Scope and Exclusions

Includes goods delivery entry, inspection outcome, partial receipt, service certification, rejection, return and reversal. Warehouse valuation, stock ownership and asset depreciation remain outside Procurement; Inventory handoff waits on contract.

### 4. Parent and Related Features

Parent Fulfillment; requires issued PROC-05 PO; PROC-07 uses accepted quantity/value. Inventory and asset modules consume facts when enabled.

### 5. Actors and Participants

Receiving officer, service owner, buyer, supplier contact, exception approver, Inventory consumer and auditor.

### 6. Entry Points and Triggers

Delivery arrival, service milestone completion, PO expected-date task, inspection decision, supplier return, correction request, duplicate delivery notice.

### 7. Preconditions

Issued PO line with remaining quantity/value, assigned receiving site or service owner, authorized actor, configured tolerance, and required evidence.

### 8. Workflow

PROC-06-O1: find PO → record delivery, quantity, condition and evidence → inspect → accept valid amount into GRN and leave rejected balance explicit. PROC-06-O2: service owner records milestone/period and proof → certifies accepted value or rejects with reason. PROC-06-O3: return or correct by linked reversal/adjustment → update remaining balance and publish accepted delta. Partial deliveries repeat without closing other lines.

### 9. Status and State Transitions

Receipt: Expected → Under Inspection → Partially Accepted or Fully Accepted; alternatives Rejected, Return Requested, Returned, Reversed, Disputed. Service: Pending Certification → Accepted, Rejected or Disputed. Original accepted event is immutable; reversal creates a new event.

### 10. Permissions and Data Scope

Receiver acts only at assigned site/entity and PO; service owner certifies assigned scope; buyer can view but not rewrite acceptance. Requester receipt requires explicit policy. Separation from buying and invoice approval is enforced server-side.

### 11. Business Rules

**P06-R1:** accepted quantity/value cannot exceed open PO balance plus authorized tolerance. **P06-R2:** rejected units do not count as accepted. **P06-R3:** every correction links to original with actor and reason. **P06-R4:** invoice matching uses only accepted, unreversed value when receipt is required.

### 12. Data Fields and Ownership

Procurement owns GRN/acceptance ID, PO version/line, delivery note, time/site, delivered/accepted/rejected quantities, unit, condition, optional lot/serial, milestone and value, actor, evidence and reversal reference. Inventory owns stock movement and valuation; Procurement stores Inventory acknowledgment only.

### 13. Screens and Data Views

Expected deliveries, PO receive form, inspection/acceptance detail, service certification, remaining-balance view, returns/reversal action and exception queue.

### 14. User Experience and Human Behaviour

Default display of ordered, already accepted and remaining quantities reduces accidental over-receipt. Require an explicit accepted-versus-rejected split and a visible reason for correction.

### 15. Notifications and Communications

Notify buyer of short/damaged delivery, service owner of milestone due, supplier contact through approved channel on rejection, AP on acceptance available and operations on Inventory handoff failure.

### 16. Documents and Attachments

Delivery note, inspection photo, service certificate, timesheet and return evidence are scanned, versioned and tied to each receipt event. Retention follows policy.

### 17. Exceptions and Edge Cases

Over-delivery, wrong item/site, duplicate delivery note, receipt before PO acknowledgment, quality inspection pending, return after invoice, replacement delivery and concurrent receiving on one line.

### 18. Integrations, APIs, Events and Sandbox

Publish accepted GRN and return/reversal facts to Inventory when enabled with PO line/version, site, quantity/unit, tenant/entity, correlation and idempotency. Consume acknowledgment; no direct Inventory table write. Sandbox tests duplicate and out-of-order events.

### 19. Audit, Security and Privacy

Keep original delivery and each acceptance/adjustment as append-only business events; scope documents by entity/site. Log privileged reversal and attachment access.

### 20. Reports, Dashboards and Metrics

Expected versus overdue, on-time delivery, partial receipts, rejection rate, accepted-but-uninvoiced value and reversal rate. Define denominator and date source.

### 21. Configuration and Localization

Tolerance, inspection requirement, requester receipt permission, units, lot/serial need, service milestone rule, locale and site are configured by category/entity.

### 22. Non-Functional Requirements

Concurrent receipt must not overrun open balance; mobile and accessible data entry at receiving site. Inventory delivery is retryable and observable. Document limits await volume decision.

### 23. Postconditions

Accepted amount is recorded exactly once, PO remaining balance updated, evidence linked, matching eligibility known, and Inventory handoff state visible if applicable.

### 24. Failure and Recovery Behaviour

Failed save does not create half a GRN. Duplicate retry returns original result. Inventory outage leaves accepted Procurement record and queued handoff with reconciliation; an adjustment compensates rather than deletes.

### 25. Acceptance Criteria

Given two partial receipts, remaining balance is correct. Given over-tolerance quantity, acceptance is blocked or routed to authorized exception. Given reversed receipt, matching excludes reversed quantity while original evidence remains.

### 26. Test Scenarios

Test full/partial/rejected goods, service milestone, simultaneous receivers, over-tolerance, wrong site, duplicate note, return after invoice, reversal and Inventory replay.

### 27. Ownership and Dependencies

Receiving lead owns goods process, service owner owns service certification, Procurement owns record, Inventory owns stock. Depends on PROC-05 and feeds PROC-07.

### 28. Open Decisions and Assumptions

Confirm tolerance, inspection categories, requester receipt, lot/serial fields, Inventory availability, return-to-supplier workflow and service evidence before build.

## PROC-07 Invoice match and AP handoff

### 1. Feature Identification

Child **PROC-07**, parent **Invoice validation**, Phase 1, version 0.1, draft. Proposed business owners: AP lead and procurement operations lead; Finance owns posting and payment data.

### 2. Purpose and Business Outcome

Check invoices against authorized order and accepted fulfillment, resolve differences, and send a single validated liability to Finance. Success is a traceable, duplicate-safe AP handoff and accurate Finance status visibility.

### 3. Scope and Exclusions

Includes controlled invoice capture, duplicate detection, basic two/three-way match, exception handling, approval, AP handoff and status sync. Excludes payment execution, general-ledger posting, OCR and supplier portal in Phase 1.

### 4. Parent and Related Features

Parent Invoice validation; needs PROC-02 supplier, PROC-05 PO and PROC-06 acceptance where required; Finance/AP consumes validated payload and returns status.

### 5. Actors and Participants

AP processor, buyer, receiver/service owner, budget owner, supplier contact through controlled channel, Finance integration operator and auditor.

### 6. Entry Points and Triggers

Dedicated inbox or authorized AP entry, invoice correction, PO/receipt change, match rerun, Finance acceptance/rejection callback, payment-status update.

### 7. Preconditions

Supplier and entity identified, invoice ID/date/currency/totals and evidence present, relevant PO available unless approved exception, configured match policy and Finance handoff contract available.

### 8. Workflow

PROC-07-O1: capture immutable original → validate fields/totals and duplicate key → match invoice to PO and accepted receipt or service as policy requires. PROC-07-O2: create named exception with owner/due date → resolve through corrected invoice, PO revision, receipt adjustment, credit or authorized variance → rerun match and approve. PROC-07-O3: send validated snapshot to Finance once with stable idempotency key → record accepted/rejected response → reconcile posting and payment statuses without initiating payment.

### 9. Status and State Transitions

Processing: Received → Validation → Match Pending → Validated → Handed to AP; branch to Exception → Match Pending, or Rejected/Duplicate/Disputed/Voided. AP Rejected returns to correction workflow. Finance-owned status is separate: Awaiting Acceptance, Posted, Partially Paid, Paid, Payment Exception. A callback alone cannot rewrite immutable invoice content.

### 10. Permissions and Data Scope

AP captures and validates within entity; exception approver has scoped authority and cannot approve own conflicting action; Procurement sees order/match status but not Finance payment credentials. Supplier portal visibility is deferred. Finance callback is service-authenticated and tenant/entity checked.

### 11. Business Rules

**P07-R1:** normalized supplier, invoice number and entity form duplicate check; exact uniqueness scope including currency needs Finance sign-off. **P07-R2:** match method and tolerance are effective-dated by category/field. **P07-R3:** unresolved hard variance cannot hand off. **P07-R4:** one approved snapshot maps to one idempotency key and Finance payable reference. **P07-R5:** Paid status comes only from Finance confirmation.

### 12. Data Fields and Ownership

Procurement owns captured invoice image and line snapshot, supplier/PO/receipt refs, match result, exception, resolution and handoff record. Finance owns AP document, journal, tax accounting, due date as posted, payment status/date/reference. Procurement stores returned references and status, not a second payable ledger.

### 13. Screens and Data Views

Invoice inbox, capture/detail, duplicate warning, PO/receipt comparison, exception work queue, approval and handoff history, Finance status/reconciliation view; buyer sees limited read-only match context.

### 14. User Experience and Human Behaviour

Show discrepancy by line and amount with reason and next owner. Make “validated,” “sent,” “accepted by AP,” and “paid” visually distinct to avoid false assurance to supplier or requester.

### 15. Notifications and Communications

Notify exception owner, AP on new match, buyer/receiver on their discrepancy, operator on AP rejection or failed callback and appropriate stakeholders on confirmed payment. External message omits sensitive account and internal coding.

### 16. Documents and Attachments

Preserve original invoice, corrected versions, credit note and variance evidence with checksum, scan status, classification, access log and retention. Finance handoff references immutable evidence versions.

### 17. Exceptions and Edge Cases

Duplicate number with different formatting, invoice before receipt, multiple partial invoices, tax or currency variance, credit note, closed PO, supplier suspension after work done, Finance rejection and delayed or conflicting payment callback.

### 18. Integrations, APIs, Events and Sandbox

Versioned Finance/AP handoff includes tenant/entity, supplier reference, invoice and approved line snapshot, coding, evidence hashes, source IDs, correlation and idempotency. Process accepted/rejected and payment-status callbacks idempotently; publish `procurement.invoice.validated` and `.ap_handoff_requested` after commit. Sandbox uses synthetic invoice, duplicate and late callback cases; no real payment.

### 19. Audit, Security and Privacy

Append capture, match rule/version, variance, approver, correction, handoff attempt and Finance response. Restrict supplier financial data and enforce entity scope; log downloads and exports.

### 20. Reports, Dashboards and Metrics

First-pass match rate, exception type/age, duplicate prevention, validated-to-AP time, AP rejection age and payment status received. Define denominator, currency and as-of time before publishing.

### 21. Configuration and Localization

Two/three-way method, price/quantity/tax tolerances, duplicate normalization, invoice number format, entity tax presentation, locale and currency handling are versioned by effective date. Tax rules remain Finance-owned.

### 22. Non-Functional Requirements

Duplicate detection and handoff are concurrency safe; retries and callbacks are idempotent and observable; scanned documents are protected. Peak invoice volume and latency targets need business input.

### 23. Postconditions

Validated invoice has resolved match evidence and one stable Finance handoff; Finance acceptance/rejection and later payment status are linked without changing original evidence.

### 24. Failure and Recovery Behaviour

Finance outage leaves Hand-off Pending and retryable snapshot, not Posted or Paid. Unknown send outcome is reconciled by idempotency key before replay. Conflicting callback is quarantined for operator review.

### 25. Acceptance Criteria

Given duplicate supplier invoice identity, a second payable handoff is blocked. Given receipt-required variance above tolerance, invoice remains in exception. Given repeated send, Finance sees one payable request. Given AP rejection, record shows owner and reason; Paid appears only after Finance confirmation.

### 26. Test Scenarios

Test two/three-way match, partial receipt and invoice, threshold boundaries, duplicate formatting and concurrency, credit note, unauthorized exception approval, Finance outage, unknown send result, callback replay, conflicting status and tenant isolation.

### 27. Ownership and Dependencies

AP lead owns validation and posting acceptance; Procurement owns match evidence; Finance owns ledger/payment. Depends on PROC-02, PROC-05, PROC-06, Finance contract and document service.

### 28. Open Decisions and Assumptions

Confirm invoice system of record, duplicate key, tax validation owner, tolerance matrix, exception authority, AP payload, status callback and evidence retention before build.

## Specification readiness and deferred work

Each Phase 1 child now has all 28 required sections and operation scenarios. The documents are **drafts**, not approved implementation contracts. Product and business owners must resolve section 28, assign owners, confirm the cross-module interfaces, and approve policy values before engineering marks an individual child ready.

Phase 2 and Phase 3 items in the [blueprint](ENTERPRISE-WORKFLOW-BLUEPRINT.md) are candidate capabilities, not implementation-ready child features. Before any is scheduled for development, add it to this hierarchy and write the same 28-section specification plus operation scenarios. Candidate parents include Strategic sourcing, Contract management, Supplier portal, Catalog buying, Inventory integration, and Spend optimization.
