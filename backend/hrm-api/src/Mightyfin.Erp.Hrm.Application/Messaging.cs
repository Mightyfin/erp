using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application;

public static class HrmEventTypes
{
    public const string PayslipReleased = "hrm.payslip.released";
    public const string RequestDecided = "hrm.request.decided";
}

/// <summary>Adds an event to the current scoped DbContext. The surrounding
/// unit of work decides when the transaction commits.</summary>
public interface IOutboxWriter
{
    Task<OutboxMessage> EnqueueAsync(
        string eventType,
        string subjectId,
        object privacySafePayload,
        CancellationToken ct);
}

/// <summary>Runs all repository saves made by an operation under one database
/// transaction. Repositories share the same scoped DbContext.</summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct);
}

/// <summary>Minimum recipient and payslip facts needed to build a privacy-safe
/// notification event after payslips are finalized.</summary>
public sealed record PayslipNotificationTarget(
    Guid PayslipId,
    string PayslipNo,
    string PeriodLabel,
    Guid WorkerId,
    string? SubjectId,
    string? Email,
    string FirstName,
    string LastName);
