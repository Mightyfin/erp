// M48: the top-HR payroll approval queue. In-review branch runs (plus
// calculated branch runs not yet submitted) surface for org-wide HR with
// branch names, control totals, and submission stamps. Confined branch HR
// cannot open the queue at all — the run already lives on the runs list for
// them, and approval decisions belong to top HR.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests.Payroll;

public class PayrollQueueTests
{
    private static readonly Guid BranchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string Creator = "m48-creator";

    private static ShellContext ScopedContext(Guid? locationId = null, bool confined = false)
    {
        var scope = new ShellContext { LocationId = locationId, EntityId = Guid.Empty };
        if (confined && locationId.HasValue) scope.AllowedLocationIds.Add(locationId.Value);
        return scope;
    }

    private static SalaryComponent Comp(string code)
        => new() { Code = code, Name = code, ComponentType = "earning", CalculationBasis = "fixed",
            IsTaxable = true, IsStatutory = false, Priority = 100, Version = 1, IsActive = true,
            EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)) };

    private static Worker TestWorker(string empNo, Guid locationId) => new()
    {
        EmployeeNo = empNo, FirstName = "Queue", LastName = "Worker", WorkerType = "employee", Status = "active",
        LocationId = locationId,
        Nrc = "123456/10/1", Tpin = "1000000001", NapsaNumber = "NAPSA-1", NhimaNumber = "NHIMA-1",
    };

    private static async Task<(PayGroup Group, PayPeriod Period)> SeedStackAsync(HrmDbContext ctx, Guid workerLocationId, string empNo = "Q001")
    {
        var basic = Comp("basic");
        ctx.SalaryComponents.Add(basic);
        ctx.TaxSlabs.AddRange(
            new TaxSlab { TaxYear = "2026", MinAmount = 0m, MaxAmount = null, Rate = 0m, Sequence = 10, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new TaxSlab { TaxYear = "2026", MinAmount = 5100m, MaxAmount = null, Rate = 30m, Sequence = 20, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 });
        ctx.ContributionRules.AddRange(
            new ContributionRule { Code = "napsa-ee", Name = "NAPSA EE", Payer = "employee", Rate = 5m, Ceiling = 1861.80m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new ContributionRule { Code = "nhima-ee", Name = "NHIMA EE", Payer = "employee", Rate = 1m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 });
        var legalEntity = new LegalEntity { Code = "M48-ENTITY", RegisteredName = "M48 Entity" };
        ctx.LegalEntities.Add(legalEntity);
        ctx.WorkLocations.Add(new WorkLocation { Id = BranchId, Code = "M48-BRANCH", Name = "M48 Branch", LegalEntityId = legalEntity.Id, Type = "branch" });
        var group = new PayGroup { Code = "M48-MONTHLY", Name = "M48 Monthly", Frequency = "monthly", Currency = "ZMW", CalendarDayOfMonth = 25 };
        ctx.PayGroups.Add(group);
        var period = new PayPeriod { PayGroupId = group.Id, PeriodLabel = "Sep 2026", StartDate = DateOnly.FromDateTime(new DateTime(2026, 9, 1)), EndDate = DateOnly.FromDateTime(new DateTime(2026, 9, 30)), CutoffDate = DateOnly.FromDateTime(new DateTime(2026, 9, 20)), PayDate = DateOnly.FromDateTime(new DateTime(2026, 9, 30)), IsCurrent = true };
        ctx.PayPeriods.Add(period);
        var worker = TestWorker(empNo, workerLocationId);
        worker.BankDetails.Add(new WorkerBankDetail { Worker = worker, BankName = "ZANACO", BranchCode = "001",
            AccountNumber = "123456789", AccountName = "Queue Worker", PaymentMethod = "bank", IsPrimary = true });
        ctx.Workers.Add(worker);
        var structure = new SalaryStructure { Code = "M48-STD", Name = "M48 Standard" };
        structure.Items.Add(new SalaryStructureItem { Component = basic, DefaultAmount = 20000m });
        ctx.SalaryStructures.Add(structure);
        var profile = new WorkerPayrollProfile { WorkerId = worker.Id, PayGroupId = group.Id, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Structure = structure };
        profile.ComponentValues.Add(new WorkerComponentValue { ComponentId = basic.Id, Component = basic, Amount = 20000m });
        ctx.WorkerPayrollProfiles.Add(profile);
        await ctx.SaveChangesAsync();
        return (group, period);
    }

    [Fact]
    public async Task InReviewBranchRunAppearsOnQueueWithBranchNameAndSubmittedAt()
    {
        var ctx = TestDbContextFactory.Create("m48-tenant");
        var (group, period) = await SeedStackAsync(ctx, BranchId);
        var repo = new PayrollRepository(ctx);
        var svc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext(BranchId));

        var run = await svc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, Creator);
        await svc.LockRunAsync(run.Id, default);
        await svc.CalculateRunAsync(run.Id, default, Creator);
        await svc.SubmitRunAsync(run.Id, default, Creator); // draft | calculated -> in-review

        var topSvc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext());
        var queue = await topSvc.ListPayrollQueueAsync(default);

        var item = Assert.Single(queue);
        Assert.Equal(run.Id, item.RunId);
        Assert.Equal("in-review", item.Status);
        Assert.Equal(BranchId, item.BranchId);
        Assert.Equal("M48 Branch", item.BranchName);
        Assert.True(item.SubmittedAt.HasValue); // the submitted-for-review event stamp
        Assert.Equal("Sep 2026", item.PeriodLabel);
        Assert.Equal(1, item.EmployeeCount);
        Assert.True(item.TotalGross > 0m);
    }

    [Fact]
    public async Task CalculatedNotYetSubmittedRunIsOnQueueWithoutSubmittedAt()
    {
        // A calculated branch run sits on the queue too — it is awaiting the
        // branch preparer's own "send for review", so the approver can see
        // nothing has been submitted yet (no stamp).
        var ctx = TestDbContextFactory.Create("m48-tenant");
        var (group, period) = await SeedStackAsync(ctx, BranchId);
        var repo = new PayrollRepository(ctx);
        var svc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext(BranchId));

        var run = await svc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, Creator);
        await svc.LockRunAsync(run.Id, default);
        await svc.CalculateRunAsync(run.Id, default, Creator);

        var topSvc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext());
        var queue = await topSvc.ListPayrollQueueAsync(default);

        var item = Assert.Single(queue);
        Assert.Equal(run.Id, item.RunId);
        Assert.Equal("calculated", item.Status);
        Assert.Null(item.SubmittedAt);
        Assert.Equal("M48 Branch", item.BranchName);
    }

    [Fact]
    public async Task OrgWideRunNeverEntersTheQueue()
    {
        var ctx = TestDbContextFactory.Create("m48-tenant");
        var (group, period) = await SeedStackAsync(ctx, BranchId);
        var repo = new PayrollRepository(ctx);
        var topSvc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext());

        var orgRun = await topSvc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "top-hr");
        await topSvc.LockRunAsync(orgRun.Id, default);
        await topSvc.CalculateRunAsync(orgRun.Id, default, "top-hr");

        // The org run stays calculated; only branch runs populate the queue.
        var queue = await topSvc.ListPayrollQueueAsync(default);
        Assert.DoesNotContain(queue, q => q.RunId == orgRun.Id);
        Assert.Empty(queue);
    }

    [Fact]
    public async Task ConfinedUserCannotOpenTheApprovalQueue()
    {
        var ctx = TestDbContextFactory.Create("m48-tenant");
        var (group, period) = await SeedStackAsync(ctx, BranchId);
        var repo = new PayrollRepository(ctx);
        var svc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"), ScopedContext(BranchId));

        var run = await svc.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, Creator);
        await svc.LockRunAsync(run.Id, default);
        await svc.CalculateRunAsync(run.Id, default, Creator);
        await svc.SubmitRunAsync(run.Id, default, Creator);

        // The branch HR who prepared the run cannot open top HR's queue.
        var confinedSvc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDoc("https://x.example/doc"),
            ScopedContext(BranchId, confined: true));
        var ex = await Assert.ThrowsAsync<DomainException>(async () =>
            await confinedSvc.ListPayrollQueueAsync(default));
        Assert.Equal("payroll-queue-confined", ex.Code);
    }

    private sealed class FakeDoc(string url) : IPayslipDocumentService
    {
        public Task<string> GenerateAsync(Payslip payslip, PayrollRunLine line, CancellationToken ct)
            => Task.FromResult(url);
    }
}
