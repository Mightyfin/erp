# M26 — Notification Delivery

M26 delivers HRM notifications through the platform communications path:

```text
HRM business transaction
  -> hrm.outbox_messages
  -> HRM outbox publisher
  -> NATS JetStream (HRM_EVENTS / mightyfin.hrm.>)
  -> communications orchestrator
  -> Novu workflow
  -> configured delivery provider
```

The supported version-1 events are:

| Event | Trigger | Novu workflow |
|---|---|---|
| `hrm.payslip.released` | One event per payslip finalized by payroll release | `hrm-payslip-released` |
| `hrm.request.decided` | An employee-bound HR request moves to `resolved` or `closed` | `hrm-request-decided` |
| `hrm.leave.requested` | A leave request and its balance reservation enter the approval workflow | `hrm-leave-requested` |
| `hrm.leave.decided` | A leave request is approved, returned, or rejected | `hrm-leave-decided` |
| `hrm.leave.cancelled` | An employee cancels an open leave request | `hrm-leave-cancelled` |

## Delivery guarantees

- The business mutation and outbox insert share one database transaction.
- JetStream de-duplicates on the stable public event ID (`Nats-Msg-Id`).
- The publisher claims rows with `FOR UPDATE SKIP LOCKED`; failed rows retry with bounded exponential backoff.
- The communications orchestrator applies its persistent idempotency, retry, audit, and dead-letter behavior.
- Events carry routing and display facts only. Payslip amounts, statutory identifiers, request subjects/bodies, leave reasons/evidence/balances, and internal notes are excluded.

## Runtime configuration

The publisher runs from the API image with `--run-outbox-publisher`. Required settings are `HRM:NatsUrl` and either `HRM:NatsToken` or `HRM:NatsTokenFile`. `HRM:Environment`, `HRM:OutboxPollSeconds`, and `HRM:PublicUrl` are optional.

Direct SMTP is disabled by default. It becomes active only when `HRM:NotificationFallback=smtp` and the `HRM:Smtp:*` settings are explicitly supplied. SMTP is an emergency fallback after NATS publication fails; it is not an independent default notification system.

## Operations

Inspect backlog and failures without exposing payloads:

- HR admins use **Configuration → Technical → Notification delivery** to see tenant-scoped handoff counts, attempts, transport, and trace identifiers, and to requeue failed rows.
- `GET /api/hrm/admin/notifications` and `POST /api/hrm/admin/notifications/{id}/retry` provide the same role-gated operational surface through the production proxy (the API also registers its versioned `/api/v1/hrm` surface internally). Neither endpoint returns event payloads, subject IDs, or recipient addresses.
- A `published` row means JetStream accepted the HRM handoff. Provider-level delivery remains authoritative in Novu's Activity Feed; it is not presented as delivered by HRM.

Database operators can inspect aggregate state directly:

```sql
SELECT status, event_type, count(*)
FROM hrm.outbox_messages
GROUP BY status, event_type
ORDER BY status, event_type;
```

The expected healthy state is a running `hrm-outbox-publisher-1`, an `HRM_EVENTS` JetStream stream, a `communications-orchestrator-hrm` durable consumer, and all five active Novu workflows listed above.
