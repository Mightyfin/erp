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

    // M3: attendance, roster and decisions
    Task<PunchResultDto> ClockInAsync(Guid workerId, CancellationToken ct);
    Task<PunchResultDto> ClockOutAsync(Guid workerId, CancellationToken ct);
    Task<PunchResultDto> GetTodayAsync(Guid workerId, CancellationToken ct);
    Task<List<AttendanceRecordDto>> ListAttendanceAsync(Guid workerId, string? from, string? to, CancellationToken ct);
    Task<List<RosterDayDto>> GetRosterAsync(Guid workerId, string? from, string? to, CancellationToken ct);
    Task<AttendanceCorrectionDto> DecideCorrectionAsync(Guid id, TimeDecisionRequest request, CancellationToken ct);
    Task<LeaveRequestDto> DecideLeaveAsync(Guid id, TimeDecisionRequest request, CancellationToken ct);
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
            // releases (reason == "request-release") just undo open reservations and
            // must not be counted as new accruals
            var accrued = rows.Where(r => r.Days > 0 && r.Reason != "request" && r.Reason != "request-release").Sum(r => r.Days);
            // taken is permanent deductions only: open reservations (reason == "request")
            // and forfeitures are reported separately so available arithmetic stays clean
            var taken = -rows.Where(r => r.Days < 0 && r.Reason != "request" && r.Reason != "forfeiture").Sum(r => r.Days);
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

    // ===================== M3: attendance, roster, decisions =====================

    public async Task<PunchResultDto> ClockInAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        return await GetOrCreatePunchAsync(workerId, DateOnly.FromDateTime(DateTime.UtcNow), clockIn: true, ct);
    }

    public async Task<PunchResultDto> ClockOutAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        return await GetOrCreatePunchAsync(workerId, DateOnly.FromDateTime(DateTime.UtcNow), clockIn: false, ct);
    }

    public async Task<PunchResultDto> GetTodayAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager", "payroll");
        return await GetOrCreatePunchAsync(workerId, DateOnly.FromDateTime(DateTime.UtcNow), clockIn: null, ct);
    }

    public async Task<List<AttendanceRecordDto>> ListAttendanceAsync(Guid workerId, string? from, string? to, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee", "payroll");
        var f = from is null ? (DateOnly?)null : DateOnly.Parse(from);
        var t = to is null ? (DateOnly?)null : DateOnly.Parse(to);
        var items = await repo.ListAttendanceAsync(workerId, f, t, ct);
        return items.Select(MapAttendance).ToList();
    }

    public async Task<List<RosterDayDto>> GetRosterAsync(Guid workerId, string? from, string? to, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager", "payroll");
        var start = from is null ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)) : DateOnly.Parse(from);
        var end = to is null ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)) : DateOnly.Parse(to);
        if (end < start) throw new DomainException("roster-invalid-dates", "End date is before start date.");

        var calendars = await repo.ListCalendarsAsync(ct);
        var calendar = calendars.FirstOrDefault(c => c.IsDefault) ?? calendars.FirstOrDefault();
        var holidays = calendar?.Holidays?.ToList() ?? new List<PublicHoliday>();

        // holiday dates observed on the day (ignoring IsRecurring year matching for simplicity of current year)
        var holidayByDate = holidays.GroupBy(h => h.ObservedOn is not null && DateOnly.TryParse(h.ObservedOn, out _) ? DateOnly.Parse(h.ObservedOn) : h.HolidayDate)
            .ToDictionary(g => g.Key, g => g.First());

        var attendance = (await repo.ListAttendanceAsync(workerId, start, end, ct)).ToDictionary(a => a.WorkDate, a => a);
        var corrections = (await repo.ListCorrectionsAsync(workerId, null, ct)).Items
            .Where(c => c.WorkDate >= start && c.WorkDate <= end)
            .GroupBy(c => c.WorkDate).ToDictionary(g => g.Key, g => g.First());

        var cutoff = await repo.GetCurrentCutoffAsync(ct);

        var days = new List<RosterDayDto>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var isWeekend = calendar is not null && calendar.WeekendDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(w => string.Equals(w, d.DayOfWeek.ToString().ToLowerInvariant().Substring(0, 3), StringComparison.OrdinalIgnoreCase));
            holidayByDate.TryGetValue(d, out var hol);
            var isHoliday = hol is not null;
            var att = attendance.GetValueOrDefault(d);
            var cor = corrections.GetValueOrDefault(d);

            string? status = null;
            if (att is not null) status = att.DerivedStatus;
            else if (d <= DateOnly.FromDateTime(DateTime.UtcNow) && !isWeekend && !isHoliday) status = "missing-punch";

            days.Add(new RosterDayDto(d.ToString(), d.DayOfWeek.ToString("d"), !isWeekend && !isHoliday,
                att?.ClockIn?.ToString(), att?.ClockOut?.ToString(), status,
                null, null, null, calendar?.Name, isHoliday, hol?.Name,
                cutoff?.ToString(), cor?.Status));
        }
        return days;
    }

    public async Task<AttendanceCorrectionDto> DecideCorrectionAsync(Guid id, TimeDecisionRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager");
        var c = await repo.GetCorrectionAsync(id, ct) ?? throw new DomainException("correction-not-found", $"Attendance correction {id} does not exist.");
        if (c.Status != "submitted" && c.Status != "in-review")
            throw new DomainException("correction-not-reviewable", $"Correction is {c.Status} and cannot be decided.");

        switch (request.Action.ToLowerInvariant())
        {
            case "approve":
                c.Status = "approved";
                // apply the proposed values to the underlying attendance record (or create one)
                var existing = await repo.GetAttendanceAsync(c.WorkerId, c.WorkDate, ct);
                if (existing is null)
                {
                    await repo.CreateAttendanceAsync(new AttendanceRecord
                    {
                        WorkerId = c.WorkerId,
                        WorkDate = c.WorkDate,
                        ClockIn = c.ProposedClockIn,
                        ClockOut = c.ProposedClockOut,
                        Source = "corrected",
                        DerivedStatus = DeriveStatus(c.ProposedClockIn, c.ProposedClockOut, c.ProposedStatus),
                        TotalHours = c.ProposedClockIn.HasValue && c.ProposedClockOut.HasValue
                            ? (decimal)(c.ProposedClockOut.Value - c.ProposedClockIn.Value).TotalHours : 0,
                    }, ct);
                }
                else
                {
                    if (c.ProposedClockIn.HasValue) existing.ClockIn = c.ProposedClockIn;
                    if (c.ProposedClockOut.HasValue) existing.ClockOut = c.ProposedClockOut;
                    if (c.ProposedStatus is not null) existing.DerivedStatus = c.ProposedStatus;
                    existing.Source = "corrected";
                    if (existing.ClockIn.HasValue && existing.ClockOut.HasValue)
                        existing.TotalHours = (decimal)(existing.ClockOut.Value - existing.ClockIn.Value).TotalHours;
                    await repo.UpdateAttendanceAsync(existing, ct);
                }
                break;
            case "return":
                c.Status = "returned";
                c.RejectionReason = request.Reason;
                break;
            case "reject":
                c.Status = "rejected";
                c.RejectionReason = request.Reason;
                break;
            default:
                throw new DomainException("correction-invalid-decision",
                    "Decision action must be approve, return or reject.");
        }
        var decided = await repo.UpdateCorrectionAsync(c, ct);
        return new AttendanceCorrectionDto(decided.Id, decided.WorkerId, decided.Worker?.FullName ?? "",
            decided.WorkDate.ToString(), decided.IssueType, decided.ProposedClockIn?.ToString(),
            decided.ProposedClockOut?.ToString(), decided.ProposedStatus, decided.Reason,
            decided.Status, decided.CreatedAt);
    }

    public async Task<LeaveRequestDto> DecideLeaveAsync(Guid id, TimeDecisionRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager");
        var lr = await repo.GetLeaveRequestAsync(id, ct) ?? throw new DomainException("leave-not-found", $"Leave request {id} does not exist.");
        if (lr.Status != "submitted" && lr.Status != "in-review")
            throw new DomainException("leave-not-reviewable", $"Leave request is {lr.Status} and cannot be decided.");

        switch (request.Action.ToLowerInvariant())
        {
            case "approve":
                lr.Status = "approved";
                lr.BalanceReserved = true;
                // convert the open reservation into a permanent (taken) ledger deduction
                await repo.ConvertReservationAsync(lr.Id, ct);
                break;
            case "return":
                lr.Status = "returned";
                lr.RejectionReason = request.Reason;
                await repo.ReleaseReservationAsync(lr.Id, ct);
                break;
            case "reject":
                lr.Status = "rejected";
                lr.RejectionReason = request.Reason;
                await repo.ReleaseReservationAsync(lr.Id, ct);
                break;
            default:
                throw new DomainException("leave-invalid-decision",
                    "Decision action must be approve, return or reject.");
        }
        return Map(await repo.UpdateLeaveRequestAsync(lr, ct));
    }

    private async Task<PunchResultDto> GetOrCreatePunchAsync(Guid workerId, DateOnly date, bool? clockIn, CancellationToken ct)
    {
        var rec = await repo.GetAttendanceAsync(workerId, date, ct);
        if (rec is null)
        {
            rec = new AttendanceRecord { WorkerId = workerId, WorkDate = date, Source = "self-service" };
            rec = await repo.CreateAttendanceAsync(rec, ct);
        }

        var now = TimeOnly.FromDateTime(DateTime.UtcNow);
        if (clockIn == true && rec.ClockIn is null)
            rec.ClockIn = now;
        if (clockIn == false && rec.ClockOut is null)
            rec.ClockOut = now;

        if (rec.ClockIn.HasValue && rec.ClockOut.HasValue)
            rec.TotalHours = (decimal)(rec.ClockOut.Value - rec.ClockIn.Value).TotalHours;
        rec.DerivedStatus = DeriveStatus(rec.ClockIn, rec.ClockOut, rec.DerivedStatus);

        rec = await repo.UpdateAttendanceAsync(rec, ct);
        var state = rec.ClockIn is null ? "out" : rec.ClockOut is null ? "in" : "done";
        return new PunchResultDto(rec.Id, rec.WorkerId, rec.WorkDate.ToString(),
            rec.ClockIn?.ToString() ?? "", rec.ClockOut?.ToString() ?? "",
            rec.Source, rec.DerivedStatus, rec.TotalHours, state);
    }

    private static string DeriveStatus(TimeOnly? clockIn, TimeOnly? clockOut, string? current)
    {
        if (clockIn.HasValue && clockOut.HasValue) return "present";
        if (clockIn is null && clockOut is null) return "unknown";
        return current is "late" or "early-departure" or "half-day" or "absent" ? current : "present";
    }

    private static LeaveRequestDto Map(LeaveRequest r) => new(
        r.Id, r.WorkerId, r.Worker?.FullName ?? "", r.LeaveTypeCode, r.StartDate.ToString(),
        r.EndDate.ToString(), r.RequestedDays, r.Status, r.BalanceReserved, r.CrossesCutoff, r.CreatedAt);

    private static AttendanceRecordDto MapAttendance(AttendanceRecord a) => new(
        a.Id, a.WorkerId, a.Worker?.FullName ?? "", a.WorkDate.ToString(),
        a.ClockIn?.ToString(), a.ClockOut?.ToString(), a.Source, a.DerivedStatus, a.TotalHours);
}
