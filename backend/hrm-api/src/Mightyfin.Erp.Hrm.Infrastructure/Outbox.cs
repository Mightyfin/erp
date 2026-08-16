using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;

namespace Mightyfin.Erp.Hrm.Infrastructure;

public sealed class EfUnitOfWork(HrmDbContext db) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            await operation(ct);
            return;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await operation(ct);
            await transaction.CommitAsync(ct);
        });
    }
}

public sealed class EfOutboxWriter(
    HrmDbContext db,
    IConfiguration configuration,
    Microsoft.AspNetCore.Http.IHttpContextAccessor httpContext) : IOutboxWriter
{
    public async Task<OutboxMessage> EnqueueAsync(
        string eventType,
        string subjectId,
        object privacySafePayload,
        CancellationToken ct)
    {
        if (!eventType.StartsWith("hrm.", StringComparison.Ordinal))
            throw new ArgumentException("HRM event types must start with 'hrm.'.", nameof(eventType));

        var correlationId = httpContext.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? httpContext.HttpContext?.TraceIdentifier
            ?? $"corr_{Guid.NewGuid():N}";
        var environment = configuration["HRM:Environment"]
            ?? configuration["ASPNETCORE_ENVIRONMENT"]?.ToLowerInvariant()
            ?? "production";
        var message = new OutboxMessage
        {
            PublicId = $"evt_{Guid.NewGuid():N}",
            EventType = eventType,
            EventVersion = "1",
            Environment = environment,
            SubjectId = subjectId,
            CorrelationId = correlationId,
            PayloadJson = JsonSerializer.Serialize(privacySafePayload),
            Status = "pending",
            AvailableAt = DateTimeOffset.UtcNow,
        };
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(ct);
        return message;
    }
}

public interface IOutboxPublisherStore
{
    Task<List<OutboxMessage>> ClaimAsync(int limit, CancellationToken ct);
    Task CompleteAsync(Guid id, bool success, string transport, string? error, CancellationToken ct);
}

public sealed class EfOutboxPublisherStore(HrmDbContext db) : IOutboxPublisherStore
{
    public async Task<List<OutboxMessage>> ClaimAsync(int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var staleBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var rows = await db.OutboxMessages
            .FromSqlInterpolated($@"
                SELECT * FROM hrm.outbox_messages
                WHERE available_at <= now()
                  AND (status IN ('pending', 'failed')
                       OR (status = 'publishing' AND updated_at < {staleBefore}))
                ORDER BY created_at
                FOR UPDATE SKIP LOCKED
                LIMIT {limit}")
            .IgnoreQueryFilters()
            .ToListAsync(ct);
        foreach (var row in rows)
        {
            row.Status = "publishing";
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return rows;
    }

    public async Task CompleteAsync(Guid id, bool success, string transport, string? error, CancellationToken ct)
    {
        var row = await db.OutboxMessages.IgnoreQueryFilters().FirstAsync(x => x.Id == id, ct);
        row.PublishAttempts++;
        row.LastTransport = transport;
        row.LastError = string.IsNullOrWhiteSpace(error) ? null : error[..Math.Min(error.Length, 2000)];
        row.UpdatedAt = DateTimeOffset.UtcNow;
        if (success)
        {
            row.Status = transport == "smtp" ? "fallback-delivered" : "published";
            row.PublishedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            row.Status = "failed";
            var delaySeconds = Math.Min(900, 10 * (int)Math.Pow(2, Math.Min(row.PublishAttempts - 1, 6)));
            row.AvailableAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
        }
        await db.SaveChangesAsync(ct);
    }
}

public interface IHrmEventPublisher : IAsyncDisposable
{
    Task EnsureStreamAsync(CancellationToken ct);
    Task PublishAsync(OutboxMessage row, CancellationToken ct);
}

public sealed class NatsHrmEventPublisher : IHrmEventPublisher
{
    private readonly NatsClient client;
    private readonly INatsJSContext jetStream;

    public NatsHrmEventPublisher(IConfiguration configuration)
    {
        var url = configuration["HRM:NatsUrl"];
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("HRM:NatsUrl is required by the outbox publisher.");
        var token = configuration["HRM:NatsToken"];
        var tokenFile = configuration["HRM:NatsTokenFile"];
        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(tokenFile))
            token = File.ReadAllText(tokenFile).Trim();
        var opts = NatsOpts.Default with
        {
            Url = url,
            AuthOpts = NatsAuthOpts.Default with { Token = token },
        };
        client = new NatsClient(opts);
        jetStream = client.CreateJetStreamContext();
    }

    public async Task EnsureStreamAsync(CancellationToken ct)
    {
        await jetStream.CreateOrUpdateStreamAsync(
            new StreamConfig("HRM_EVENTS", ["mightyfin.hrm.>"]) { Storage = StreamConfigStorage.File }, ct);
    }

    public async Task PublishAsync(OutboxMessage row, CancellationToken ct)
    {
        var envelope = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = row.PublicId,
            type = row.EventType,
            version = row.EventVersion,
            occurred_at = row.CreatedAt.UtcDateTime,
            tenant_id = row.TenantId,
            environment = row.Environment,
            subject_id = row.SubjectId,
            correlation_id = row.CorrelationId,
            data = JsonDocument.Parse(row.PayloadJson).RootElement,
        });
        var ack = await jetStream.PublishAsync(
            $"mightyfin.{row.EventType}",
            envelope,
            opts: new NatsJSPubOpts { MsgId = row.PublicId },
            cancellationToken: ct);
        ack.EnsureSuccess();
    }

    public ValueTask DisposeAsync() => client.DisposeAsync();
}

