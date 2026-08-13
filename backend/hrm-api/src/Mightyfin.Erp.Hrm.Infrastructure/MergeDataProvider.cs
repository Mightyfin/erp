using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

/// <summary>Gathers the worker and employment snapshot that letter templates
/// merge in (UI-XPR-002). Falls back gracefully when optional data is missing
/// (e.g. no payroll profile yet before M5/M6).</summary>
public sealed class MergeDataProviderImpl(HrmDbContext db) : IMergeDataProvider
{
    public async Task<LetterMergeData> GetMergeDataAsync(Guid workerId, string letterType, CancellationToken ct)
    {
        var worker = await db.Workers
            .Where(w => w.Id == workerId)
            .Select(w => new { w.FirstName, w.LastName, w.EmployeeNo, w.JobTitle, w.Grade, w.StartDate, OrgUnitId = w.OrgUnitId })
            .FirstOrDefaultAsync(ct);
        var fullName = worker == null ? "n/a" : $"{worker.FirstName} {worker.LastName}".Trim();
        var jobTitle = worker?.JobTitle;
        var grade = worker?.Grade;
        var startDate = worker?.StartDate;
        var orgUnitId = worker?.OrgUnitId;

        if (orgUnitId != null)
        {
            var asgn = await db.Assignments
                .Where(a => a.WorkerId == workerId && a.Status == "current"
                    && a.EffectiveFrom <= DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date)
                    && (a.EffectiveTo == null || a.EffectiveTo >= DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date)))
                .OrderByDescending(a => a.EffectiveFrom).FirstOrDefaultAsync(ct);
            if (asgn != null)
            {
                jobTitle ??= asgn.JobTitle;
                grade ??= asgn.Grade;
                startDate ??= asgn.StartDate;
            }
        }

        string? entityName = null;
        if (orgUnitId != null)
        {
            var entityIds = await db.Assignments
                .Where(a => a.WorkerId == workerId && a.Status == "current")
                .Select(a => a.LegalEntityId).ToListAsync(ct);
            if (entityIds.Count > 0)
            {
                entityName = await db.LegalEntities
                    .Where(le => entityIds.Contains(le.Id))
                    .OrderBy(le => le.RegisteredName)
                    .Select(le => le.RegisteredName)
                    .FirstOrDefaultAsync(ct);
            }
        }

        decimal? basic = null;
        bool needsPayroll = letterType == "salary-confirmation" || letterType == "bank" || letterType == "employment-confirmation";
        if (needsPayroll)
        {
            var profile = await db.WorkerPayrollProfiles
                .Where(p => p.WorkerId == workerId && p.EffectiveFrom <= DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date)
                    && (p.EffectiveTo == null || p.EffectiveTo >= DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date)))
                .OrderByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync(ct);
            if (profile != null)
            {
                var values = await db.WorkerComponentValues
                    .Where(v => v.ProfileId == profile.Id)
                    .ToListAsync(ct);
                var componentIds = await db.SalaryComponents
                    .Where(c => c.Code == "basic")
                    .Select(c => c.Id).ToListAsync(ct);
                basic = values.Where(v => componentIds.Contains(v.ComponentId)).Select(v => (decimal?)v.Amount).FirstOrDefault();
            }
        }

        var refText = worker == null ? null : "HRM/" + worker.EmployeeNo;
        return new LetterMergeData(fullName, worker?.EmployeeNo ?? "n/a", jobTitle, grade, startDate, entityName, basic, refText);
    }
}
