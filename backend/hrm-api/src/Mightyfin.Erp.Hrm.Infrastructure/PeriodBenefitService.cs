using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Benefits;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Infrastructure;
public sealed class PeriodBenefitService(HrmDbContext db, IAuthzService authz, ShellContext scope) : IPeriodBenefitService
{
    private async Task<(PayPeriod Period, BenefitType Type, string? Locked)> Context(Guid periodId, Guid typeId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops", "payroll");
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == periodId && !p.IsArchived, ct)
            ?? throw new DomainException("pay-period-not-found", "Pay period not found.");
        var type = await db.BenefitTypes.FirstOrDefaultAsync(t => t.Id == typeId && !t.IsArchived, ct)
            ?? throw new DomainException("benefit-type-not-found", "Benefit type not found.");
        var locked = period.Status != "open" || await db.PayrollRuns.AnyAsync(r => !r.IsArchived && r.PayPeriodId == periodId && r.Status != "draft" && r.Status != "locked" && r.Status != "calculated" && r.Status != "reversed" && r.Status != "cancelled" && r.Status != "void", ct);
        return (period, type, locked ? "Benefit changes are blocked while payroll is calculating, under review, approved or released, or the period is closed." : !type.IsActive || !type.IncludeInPayroll ? "This benefit must be active and configured as Add to payslip before assigning it to a payroll month." : null);
    }
    private IQueryable<Worker> Eligible(PayPeriod p) => db.Workers.Where(w => !w.IsArchived
        && (w.Status == "active" || w.Status == "on-leave" || w.Status == "notice")
        && (w.StartDate == null || w.StartDate <= p.EndDate) && (w.EndDate == null || w.EndDate >= p.StartDate)
        && (!scope.LocationId.HasValue || w.LocationId == scope.LocationId) && (!scope.OrgUnitId.HasValue || w.OrgUnitId == scope.OrgUnitId)
        && (!scope.IsConfined || (w.LocationId.HasValue && scope.AllowedLocationIds.Contains(w.LocationId.Value)))
        && db.WorkerPayrollProfiles.Any(pr => !pr.IsArchived && pr.WorkerId == w.Id && pr.PayGroupId == p.PayGroupId && pr.EffectiveFrom <= p.EndDate && (pr.EffectiveTo == null || pr.EffectiveTo >= p.StartDate)));
    public async Task<PeriodBenefitBatch> GetAsync(Guid periodId, Guid typeId, CancellationToken ct)
    {
        var (period, type, locked) = await Context(periodId, typeId, ct);
        var workers = await Eligible(period).OrderBy(w => w.EmployeeNo).ToListAsync(ct);
        var amounts = await db.PeriodBenefits.Where(b => !b.IsArchived && b.PayPeriodId == periodId && b.BenefitTypeId == typeId).ToDictionaryAsync(b => b.WorkerId, ct);
        var managed = await db.BenefitClaims.Where(c => !c.IsArchived && c.PayPeriodId == periodId && c.BenefitTypeId == typeId).Select(c => c.WorkerId).Distinct().ToListAsync(ct);
        var recurring = await db.WorkerBenefitAllowances.Where(b => !b.IsArchived && b.BenefitTypeId == typeId && b.Year == period.StartDate.Year).ToListAsync(ct);
        var branches = await db.WorkLocations.ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var departments = await db.OrgUnits.ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var currency = await db.PayGroups.Where(g => g.Id == period.PayGroupId).Select(g => g.Currency).FirstAsync(ct);
        return new(periodId, period.PeriodLabel, currency, typeId, type.Name, locked == null, locked, workers.Select(w => new PeriodBenefitRow(w.Id,w.EmployeeNo,w.FullName,w.LocationId,w.LocationId.HasValue ? branches.GetValueOrDefault(w.LocationId.Value,"Unassigned") : "Unassigned",w.OrgUnitId,w.OrgUnitId.HasValue ? departments.GetValueOrDefault(w.OrgUnitId.Value,"Unassigned") : "Unassigned",amounts.GetValueOrDefault(w.Id)?.Amount,amounts.GetValueOrDefault(w.Id)?.Note ?? "", Math.Round(recurring.Where(a => a.WorkerId == w.Id).Sum(a => a.AnnualAmount) / 12m,2),managed.Contains(w.Id))).ToList());
    }
    public async Task SaveAsync(Guid periodId, Guid typeId, PeriodBenefitSave input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await SaveCore(periodId, typeId, input, ct);
        await tx.CommitAsync(ct);
    }
    public async Task<BenefitClaim> SubmitClaimAsync(Guid typeId, BenefitClaimCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops", "payroll");
        if (!request.PayPeriodId.HasValue) throw new DomainException("benefit-claim-period", "Select the payroll month for this claim.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var periodId = request.PayPeriodId.Value;
        var (period, type, locked) = await Context(periodId, typeId, ct);
        if (locked != null) throw new DomainException("benefit-period-locked", locked);
        if (request.AmountClaimed <= 0 || decimal.Round(request.AmountClaimed,2) != request.AmountClaimed)
            throw new DomainException("benefit-claim-invalid", "Enter a positive amount with at most two decimal places.");
        if (type.RequiresEvidence && !request.EvidenceAttached)
            throw new DomainException("benefit-claim-evidence", $"Evidence is required for {type.Name} claims.");
        var currency = await db.PayGroups.Where(g => g.Id == period.PayGroupId).Select(g => g.Currency).FirstAsync(ct);
        if (!string.Equals(currency, request.Currency?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new DomainException("benefit-claim-currency", $"This payroll uses {currency}. Enter the claim in {currency}.");
        var worker = await Eligible(period).FirstOrDefaultAsync(w => w.Id == request.WorkerId, ct)
            ?? throw new DomainException("benefit-worker-ineligible", "Employee is outside your access or is not eligible for this payroll period.");
        var existing = await db.PeriodBenefits.SingleOrDefaultAsync(b => !b.IsArchived && b.PayPeriodId == periodId && b.BenefitTypeId == typeId && b.WorkerId == request.WorkerId, ct);
        await SaveCore(periodId, typeId, new([new(request.WorkerId, (existing?.Amount ?? 0) + request.AmountClaimed, request.Note)]), ct, fromClaim: true);
        var claim = new BenefitClaim {
            WorkerId = worker.Id, Worker = worker, BenefitTypeId = typeId, BenefitType = type, PayPeriodId = periodId,
            LocationId = worker.LocationId, AmountClaimed = request.AmountClaimed, ApprovedAmount = request.AmountClaimed,
            Currency = currency, Note = request.Note?.Trim(), EvidenceAttached = request.EvidenceAttached,
            Status = "approved", CreatedBySubjectId = authz.CurrentSubjectId, DecidedBySubjectId = authz.CurrentSubjectId,
            DecidedAt = DateTimeOffset.UtcNow, DecisionReason = "Assigned directly to payroll by HR/admin."
        };
        db.BenefitClaims.Add(claim);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return claim;
    }
    public async Task<BenefitClaim> UpdateClaimAsync(Guid id, BenefitClaimUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops", "payroll");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var claim = await db.BenefitClaims.Include(c => c.Worker).Include(c => c.BenefitType)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsArchived && c.PayPeriodId != null, ct)
            ?? throw new DomainException("benefit-claim-not-found", "Payroll claim not found.");
        if (claim.Status != "approved") throw new DomainException("benefit-claim-state", "Only an active payroll claim can be edited.");
        var periodId = claim.PayPeriodId!.Value;
        var (period, type, locked) = await Context(periodId, claim.BenefitTypeId, ct);
        if (locked != null) throw new DomainException("benefit-period-locked", locked);
        if (request.AmountClaimed <= 0 || decimal.Round(request.AmountClaimed, 2) != request.AmountClaimed)
            throw new DomainException("benefit-claim-invalid", "Enter a positive amount with at most two decimal places.");
        if (type.RequiresEvidence && !request.EvidenceAttached)
            throw new DomainException("benefit-claim-evidence", $"Evidence is required for {type.Name} claims.");
        var currency = await db.PayGroups.Where(g => g.Id == period.PayGroupId).Select(g => g.Currency).FirstAsync(ct);
        if (!string.Equals(currency, request.Currency?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new DomainException("benefit-claim-currency", $"This payroll uses {currency}. Enter the claim in {currency}.");
        if (!await Eligible(period).AnyAsync(w => w.Id == claim.WorkerId, ct))
            throw new DomainException("benefit-worker-ineligible", "Employee is outside your access or is not eligible for this payroll period.");
        var entry = await db.PeriodBenefits.SingleOrDefaultAsync(b => !b.IsArchived && b.PayPeriodId == periodId && b.BenefitTypeId == claim.BenefitTypeId && b.WorkerId == claim.WorkerId, ct)
            ?? throw new DomainException("benefit-assignment-changed", "The payroll assignment changed. Refresh before editing this claim.");
        var next = entry.Amount - claim.AmountClaimed + request.AmountClaimed;
        if (next < 0) throw new DomainException("benefit-assignment-changed", "The payroll assignment changed. Refresh before editing this claim.");
        var before = JsonSerializer.Serialize(new { claim.AmountClaimed, claim.Note, claim.EvidenceAttached });
        await SaveCore(periodId, claim.BenefitTypeId, new([new(claim.WorkerId, next, request.Note)]), ct, fromClaim: true);
        claim.AmountClaimed = request.AmountClaimed; claim.ApprovedAmount = request.AmountClaimed;
        claim.Note = request.Note?.Trim(); claim.EvidenceAttached = request.EvidenceAttached;
        db.AuditEntries.Add(new AuditEntry { EntityType = "benefit-claim", EntityId = claim.Id.ToString(), Action = "edit", ActorSubjectId = authz.CurrentSubjectId ?? "system", BeforeJson = before, AfterJson = JsonSerializer.Serialize(new { claim.AmountClaimed, claim.Note, claim.EvidenceAttached }) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return claim;
    }
    public async Task DeleteClaimAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops", "payroll");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var claim = await db.BenefitClaims.FirstOrDefaultAsync(c => c.Id == id && !c.IsArchived && c.PayPeriodId != null, ct)
            ?? throw new DomainException("benefit-claim-not-found", "Payroll claim not found.");
        if (claim.Status != "approved") throw new DomainException("benefit-claim-state", "Only an active payroll claim can be deleted.");
        var periodId = claim.PayPeriodId!.Value;
        var (period, _, locked) = await Context(periodId, claim.BenefitTypeId, ct);
        if (locked != null) throw new DomainException("benefit-period-locked", locked);
        if (!await Eligible(period).AnyAsync(w => w.Id == claim.WorkerId, ct))
            throw new DomainException("benefit-worker-ineligible", "Employee is outside your access or is not eligible for this payroll period.");
        var entry = await db.PeriodBenefits.SingleOrDefaultAsync(b => !b.IsArchived && b.PayPeriodId == periodId && b.BenefitTypeId == claim.BenefitTypeId && b.WorkerId == claim.WorkerId, ct)
            ?? throw new DomainException("benefit-assignment-changed", "The payroll assignment changed. Refresh before deleting this claim.");
        var next = entry.Amount - claim.AmountClaimed;
        if (next < 0) throw new DomainException("benefit-assignment-changed", "The payroll assignment changed. Refresh before deleting this claim.");
        await SaveCore(periodId, claim.BenefitTypeId, new([new(claim.WorkerId, next == 0 ? null : next, entry.Note)]), ct, fromClaim: true);
        claim.IsArchived = true;
        db.AuditEntries.Add(new AuditEntry { EntityType = "benefit-claim", EntityId = claim.Id.ToString(), Action = "delete", ActorSubjectId = authz.CurrentSubjectId ?? "system", BeforeJson = JsonSerializer.Serialize(new { claim.AmountClaimed, claim.PayPeriodId, claim.WorkerId }), AfterJson = JsonSerializer.Serialize(new { claim.IsArchived }) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task SaveCore(Guid periodId, Guid typeId, PeriodBenefitSave input, CancellationToken ct, bool fromClaim = false)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops", "payroll");
        if (input.Rows == null || input.Rows.Count is < 1 or > 500 || input.Rows.Any(r => r == null) || input.Rows.Select(r => r.WorkerId).Distinct().Count() != input.Rows.Count)
            throw new DomainException("benefit-invalid-rows", "Submit 1–500 rows, with each employee appearing once.");
        var (period, type, locked) = await Context(periodId,typeId,ct);
        if (locked != null) throw new DomainException("benefit-period-locked",locked);
        var eligible = await Eligible(period).Select(w => w.Id).ToListAsync(ct);
        var workerIds = input.Rows.Select(r => r.WorkerId).ToList();
        if (!fromClaim && await db.BenefitClaims.AnyAsync(c => !c.IsArchived && c.PayPeriodId == periodId && c.BenefitTypeId == typeId && workerIds.Contains(c.WorkerId), ct))
            throw new DomainException("benefit-claim-managed", "This employee's monthly amount includes claims. Edit or delete the claims in the Claims tab.");
        var existing = await db.PeriodBenefits.Where(b => !b.IsArchived && b.PayPeriodId == periodId && b.BenefitTypeId == typeId).ToListAsync(ct);
        foreach(var row in input.Rows)
        {
            if (!eligible.Contains(row.WorkerId)) throw new DomainException("benefit-worker-ineligible","Employee is outside your access or is not eligible for this payroll period.");
            if (row.Amount is < 0 or > 999999999m || (row.Amount.HasValue && decimal.Round(row.Amount.Value,2) != row.Amount))
                throw new DomainException("benefit-invalid-amount","Enter a non-negative amount with at most two decimal places, or clear the amount to remove the assignment.");
            if ((row.Note?.Length ?? 0) > 500) throw new DomainException("benefit-note-too-long","Notes must not exceed 500 characters.");
            if (type.AnnualCap > 0 && row.Amount.HasValue)
            {
                var other = await (from b in db.PeriodBenefits where !b.IsArchived && b.WorkerId == row.WorkerId && b.BenefitTypeId == typeId && b.PayPeriodId != periodId
                    join p in db.PayPeriods on b.PayPeriodId equals p.Id where p.StartDate.Year == period.StartDate.Year && !p.IsArchived select b.Amount).SumAsync(ct);
                if (other + row.Amount > type.AnnualCap) throw new DomainException("benefit-over-cap","These monthly assignments exceed the benefit's annual cap for this employee.");
            }
        }
        foreach(var row in input.Rows)
        {
            var entry = existing.SingleOrDefault(b => b.WorkerId == row.WorkerId);
            if(entry == null && row.Amount == null) continue;
            var before = entry == null ? null : JsonSerializer.Serialize(new { entry.Amount, entry.Note, entry.IsArchived });
            if(entry == null) { entry = new PeriodBenefit { PayPeriodId=periodId,WorkerId=row.WorkerId,BenefitTypeId=typeId }; db.PeriodBenefits.Add(entry); }
            entry.Amount=row.Amount ?? 0;entry.Note=row.Note?.Trim() ?? "";entry.IsArchived=row.Amount==null;
            db.AuditEntries.Add(new AuditEntry { EntityType="payroll.period-benefit",EntityId=entry.Id.ToString(),Action=entry.IsArchived?"remove":"assign",ActorSubjectId=authz.CurrentSubjectId ?? "system",BeforeJson=before,AfterJson=JsonSerializer.Serialize(new { entry.WorkerId,entry.PayPeriodId,entry.BenefitTypeId,entry.Amount,entry.Note,entry.IsArchived }) });
        }
        // Existing results become stale when inputs change. Reuse the calculation-ready
        // status so approval and submission require a fresh calculation first.
        var runs = await db.PayrollRuns.Where(r => !r.IsArchived && r.PayPeriodId == periodId && r.Status == "calculated").ToListAsync(ct);
        foreach (var run in runs)
        {
            run.Status = "locked";
            db.PayrollRunEvents.Add(new PayrollRunEvent {
                RunId = run.Id, Action = "benefit-inputs-updated", ActorSubjectId = authz.CurrentSubjectId ?? "system",
                FromStatus = "calculated", ToStatus = "locked",
                Reason = "Benefit inputs changed. Calculate payroll again before approval.",
                DetailsJson = JsonSerializer.Serialize(new { BenefitTypeId = typeId, WorkerIds = input.Rows.Select(r => r.WorkerId) })
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
