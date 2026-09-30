using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

public sealed class PeriodOvertimeService(HrmDbContext db, IAuthzService authz, ShellContext scope) : IPeriodOvertimeService
{
    private async Task<(PayPeriod Period, string? Lock)> CheckAsync(Guid periodId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "payroll");
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == periodId && !p.IsArchived, ct)
            ?? throw new DomainException("pay-period-not-found", "Pay period not found.");
        var locked = period.Status != "open" || await db.PayrollRuns.AnyAsync(r =>
            !r.IsArchived && r.PayPeriodId == periodId && r.Status != "draft" && r.Status != "reversed" && r.Status != "cancelled", ct);
        return (period, locked ? "Overtime is read-only because this period is closed or payroll inputs have been locked. Cancel an unreleased run before changing its inputs." : null);
    }

    private IQueryable<Worker> Eligible(PayPeriod period) => db.Workers.Where(w =>
        !w.IsArchived && (w.Status == "active" || w.Status == "on-leave" || w.Status == "notice") && (w.StartDate == null || w.StartDate <= period.EndDate) && (w.EndDate == null || w.EndDate >= period.StartDate)
        && (!scope.LocationId.HasValue || w.LocationId == scope.LocationId)
        && (!scope.OrgUnitId.HasValue || w.OrgUnitId == scope.OrgUnitId)
        && (!scope.IsConfined || (w.LocationId.HasValue && scope.AllowedLocationIds.Contains(w.LocationId.Value)))
        && db.WorkerPayrollProfiles.Any(p => !p.IsArchived && p.WorkerId == w.Id && p.PayGroupId == period.PayGroupId
            && p.EffectiveFrom <= period.EndDate && (p.EffectiveTo == null || p.EffectiveTo >= period.StartDate)));

    public async Task<PeriodOvertimeBatch> GetAsync(Guid periodId, CancellationToken ct)
    {
        var (period, reason) = await CheckAsync(periodId, ct);
        var workers = await Eligible(period).OrderBy(w => w.EmployeeNo).ToListAsync(ct);
        var amounts = await db.PeriodOvertime.Where(o => !o.IsArchived && o.PayPeriodId == periodId).ToDictionaryAsync(o => o.WorkerId, ct);
        return new(periodId, period.PeriodLabel, reason == null, reason,
            workers.Select(w => new PeriodOvertimeRow(w.Id, w.EmployeeNo, $"{w.FirstName} {w.LastName}",
                amounts.TryGetValue(w.Id, out var row) ? row.Amount : null, row?.Note ?? "")).ToList());
    }

    public async Task SaveAsync(Guid periodId, Guid workerId, PeriodOvertimeSave request, CancellationToken ct)
    {
        var (period, reason) = await CheckAsync(periodId, ct);
        if (reason != null) throw new DomainException("overtime-period-locked", reason);
        if (request.Amount is < 0 or > 999999999m || (request.Amount.HasValue && decimal.Round(request.Amount.Value, 2) != request.Amount))
            throw new DomainException("invalid-overtime-amount", "Enter a non-negative amount with at most two decimal places.");
        if ((request.Note?.Length ?? 0) > 500) throw new DomainException("invalid-overtime-note", "The note must be at most 500 characters.");
        if (!await Eligible(period).AnyAsync(w => w.Id == workerId, ct))
            throw new DomainException("worker-not-eligible", "Employee is not eligible for this pay period or branch.");
        var row = await db.PeriodOvertime.FirstOrDefaultAsync(o => !o.IsArchived && o.PayPeriodId == periodId && o.WorkerId == workerId, ct);
        if (row == null && request.Amount == null) return;
        if (row == null) { row = new PeriodOvertime { PayPeriodId = periodId, WorkerId = workerId }; db.PeriodOvertime.Add(row); }
        row.IsArchived = request.Amount == null;
        row.Amount = request.Amount ?? 0;
        row.Note = request.Note?.Trim() ?? "";
        await db.SaveChangesAsync(ct);
    }
}
