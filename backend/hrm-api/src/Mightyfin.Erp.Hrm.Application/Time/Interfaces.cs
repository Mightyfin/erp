using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Time;

public interface ITimeRepository
{
    Task<(List<LeaveRequest> Items, int Total)> ListLeaveRequestsAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<LeaveRequest> CreateLeaveRequestAsync(LeaveRequest request, CancellationToken ct);
    Task<List<LeaveBalanceLedger>> GetBalancesAsync(Guid workerId, string leaveTypeCode, CancellationToken ct);
    Task<List<LeaveBalanceLedger>> GetLedgerAsync(Guid workerId, CancellationToken ct);
    Task<LeaveType?> GetLeaveTypeAsync(string code, CancellationToken ct);
    Task<List<LeaveType>> GetLeaveTypesAsync(CancellationToken ct);
    Task<DateOnly?> GetCurrentCutoffAsync(CancellationToken ct);
    Task ReserveBalanceAsync(Guid workerId, string leaveTypeCode, decimal days, Guid referenceId, CancellationToken ct);
    Task<(List<AttendanceCorrection> Items, int Total)> ListCorrectionsAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<AttendanceCorrection> CreateCorrectionAsync(AttendanceCorrection correction, CancellationToken ct);
}

/// <summary>Workflow/approval engine interface: every approvable action opens a
/// WorkflowRequest; decisions transition it through the state machine.</summary>
public interface IWorkflowService
{
    Task<WorkflowRequest> OpenAsync(string workflowType, Guid subjectId, Guid? subjectWorkerId, string payloadJson, CancellationToken ct);
    Task<WorkflowRequest> DecideAsync(Guid requestId, Guid actorId, WorkflowDecisionRequest decision, CancellationToken ct);
    Task<Paged<WorkQueueItemDto>> GetWorkQueueAsync(CancellationToken ct);
    Task<WorkflowRequest?> GetByIdAsync(Guid id, CancellationToken ct);
    Task ApplyDecisionEffectsAsync(WorkflowRequest request, CancellationToken ct);
}
