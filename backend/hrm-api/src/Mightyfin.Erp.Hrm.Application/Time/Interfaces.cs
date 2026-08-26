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
    // M3 attendance, roster, decisions
    Task<AttendanceRecord?> GetAttendanceAsync(Guid workerId, DateOnly workDate, CancellationToken ct);
    Task<AttendanceRecord> CreateAttendanceAsync(AttendanceRecord record, CancellationToken ct);
    Task<AttendanceRecord> UpdateAttendanceAsync(AttendanceRecord record, CancellationToken ct);
    Task<List<AttendanceRecord>> ListAttendanceAsync(Guid workerId, DateOnly? from, DateOnly? to, CancellationToken ct);
    Task<List<AttendanceRecord>> ListAttendanceForWorkerRangeAsync(Guid workerId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<List<AttendanceRecord>> ListAttendanceForScopeAsync(DateOnly? from, DateOnly? to, Guid? locationId, Guid? orgUnitId, CancellationToken ct);
    Task<List<AttendanceRecord>> ListOvertimeAsync(Guid? workerId, DateOnly? from, DateOnly? to, string? status, CancellationToken ct);
    Task<AttendanceCorrection?> GetCorrectionAsync(Guid id, CancellationToken ct);
    Task<AttendanceCorrection> UpdateCorrectionAsync(AttendanceCorrection correction, CancellationToken ct);
    Task<LeaveRequest?> GetLeaveRequestAsync(Guid id, CancellationToken ct);
    Task<LeaveRequest> UpdateLeaveRequestAsync(LeaveRequest request, CancellationToken ct);
    Task ReleaseReservationAsync(Guid leaveRequestId, CancellationToken ct);
    Task ConvertReservationAsync(Guid leaveRequestId, CancellationToken ct);
    Task<List<WorkCalendar>> ListCalendarsAsync(CancellationToken ct);
    Task<List<ShiftDefinition>> ListShiftsAsync(CancellationToken ct);
    Task<ShiftDefinition> CreateShiftAsync(ShiftDefinition shift, CancellationToken ct);
    Task<ShiftDefinition?> GetShiftAsync(Guid id, CancellationToken ct);
    Task<ShiftDefinition> UpdateShiftAsync(ShiftDefinition shift, CancellationToken ct);
    Task<WorkerShiftAssignment?> GetShiftAssignmentAsync(Guid workerId, DateOnly date, CancellationToken ct);
    Task<WorkerShiftAssignment> CreateShiftAssignmentAsync(WorkerShiftAssignment assignment, CancellationToken ct);
    Task CloseOpenShiftAssignmentsAsync(Guid workerId, DateOnly effectiveTo, CancellationToken ct);
    Task<Worker?> FindWorkerByEmployeeNoAsync(string employeeNo, CancellationToken ct);
    Task<AttendanceImportBatch> CreateImportBatchAsync(AttendanceImportBatch batch, CancellationToken ct);
    Task UpdateImportBatchAsync(AttendanceImportBatch batch, CancellationToken ct);
    Task<List<AttendanceImportBatch>> ListImportBatchesAsync(CancellationToken ct);
    Task<LeaveAccrualRun?> GetAccrualRunAsync(string period, CancellationToken ct);
    Task<LeaveAccrualRun> CreateAccrualRunAsync(LeaveAccrualRun run, CancellationToken ct);
    Task UpdateAccrualRunAsync(LeaveAccrualRun run, CancellationToken ct);
    Task<List<LeaveAccrualRun>> ListAccrualRunsAsync(CancellationToken ct);
    Task<List<Worker>> ListAccrualWorkersAsync(CancellationToken ct);
    Task<LeaveBalanceLedger> AddLedgerEntryAsync(LeaveBalanceLedger entry, CancellationToken ct);
    Task<LeaveBalanceAdjustment> CreateAdjustmentAsync(LeaveBalanceAdjustment adjustment, CancellationToken ct);
    Task<List<LeaveBalanceAdjustment>> ListAdjustmentsAsync(CancellationToken ct);
    // M41 Gap 6a: leave encashment
    Task<(List<LeaveEncashmentRequest> Items, int Total)> ListEncashmentsAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<LeaveEncashmentRequest?> GetEncashmentAsync(Guid id, CancellationToken ct);
    Task<LeaveEncashmentRequest> CreateEncashmentAsync(LeaveEncashmentRequest request, CancellationToken ct);
    Task<LeaveEncashmentRequest> UpdateEncashmentAsync(LeaveEncashmentRequest request, CancellationToken ct);
}

/// <summary>Workflow/approval engine interface: every approvable action opens a
/// WorkflowRequest; decisions transition it through the state machine.</summary>
public interface IWorkflowService
{
    Task<WorkflowRequest> OpenAsync(string workflowType, Guid subjectId, Guid? subjectWorkerId, string payloadJson, CancellationToken ct);
    Task<WorkflowRequest> DecideAsync(Guid requestId, Guid actorId, WorkflowDecisionRequest decision, CancellationToken ct);
    Task<WorkflowRequest> EscalateAsync(Guid requestId, Guid actorId, CancellationToken ct);
    // M16: the submitter cancels their own request. Transitions through the
    // state machine (cancelled is a legal terminal state) and records a
    // decision row so the trail stays complete.
    Task<WorkflowRequest> CancelAsync(Guid requestId, CancellationToken ct);
    Task<WorkflowRequest?> GetOpenBySubjectAsync(string workflowType, Guid subjectWorkerId, CancellationToken ct);
    Task<Paged<WorkQueueItemDto>> GetWorkQueueAsync(CancellationToken ct);
    Task<WorkflowRequestDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task ApplyDecisionEffectsAsync(WorkflowRequest request, CancellationToken ct);
    Task<EscalationRunDto> EscalateOverdueAsync(CancellationToken ct);
}
