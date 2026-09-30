using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;
public sealed class RecordedLeaveService(HrmDbContext db, IAuthzService authz, ShellContext scope) : IRecordedLeaveService
{
    const string Source = "admin-recorded-leave";
    private void Authorize() => authz.RequireAnyRole("hr_admin", "hr_ops");
    private IQueryable<Worker> Workers() => db.Workers.Where(w => !w.IsArchived
        && (!scope.LocationId.HasValue || w.LocationId == scope.LocationId)
        && (!scope.OrgUnitId.HasValue || w.OrgUnitId == scope.OrgUnitId)
        && (!scope.IsConfined || (w.LocationId.HasValue && scope.AllowedLocationIds.Contains(w.LocationId.Value))));
    private async Task<Worker> Worker(Guid id, CancellationToken ct) => await Workers().FirstOrDefaultAsync(w => w.Id == id, ct)
        ?? throw new DomainException("forbidden", "Employee is unavailable or outside your HR access.");
    private static (DateOnly Start, DateOnly End) Dates(RecordedLeaveInput input)
    {
        if (!DateOnly.TryParseExact(input.StartDate, "yyyy-MM-dd", out var start)
            || !DateOnly.TryParseExact(input.EndDate, "yyyy-MM-dd", out var end) || end < start || end.DayNumber - start.DayNumber > 366)
            throw new DomainException("leave-invalid-dates", "Choose valid dates covering no more than 367 calendar days.");
        if (start > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new DomainException("leave-future-start", "Record leave that has already started. Use the request workflow for future leave.");
        return (start, end);
    }
    public async Task<List<RecordedLeaveRow>> ListAsync(CancellationToken ct)
    {
        Authorize();
        var workers = Workers();
        var rows = await (from l in db.LeaveRequests where !l.IsArchived
            join w in workers on l.WorkerId equals w.Id
            join b in db.LeaveBalanceLedgers.Where(b => !b.IsArchived && b.ReferenceType == Source) on l.Id equals b.ReferenceId
            select new { Leave = l, Worker = w, Ledger = b }).ToListAsync(ct);
        return rows.OrderByDescending(r => r.Leave.StartDate).ThenByDescending(r => r.Leave.CreatedAt).Select(r => Map(r.Leave, r.Worker, r.Ledger)).ToList();
    }
    private static RecordedLeaveRow Map(LeaveRequest l, Worker w, LeaveBalanceLedger b) => new(l.Id, w.Id, w.EmployeeNo, w.FullName,
        l.LeaveTypeCode, l.StartDate.ToString("yyyy-MM-dd"), l.EndDate.ToString("yyyy-MM-dd"), l.RequestedDays, l.Status, b.Note, l.EvidenceAttached);
    public async Task<RecordedLeaveQuote> QuoteAsync(RecordedLeaveInput input, CancellationToken ct)
    {
        Authorize(); var worker = await Worker(input.WorkerId, ct); var (start, end) = Dates(input);
        if ((worker.StartDate.HasValue && start < worker.StartDate) || (worker.EndDate.HasValue && end > worker.EndDate))
            throw new DomainException("leave-outside-employment", "Leave must fall within the employee's employment dates.");
        var type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Code == input.LeaveTypeCode && !t.IsArchived && t.IsActive, ct)
            ?? throw new DomainException("leave-type-not-found", "Select an active leave type. Configure a missing type before recording leave.");
        if (start < type.EffectiveFrom || (type.EffectiveTo.HasValue && end > type.EffectiveTo))
            throw new DomainException("leave-type-dates", "This leave type is not effective for the selected dates.");
        var calendars = await db.WorkCalendars.Include(c => c.Holidays).Where(c => !c.IsArchived).ToListAsync(ct);
        var assignments = await db.WorkerShiftAssignments.Where(a => a.WorkerId == worker.Id && !a.IsArchived && a.EffectiveFrom <= end && (a.EffectiveTo == null || a.EffectiveTo >= start)).ToListAsync(ct);
        var branchCalendar = await db.WorkLocations.Where(l => l.Id == worker.LocationId && !l.IsArchived).Select(l => l.DefaultCalendarId).FirstOrDefaultAsync(ct);
        var excluded = new List<string>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var assignment = assignments.Where(a => a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date)).OrderByDescending(a => a.EffectiveFrom).FirstOrDefault();
            var calendar = calendars.FirstOrDefault(c => c.Id == assignment?.CalendarId) ?? calendars.FirstOrDefault(c => c.Id == branchCalendar) ?? calendars.FirstOrDefault(c => c.IsDefault)
                ?? throw new DomainException("leave-calendar-missing", "Configure a work calendar before recording leave.");
            var day = date.DayOfWeek.ToString()[..3];
            var weekend = calendar.WeekendDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(day, StringComparer.OrdinalIgnoreCase);
            var holiday = calendar.Holidays.Where(h => !h.IsArchived).Any(h =>
            {
                var observed = DateOnly.TryParse(h.ObservedOn, out var d) ? d : h.HolidayDate;
                return observed == date || (h.IsRecurring && observed.Month == date.Month && observed.Day == date.Day);
            });
            if (weekend || holiday) excluded.Add(date.ToString("yyyy-MM-dd"));
        }
        decimal days = end.DayNumber - start.DayNumber + 1 - excluded.Count;
        if (days <= 0) throw new DomainException("leave-no-working-days", "The selected dates contain no working days.");
        if (days > type.MaxConsecutiveDays) throw new DomainException("leave-too-long", "The selected dates exceed this leave type's maximum consecutive days.");
        var available = await db.LeaveBalanceLedgers.Where(b => !b.IsArchived && b.WorkerId == worker.Id && b.LeaveTypeCode == type.Code).SumAsync(b => b.Days, ct);
        return new(days, available, excluded);
    }
    private async Task Unlocked(Guid worker, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var locationId = await db.Workers.Where(w => w.Id == worker).Select(w => w.LocationId).FirstAsync(ct);
        var groups = db.WorkerPayrollProfiles.Where(p => p.WorkerId == worker && !p.IsArchived).Select(p => p.PayGroupId);
        var blocked = await db.PayPeriods.AnyAsync(p => !p.IsArchived && p.StartDate <= end && p.EndDate >= start
            && groups.Contains(p.PayGroupId) && (p.Status != "open"
                || db.PayrollRuns.Any(r => !r.IsArchived && r.PayPeriodId == p.Id
                    && (r.LocationId == null || r.LocationId == locationId)
                    && r.Status != "draft" && r.Status != "locked" && r.Status != "calculated"
                    && r.Status != "reversed" && r.Status != "cancelled" && r.Status != "void")), ct);
        if (blocked)
            throw new DomainException("leave-payroll-locked", "Leave cannot change after payroll enters review or is released, or when the pay period is closed. Use payroll corrections for a finalized period.");
    }
    private async Task RequireRecalculation(Guid worker, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var locationId = await db.Workers.Where(w => w.Id == worker).Select(w => w.LocationId).FirstAsync(ct);
        var groups = db.WorkerPayrollProfiles.Where(p => p.WorkerId == worker && !p.IsArchived).Select(p => p.PayGroupId);
        var periodIds = db.PayPeriods.Where(p => !p.IsArchived && p.StartDate <= end && p.EndDate >= start && groups.Contains(p.PayGroupId)).Select(p => p.Id);
        var runs = await db.PayrollRuns.Where(r => !r.IsArchived && periodIds.Contains(r.PayPeriodId)
            && (r.LocationId == null || r.LocationId == locationId) && r.Status == "calculated").ToListAsync(ct);
        foreach (var run in runs)
        {
            run.Status = "locked";
            db.PayrollRunEvents.Add(new PayrollRunEvent { RunId = run.Id, Action = "leave-inputs-updated",
                ActorSubjectId = authz.CurrentSubjectId ?? "system", FromStatus = "calculated", ToStatus = "locked",
                Reason = "Recorded leave changed. Calculate payroll again before approval.",
                DetailsJson = JsonSerializer.Serialize(new { WorkerId = worker, StartDate = start, EndDate = end }) });
        }
    }
    private async Task<(LeaveRequest Leave, LeaveBalanceLedger Ledger)> Existing(Guid id, CancellationToken ct)
    {
        var l = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id && !l.IsArchived, ct)
            ?? throw new DomainException("leave-not-found", "Recorded leave not found.");
        await Worker(l.WorkerId, ct);
        var b = await db.LeaveBalanceLedgers.SingleOrDefaultAsync(b => b.ReferenceId == id && b.ReferenceType == Source && !b.IsArchived, ct)
            ?? throw new DomainException("leave-workflow-record", "Manage employee requests through their original approval workflow.");
        if (l.Status != "approved") throw new DomainException("leave-not-editable", "This record has already been cancelled.");
        await Unlocked(l.WorkerId, l.StartDate, l.EndDate, ct);
        return (l, b);
    }
    private void Audit(string action, LeaveRequest l, object? before, object after) => db.AuditEntries.Add(new AuditEntry {
        EntityType = "time.recorded-leave", EntityId = l.Id.ToString(), Action = action,
        ActorSubjectId = authz.CurrentSubjectId ?? "system", BeforeJson = before == null ? null : JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(after) });
    public async Task<RecordedLeaveRow> SaveAsync(Guid? id, RecordedLeaveInput input, CancellationToken ct)
    {
        Authorize();
        if (string.IsNullOrWhiteSpace(input.Note) || input.Note.Length > 1000)
            throw new DomainException("leave-note-required", "Enter an approval reference or reason for recording/correcting this leave (maximum 1,000 characters).");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var quote = await QuoteAsync(input, ct); var worker = await Worker(input.WorkerId, ct); var (start, end) = Dates(input);
        LeaveRequest l; LeaveBalanceLedger ledger; RecordedLeaveRow? before = null;
        if (id.HasValue) { (l, ledger) = await Existing(id.Value, ct); before = Map(l, worker, ledger);
            if (l.WorkerId != input.WorkerId) throw new DomainException("leave-worker-change", "Cancel and create a new record to change the employee."); }
        else { l = new LeaveRequest { WorkerId = worker.Id }; ledger = new LeaveBalanceLedger { WorkerId = worker.Id, ReferenceId = l.Id, ReferenceType = Source, Reason = "taken" }; db.LeaveRequests.Add(l); db.LeaveBalanceLedgers.Add(ledger); }
        await Unlocked(worker.Id, start, end, ct);
        if (await db.LeaveRequests.AnyAsync(r => !r.IsArchived && r.WorkerId == worker.Id && r.Id != l.Id && r.Status != "cancelled" && r.Status != "rejected" && r.EndDate >= start && r.StartDate <= end, ct))
            throw new DomainException("leave-overlap", "An existing leave request overlaps these dates. Review it before recording another entry.");
        var type = await db.LeaveTypes.FirstAsync(t => t.Code == input.LeaveTypeCode && !t.IsArchived && t.IsActive, ct);
        var available = quote.Available + (id.HasValue && ledger.LeaveTypeCode == input.LeaveTypeCode ? -ledger.Days : 0);
        if (!type.AllowNegative && quote.Days > available) throw new DomainException("leave-insufficient-balance", $"Available balance is {available:0.##} days; this entry needs {quote.Days:0.##}. HR must allocate the balance first.");
        if (type.RequiresEvidence && !input.EvidenceAttached) throw new DomainException("leave-evidence-required", "Confirm that the required supporting evidence is on file.");
        l.LeaveTypeCode = input.LeaveTypeCode; l.StartDate = start; l.EndDate = end; l.RequestedDays = quote.Days; l.Status = "approved";
        l.BalanceReserved = true; l.LocationId = worker.LocationId; l.CreatedForPeriod = start; l.EvidenceAttached = input.EvidenceAttached;
        ledger.LeaveTypeCode = input.LeaveTypeCode; ledger.Days = -quote.Days; ledger.ForDate = start; ledger.Note = input.Note.Trim();
        await RequireRecalculation(worker.Id, start, end, ct);
        if (before != null) await RequireRecalculation(worker.Id, DateOnly.Parse(before.StartDate), DateOnly.Parse(before.EndDate), ct);
        var result = Map(l, worker, ledger); Audit(id.HasValue ? "correct-recorded-leave" : "record-approved-leave", l, before, result);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }
    public async Task CancelAsync(Guid id, string reason, CancellationToken ct)
    {
        Authorize(); if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new DomainException("leave-reason-required", "Enter a cancellation reason (maximum 1,000 characters).");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var (l, ledger) = await Existing(id, ct); var worker = await Worker(l.WorkerId, ct); var before = Map(l, worker, ledger);
        l.Status = "cancelled"; l.BalanceReserved = false; l.RejectionReason = reason.Trim(); ledger.Days = 0;
        await RequireRecalculation(worker.Id, l.StartDate, l.EndDate, ct);
        Audit("cancel-recorded-leave", l, before, new { Record = Map(l, worker, ledger), Reason = reason.Trim() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
