# Procurement navigation and Lovable prompts

**Status:** Draft UI handoff<br>
**Prepared:** 3 October 2026<br>
**Source:** [PROC-00 capability map](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md) and the ERP [information architecture](../hrm/03-information-architecture.md)

This document translates the procurement capability map into role-aware navigation. The 12 parent capabilities are documentation boundaries, not 12 required sidebar entries. The proposed labels and release visibility are UI decisions for review; permissions and record scope are enforced by the ERP, not by hiding links alone.

## Navigation rules

- Use at most one level of expandable sublinks. A top-level entry opens a useful overview or work queue.
- Create, approve, issue, receive, match, return, and similar operations are actions on a record or work queue, not separate navigation destinations.
- Show only enabled capabilities and links permitted for the current user's role and data scope. APIs, search, exports, and direct URLs must enforce the same scope.
- Keep Tasks & Approvals, organization/entity switcher, global search, notifications, help, and profile in the shared ERP shell. A pending task count may appear there.
- Keep one Procurement Configuration entry for administrators. Put policy, numbering, workflow, templates, and preferences inside it.
- Give supplier users a separate, supplier-scoped portal. They do not use the internal Procurement sidebar.
- Give each complex record a stable URL. A requisition detail shows line-level routing and progress because one request may feed several sourcing or ordering routes.

## Proposed menus

### Employee

| Link | Purpose |
|---|---|
| Home | Next actions and recent buying activity |
| Catalog | Find approved goods and services |
| My Requests | Create and track requisitions |
| My Orders & Deliveries | Track orders and attest receipt where authorized |
| Approvals | Show only for users assigned procurement approvals; may resolve to shared Tasks & Approvals |

Use a prominent **Create Request** action near the top of Home and the sidebar. This action opens guided buying; it is not another menu group. In an initial release without a catalog, Home should still offer Create Request and the Catalog link should remain hidden until enabled.

### Procurement operations

| Top level | Sublinks |
|---|---|
| Home | None |
| Buying | Catalog; Requests; Procurement Plans when enabled |
| Suppliers | Directory; Onboarding; Risk & Performance when enabled |
| Sourcing | Events; Supplier Responses; Evaluations & Awards |
| Contracts | Agreements; Frameworks; Renewals |
| Purchasing | Purchase Orders; Goods Receipts; Service Acceptance; Returns |
| Invoices | Invoice Inbox; Matching & Exceptions; Payment Status |
| Reports | None; use tabs and saved views inside the reporting workspace |
| Configuration | None; admin-only setup workspace with internal tabs or sections |

Early releases show only implemented and authorized destinations. Do not render inactive feature links as if they work. Receiving remains grouped with Purchasing in navigation, while its data and workflow still have their own feature specification.

### Supplier portal

| Link | Purpose |
|---|---|
| Home | Pending supplier actions |
| Opportunities | Invited RFx events and submitted responses |
| Orders | Issued POs, acknowledgments, delivery updates |
| Invoices & Credits | Submitted documents and status |
| Documents | Requested and expiring compliance evidence |
| Messages | Supplier-visible transaction conversations |
| Company Profile | Own company and permitted contact details |

Payment status may appear on the related invoice and supplier Home, subject to Finance confirmation and supplier-view policy. Supplier users cannot navigate to another supplier's records or internal evaluation workspaces.

## Copyable Lovable prompts

### Employee sidebar

```text
Design the Procurement sidebar for an employee in our ERP. Keep it focused on buying tasks:

Home
Catalog
My Requests
My Orders & Deliveries
Approvals (show only when this employee has approval responsibility)

Put “Create Request” in a prominent button at the top, not as another menu group. Home should show the next action for each request. A request can contain lines that take different routes, so its detail page must show progress per line.

Use the existing ERP shell for organization switching, global search, notifications, help, and profile. Show only pages the user can access. Keep the sidebar readable when collapsed and on mobile. Hide Catalog until it is enabled.
```

### Procurement team sidebar

```text
Design a role-aware Procurement operations sidebar. Use these top-level links and expandable sublinks:

Home
Buying
  Catalog
  Requests
  Procurement Plans (only when enabled)
Suppliers
  Directory
  Onboarding
  Risk & Performance (only when enabled)
Sourcing
  Events
  Supplier Responses
  Evaluations & Awards
Contracts
  Agreements
  Frameworks
  Renewals
Purchasing
  Purchase Orders
  Goods Receipts
  Service Acceptance
  Returns
Invoices
  Invoice Inbox
  Matching & Exceptions
  Payment Status
Reports
Configuration (administrators only)

Add Tasks & Approvals to the shared ERP shell with a pending count. Keep only one submenu open at a time and no third navigation level. Each section opens an overview or work queue. Complex records have their own pages and stable URLs. Use clear status badges and show the next permitted action on each record.

Hide links the user cannot access or that are not enabled. Keep supplier banking details, sealed bids, and restricted records out of unauthorized search and navigation. Configuration is the single place for Procurement setup, policies, templates, and workflow rules.
```

### Supplier portal

```text
Design a separate supplier portal navigation, scoped to the signed-in supplier:

Home
Opportunities
Orders
Invoices & Credits
Documents
Messages
Company Profile

Show pending actions first: respond to an invitation, acknowledge an order, provide a document, or resolve an invoice issue. A supplier must only see its own records and explicitly shared sourcing events. Keep this portal visually related to the ERP, without exposing the internal Procurement sidebar.
```

## UI handoff checks

1. An employee can start and track a purchase request without seeing sourcing administration.
2. A buyer can reach a request, supplier, RFx, PO, receipt, or invoice exception in no more than three navigation decisions from Home.
3. A manager sees only assigned approvals and records within authorized scope.
4. Collapsed and mobile navigation preserve section names, active location, and pending task count.
5. Direct URL access and search do not expose restricted records when their sidebar link is hidden.
6. Unavailable advanced capabilities are hidden until their release and permission gates are met.
