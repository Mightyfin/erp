using System.Text.Json;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Time;

/// <summary>Leave requests, balances and attendance corrections. Balance is a
/// ledger sum (explainable), reservations happen on submit and are released on
/// approval/rejection/cancellation. Payroll cutoff warnings come from the
/// owning pay period.</summary>
public interface ITimeService
{
    Task<Paged<LeaveRequestDto>> ListLeaveAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<LeaveRequestDto> CreateLeaveAsync(LeaveRequestCreate request, CancellationToken ct);
    Task<List<LeaveBalanceDto>> GetBalancesAsync(Guid workerId, CancellationToken ct);
    Task<Paged<AttendanceCorrectionDto>> ListCorrectionsAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<AttendanceCorrectionDto> CreateCorrectionAsync(AttendanceCorrectionCreate request, CancellationToken ct);
}

public sealed record LeaveRequestDto(Guid Id, Guid WorkerId, string WorkerName, string LeaveTypeCode,
    string StartDate, string EndDate, decimal RequestedDays, string Status, bool BalanceReserved,
    bool CrossesCutoff, DateTimeOffset CreatedAt);
public sealed record AttendanceCorrectionDto(Guid Id, Guid WorkerId, string WorkerName, string WorkDate,
    string IssueType, string? ProposedClockIn, string? ProposedClockOut, string? ProposedStatus,
    string Reason, string Status, DateTimeOffset CreatedAt);

public sealed class TimeServiceImpl(ITimeRepository repo, IAuthzService authz, IWorkflowService workflow) : ITimeService
{
    public async Task<Paged<LeaveRequestDto>> ListLeaveAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        var (items, total) = await repo.ListLeaveRequestsAsync(workerId, status, ct);
        return new Paged<LeaveRequestDto>(items.Select(Map).ToList(), total, 1, 50);
    }

    public async Task<LeaveRequestDto> CreateLeaveAsync(LeaveRequestCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        var start = DateOnly.Parse(request.StartDate);
        var end = DateOnly.Parse(request.EndDate);
        if (end < start) throw new DomainException("leave-invalid-dates", "End date is before start date.");

        var ledger = await repo.GetBalancesAsync(request.WorkerId, request.LeaveTypeCode, ct);
        var available = ledger.Where(l => l.Reason != "request").Sum(l => l.Days)
                       - ledger.Where(l => l.Reason == "request" && l.ReferenceId is null).Sum(l => Math.Abs(l.Days));

        var requestedDays = (decimal)(end.DayNumber - start.DayNumber + 1);
        var balances = await repo.GetLeaveTypeAsync(request.LeaveTypeCode, ct);
        if (balances is null) throw new DomainException("leave-type-not-found", $"Leave type {request.LeaveTypeCode} is not configured.");
        if (!balances.AllowNegative && requestedDays > available + 0.0001m)
            throw new DomainException("leave-insufficient-balance",
                $"Available balance ({available:F1} days) is less than requested ({requestedDays:F1} days).");

        // payroll cutoff warning: does the absence cross the current period cutoff?
        var cutoff = await repo.GetCurrentCutoffAsync(ct);
        var crosses = cutoff.HasValue && start <= cutoff.Value && end >= cutoff.Value;

        var lr = new LeaveRequest
        {
            WorkerId = request.WorkerId,
            LeaveTypeCode = request.LeaveTypeCode,
            StartDate = start, EndDate = end,
            IsPartialDay = request.IsPartialDay,
            StartTime = request.StartTime, EndTime = request.EndTime,
            RequestedDays = requestedDays,
            EvidenceAttached = request.EvidenceAttached,
            CreatedForPeriod = cutoff ?? start,
            CrossesCutoff = crosses,
            Status = "submitted",
        };
        var created = await repo.CreateLeaveRequestAsync(lr, ct);

        // reserve balance and open approval workflow
        await repo.ReserveBalanceAsync(request.WorkerId, request.LeaveTypeCode, -requestedDays, created.Id, ct);
        await workflow.OpenAsync("leave", created.Id, created.WorkerId, JsonSerializer.Serialize(new { created.LeaveTypeCode, created.StartDate, created.EndDate, created.RequestedDays }), ct);
        return Map(created);
    }

    public async Task<List<LeaveBalanceDto>> GetBalancesAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager", "payroll");
        var ledger = await repo.GetLedgerAsync(workerId, ct);
        var types = await repo.GetLeaveTypesAsync(ct);
        return types.Select(t =>
        {
            var rows = ledger.Where(l => l.LeaveTypeCode == t.Code).ToList();
            var accrued = rows.Where(r => r.Days > 0 && r.Reason != "request").Sum(r => r.Days);
            var taken = -rows.Where(r => r.Days < 0).Sum(r => r.Days);
            var reserved = -rows.Where(r => r.Days < 0 && r.Reason == "request").Sum(r => r.Days);
            var expired = -rows.Where(r => r.Days < 0 && r.Reason == "forfeiture").Sum(r => r.Days);
            return new LeaveBalanceDto(t.Code, t.Name, accrued, taken, reserved, expired, accrued - taken - reserved - expired);
        }).ToList();
    }

    public async Task<Paged<AttendanceCorrectionDto>> ListCorrectionsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        var (items, total) = await repo.ListCorrectionsAsync(workerId, status, ct);
        return new Paged<AttendanceCorrectionDto>(items.Select(c => new AttendanceCorrectionDto(
            c.Id, c.WorkerId, c.Worker?.FullName ?? "", c.WorkDate.ToString(), c.IssueType,
            c.ProposedClockIn?.ToString(), c.ProposedClockOut?.ToString(), c.ProposedStatus,
            c.Reason, c.Status, c.CreatedAt)).ToList(), total, 1, 50);
    }

    public async Task<AttendanceCorrectionDto> CreateCorrectionAsync(AttendanceCorrectionCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        var c = new AttendanceCorrection
        {
            WorkerId = request.WorkerId,
            WorkDate = DateOnly.Parse(request.WorkDate),
            IssueType = request.IssueType,
            ProposedClockIn = request.ProposedClockIn is null ? null : TimeOnly.Parse(request.ProposedClockIn),
            ProposedClockOut = request.ProposedClockOut is null ? null : TimeOnly.Parse(request.ProposedClockOut),
            ProposedStatus = request.ProposedStatus,
            Reason = request.Reason,
            Status = "submitted",
        };
        var created = await repo.CreateCorrectionAsync(c, ct);
        await workflow.OpenAsync("attendance-correction", created.Id, created.WorkerId,
            JsonSerializer.Serialize(new { created.WorkDate, created.IssueType, created.ProposedClockIn, created.ProposedClockOut, created.ProposedStatus }), ct);
        return new AttendanceCorrectionDto(created.Id, created.WorkerId, created.Worker?.FullName ?? "",
            created.WorkDate.ToString(), created.IssueType, created.ProposedClockIn?.ToString(),
            created.ProposedClockOut?.ToString(), created.ProposedStatus, created.Reason, created.Status, created.CreatedAt);
    }

    private static LeaveRequestDto Map(LeaveRequest r) => new(
        r.Id, r.WorkerId, r.Worker?.FullName ?? "", r.LeaveTypeCode, r.StartDate.ToString(),
        r.EndDate.ToString(), r.RequestedDays, r.Status, r.BalanceReserved, r.CrossesCutoff, r.CreatedAt);
}
