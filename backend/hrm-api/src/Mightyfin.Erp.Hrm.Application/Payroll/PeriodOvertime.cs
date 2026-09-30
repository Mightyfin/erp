namespace Mightyfin.Erp.Hrm.Application.Payroll;

public sealed record PeriodOvertimeRow(Guid WorkerId, string EmployeeNo, string WorkerName, decimal? Amount, string Note);
public sealed record PeriodOvertimeBatch(Guid PayPeriodId, string PeriodLabel, bool Editable, string? LockReason, List<PeriodOvertimeRow> Rows);
public sealed record PeriodOvertimeSave(decimal? Amount, string? Note);
public interface IPeriodOvertimeService
{
    Task<PeriodOvertimeBatch> GetAsync(Guid periodId, CancellationToken ct);
    Task SaveAsync(Guid periodId, Guid workerId, PeriodOvertimeSave request, CancellationToken ct);
}
