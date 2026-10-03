# Procurement Documentation Coverage and Decision Register

**Status:** Draft review aid<br>
**Version:** 0.1<br>
**Applies to:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md) and the active child specifications in the [index](README.md)

This register shows where the supplied module outline and later Zoho coverage findings are documented. It also collects the decisions that prevent a draft feature from being approved for development. Coverage means **documented as a proposal**, not implemented or approved.

## Coverage of the supplied module outline

| Supplied outline sections | Topic | Current source |
|---|---|---|
| 1–2, 39, 42 | Purpose, hierarchy, end-to-end flow, module boundary | [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md) |
| 3 | Annual procurement planning | [PROC-05](PROC-05-PROCUREMENT-PLANNING.md) |
| 4 | PR header and per-line lifecycle | [PROC-02](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) |
| 5–6, 29 | Approvals, delegation, budget and commitment | [PROC-03](PROC-03-APPROVAL-AND-BUDGET-CONTROL.md), [PROC-CFG-01](PROC-CFG-01-PROCUREMENT-CONFIGURATION-AND-POLICY.md) |
| 7–11 | Sourcing methods, RFx, quotations, evaluation and award | [PROC-06](PROC-06-STRATEGIC-SOURCING-AND-AWARD.md) |
| 12–13 | Standard/blanket PO, issue, acknowledgment and change | [PROC-08](PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md) |
| 14–18, 26 | Goods and service receipt, inspection, returns, Inventory/Asset handoff | [PROC-09](PROC-09-RECEIVING-INSPECTION-AND-RETURNS.md) |
| 19, 27 | Invoice intake, correction and supplier credit reason | [PROC-10](PROC-10-INVOICE-INTAKE-AND-SUPPLIER-ADJUSTMENTS.md) |
| 20–23 | Two/three-way match, exceptions, AP and payment visibility | [PROC-11](PROC-11-INVOICE-MATCH-AP-HANDOFF-AND-PAYMENT-VISIBILITY.md) |
| 24–25 | Contracts, framework agreements, recurring commitment | [PROC-07](PROC-07-CONTRACTS-FRAMEWORKS-AND-RECURRING-COMMITMENTS.md) |
| 28, 35–36 | Documents, notifications, business audit and collaboration | [PROC-CFG-01](PROC-CFG-01-PROCUREMENT-CONFIGURATION-AND-POLICY.md), [PROC-14](PROC-14-ANALYTICS-CONTROLS-AND-RECORD-COLLABORATION.md), shared ERP services |
| 30–34 | Permission versus scope, segregation, rules, ownership, events | [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md), section 10/11/12/18 of each child |
| 37–38 | Dashboards, spend/supplier/source-to-pay reports | [PROC-14](PROC-14-ANALYTICS-CONTROLS-AND-RECORD-COLLABORATION.md), [PROC-13](PROC-13-SUPPLIER-PERFORMANCE-AND-RISK.md) |
| 40–41 | 28-section standard and build order | [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md), [index](README.md) |

| Later coverage finding | Documented in |
|---|---|
| Item master, catalog, preferred supplier/price and non-catalog fallback | [PROC-04](PROC-04-PROCUREMENT-ITEMS-AND-CATALOG.md), [PROC-02](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) |
| Employee guided buying | [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md), [PROC-02](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) |
| Line-level PR conversion and balance | [PROC-02](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md) |
| Approval strategy types and state SLAs | [PROC-CFG-01](PROC-CFG-01-PROCUREMENT-CONFIGURATION-AND-POLICY.md), [PROC-03](PROC-03-APPROVAL-AND-BUDGET-CONTROL.md) |
| Surrogate/offline supplier bids | [PROC-06](PROC-06-STRATEGIC-SOURCING-AND-AWARD.md) |
| Supplier portal isolation | [PROC-12](PROC-12-SUPPLIER-PORTAL.md) |
| Supplier performance and risk | [PROC-13](PROC-13-SUPPLIER-PERFORMANCE-AND-RISK.md) |
| Blanket PO, recurring commitment, credits and refunds | [PROC-08](PROC-08-PURCHASE-ORDERS-AND-AMENDMENTS.md), [PROC-07](PROC-07-CONTRACTS-FRAMEWORKS-AND-RECURRING-COMMITMENTS.md), [PROC-10](PROC-10-INVOICE-INTAKE-AND-SUPPLIER-ADJUSTMENTS.md) |
| Invoice inbox and optional OCR | [PROC-10](PROC-10-INVOICE-INTAKE-AND-SUPPLIER-ADJUSTMENTS.md) |
| Record comments, sharing and supplier-visible communication | [PROC-14](PROC-14-ANALYTICS-CONTROLS-AND-RECORD-COLLABORATION.md), [PROC-12](PROC-12-SUPPLIER-PORTAL.md) |
| Custom fields, global search and quick create | Shared ERP platform boundary in [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md); procurement-specific policy in [PROC-CFG-01](PROC-CFG-01-PROCUREMENT-CONFIGURATION-AND-POLICY.md) |

## Cross-module decisions to resolve

| ID | Decision and affected features | Proposed owner | Gate |
|---|---|---|---|
| D01 | Launch entity, country, currency, transaction types and sequence | Product and Procurement | Release scope |
| D02 | Procurement runtime, migrations, deployment and API/event transport | ERP architecture | Technical design |
| D03 | Organization and workforce reference ownership while HRMS holds current organization records | HRMS and ERP platform owners | Identity/org contract |
| D04 | Item master, stock/asset classification and catalog ownership | Procurement, Inventory and Assets | PROC-04 and PR item references |
| D05 | Approval strategy, limits, delegation, segregation and policy precedence | Procurement governance and Workflow owner | PROC-CFG-01/03 |
| D06 | Finance budget check, reservation point, release, AP payload and payment-status contracts | Finance and Procurement | PROC-02/03/08/11 |
| D07 | Supplier diligence, remittance verification, risk tiers and data retention | Procurement, Compliance, Finance and Security | PROC-01/13 |
| D08 | Sourcing methods, quote minimum, tender process, sealed/surrogate bid and award authority | Procurement and Legal | PROC-06 |
| D09 | Contract signature, ceiling basis, framework/blanket/recurring rules | Procurement, Legal and Finance | PROC-07/08 |
| D10 | Receipt/inspection tolerances, service evidence, Inventory/Asset handoff | Operations, Inventory and Assets | PROC-09 |
| D11 | Invoice duplicate key, match method/tolerances, credit and AP exception authority | AP and Finance | PROC-10/11 |
| D12 | External supplier identity, portal release scope, record sharing and incident response | Security, Procurement and Product | PROC-12/14 |
| D13 | KPI formulas, spend/savings meaning, FX/as-of method and reporting access | Finance, Procurement and Data | PROC-13/14 |
| D14 | Shared document, audit, notification, search and metadata capabilities; service levels and retention | ERP platform, Security and Records | All child features |

Each child section 28 contains its more detailed decisions and proposed owners. A decision is complete only after the named owner records the approved value, effective scope/date and evidence. Until then, an example or suggested starting point is not implementation policy.
