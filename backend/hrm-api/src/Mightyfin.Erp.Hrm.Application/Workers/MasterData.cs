namespace Mightyfin.Erp.Hrm.Application.Workers;

public sealed record WorkerImportRow(
    string? EmployeeNo, string FirstName, string LastName,
    string? MiddleName = null, string? Email = null, string? Phone = null,
    string? Nrc = null, string? Tpin = null, string? NapsaNumber = null,
    string? NhimaNumber = null, string WorkerType = "employee",
    string? OrgUnitCode = null, string? LocationCode = null,
    string? Grade = null, string? JobTitle = null, string? StartDate = null);

public sealed record WorkerImportPreviewRequest(string FileName, List<WorkerImportRow> Rows);

public sealed record WorkerBulkChangeRow(
    string EmployeeNo,
    string? Email = null, string? Phone = null, string? Nrc = null,
    string? Tpin = null, string? NapsaNumber = null, string? NhimaNumber = null,
    string? OrgUnitCode = null, string? LocationCode = null,
    string? ManagerEmployeeNo = null, string? Grade = null,
    string? JobTitle = null, string? Status = null);

public sealed record WorkerBulkPreviewRequest(string EffectiveDate, List<WorkerBulkChangeRow> Rows);
public sealed record WorkerReactivateRequest(string Reason);

public sealed record MasterDataBatchError(int Row, string? EmployeeNo, string Field, string Message);
public sealed record MasterDataBatchSample(string EmployeeNo, string Action, string Before, string After);
public sealed record MasterDataBatchDto(
    Guid Id, string BatchType, string? FileName, string Status, string EffectiveDate,
    int RowCount, int ReadyCount, int UnchangedCount, int ErrorCount,
    string RequestedBySubjectId, string? AppliedBySubjectId,
    DateTimeOffset CreatedAt, DateTimeOffset? AppliedAt, DateTimeOffset? RolledBackAt,
    bool CanRollback, List<MasterDataBatchError> Errors, List<MasterDataBatchSample> Samples);

public interface IMasterDataService
{
    Task<MasterDataBatchDto> PreviewImportAsync(WorkerImportPreviewRequest request, string actorSubjectId, CancellationToken ct);
    Task<MasterDataBatchDto> PreviewBulkAsync(WorkerBulkPreviewRequest request, string actorSubjectId, CancellationToken ct);
    Task<MasterDataBatchDto> ApplyAsync(Guid id, string actorSubjectId, CancellationToken ct);
    Task<MasterDataBatchDto> RollbackAsync(Guid id, string actorSubjectId, CancellationToken ct);
    Task<MasterDataBatchDto> ReactivateAsync(Guid workerId, WorkerReactivateRequest request, string actorSubjectId, CancellationToken ct);
    Task<Paged<MasterDataBatchDto>> ListAsync(string? batchType, string? status, CancellationToken ct);
}
