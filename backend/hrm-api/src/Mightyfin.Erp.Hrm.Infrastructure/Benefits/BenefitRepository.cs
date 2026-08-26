using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application.Benefits;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Application = Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Infrastructure.Benefits;

/// <summary>M41 Gap 6b: flexible benefit claims — EF repository.</summary>
public sealed class BenefitRepository(HrmDbContext ctx) : IBenefitRepository
{
    public Task<List<BenefitType>> ListBenefitTypesAsync(CancellationToken ct) =>
        ctx.BenefitTypes.ToListAsync(ct);

    public Task<BenefitType?> GetBenefitTypeByCodeAsync(string code, CancellationToken ct) =>
        ctx.BenefitTypes.FirstOrDefaultAsync(x => x.Code.ToLower() == code.ToLower(), ct);

    public async Task<BenefitType> CreateBenefitTypeAsync(BenefitType type, CancellationToken ct)
    {
        ctx.BenefitTypes.Add(type);
        await ctx.SaveChangesAsync(ct);
        return type;
    }

    public async Task<BenefitType> UpdateBenefitTypeAsync(BenefitType type, CancellationToken ct)
    {
        ctx.BenefitTypes.Update(type);
        await ctx.SaveChangesAsync(ct);
        return type;
    }

    public async Task DeleteBenefitTypeAsync(Guid id, CancellationToken ct)
    {
        var type = await ctx.BenefitTypes.FindAsync([id], ct)
            ?? throw new Application.DomainException("benefit-type-not-found", $"Benefit type {id} not found.");
        ctx.BenefitTypes.Remove(type);
        await ctx.SaveChangesAsync(ct);
    }

    public Task<List<WorkerBenefitAllowance>> ListAllowancesAsync(Guid? workerId, CancellationToken ct) =>
        ctx.WorkerBenefitAllowances
            .Include(x => x.Worker)
            .Include(x => x.BenefitType)
            .Where(x => !workerId.HasValue || x.WorkerId == workerId.Value)
            .ToListAsync(ct);

    public Task<WorkerBenefitAllowance?> GetAllowanceAsync(Guid workerId, Guid benefitTypeId, int year, CancellationToken ct) =>
        ctx.WorkerBenefitAllowances
            .FirstOrDefaultAsync(x => x.WorkerId == workerId && x.BenefitTypeId == benefitTypeId && x.Year == year, ct);

    public async Task<WorkerBenefitAllowance> SetAllowanceAsync(WorkerBenefitAllowance allowance, CancellationToken ct)
    {
        var existing = await GetAllowanceAsync(allowance.WorkerId, allowance.BenefitTypeId, allowance.Year, ct);
        if (existing is null)
            ctx.WorkerBenefitAllowances.Add(allowance);
        else
            existing.AnnualAmount = allowance.AnnualAmount;
        await ctx.SaveChangesAsync(ct);
        return existing ?? allowance;
    }

    public async Task<(List<BenefitClaim> Items, int Total)> ListClaimsAsync(Guid? workerId, string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = ctx.BenefitClaims
            .Include(x => x.Worker)
            .Include(x => x.BenefitType)
            .AsQueryable();
        if (workerId.HasValue) query = query.Where(x => x.WorkerId == workerId.Value);
        if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct);
        // SQLite cannot translate ORDER BY on DateTimeOffset, so page the raw
        // results and sort newest-first in memory.
        var loaded = await query
            .Skip(Math.Max(0, (page - 1) * pageSize))
            .Take(Math.Clamp(pageSize * 4, 1, 400))
            .ToListAsync(ct);
        var items = loaded
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToList();
        return (items, total);
    }

    public Task<BenefitClaim?> GetClaimAsync(Guid id, CancellationToken ct) =>
        ctx.BenefitClaims
            .Include(x => x.Worker)
            .Include(x => x.BenefitType)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<BenefitClaim> CreateClaimAsync(BenefitClaim claim, CancellationToken ct)
    {
        ctx.BenefitClaims.Add(claim);
        await ctx.SaveChangesAsync(ct);
        return claim;
    }

    public async Task UpdateClaimAsync(BenefitClaim claim, CancellationToken ct)
    {
        ctx.BenefitClaims.Update(claim);
        await ctx.SaveChangesAsync(ct);
    }

    public Task<decimal> SumApprovedAsync(Guid workerId, Guid benefitTypeId, int year, CancellationToken ct) =>
        ctx.BenefitClaims
            .Where(x => x.WorkerId == workerId && x.BenefitTypeId == benefitTypeId
                        && (x.Status == "approved" || x.Status == "paid"))
            .SumAsync(x => (decimal?)x.ApprovedAmount ?? 0, ct);

    public Task SaveChangesAsync(CancellationToken ct) => ctx.SaveChangesAsync(ct);
}
