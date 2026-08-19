// M46: branch payroll drafts flow up for organisation-wide HR approval.
// Branch runs calculate for their branch's workers only, may coexist across
// branches for one period, and must be sent up for review before top HR can
// approve them. Confined (branch-only) HR cannot approve a branch run.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests.Payroll;

public class PayrollBranchDraftTests
{
    private static readonly Guid BranchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherBranchId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private const string Creator = "m46-creator";

    private static ShellContext ScopedContext(Guid? locationId = null, bool confined = false)
    {
        var scope = new ShellContext { LocationId = locationId, EntityId = Guid.Empty };
        if (confined && locationId.HasValue) scope.AllowedLocationIds.Add(locationId.Value);
        return scope;
    }

    private static SalaryComponent Comp(string code, string type, string basis, string? tied = null, decimal? rate = null, bool statutory = false, int priority = 100)
        => new() { Code = code, Name = code, ComponentType = type, CalculationBasis = basis, BasisComponentCode = tied,
            Rate = rate, FixedAmount = null, Ceiling = null, IsTaxable = true, IsStatutory = statutory, Priority = priority,
            Version = 1, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)) };

    private static Worker TestWorker(string empNo, Guid? locationId) => new()
    {
        EmployeeNo = empNo, FirstName = "Test", LastName = "Worker", WorkerType = "employee", Status = "active",
        LocationId = locationId,
        // M24 release gate: every test worker must carry the statutory identity
        // pack or the release step (used by the payslip-scope test) blocks them.
        Nrc = "123456/10/1", Tpin = "1000000001", NapsaNumber = "NAPSA-1", NhimaNumber = "NHIMA-1",
    };

    private static async Task<(PayGroup Group, PayPeriod Period, SalaryStructure Structure, WorkerPayrollProfile Profile, SalaryComponent Basic)>
        SeedStackAsync(HrmDbContext ctx, Guid workerLocationId, string empNo = "T001")
    {
        var basic = Comp("basic", "earning", "fixed");
        ctx.SalaryComponents.Add(basic);
        var slabs = new[]
        {
            new TaxSlab { TaxYear = "2026", MinAmount = 0m, MaxAmount = 5100m, Rate = 0m, Sequence = 10, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new TaxSlab { TaxYear = "2026", MinAmount = 5100m, MaxAmount = 7100m, Rate = 20m, Sequence = 20, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new TaxSlab { TaxYear = "2026", MinAmount = 7100m, MaxAmount = 9200m, Rate = 30m, Sequence = 30, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new TaxSlab { TaxYear = "2026", MinAmount = 9200m, MaxAmount = null, Rate = 37m, Sequence = 40, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
        };
        foreach (var s in slabs) ctx.TaxSlabs.Add(s);
        var rules = new[]
        {
            new ContributionRule { Code = "napsa-ee", Name = "NAPSA EE", Payer = "employee", Rate = 5m, Ceiling = 1861.80m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new ContributionRule { Code = "napsa-er", Name = "NAPSA ER", Payer = "employer", Rate = 5m, Ceiling = 1861.80m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new ContributionRule { Code = "nhima-ee", Name = "NHIMA EE", Payer = "employee", Rate = 1m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new ContributionRule { Code = "nhima-er", Name = "NHIMA ER", Payer = "employer", Rate = 1m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
        };
        foreach (var r in rules) ctx.ContributionRules.Add(r);
        // LegalEntity + WorkLocation rows carry the branch ids used by the
        // scoping tests so Worker.LocationId (FK → WorkLocation) resolves.
        var legalEntity = new LegalEntity { Code = "M46-ENTITY", RegisteredName = "M46 Entity" };
        ctx.LegalEntities.Add(legalEntity);
        ctx.WorkLocations.AddRange(
            new WorkLocation { Id = BranchId, Code = "BRANCH", Name = "Branch", LegalEntityId = legalEntity.Id, Type = "branch" },
            new WorkLocation { Id = OtherBranchId, Code = "OTHER", Name = "Other Branch", LegalEntityId = legalEntity.Id, Type = "branch" });
        var group = new PayGroup { Code = "TEST-MONTHLY", Name = "Test Monthly", Frequency = "monthly", Currency = "ZMW", CalendarDayOfMonth = 25 };
        ctx.PayGroups.Add(group);
        var period = new PayPeriod { PayGroupId = group.Id, PeriodLabel = "Aug 2026", StartDate = DateOnly.FromDateTime(new DateTime(2026, 8, 1)), EndDate = DateOnly.FromDateTime(new DateTime(2026, 8, 31)), CutoffDate = DateOnly.FromDateTime(new DateTime(2026, 8, 20)), PayDate = DateOnly.FromDateTime(new DateTime(2026, 8, 31)), IsCurrent = true };
        ctx.PayPeriods.Add(period);
        var worker = TestWorker(empNo, workerLocationId);
        worker.BankDetails.Add(new WorkerBankDetail { Worker = worker, BankName = "ZANACO", BranchCode = "001",
            AccountNumber = "123456789", AccountName = "Test Worker", PaymentMethod = "bank", IsPrimary = true });
        ctx.Workers.Add(worker);
        var structure = new SalaryStructure { Code = "TEST-STANDARD", Name = "Test Standard" };
        var item = new SalaryStructureItem { Component = basic, DefaultAmount = 25000m };
        structure.Items.Add(item);
        ctx.SalaryStructures.Add(structure);
        var profile = new WorkerPayrollProfile { WorkerId = worker.Id, PayGroupId = group.Id, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Structure = structure };
        profile.ComponentValues.Add(new WorkerComponentValue { ComponentId = basic.Id, Component = basic, Amount = 25000m });
        ctx.WorkerPayrollProfiles.Add(profile);
        await ctx.SaveChangesAsync();
        return (group, period, structure, profile, basic);
    }

    [Fact]
    public async Task BranchRunCalculatesOnlyBranchWorkers()
    {
        var ctx = TestDbContextFactory.Create("m46-tenant");
        var (group, period, _, profile, basic) = await SeedStackAsync(ctx, BranchId, "B001");
        // A second worker in the same group sits at a different branch.
        var other = TestWorker("B002", OtherBranchId);
        ctx.Workers.Add(other);
        var otherProfile = new WorkerPayrollProfile { WorkerId = other.Id, PayGroupId = group.Id, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), StructureId = (await ctx.SalaryStructures.FirstAsync()).Id };
        otherProfile.ComponentValues.Add(new WorkerComponentValue { ComponentId = basic.Id, Component = basic, Amount = 30000m });
        ctx.WorkerPayrollProfiles.Add(otherProfile);
        await ctx.SaveChangesAsync();

        var repo = new PayrollRepository(ctx);
        var svc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext(BranchId));

        var run = await svc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, Creator);
        await svc.LockRunAsync(run.Id, default);
        await svc.CalculateRunAsync(run.Id, default, Creator);

        var lines = await ctx.PayrollRunLines.Where(l => l.RunId == run.Id).ToListAsync();
        Assert.Single(lines); // only the branch-attached worker is paid
        var paidWorker = await ctx.Workers.FirstAsync(w => w.Id == lines.Single().WorkerId);
        Assert.Equal(BranchId, paidWorker.LocationId);
        Assert.Equal(BranchId, run.LocationId!.Value);
    }

    [Fact]
    public async Task OrgRunBlocksWhileBranchDraftOpen()
    {
        var ctx = TestDbContextFactory.Create("m46-tenant");
        var (group, period, _, _, _) = await SeedStackAsync(ctx, BranchId, "B001");

        var branchRepo = new PayrollRepository(ctx);
        var branchSvc = new PayrollServiceImpl(branchRepo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext(BranchId));
        await branchSvc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "branch-hr");

        // Top HR (org-wide scope) cannot start a parallel organisation-wide run.
        var topSvc = new PayrollServiceImpl(branchRepo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext());
        await Assert.ThrowsAsync<DomainException>(async () =>
            await topSvc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "top-hr"));
    }

    [Fact]
    public async Task BranchDraftsMayCoexistAcrossBranches()
    {
        var ctx = TestDbContextFactory.Create("m46-tenant");
        var (group, period, _, _, _) = await SeedStackAsync(ctx, BranchId, "B001");

        var repo = new PayrollRepository(ctx);
        var svcA = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext(BranchId));
        var svcB = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext(OtherBranchId));

        var runA = await svcA.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "hr-a");
        var runB = await svcB.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "hr-b");

        Assert.Equal(BranchId, runA.LocationId!.Value);
        Assert.Equal(OtherBranchId, runB.LocationId!.Value);
        Assert.NotEqual(runA.Id, runB.Id);
    }

    [Fact]
    public async Task SubmitForReviewRequiresBranchRunWithCalculatedFigures()
    {
        var ctx = TestDbContextFactory.Create("m46-tenant");
        var (group, period, _, _, _) = await SeedStackAsync(ctx, BranchId, "B001");
        var repo = new PayrollRepository(ctx);
        var svc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext(BranchId));

        // Org-wide run cannot be sent for review — it goes straight to approval.
        var orgSvc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext());
        var orgRun = await orgSvc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "top-hr");
        await Assert.ThrowsAsync<DomainException>(async () =>
            await orgSvc.SubmitRunAsync(orgRun.Id, default, "top-hr"));

        // Fresh draft with no figures cannot be submitted.
        var run = await svc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, Creator);
        await Assert.ThrowsAsync<DomainException>(async () =>
            await svc.SubmitRunAsync(run.Id, default, Creator));

        // Calculated branch run can be sent for review.
        await svc.LockRunAsync(run.Id, default);
        await svc.CalculateRunAsync(run.Id, default, Creator);
        var submitted = await svc.SubmitRunAsync(run.Id, default, Creator);
        Assert.Equal("in-review", submitted.Status);
        Assert.Contains(ctx.PayrollRunEvents.Where(e => e.RunId == run.Id),
            e => e.Action == "submitted-for-review");
    }

    [Fact]
    public async Task ConfinedApproverCannotApproveBranchRun()
    {
        var ctx = TestDbContextFactory.Create("m46-tenant");
        var (group, period, _, _, _) = await SeedStackAsync(ctx, BranchId, "B001");
        var repo = new PayrollRepository(ctx);
        var svc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext(BranchId));

        var run = await svc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "prep");
        await svc.LockRunAsync(run.Id, default);
        await svc.CalculateRunAsync(run.Id, default, Creator);
        await svc.SubmitRunAsync(run.Id, default, Creator);

        // Confined branch HR is rejected.
        var confinedSvc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"),
            ScopedContext(BranchId, confined: true));
        await Assert.ThrowsAsync<DomainException>(async () =>
            await confinedSvc.ApproveRunAsync(run.Id, "reviewed", default, "branch-hr"));

        // Org-wide (unconfined) approver succeeds.
        var topSvc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext());
        var approved = await topSvc.ApproveRunAsync(run.Id, "ok", default, "top-hr");
        Assert.Equal("approved", approved.Status);
    }

    [Fact]
    public async Task SameBranchCannotOpenTwoRunsForOnePeriod()
    {
        var ctx = TestDbContextFactory.Create("m46-tenant");
        var (group, period, _, _, _) = await SeedStackAsync(ctx, BranchId, "B001");
        var repo = new PayrollRepository(ctx);
        var svc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext(BranchId));

        await svc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, Creator);
        await Assert.ThrowsAsync<DomainException>(async () =>
            await svc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, Creator));
    }

    [Fact]
    public async Task BranchRunReleasedCarriesBranchScopeToPayslips()
    {
        // Branch prepares/calculates/submits; org-wide approver + releaser take it
        // over the fence, and the released payslips remember the branch.
        var ctx = TestDbContextFactory.Create("m46-tenant");
        var (group, period, _, _, _) = await SeedStackAsync(ctx, BranchId, "B001");
        var repo = new PayrollRepository(ctx);
        var branchSvc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext(BranchId));

        var run = await branchSvc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "prep");
        await branchSvc.LockRunAsync(run.Id, default);
        await branchSvc.CalculateRunAsync(run.Id, default, Creator);
        await branchSvc.SubmitRunAsync(run.Id, default, Creator);

        var orgSvc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext());
        var approved = await orgSvc.ApproveRunAsync(run.Id, "ok", default, "top-hr");
        await orgSvc.ReleaseRunAsync(approved.Id, default, "release-hr");

        var slips = await ctx.Payslips.Where(s => s.RunLine!.Run.LocationId == BranchId).ToListAsync();
        Assert.NotEmpty(slips);
        Assert.All(slips, s => Assert.Equal(BranchId, s.LocationId));
    }

    private sealed class FakeDoc(string url) : IPayslipDocumentService
    {
        public Task<string> GenerateAsync(Payslip payslip, PayrollRunLine line, CancellationToken ct)
            => Task.FromResult(url);
    }
}
