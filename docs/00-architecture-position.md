# HRM Architecture Position — Modular Monolith Inside the ERP

Status: **Adopted position for the initial build; one spike outstanding (§7)**
Last updated: 2026-08-03

This record exists so the deployment shape of HRM is not relitigated every time a new module
is added. It states what was decided, the evidence behind it, and the conditions under which
the decision should be revisited.

## 1. Decision

HRM is built as a **bounded module inside a single deployable ERP service**, not as a
microservice. Payroll stays inside HRM. Physically together; logically isolated from the start.

```text
ERP API                          ERP database
├── HRM                          ├── hrm schema
│   └── Payroll                  ├── finance schema
├── Finance                      ├── accounting schema
├── Accounting                   ├── procurement schema
├── Procurement                  └── inventory schema
└── Inventory
```

## 2. Why this and not microservices

Applying the extraction gate recorded in the platform workspace's `microservices/README.md`:
*"Extract a deployment only for justified ownership, scaling, isolation or availability needs."*

| Gate | HRM verdict |
|---|---|
| Ownership | One internal team owns all of HR. No independent ownership need. |
| Scaling | Load scales with headcount — bounded and small. Payroll is a monthly batch spike, answered by a worker process, not a service split. |
| Isolation | The strongest argument. Salary, disciplinary and health data are unusually sensitive — but that is solved by schema separation, per-module database roles and field-level masking inside one service, not by a network boundary. |
| Availability | HRM must not be able to take down lending or payments. Separation from `microservices/` already achieves that. HRM does not need independent availability. |

None of the four justify extraction. The same test already produced the same answer for
`case-management`, which remains product-local until extraction is justified.

Supporting reasons: employee, payroll, accounting, expenses and organization workflows
coordinate constantly; one deployment is easier to build, test and operate; transactions and
cross-module reporting stay simple; and premature network calls, distributed failure and
event-consistency problems are avoided entirely.

## 3. Module rules

HRM behaves as an independent bounded module:

- Owns its domain models and the `hrm` database schema.
- Payroll stays inside HRM.
- Exposes internal APIs and events; other modules are reached only through defined contracts.
- Never reads or writes another module's tables.
- Maintains its own permissions, migrations, background jobs and tests.
- Consumes shared IdP, notifications, object storage, event platform, analytics and audit.

Two clarifications that decide whether the above holds in practice:

**Internal APIs means in-process interfaces, not HTTP to localhost.** Modules in one process
calling each other over HTTP take on distributed failure modes while keeping monolith
deployment — the worst of both. The event platform is for genuinely asynchronous cross-module
facts only. The seams are already named in
[`08-frontend-build-contract.md`](hrm/08-frontend-build-contract.md):
`IdentityClient`, `PeopleClient`, `WorkflowClient`, `TimeClient`, `PayrollClient`,
`DocumentClient`, `ReportingClient`, `ProtectedDisclosureClient`. The backend mirrors these as
internal module interfaces.

**Table isolation is enforced by Postgres, not by code review.** One database role per module,
granted only on its own schema. A cross-schema read then fails in development rather than
passing review and surfacing in production.

## 4. Payroll

Payroll is an HRM domain concern and stays in the module. The **calculation engine runs as a
separate worker process off the same binary**, never in the API request path: the work is
long-running, chunked, resumable and cancellable. The frontend already assumes exactly this
shape (`POST /payroll/runs/:runId/calculate` returning a job id, then polling) — see
[`calculation.ts`](../modules/hrm/frontend/module-connect/src/mock/calculation.ts).

Statutory rule sets (PAYE bands, NAPSA ceiling, NHIMA rate) are versioned per period as a
country pack, so a prior period can be recalculated on the rates that applied then. Filing and
remittance to ZRA, NAPSA and NHIMA belong to
the platform workspace's `integrations/government-and-payroll` boundary,
not to the ERP: those systems run on their own schedule, credentials, outages and retry
semantics.

## 5. The two-ledger boundary

MightyFin now has two double-entry systems, and the boundary between them is load-bearing:

| System | Books | Owner |
|---|---|---|
| `microservices/ledger` | Customer money — regulated, immutable, balanced | Regulated core |
| ERP `accounting` schema | Corporate books — payroll cost, supplier invoices, fixed assets | ERP |

They must never merge and must never share a table. Flow is one-directional: the regulated
ledger emits summarised postings *out* to ERP accounting through
`integrations/accounting-and-erp`. ERP never writes journal entries to the regulated ledger.

Net pay reaches employees through `payment-rails` like any other disbursement. HRM produces a
payment instruction and a cost figure; it does not post to the financial journal.

## 6. Stack

Follow the pattern already proven twice in this workspace by `microservices/wallet` and
`microservices/payment-rails` — Go, PostgreSQL with goose migrations, transactional outbox,
`internal/` package layout, OIDC middleware, environment config with secret-file support,
compose and CI per service. Two services sharing one shape is a stack decision; the runbooks,
tooling and reviewer familiarity transfer.

Two HRM-specific adjustments:

- **Schema-per-module in one database**, consistent with the position recorded in the root
  `docker-compose.yml`: domain schemas and roles first, not separate physical databases. One
  migration runner, with a defined per-schema ordering at boot.
- **Staff identity is not customer identity.** Employees authenticating into the ERP belong to
  a workforce realm, separate from the customer realm — see
  the platform workspace's `docs/infrastructure/25-shared-idp-keycloak.md`.

The frontend stays as built — React with TanStack Router, mock clients swapped for real fetches,
which the build contract was explicitly designed to allow without touching UI code.

## 7. Decisions taken 2026-08-03

