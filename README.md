# MightyFin Enterprise ERP

Status: **Architecture settled, HRM frontend built against mocks, no backend yet**

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

Module rules in brief: each module owns its schema, never reads another module's tables,
keeps its own permissions, migrations, jobs and tests, and reaches other modules through
defined contracts. Isolation is enforced by per-module Postgres roles, not by code review.

## Layout

| Path | What |
|---|---|
| `docs/` | Architecture position and product documentation |
| `docs/hrm/` | HRM product principles, personas, information architecture, workflow catalogue and the frontend build contract |
| `docs/hrm/feature-specifications/` | Prepared HRM and ERP feature specification documents |
| `modules/hrm/frontend/module-connect/` | HRM web UI — React, TanStack Router, Vite |

Backend modules are not yet present. When they land they follow the pattern already proven
by `Mightyfin/wallet` and `Mightyfin/payment-rails`: Go, PostgreSQL with goose migrations,
transactional outbox, `internal/` package layout, OIDC middleware.

## HRM frontend

```bash
cd modules/hrm/frontend/module-connect && npm install && npm run dev
```

Every screen currently reads from `src/mock/`. The mock clients are written to be replaced by
real fetches without touching UI code — that is the explicit contract in
[`docs/hrm/08-frontend-build-contract.md`](docs/hrm/08-frontend-build-contract.md). No figure
shown anywhere in the app is calculated by the frontend, and none should ever be.

Branding lives entirely in `src/theme/tokens.css`. MightyFin is the **vendor**; the employer
whose data appears on screen is a **tenant** and must stay swappable.

## Known gaps

Recorded rather than discovered later:

- No backend. The payroll calculation engine, statutory country packs and every write path
  are unimplemented.
- Pay group is a bare string with no registry, and the values on the employee record do not
  match those offered when opening a pay run.
- Tax bands do not exist. `basis: "ZRA 2026 monthly bands"` is a display string; the figures
  in the mocks are literals.
- Configuration screens read but do not write.

The payroll engine is a **port, not a greenfield build** — `Mightyfin/admin-lms` already runs
a tested, data-driven Zambian slab engine in production. See `docs/00-architecture-position.md`.
