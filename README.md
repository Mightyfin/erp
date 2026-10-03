# MightyFin Enterprise ERP

Status: **HRM frontend and ASP.NET Core API present; Procurement is in specification**

MightyFin's internal employee and corporate administration platform — HR and payroll,
finance, procurement and inventory. It is deliberately separate from the regulated core
(customer identity, wallet, ledger, lending, payments, partners); those are their own
services in their own repositories.

## Shape

One deployable, modular inside. Not microservices — see
[`docs/00-architecture-position.md`](docs/00-architecture-position.md) for the decision, the
extraction gate it was tested against, and the conditions that would reverse it.

```text
ERP API                          ERP database
├── HRM                          ├── hrm schema
│   └── Payroll                  ├── finance schema
├── Finance                      ├── procurement schema
├── Procurement                  └── inventory schema
└── Inventory
```

Target module rules: each module owns its schema, never reads another module's tables,
keeps its own permissions, migrations, jobs and tests, and reaches other modules through
defined contracts. The architecture calls for per-module Postgres roles; the bootstrap
migration creates schemas but does not yet configure those roles.

## Layout

| Path | What |
|---|---|
| `docs/` | Architecture position and product documentation |
| `docs/hrm/` | HRM product principles, personas, information architecture, workflow catalogue and the frontend build contract |
| `docs/hrm/feature-specifications/` | Prepared HRM and ERP feature specification documents |
| `docs/procurement/` | Procurement module architecture, capability map and feature documentation sequence |
| `modules/hrm/frontend/module-connect/` | HRM web UI — React, TanStack Router, Vite |
| `backend/hrm-api/` | ASP.NET Core HRM API and EF Core migrations for the `hrm` schema |
| `cmd/`, `internal/` | Go ERP API bootstrap, authentication, migrations and health endpoints |

The earlier architecture record proposes a Go backend. The implemented HRM backend uses
ASP.NET Core and EF Core, while the Go ERP API currently provides shared bootstrap facilities.
The Procurement specification records the runtime and migration ownership decision needed
before its backend is built.

## HRM frontend

```bash
cd modules/hrm/frontend/module-connect && npm install && npm run dev
```

The frontend contains both mock and real API paths. The original replacement contract is
recorded in [`docs/hrm/08-frontend-build-contract.md`](docs/hrm/08-frontend-build-contract.md);
consult the current route and client code to see which screens use each path.

Branding lives entirely in `src/theme/tokens.css`. MightyFin is the **vendor**; the employer
whose data appears on screen is a **tenant** and must stay swappable.

## Procurement planning

The [PROC-00 Procurement Module Architecture and Capability Map](docs/procurement/PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)
is the current draft source for the module boundary, parent capabilities, data ownership and build order.
Child features will receive the required 28-section specification before implementation.
The [complete Procurement UI prompt pack](docs/procurement/PROC-UX-02-COMPLETE-LOVABLE-UI-PROMPT-PACK.md)
provides the sequenced Lovable handoff for internal and supplier-facing screens.