**Finance and Accounting are one module, not two.** They are a single bounded context —
general ledger, payables, receivables, fixed assets. The module list becomes HRM (with
Payroll), Finance, Procurement, Inventory, and the schema list follows.

**Standalone HRM sales are in scope, with a deadline.** Therefore `tenant_id` is present on
every table from the first migration, matching `wallet` and `payment-rails`. This is not
deferred to the extraction point: single-tenant ERP and multi-tenant HRM are different
schemas, not different deployments. The standalone build is a packaging profile of this
repository (§8), never a fork.

**Build the HRM and payroll engine; buy the statutory rules; do not adopt a foundation
wholesale.** Three constraints decide this together:

- *Licensing.* Selling HRM standalone makes copyleft binding. Frappe HR and ERPNext are
  GPLv3 — deploying to a customer's own server is distribution, which would oblige us to
  offer our Zambian payroll work under GPLv3. Odoo Community is LGPL-3.0 and materially
  safer. OrangeHRM's Starter edition is GPLv2 and gates payroll connectors and API
  integration behind a proprietary tier.
- *Data residency.* The Zambia Data Protection Act (2021) rules out foreign-hosted SaaS for
  employee PII, which removes most commercial HRIS candidates before licensing is even
  reached.
- *Stack.* Frappe/ERPNext runs on MariaDB, against both the one-Postgres position in §6 and
  the AWS-exit and Zambia-repatriation constraints. Odoo is Postgres-native.

What is bought rather than built is **maintained statutory rules with professional liability
attached** — who updates the PAYE bands, NAPSA ceiling and NHIMA rate at each budget cycle,
in perpetuity. Computing PAYE is roughly a week of work; keeping it correct forever is the
recurring cost. Sourced either from a Zambian payroll vendor's rules service or from a
retained Zambian tax adviser publishing a signed country pack per cycle.

**Adopted as a model, not as code:** ERPNext's Income Tax Slab design — progressive bands as
effective-dated configuration scoped to a payroll period, with salary components resolving
against them at calculation time ("variable based on taxable salary"). This is a design, not
source, so no licence attaches. It is also the shape the investor replication requirement
needs: a new country is new slab data, not new code. The existing component model in
`configuration.ts` already separates taxable from pensionable and carries `effectiveFrom`;
what it lacks is the slab table and run-level version pinning.

### Spike closed 2026-08-03 — build confirmed

`hr_payroll` is **Odoo Enterprise only**, not Community. That was the stated reversal
condition, and it does not hold: adopting Odoo for payroll means per-user Enterprise
licensing on a product we intend to resell, on top of the LGPL boundary work. The ZRA-approval
and per-user-cost questions are moot for the build decision and are deferred rather than
answered.

**Build is confirmed. No foundation is adopted.**

### The legacy payroll engine is the real starting point

This substantially revises the "buy the statutory rules" position above. The legacy LMS
(`Mightyfin/admin-lms`, Laravel — `legacy/lms` in the platform workspace) already contains a
working, tested Zambian payroll implementation — not a prototype. Paths below are relative to
that repository:

| Asset | Location |
|---|---|
| Slab/band engine | `app/Models/PayrollComponentSlab.php` — `min_amount`, `max_amount`, `rate`, `fixed_amount`, `sequence`, per rule |
| Rule resolution | `app/Services/Payroll/PayrollRuleResolver.php`, `PayrollComponentRule`, `PayrollComponentCondition` |
| Calculation engine | `app/Services/Payroll/PayrollCalculationService.php` (~21KB), with a `statutory` component category and a `SLAB` calc method |
| Salary structures | `app/Services/Payroll/SalaryStructureService.php` |
| Run lifecycle | `PayrollRunExecutionService`, `PayrollRunStateService`, `PayrollApproval` |
| Statutory identifiers | `app/Models/EmployeeStatutoryId.php` |
| Payroll → general ledger | `app/Services/Accounting/PayrollAccountingService.php` (~17KB) |
| Tests | `tests/Feature/PayrollV1/` — engine, run execution, settings hardening, seeder, onboarding |

The slab model is generic and data-driven — bands are rows, not code — which is the same shape
recommended above from ERPNext, already built and already proven against ZRA, NAPSA and NHIMA
in production. Two consequences:

1. **Port the proven model rather than copying anyone else's.** The design question is settled;
   what remains is a Go re-implementation with effective-dating and run-level version pinning
   added, neither of which the legacy schema appears to carry.
2. **What is bought narrows.** We do not need a vendor's rules engine — we own a working one.
   What is still worth buying is the *maintenance obligation*: who supplies the corrected band
   rows each budget cycle, with professional liability attached. That is a retained tax adviser
   or a rules subscription, not an application.

`PayrollAccountingService` is also direct prior art for the §5 two-ledger boundary and should be
read before that interface is designed.

Note for future sessions: ERPNext previously appeared in early architecture diagrams as a
stand-in for "we want ERP-type capability" and was confirmed **not** in use. The assessment
above is a fresh evaluation, not a resumption of that.

## 8. When to revisit

Extract HRM into its own service only on evidence, not anticipation:

- HRM needs independent scaling or release cycles.
- A separate team owns it.
- It must be sold and deployed independently.
- Customers need HRM without the ERP runtime.
- Security or data-residency requirements demand isolation.
- HRM workloads materially affect other ERP modules.
- HRM requires a different availability SLO from the rest of the ERP.

If standalone HRM becomes an immediate requirement, ship the **same modular HRM code through a
separate packaging profile** — not a duplicated HRM codebase. A fork is the failure mode this
position exists to prevent.
