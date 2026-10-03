# PROC-13 Supplier Performance and Risk

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Supplier Management → Performance and Risk Review → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

## 1. Feature Identification

`PROC-13`, advanced phase. Business owner: supplier relationship/risk lead; compliance, product/data/technical owners to name. Operations: `O1` gather facts, `O2` calculate score, `O3` review/dispute, `O4` decide action, `O5` monitor improvement/expiry.

## 2. Purpose and Business Outcome

Assess supplier delivery, quality, responsiveness and commercial compliance with transparent evidence. Success is an explainable review that informs eligibility or sourcing without treating an unreviewed score as automatic punishment.

## 3. Scope and Exclusions

Includes KPI definitions, receipt/PO/contract/issue facts, scorecard, periodic review, dispute/evidence, action plan and status recommendation. Automated sanctions/legal due diligence and AP accounting remain external; supplier suspension follows PROC-01 authority.

## 4. Parent and Related Features

Parent Supplier Management; PROC-01 owns profile/eligibility, PROC-08/09/07 provide order, delivery, quality and contract facts, PROC-06 may use approved risk/qualification output. Analytics uses reviewed score.

## 5. Actors and Participants

Supplier manager, buyer, receiver/inspector, contract owner, risk/compliance reviewer, supplier representative where sharing allowed, approver and auditor.

## 6. Entry Points and Triggers

Scheduled review period, late PO/receipt, inspection failure, contract breach, dispute, expiring certification, high concentration alert and supplier response.

## 7. Preconditions

Identified supplier/entity and defined KPI source/denominator/time window, sufficient reliable facts, current review policy and assigned owner. Missing data is “insufficient evidence,” not a zero score.

## 8. Workflow

`O1`: collect versioned facts. `O2`: calculate draft score by approved weights/period and show source records. `O3`: reviewer validates data, records supplier response or dispute and corrections. `O4`: authorized owner approves score/action—preferred, watch, improvement, suspension referral—under policy. `O5`: track corrective tasks and schedule next review. PROC-01 decides any eligibility change.

## 9. Status and State Transitions

Review: Draft → Data Check → Pending Review → Approved → Published/Closed; Disputed/Returned branches. Action plan: Open → In Progress → Verified/Overdue/Closed. Actor, evidence, SLA and escalation accompany transitions; published score changes by new review version.

## 10. Permissions and Data Scope

Supplier manager reads scoped commercial facts; reviewer scores assigned supplier/entity; supplier sees only explicitly shared approved score/action; compliance evidence is field restricted. Score approver cannot silently edit source events or bypass PROC-01 suspension control.

## 11. Business Rules

`BR-PROC-PERF-001` KPI formula/weights/version required; `002` missing evidence flagged, not scored zero by default; `003` source corrections create recalculation version; `004` supplier dispute retained; `005` score alone cannot suspend without PROC-01 decision; `006` preferred classification requires approved review.

## 12. Data Fields and Ownership

Procurement owns scorecard ID/version, supplier/entity, period, metric inputs/formulas, evidence refs, reviewer, supplier response, score/decision and action plan. PO/Receipt/Contract own source facts; Compliance owns external screening verdict; PROC-01 owns supplier eligibility.

## 13. Screens and Data Views

Supplier scorecard, source drill-down, review queue, comparison trends, disputed metric view, corrective plan, concentration and qualification expiry view. Restricted facts masked by role.

## 14. User Experience and Human Behaviour

Explain scoring and missing data; distinguish late supplier delivery from internal receiving delay. Show trend and sample size. Give reviewer a way to correct source reference rather than manually changing a final score.

## 15. Notifications and Communications

Notify owner on review due, metric exception, dispute, improvement task/overdue and impending qualification expiry. Supplier-facing notice follows approved sharing policy and omits other suppliers' data.

## 16. Documents and Attachments

Inspection evidence, correspondence, corrective plan, supplier response and certification are versioned, classified, scanned and linked to review/period. Source PO/receipt document remains with source record.

## 17. Exceptions and Edge Cases

Low transaction count, disputed delivery timestamp, mixed currency price score, split PO, supplier merger, late source correction, force-majeure delivery, suspended supplier with open contract and confidential complaint.

## 18. Integrations, APIs, Events and Sandbox

Consume versioned PO/receipt/contract/issue facts and approved external risk check when enabled; publish reviewed score/action recommendation, never automatic suspension. Contracts include supplier/entity/period/formula version and correlation. Sandbox tests missing/duplicate source facts and recalculation.

## 19. Audit, Security and Privacy

Append source snapshot, formula, score, reviewer changes, dispute and approval. Restrict confidential complaint and competitor data, log external sharing/export; keep published review immutable.

## 20. Reports, Dashboards and Metrics

On-time delivery, rejection rate, responsiveness, contract compliance, concentration and corrective-plan closure. Every metric defines numerator/denominator, period, exclusions and owner; risk categories require approved meaning.

## 21. Configuration and Localization

Review frequency, KPI weights, minimum sample, risk tiers, thresholds, supplier sharing, category/region and currency conversion are effective-dated; country-specific diligence remains separately reviewed.

## 22. Non-Functional Requirements

Recalculation is reproducible by formula/source version and idempotent on replay. Scoped dashboards scale to supplier volume; stale source facts and failed jobs observable. Performance targets await estimates.

## 23. Postconditions

Approved review has explainable score, evidence, decision and action owner; downstream eligibility stays unchanged until PROC-01 acts. Historical reviews remain available.

## 24. Failure and Recovery

Source outage leaves review Data Check/Incomplete. Duplicate facts are deduplicated by source ID. A correction creates new calculated version; conflicting review approval is blocked. Failed notification retries without changing decision.

## 25. Acceptance Criteria

Given missing delivery data, review marks incomplete rather than inventing score. Given corrected receipt, new review version shows changed metric and prior score remains. Given low score, supplier is not automatically suspended. Given another entity, confidential review is inaccessible.

## 26. Test Scenarios

Test normal/low sample, late/early delivery, rejection, source correction/replay, disputed metric, supplier merger, category-specific formula, approval conflict, sharing and tenant isolation.

## 27. Ownership and Dependencies

Supplier relationship lead owns review, Procurement owns scorecard, Compliance owns risk criteria, source modules own facts, PROC-01 owns eligibility. Depends on PROC-01/07/08/09 and reporting infrastructure.

## 28. Open Decisions and Assumptions

Confirm metrics/formulas, source quality, minimum sample, risk tiers, supplier response rights, preferred status and suspension linkage, review frequency and retention. Owners: Procurement, Compliance, Operations; resolve before implementation.
