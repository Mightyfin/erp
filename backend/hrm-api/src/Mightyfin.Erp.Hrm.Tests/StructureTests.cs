using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M21: salary structure administration — which components (with
/// default amounts) ship with a structure, HR-gated CRUD plus default
/// protection and archived-component exclusion.</summary>
public class StructureTests
{
    private const string TestTenantId = "test-tenant";
    private static (PayrollServiceImpl service, HrmDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create(TestTenantId);
        var repo = new PayrollRepository(ctx);
        var service = new PayrollServiceImpl(repo, new PermissiveAuthz(),
            new PayslipDocumentServiceImpl(ctx));
        return (service, ctx);
    }

    private static SalaryComponent SeedComponent(HrmDbContext ctx, string code, string type)
    {
        var comp = new SalaryComponent
        {
            Code = code, Name = code, ComponentType = type,
            CalculationBasis = type == "earning" ? "fixed" : "percent-of",
            IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)),
            Version = 1, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test",
            IsArchived = false,
        };
        ctx.SalaryComponents.Add(comp);
        ctx.SaveChanges();
        return comp;
    }

    [Fact]
    public async Task ListStructures_Returns_ZmwStandard_With_All_Items()
    {
        var (service, ctx) = Build();
        var basic = SeedComponent(ctx, "basic", "earning");
        var housing = SeedComponent(ctx, "housing", "earning");
        var napsa = SeedComponent(ctx, "napsa-ee", "deduction");
        var structure = new SalaryStructure
        {
            Code = "ZMW-STANDARD", Name = "Zambia Standard", Version = 1, IsActive = true,
            TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false,
        };
        ctx.SalaryStructures.Add(structure);
        ctx.SaveChanges();
        ctx.SalaryStructureItems.AddRange(
            new SalaryStructureItem { StructureId = structure.Id, ComponentId = basic.Id, DefaultAmount = 0m, IsOptional = false, Order = 0, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false },
            new SalaryStructureItem { StructureId = structure.Id, ComponentId = housing.Id, DefaultAmount = 0m, IsOptional = false, Order = 1, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false },
            new SalaryStructureItem { StructureId = structure.Id, ComponentId = napsa.Id, DefaultAmount = 0m, IsOptional = true, Order = 2, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false });
        ctx.SaveChanges();

        var list = await service.ListStructuresAsync(CancellationToken.None);
        Assert.Single(list);
        Assert.Equal("ZMW-STANDARD", list[0].Code);
        Assert.Equal(3, list[0].Items.Count);
    }

    [Fact]
    public async Task CreateStructure_Requires_Uppercase_Unique_Code()
    {
        var (service, ctx) = Build();
        var basic = SeedComponent(ctx, "basic", "earning");

        await Assert.ThrowsAsync<DomainException>(() => service.CreateStructureAsync(
            new SalaryStructureCreateRequest("bad code", "Bad", new List<SalaryStructureItemUpsert>()),
            CancellationToken.None));

        var created = await service.CreateStructureAsync(
            new SalaryStructureCreateRequest("EXEC", "Executive", new List<SalaryStructureItemUpsert>
            {
                new(basic.Id, DefaultAmount: 10000m, IsOptional: false),
            }), CancellationToken.None);
        Assert.Equal("EXEC", created.Code);
        Assert.Single(created.Items);
        Assert.Equal(10000m, created.Items[0].DefaultAmount);

        // Second structure with the same code (case-insensitive duplicate) is rejected.
        await Assert.ThrowsAsync<DomainException>(() => service.CreateStructureAsync(
            new SalaryStructureCreateRequest("exec", "Exec Copy", new List<SalaryStructureItemUpsert>()),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateStructure_Replaces_All_Items_With_New_Defaults()
    {
        var (service, ctx) = Build();
        var basic = SeedComponent(ctx, "basic", "earning");
        var housing = SeedComponent(ctx, "housing", "earning");
        var structure = new SalaryStructure
        {
            Code = "EXEC", Name = "Executive", Version = 1, IsActive = true,
            TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false,
        };
        ctx.SalaryStructures.Add(structure);
        ctx.SaveChanges();
        ctx.SalaryStructureItems.Add(new SalaryStructureItem
        {
            StructureId = structure.Id, ComponentId = basic.Id, DefaultAmount = 5000m,
            IsOptional = false, Order = 0, TenantId = TestTenantId, CreatedAt = DateTime.UtcNow,
            CreatedBy = "test", IsArchived = false,
        });
        ctx.SaveChanges();

        var updated = await service.UpdateStructureAsync(structure.Id,
            new SalaryStructureUpdateRequest
            {
                Name = "Executive 2026",
                Items = new List<SalaryStructureItemUpsert>
                {
                    new(basic.Id, DefaultAmount: 15000m, IsOptional: false),
                    new(housing.Id, DefaultAmount: 3000m, IsOptional: false),
                },
            }, CancellationToken.None);

        Assert.Equal("Executive 2026", updated.Name);
        Assert.Equal(2, updated.Items.Count);
        Assert.Equal(15000m, updated.Items.First(i => i.ComponentCode == "basic").DefaultAmount);
        Assert.Equal(3000m, updated.Items.First(i => i.ComponentCode == "housing").DefaultAmount);
    }

    [Fact]
    public async Task UpdateStructure_Protects_Default_Structure_From_Deactivation()
    {
        var (service, ctx) = Build();
        var structure = new SalaryStructure
        {
            Code = "ZMW-STANDARD", Name = "Zambia Standard", Version = 1, IsActive = true,
            TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false,
        };
        ctx.SalaryStructures.Add(structure);
        ctx.SaveChanges();

        // Renaming the default structure is allowed.
        var renamed = await service.UpdateStructureAsync(structure.Id,
            new SalaryStructureUpdateRequest { Name = "Zambia Standard v2" }, CancellationToken.None);
        Assert.Equal("Zambia Standard v2", renamed.Name);

        // But deactivating it is blocked — pay runs fall back to this code.
        await Assert.ThrowsAsync<DomainException>(() => service.UpdateStructureAsync(structure.Id,
            new SalaryStructureUpdateRequest { IsActive = false }, CancellationToken.None));
    }

    [Fact]
    public async Task CreateStructure_Rejects_Archived_Component()
    {
        var (service, ctx) = Build();
        var basic = SeedComponent(ctx, "basic", "earning");
        basic.IsArchived = true;
        basic.IsActive = false;
        ctx.SaveChanges();

        await Assert.ThrowsAsync<DomainException>(() => service.CreateStructureAsync(
            new SalaryStructureCreateRequest("ARCH", "Archived Test", new List<SalaryStructureItemUpsert>
            {
                new(basic.Id, DefaultAmount: 1m),
            }), CancellationToken.None));
    }

    [Fact]
    public async Task CreateStructure_Rejects_Duplicate_Component_In_Items()
    {
        var (service, ctx) = Build();
        var basic = SeedComponent(ctx, "basic", "earning");

        await Assert.ThrowsAsync<DomainException>(() => service.CreateStructureAsync(
            new SalaryStructureCreateRequest("DUP", "Duplicate Item", new List<SalaryStructureItemUpsert>
            {
                new(basic.Id, DefaultAmount: 1m),
                new(basic.Id, DefaultAmount: 2m),
            }), CancellationToken.None));
    }

    [Fact]
    public async Task NonDefault_Structure_Can_Be_Deactivated_But_Is_Hidden_From_List()
    {
        var (service, ctx) = Build();
        var structure = new SalaryStructure
        {
            Code = "OLD-EXEC", Name = "Old Executive", Version = 1, IsActive = true,
            TenantId = TestTenantId, CreatedAt = DateTime.UtcNow, CreatedBy = "test", IsArchived = false,
        };
        ctx.SalaryStructures.Add(structure);
        ctx.SaveChanges();

        var deactivated = await service.UpdateStructureAsync(structure.Id,
            new SalaryStructureUpdateRequest { IsActive = false }, CancellationToken.None);
        Assert.False(deactivated.IsActive);

        var list = await service.ListStructuresAsync(CancellationToken.None);
        Assert.Empty(list); // inactive structures drop out of the admin list
    }
}
