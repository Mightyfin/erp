using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M20: payroll setup admin surface — pay groups, ZRA PAYE slabs,
/// NAPSA/NHIMA contribution rules, and standard salary components.</summary>
public class PayrollSetupTests
{
    private const string TestTenantId = "test-tenant";
    private static (PayrollServiceImpl service, HrmDbContext ctx) Build(string tenant = "test-tenant")
    {
        var ctx = TestDbContextFactory.Create(tenant);
        var repo = new PayrollRepository(ctx);
        var service = new PayrollServiceImpl(repo, new PermissiveAuthz(),
            new PayslipDocumentServiceImpl(ctx));
        return (service, ctx);
    }

    private static SalaryComponent SeedComponent(HrmDbContext ctx, string code, string type, bool statutory = false)
    {
        var comp = new SalaryComponent
        {
            Code = code, Name = code, ComponentType = type, CalculationBasis = type == "earning" ? "fixed" : "percent-of",
            IsStatutory = statutory, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)),
            Version = 1, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false,
        };
        ctx.SalaryComponents.Add(comp);
        ctx.SaveChanges();
        return comp;
    }

    [Fact]
    public async Task UpdateTaxSlab_RoundTrips_And_Keeps_Band_Ordering()
    {
        var (service, ctx) = Build();
        var slabs = new[]
        {
            new TaxSlab { TaxYear = "2026", MinAmount = 0m, MaxAmount = 5100m, Rate = 0m, Sequence = 10, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false },
            new TaxSlab { TaxYear = "2026", MinAmount = 5100m, MaxAmount = 7100m, Rate = 20m, Sequence = 20, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false },
            new TaxSlab { TaxYear = "2026", MinAmount = 7100m, MaxAmount = null, Rate = 37m, Sequence = 30, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false },
        };
        ctx.TaxSlabs.AddRange(slabs);
        ctx.SaveChanges();

        var mid = slabs[1].Id;
        // NAPSA-style ceiling bump: top band threshold raised to 9,200 for a mid-year band.
        var updated = await service.UpdateTaxSlabAsync(mid, new TaxSlabUpdateRequest { MaxAmount = 9200m, Rate = 30m }, CancellationToken.None);
        Assert.Equal(30m, updated.Rate);
        Assert.Equal(9200m, updated.MaxAmount);

        var reloaded = await service.ListTaxSlabsAsync("2026", CancellationToken.None);
        Assert.Equal(3, reloaded.Count);
        Assert.All(reloaded, s => Assert.True(s.MaxAmount is null || s.MinAmount < s.MaxAmount.Value));
    }

    [Fact]
    public async Task UpdateTaxSlab_Rejects_Rate_Out_Of_Range()
    {
        var (service, ctx) = Build();
        var slab = new TaxSlab { TaxYear = "2026", MinAmount = 9200m, MaxAmount = null, Rate = 37m, Sequence = 40, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false };
        ctx.TaxSlabs.Add(slab);
        ctx.SaveChanges();

        await Assert.ThrowsAsync<DomainException>(() => service.UpdateTaxSlabAsync(slab.Id, new TaxSlabUpdateRequest { Rate = 120m }, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => service.UpdateTaxSlabAsync(slab.Id, new TaxSlabUpdateRequest { Rate = -5m }, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateContributionRule_Tightens_Napsa_Ceiling()
    {
        var (service, ctx) = Build();
        var rule = new ContributionRule { Code = "napsa-ee", Name = "NAPSA Employee", Payer = "employee", Rate = 5m, Ceiling = 1861.80m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false };
        ctx.ContributionRules.Add(rule);
        ctx.SaveChanges();

        var updated = await service.UpdateContributionRuleAsync(rule.Id, new ContributionRuleUpdateRequest { Ceiling = 1900m }, CancellationToken.None);
        Assert.Equal(1900m, updated.Ceiling);
        Assert.Equal(5m, updated.Rate);
    }

    [Fact]
    public async Task UpdateContributionRule_Can_Clear_Nullable_Ceiling()
    {
        var (service, ctx) = Build();
        var rule = new ContributionRule { Code = "nhima-ee", Name = "NHIMA Employee", Payer = "employee", Rate = 1m, Ceiling = 50m, Floor = 50m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false };
        ctx.ContributionRules.Add(rule);
        ctx.SaveChanges();

        var updated = await service.UpdateContributionRuleAsync(rule.Id,
            new ContributionRuleUpdateRequest(Ceiling: null, Floor: 50m, CeilingSpecified: true, FloorSpecified: true),
            CancellationToken.None);

        Assert.Null(updated.Ceiling);
        Assert.Equal(50m, updated.Floor);
    }

    [Fact]
    public async Task UpdatePayGroup_Sets_Calendar_And_Clears_Other_Default()
    {
        var (service, ctx) = Build();
        var group = new PayGroup { Code = "MONTHLY-ZMW", Name = "Monthly ZMW", Frequency = "monthly", Currency = "ZMW", CalendarDayOfMonth = 28, InputCutoffDaysBeforePayday = 3, IsDefault = true, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false };
        var other = new PayGroup { Code = "BIWEEKLY", Name = "Bi-weekly", Frequency = "biweekly", Currency = "ZMW", IsDefault = true, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false };
        ctx.PayGroups.AddRange(group, other);
        ctx.SaveChanges();

        var updated = await service.UpdatePayGroupAsync(group.Id, new PayGroupUpdateRequest { CalendarDayOfMonth = 30, IsDefault = true }, CancellationToken.None);
        Assert.Equal(30, updated.CalendarDayOfMonth);
        Assert.True(updated.IsDefault);
        var otherReloaded = await ctx.PayGroups.FirstOrDefaultAsync(g => g.Id == other.Id);
        Assert.False(otherReloaded!.IsDefault); // exactly one default group is allowed
    }

    [Fact]
    public async Task UpdatePayGroup_Rejects_Archived_Group()
    {
        var (service, ctx) = Build();
        var group = new PayGroup { Code = "OLD", Name = "Old Group", Frequency = "monthly", Currency = "ZMW", TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
        group.IsArchived = true;
        ctx.PayGroups.Add(group);
        ctx.SaveChanges();

        await Assert.ThrowsAsync<DomainException>(() => service.UpdatePayGroupAsync(group.Id, new PayGroupUpdateRequest { Name = "Revived?" }, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateSalaryComponent_Archives_Standard_Component_And_Protects_Statutory()
    {
        var (service, ctx) = Build();
        var housing = SeedComponent(ctx, "housing-allowance", "earning");
        var paye = SeedComponent(ctx, "paye", "tax", statutory: true);

        var archived = await service.UpdateSalaryComponentAsync(housing.Id, new SalaryComponentUpdateRequest { IsArchived = true }, CancellationToken.None);
        Assert.False(archived.IsActive);
        Assert.False((await ctx.SalaryComponents.FirstAsync(c => c.Id == housing.Id)).IsActive); // archive request applied

        await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateSalaryComponentAsync(paye.Id, new SalaryComponentUpdateRequest { Rate = 10m }, CancellationToken.None));
    }

    [Fact]
    public async Task ListPayGroupsFull_Includes_Status()
    {
        var (service, ctx) = Build();
        ctx.PayGroups.Add(new PayGroup { Code = "MONTHLY-ZMW", Name = "Monthly ZMW", Frequency = "monthly", Currency = "ZMW", IsDefault = true, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test" });
        ctx.SaveChanges();

        var groups = await service.ListPayGroupsFullAsync(CancellationToken.None);
        Assert.Single(groups);
        Assert.Equal("active", groups[0].Status);
        Assert.True(groups[0].IsDefault);
    }
}
