# PROC-09 Receiving, Inspection and Returns

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Receiving and Inspection → Fulfillment Acceptance → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

## 1. Feature Identification

`PROC-09`, fulfillment phase. Business owners: receiving lead and service owner; product/data/technical owners to name. Operations: `O1` record goods delivery, `O2` inspect/accept, `O3` certify service, `O4` return/reverse, `O5` hand off to Inventory/Assets, `O6` reconcile.

## 2. Purpose and Business Outcome

Establish what was physically received or satisfactorily performed before invoice validation. Success is accepted quantity/value with evidence, accurate PO remainder and one downstream stock/asset fact where relevant.

## 3. Scope and Exclusions

Includes partial goods receipt, inspection, service entry, conditional/rejected acceptance, returns/replacements, reversals, and Inventory/Asset handoff. Inventory owns stock balance/valuation; Assets owns tags, custody and depreciation. Procurement does not infer acceptance from invoice.

## 4. Parent and Related Features

Parent Receiving and Inspection; consumes issued [PROC-08](PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md) lines; supplies accepted evidence to PROC-11 matching and Inventory/Assets. Supplier Portal may submit delivery information but cannot certify acceptance.

## 5. Actors and Participants

Receiver, inspector, service owner, buyer, supplier, return approver, Inventory/Asset operator, AP processor and auditor. Requester may attest only if policy permits and segregation checks pass.

## 6. Entry Points and Triggers

Supplier delivery, expected PO schedule, service milestone completion, inspection outcome, damaged/incorrect item, return, replacement or invoice mismatch requiring receipt review.

## 7. Preconditions

Issued PO/version with open line balance, valid receiving site or service owner, authorized actor, category tolerance and required document/evidence rules. A suspended supplier does not erase a valid delivery; its acceptance follows exception policy.

## 8. Workflow

`O1`: capture delivery note, item, qty, condition and site against PO; identify short/excess/damaged units. `O2`: inspect where required, accept exact quantities, reject/hold remainder and create GRN. `O3`: service owner certifies deliverable/period and accepted value with evidence. `O4`: authorized return/reversal links original event and reason, updates eligible match balance. `O5`: accepted stock/asset fact sent with item/site/qty and acknowledged; `O6`: reconcile mismatches, retries and open balances.

## 9. Status and State Transitions

Delivery: Expected → Recorded → Under Inspection → Partially Accepted/Fully Accepted or Rejected/Disputed. Service: Pending Certification → Accepted/Partially Accepted/Rejected. Return: Requested → Approved → Returned → Credited/Replaced/Closed; reversal is a compensating event. Actor, evidence, SLA, escalation and next state accompany each transition; original acceptance stays immutable.

## 10. Permissions and Data Scope

Receiver acts only for assigned entity/site and PO; inspector certifies assigned category; service owner accepts contracted deliverable; buyer can view but not edit acceptance; return approval is scoped. Supplier portal cannot assert internal acceptance. Role conflicts with buyer/AP approval are enforced by configured segregation.

## 11. Business Rules

`BR-PROC-REC-001` accepted qty/value cannot exceed remaining PO plus authorized tolerance; `002` rejected/held amount is not invoice-eligible; `003` inspection-required goods need pass/conditional decision; `004` service acceptance requires deliverable/period evidence; `005` correction links original and never deletes it; `006` stock and asset records are created by their owning modules; `007` a return adjusts match eligibility and commercial credit follow-up.

## 12. Data Fields and Ownership

Procurement owns delivery/GRN ID, PO line/version, supplier, note/date, site, ordered/prior/received/accepted/rejected qty, unit, condition, lot/serial if required, inspector, service period/milestone/value, evidence and reversal/return links. Inventory owns stock movement/bin/valuation; Assets owns register/tag/custodian; Procurement stores acknowledgement refs.

## 13. Screens and Data Views

Expected deliveries, receive against PO, inspection checklist, service certification, accepted/rejected balance, return request, reversal, Inventory/Asset delivery status and reconciliation queue.

## 14. User Experience and Human Behaviour

Show ordered, previously accepted and remaining quantity side by side. Require explicit split of accepted/damaged/rejected units. Make service acceptance describe the deliverable rather than a vague percentage alone. Warn when return affects a matched invoice.

## 15. Notifications and Communications

Notify receiver before expected date, inspector on pending check, buyer on shortage/damage, supplier through controlled channel on rejection/return, AP on accepted evidence and operator on downstream handoff failure. Messages contain scoped links.

## 16. Documents and Attachments

Delivery note, packing list, photos, inspection sheet, service completion certificate, timesheet, return shipping and supplier credit reference are scanned, versioned, classified and tied to exact event. Retention follows policy.

## 17. Exceptions and Edge Cases

Over-delivery, wrong item/site, duplicate delivery note, lot mismatch, failed inspection, supplier return after invoice, replacement without new PO, partially accepted service, concurrent receivers, supplier suspension and asset created before reversal.

## 18. Integrations, APIs, Events and Sandbox

Emit `GoodsReceived`, `GoodsRejected`, `ServiceAccepted`, return/reversal facts after commit with tenant/entity, PO line/version, item/site, qty/UOM, source event, correlation/idempotency. Inventory/Assets acknowledge processing; no direct table write. Sandbox tests duplicate/out-of-order facts and downstream outage.

## 19. Audit, Security and Privacy

Append delivery, inspection, acceptance, return and reversal with actor, time, reason and before/after balances; evidence restricted by entity/site. Privileged reversal and export logged; original events immutable.

## 20. Reports, Dashboards and Metrics

On-time/late delivery, partial receipts, rejection/defect rate, accepted-but-uninvoiced value, service certification age, return/credit backlog and Inventory/Asset handoff failures. Define denominator and as-of time.

## 21. Configuration and Localization

Category/site inspection need, over-delivery tolerance, requester receipt permission, lot/serial fields, UOM, service evidence, return authority, SLA and locale are effective-dated by entity.

## 22. Non-Functional Requirements

Concurrent receipts cannot overrun PO balance; mobile and accessible site capture; document upload resilient to network interruption. Downstream events idempotent and observable; volume/latency targets await pilot estimates.

## 23. Postconditions

Accepted amount and PO remaining balance reconcile; invoice match eligibility is known; Inventory/Asset handoff has acknowledged or visible pending state; rejected units and returns remain traceable.

## 24. Failure and Recovery

Failed save creates no partial GRN. Unknown save outcome is queried by key. Inventory outage retains accepted Procurement fact and retries same event; receipt correction sends compensating fact. Failed credit follow-up remains assigned.

## 25. Acceptance Criteria

Given two partial receipts, remaining balance is correct. Given excess quantity, acceptance blocks or follows authorized tolerance. Given inspection failure, amount is not match-eligible. Given reversal, original remains and eligible balance falls. Given stock item, Inventory receives one accepted fact despite replay.

## 26. Test Scenarios

Test full/partial goods, service milestone, conditional/rejected inspection, over-tolerance, duplicate note, concurrent receiving, return after invoice, reversal after stock/asset handoff, site/tenant isolation and callback replay.

## 27. Ownership and Dependencies

Receiving/service leads own acceptance, Procurement owns evidence, Inventory owns stock, Assets owns register. Depends on PROC-08 and shared documents; PROC-11 consumes accepted value.

## 28. Open Decisions and Assumptions

Confirm inspection categories, tolerance, requester receipt, lot/serial and service evidence, return-to-supplier process, Inventory/Asset readiness, credit coordination and SLAs. Owners: Operations, Procurement, Inventory, Assets and Finance; resolve before implementation.
