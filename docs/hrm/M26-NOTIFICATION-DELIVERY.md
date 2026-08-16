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

## Delivery guarantees

- The business mutation and outbox insert share one database transaction.
- JetStream de-duplicates on the stable public event ID (`Nats-Msg-Id`).
- The publisher claims rows with `FOR UPDATE SKIP LOCKED`; failed rows retry with bounded exponential backoff.
- The communications orchestrator applies its persistent idempotency, retry, audit, and dead-letter behavior.
- Events carry routing and display facts only. Payslip amounts, statutory identifiers, request subjects, request bodies, and internal notes are excluded.

## Runtime configuration

The publisher runs from the API image with `--run-outbox-publisher`. Required settings are `HRM:NatsUrl` and either `HRM:NatsToken` or `HRM:NatsTokenFile`. `HRM:Environment`, `HRM:OutboxPollSeconds`, and `HRM:PublicUrl` are optional.

Direct SMTP is disabled by default. It becomes active only when `HRM:NotificationFallback=smtp` and the `HRM:Smtp:*` settings are explicitly supplied. SMTP is an emergency fallback after NATS publication fails; it is not an independent default notification system.

## Operations

Inspect backlog and failures without exposing payloads:

```sql
SELECT status, event_type, count(*)
FROM hrm.outbox_messages
GROUP BY status, event_type
ORDER BY status, event_type;
```

The expected healthy state is a running `hrm-outbox-publisher-1`, an `HRM_EVENTS` JetStream stream, a `communications-orchestrator-hrm` durable consumer, and active Novu workflows for both identifiers above.
