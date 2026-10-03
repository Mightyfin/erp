# PROC-12 Supplier Portal

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Supplier Portal → Supplier Self-Service and Collaboration → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

## 1. Feature Identification

`PROC-12`, advanced phase unless launch need changes. Business owner: supplier relationship lead; security/identity and product/data/technical owners to name. Operations: `O1` invite/link supplier user, `O2` update profile/docs, `O3` view/respond RFx, `O4` acknowledge PO/delivery, `O5` submit invoice/credit, `O6` view Finance status/message, `O7` revoke access.

## 2. Purpose and Business Outcome

Give suppliers restricted, traceable participation without internal ERP accounts. Success is faster document/bid/order/invoice exchange while each supplier sees only its own permitted records and no internal approval, competitor or banking data.

## 3. Scope and Exclusions

Includes supplier-scoped profile/evidence, invitations/bids, PO response, delivery information, invoice/credit submission, permitted payment status, statements and record messages. Internal approvals, purchasing decisions, AP posting, payment execution and general ERP access are excluded.

## 4. Parent and Related Features

Parent Supplier Portal; consumes PROC-01 supplier identity/eligibility, PROC-06 invitation, PROC-08 issued PO, PROC-10 intake and PROC-11 Finance status projection. Shared identity, documents, notifications and collaboration provide infrastructure.

## 5. Actors and Participants

Supplier organization admin/user, Procurement supplier steward/buyer, sourcing lead, AP processor, Finance status provider, external identity administrator and auditor.

## 6. Entry Points and Triggers

Invitation, approved supplier contact assignment, RFx invitation, PO issue/acknowledgment, delivery update, invoice upload, payment status change, document expiry or account revocation.

## 7. Preconditions

Verified supplier-user relationship, active external identity/MFA policy, supplier and tenant scope, explicit shared record, current portal terms and valid invitation. Suspended supplier access follows policy; historical obligations may remain readable without permitting new bids/orders.

## 8. Workflow

`O1`: steward invites contact, supplier verifies identity, admin assigns least-privilege roles. `O2`: supplier proposes profile/document change; internal PROC-01 approval decides activation. `O3`: invited supplier reviews RFx, submits sealed versioned bid and receives receipt. `O4`: supplier acknowledges/rejects issued PO or reports delivery; internal receiver certifies acceptance. `O5`: supplier uploads invoice/credit into PROC-10 intake, not directly AP. `O6`: portal displays permitted Finance-confirmed status and contextual messages. `O7`: admin revokes access and sessions, preserving authored history.

## 9. Status and State Transitions

Portal account: Invited → Pending Verification → Active → Suspended/Revoked. Submission states mirror owning feature and cannot be edited after submit except versioned correction. Each transition has actor, required evidence, SLA, notification and recovery; supplier activity never directly changes internal approval or Paid state.

## 10. Permissions and Data Scope

External role plus supplier ID, tenant, record sharing, action and field scope are all required. A supplier cannot view another supplier's RFx response, PO, invoice, statement, message or attachment—even through search, URL, export or API. Supplier admin manages only own contacts. Internal users do not impersonate suppliers without audited support procedure.

## 11. Business Rules

`BR-PROC-PORT-001` external supplier identity is separate from workforce identity; `002` invited record access is explicit; `003` supplier submission is immutable/versioned; `004` profile/bank change needs internal verification; `005` PO acknowledgment is not internal receipt; `006` payment state is Finance-confirmed; `007` revocation removes future access but not audit history.

## 12. Data Fields and Ownership

Identity platform owns login/MFA/session; Procurement owns supplier-user link, roles/scopes, invitation, portal submissions and messages; owning feature stores bid/PO/invoice record. Finance owns payment status, Procurement stores restricted projection. No bank credentials in general portal payload.

## 13. Screens and Data Views

Supplier home, profile/docs, invitations/RFx, own submitted bids, issued POs and responses, delivery updates, invoice/credit submissions, Finance-confirmed status/statements, messages and account/security settings. Internal steward sees access/link audit.

## 14. User Experience and Human Behaviour

Use simple supplier language and mobile-accessible forms; show receipt confirmation, deadline/time zone, exact PO version and action owner. Distinguish “invoice uploaded” from “accepted by AP” and “paid.” Support offline supplier route through controlled surrogate capture, not a forced portal requirement.

## 15. Notifications and Communications

Invite/verification, RFx deadline, clarification, award/PO issue, document expiry, invoice correction and confirmed payment update. External email/SMS/WhatsApp content is minimal and links to authenticated record; delivery failure is visible.

## 16. Documents and Attachments

Supplier documents, bids, PO acknowledgment, delivery notes, invoices, credits and messages are scanned, versioned, supplier/record scoped and retained. Upload does not imply internal approval.

## 17. Exceptions and Edge Cases

One contact serves multiple suppliers, supplier merger, revoked contact with open bid, wrong tenant link, missed deadline due to outage, duplicate upload, suspended supplier with unpaid invoice, lost MFA and disputed submitted document.

## 18. Integrations, APIs, Events and Sandbox

External identity and supplier-link service; scoped domain APIs for RFx/PO/invoice/status; document and notification service. Events use supplier/tenant/record scope and idempotency; no broad subscribe channel. Sandbox has two synthetic suppliers to prove mutual isolation and submission replay.

## 19. Audit, Security and Privacy

Log invitation, login-sensitive action, link changes, download, submission, message and revocation. Enforce MFA/session protections, rate limits, malware scanning, tenant and supplier isolation. No internal evaluation or bank data crosses portal boundary.

## 20. Reports, Dashboards and Metrics

Portal adoption, invitation completion, bid/PO response time, document expiry, invoice intake by channel and access-denied anomalies. Metrics segregate supplier from internal activity.

## 21. Configuration and Localization

Portal enablement, allowed modules/actions, supplier role, locale/time zone, invitation expiry, notification channels, document rules and status-sharing policy are configurable by tenant/entity.

## 22. Non-Functional Requirements

External availability, peak submission deadline capacity, accessibility, mobile support, rate limiting, session revocation and upload resilience require agreed targets. Scope enforcement must be testable at API and search layers.

## 23. Postconditions

Authorized supplier can see only shared own records, submissions carry receipt/version, and revocation stops new access without deleting business history. Internal owners receive actionable submissions.

## 24. Failure and Recovery

Unknown upload outcome is checked by submission key; duplicate retry returns receipt. Identity outage blocks access but leaves records intact. Wrong supplier link is quarantined and sessions revoked; exposure incident follows security runbook.

## 25. Acceptance Criteria

Given Supplier A, it cannot read Supplier B bid/PO/invoice/attachment through any path. Given submitted bid, supplier cannot overwrite it. Given PO acknowledgment, goods remain unreceived until internal acceptance. Given Finance-unconfirmed payment, portal cannot show Paid.

## 26. Test Scenarios

Test invitation/MFA, multi-contact supplier linking, two-supplier isolation, RFx sealed bid, PO response, duplicate invoice upload, suspension/revocation, URL enumeration, export/search scope, deadline load and recovery.

## 27. Ownership and Dependencies

Supplier relationship lead owns portal experience; security/identity owns authentication; Procurement owns supplier link and transactions; Finance owns payment status. Depends on PROC-01/06/08/10/11 and shared documents/notifications.

## 28. Open Decisions and Assumptions

Confirm launch timing, external IdP/MFA, supplier admin delegation, supported actions, offline alternative, status-sharing, data residency, SLA/availability and incident owner. Owners: Procurement, Security, Finance and Product; resolve before implementation.
