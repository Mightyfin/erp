# M26 — Notification Delivery

M26 delivers HRM notifications through the organisation’s existing communications infrastructure rather than introducing an independent SMTP system:

```text
HRM business transaction
  -> hrm.outbox_messages
  -> HRM outbox publisher
  -> NATS JetStream (HRM_EVENTS / mightyfin.hrm.>)
  -> communications orchestrator
  -> Novu workflow
  -> configured delivery provider
```

## Supported events

| Event | Trigger | Novu workflow |
|---|---|---|
| `hrm.payslip.released` | One event per payslip finalized by payroll release | `hrm-payslip-released` |
| `hrm.request.decided` | An employee-bound HR request moves to `resolved` or `closed` | `hrm-request-decided` |
| `hrm.leave.requested` | A leave request and its balance reservation enter the approval workflow | `hrm-leave-requested` |
| `hrm.leave.decided` | A leave request is approved, returned, or rejected | `hrm-leave-decided` |
| `hrm.leave.cancelled` | An employee cancels an open leave request | `hrm-leave-cancelled` |

## Delivery guarantees

The business mutation and outbox insert share one database transaction. The durable outbox therefore protects the notification handoff when the communications stack is temporarily unavailable.

The publisher claims rows with `FOR UPDATE SKIP LOCKED`; failed rows retry with bounded exponential backoff. JetStream de-duplicates messages using the stable public event ID through `Nats-Msg-Id`. The communications orchestrator provides its own persistent idempotency, retry, audit, and dead-letter behavior.

Events contain routing and display facts only. Payslip amounts, statutory identifiers, request subjects and bodies, leave reasons, evidence, balances, and internal notes are excluded from the published notification envelope.

## Runtime configuration

The publisher runs from the API image with the `--run-outbox-publisher` argument. Required settings are `HRM:NatsUrl` and either `HRM:NatsToken` or `HRM:NatsTokenFile`. Optional settings include `HRM:Environment`, `HRM:OutboxPollSeconds`, and `HRM:PublicUrl`.

Direct SMTP is disabled by default. It becomes active only when `HRM:NotificationFallback=smtp` and the `HRM:Smtp:*` settings are explicitly supplied through environment secrets. SMTP is an emergency fallback after NATS publication fails; it is not an independent default notification system.

## Operations

HR administrators can use **Configuration → Technical → Notification delivery** to view tenant-scoped handoff counts, attempts, transport, and trace identifiers, and to requeue failed rows.

The equivalent role-gated API surface is:

```text
GET  /api/hrm/admin/notifications
POST /api/hrm/admin/notifications/{id}/retry
```

Neither endpoint returns event payloads, subject IDs, or recipient addresses. A `published` row means JetStream accepted the HRM handoff. Provider-level delivery remains authoritative in Novu Activity Feed and is not represented as final delivery by HRM.

Database operators can inspect aggregate state without exposing payloads:

```sql
SELECT status, event_type, count(*)
FROM hrm.outbox_messages
GROUP BY status, event_type
ORDER BY status, event_type;
```

A healthy deployment has a running `hrm-outbox-publisher-1`, an `HRM_EVENTS` JetStream stream, a `communications-orchestrator-hrm` durable consumer, and active Novu mappings for the supported events.

## Event envelope

The event envelope is intentionally stable and privacy-preserving. It contains the public event identifier used for idempotency, event type and version, tenant and subject references required for routing, timestamps, and display metadata required by the communications orchestrator. It does not contain sensitive payroll, statutory, medical, leave-evidence, or internal case data.

The publisher uses the subject pattern `mightyfin.hrm.>` and publishes to the organisation’s `HRM_EVENTS` JetStream stream. The communications orchestrator subscribes to that subject hierarchy and maps event types to Novu workflows.

## Production verification

The M26 deployment was verified on **16 August 2026** against `https://erp.mightyfinance.co.zm`.

| Check | Result |
|---|---|
| Frontend root | HTTP 200 |
| Application health endpoint `/health/live` | HTTP 200 |
| Unauthenticated protected routes | HTTP 401, as expected |
| Authenticated `GET /api/hrm/me/payslips` | Successful authenticated response |
| Authenticated `GET /api/hrm/me/notifications` | Successful authenticated response |
| Authenticated `GET /api/hrm/admin/notifications` | Successful role-gated response |
| `hrm-outbox-publisher-1` NATS connection | Connected to NATS and confirmed `HRM_EVENTS` |
| SMTP fallback | Disabled |
| Backend regression suite | 187/187 tests passing |
| React TypeScript and Vite build | Zero errors |

The production proxy removes the `/api/hrm` prefix before forwarding to the ASP.NET API. Accordingly, `/health/live` is the application health path, while `/api/hrm/workers` and other prefixed paths are externally exposed proxy paths.

## Follow-up verification

The remaining provider-level check is to execute one controlled test-tenant notification and verify the result in Novu Activity Feed. The HRM `published` state confirms JetStream acceptance, not final provider delivery. The communications orchestrator should be checked for active mappings for at least `hrm.payslip.released` and `hrm.request.decided` before production rollout.

## Source references

The implementation contract is defined by [`Messaging.cs`](../../backend/hrm-api/src/Mightyfin.Erp.Hrm.Application/Messaging.cs), [`Outbox.cs`](../../backend/hrm-api/src/Mightyfin.Erp.Hrm.Infrastructure/Outbox.cs), and [`ApiRoutesClean.cs`](../../backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ApiRoutesClean.cs). Automated coverage is located in the M26 notification outbox tests under `backend/hrm-api/src/Mightyfin.Erp.Hrm.Tests`.

**Last verified:** 16 August 2026