public interface ISmtpNotificationFallback
{
    bool Enabled { get; }
    Task DeliverAsync(OutboxMessage row, CancellationToken ct);
}

/// <summary>Emergency-only direct delivery. It is inert unless
/// HRM:NotificationFallback is exactly "smtp" and all SMTP settings exist.</summary>
public sealed class SmtpNotificationFallback : ISmtpNotificationFallback
{
    private readonly IConfiguration configuration;
    public bool Enabled { get; }

    public SmtpNotificationFallback(IConfiguration configuration)
    {
        this.configuration = configuration;
        Enabled = string.Equals(configuration["HRM:NotificationFallback"], "smtp", StringComparison.OrdinalIgnoreCase);
    }

    public async Task DeliverAsync(OutboxMessage row, CancellationToken ct)
    {
        if (!Enabled)
            throw new InvalidOperationException("SMTP fallback is not enabled.");
        using var payload = JsonDocument.Parse(row.PayloadJson);
        var root = payload.RootElement;
        var email = Required(root, "email");
        var firstName = Optional(root, "first_name");
        var portalUrl = configuration["HRM:PublicUrl"]?.TrimEnd('/') ?? "https://hrm.mightyfinance.co.zm";
        var (subject, body) = row.EventType switch
        {
            HrmEventTypes.PayslipReleased => (
                "Your payslip is available",
                $"Hello {firstName},\n\nYour payslip for {Optional(root, "period_label")} is now available. Sign in to view it: {portalUrl}/hrm/payslips/{Optional(root, "payslip_id")}\n"),
            HrmEventTypes.RequestDecided => (
                "Your HR request was updated",
                $"Hello {firstName},\n\nThe status of your HR request is now {Optional(root, "status")}. Sign in to view it: {portalUrl}/hrm/requests/{Optional(root, "request_id")}\n"),
            _ => throw new InvalidOperationException($"No SMTP fallback template exists for {row.EventType}."),
        };

        var host = RequiredConfig("HRM:Smtp:Host");
        var from = RequiredConfig("HRM:Smtp:From");
        var port = int.TryParse(configuration["HRM:Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        using var message = new MailMessage(from, email, subject, body);
        using var smtp = new SmtpClient(host, port)
        {
            EnableSsl = !string.Equals(configuration["HRM:Smtp:UseTls"], "false", StringComparison.OrdinalIgnoreCase),
        };
        var username = configuration["HRM:Smtp:Username"];
        if (!string.IsNullOrWhiteSpace(username))
            smtp.Credentials = new NetworkCredential(username, RequiredConfig("HRM:Smtp:Password"));
        await smtp.SendMailAsync(message, ct);
    }

    private string RequiredConfig(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} is required when SMTP fallback is enabled.");
    private static string Required(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidOperationException($"Outbox payload is missing {key}.");
    private static string Optional(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
}
