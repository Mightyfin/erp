namespace Mightyfin.Erp.Hrm.Application.Time;
public sealed record RecordedLeaveInput(Guid WorkerId, string LeaveTypeCode, string StartDate, string EndDate, string Note, bool EvidenceAttached = false);
public sealed record RecordedLeaveRow(Guid Id, Guid WorkerId, string EmployeeNo, string WorkerName, string LeaveTypeCode, string StartDate, string EndDate, decimal Days, string Status, string Note, bool EvidenceAttached);
public sealed record RecordedLeaveQuote(decimal Days, decimal Available, List<string> ExcludedDates);
public interface IRecordedLeaveService
{
    Task<List<RecordedLeaveRow>> ListAsync(CancellationToken ct);
    Task<RecordedLeaveQuote> QuoteAsync(RecordedLeaveInput input, CancellationToken ct);
    Task<RecordedLeaveRow> SaveAsync(Guid? id, RecordedLeaveInput input, CancellationToken ct);
    Task CancelAsync(Guid id, string reason, CancellationToken ct);
}
