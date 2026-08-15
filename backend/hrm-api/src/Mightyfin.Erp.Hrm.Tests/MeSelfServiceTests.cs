using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M25: employee self-service — payslips and HR requests are keyed on
/// the OIDC subject so a worker can only ever reach their own records; shared
/// admin reads keep broad access for HR roles and block employee-only callers
/// when the queried record is not their own.</summary>
public class MeSelfServiceTests
{
    [Fact]
    public async Task GetMyPayslips_OwnsOnlyViaSubject()
    {
        var ctx = TestDbContextFactory.Create("m25-own");
        var service = new PayrollServiceImpl(new PayrollRepository(ctx), new PermissiveAuthz(),
            new FakeDocService("https://storage.example/x.pdf"));
        var (group, _, p2, _, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);

        // Seed the subject link on the default worker BEFORE creating the run,
        // so the run lines reference a worker whose identity we control.
        var worker = await ctx.Workers.FirstAsync();
        worker.SubjectId = "sub-m25-owner";
        await ctx.SaveChangesAsync();

        var mine = await service.GetMyPayslipsAsync("sub-m25-owner", CancellationToken.None);
        Assert.Empty(mine.Items); // no released run yet
        var other = await service.GetMyPayslipsAsync("sub-someone-else", CancellationToken.None);
        Assert.Empty(other.Items);

        // Release a run for the worker, then the own inbox sees the slip.
        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await service.LockRunAsync(run.Id, CancellationToken.None);
        await service.CalculateRunAsync(run.Id, CancellationToken.None);
        await service.ApproveRunAsync(run.Id, "ok", CancellationToken.None);
        await service.ReleaseRunAsync(run.Id, CancellationToken.None);

        mine = await service.GetMyPayslipsAsync("sub-m25-owner", CancellationToken.None);
        Assert.Single(mine.Items);
        other = await service.GetMyPayslipsAsync("sub-someone-else", CancellationToken.None);
        Assert.Empty(other.Items);
    }

    [Fact]
    public async Task GetMyPayslipById_RejectsAnotherWorkersSlip()
    {
        var ctx = TestDbContextFactory.Create("m25-block");
        var service = new PayrollServiceImpl(new PayrollRepository(ctx), new PermissiveAuthz(),
            new FakeDocService("https://storage.example/x.pdf"));
        var (group, _, p2, _, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);

        var worker = await ctx.Workers.FirstAsync();
        worker.SubjectId = "sub-m25-block";
        await ctx.SaveChangesAsync();

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await service.LockRunAsync(run.Id, CancellationToken.None);
        await service.CalculateRunAsync(run.Id, CancellationToken.None);
        await service.ApproveRunAsync(run.Id, "ok", CancellationToken.None);
        await service.ReleaseRunAsync(run.Id, CancellationToken.None);

        var slip = (await ctx.Payslips.ToListAsync()).Single();

        var found = await service.GetMyPayslipByIdAsync(slip.Id, "sub-m25-block", CancellationToken.None);
        Assert.NotNull(found);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.GetMyPayslipByIdAsync(slip.Id, "sub-an-impostor", CancellationToken.None));
        Assert.Equal("payslip-not-owned", ex.Code);
        var ex2 = await Assert.ThrowsAsync<DomainException>(() =>
            service.GetMyPayslipByIdAsync(slip.Id, "sub-not-linked", CancellationToken.None));
        Assert.Equal("payslip-not-owned", ex2.Code);
        // A non-existent slip always returns null, even for the owner.
        Assert.Null(await service.GetMyPayslipByIdAsync(Guid.NewGuid(), "sub-m25-block", CancellationToken.None));
    }

    [Fact]
    public async Task AdminSharedRead_BlocksEmployeeOnlyCallerOnForeignSlip()
    {
        var ctx = TestDbContextFactory.Create("m25-shared");
        // Employee-only principal: not payroll/hr_admin/hr_ops.
        var authz = new PermissiveAuthz { Roles = ["employee"] };
        var service = new PayrollServiceImpl(new PayrollRepository(ctx), authz,
            new FakeDocService("https://storage.example/x.pdf"));
        var (group, _, p2, _, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);

        var worker = await ctx.Workers.FirstAsync();
        worker.SubjectId = "sub-m25-shared";
        await ctx.SaveChangesAsync();

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await service.LockRunAsync(run.Id, CancellationToken.None);
        await service.CalculateRunAsync(run.Id, CancellationToken.None);
        await service.ApproveRunAsync(run.Id, "ok", CancellationToken.None);
        await service.ReleaseRunAsync(run.Id, CancellationToken.None);

        var slip = (await ctx.Payslips.ToListAsync()).Single();

        // Own record: shared admin read passes for the employee.
        var own = await service.GetPayslipsAsync(worker.Id, "sub-m25-shared", CancellationToken.None);
        Assert.Single(own.Items);
        // Unknown subject: legacy broad-read path (test harness behaviour).
        var broad = await service.GetPayslipByIdAsync(slip.Id, null, CancellationToken.None);
        Assert.NotNull(broad);
        // Impostor subject on the shared endpoints: blocked.
        await Assert.ThrowsAsync<DomainException>(() =>
            service.GetPayslipByIdAsync(slip.Id, "sub-impostor", CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() =>
            service.GetPayslipsAsync(worker.Id, "sub-impostor", CancellationToken.None));
    }

    [Fact]
    public async Task GetMyRequests_ResolvesOwnInboxViaSubject()
    {
        var ctx = TestDbContextFactory.Create("m25-requests");
        var repo = new ExperienceRepository(ctx);
        var service = new ExperienceServiceImpl(repo, new PermissiveAuthz(),
            new WorkflowServiceImpl(new WorkflowRepository(ctx), new PermissiveAuthz(), new NoOpEffects()),
            new LetterTemplatesImpl(), new FakeMergeProvider(),
            new WorkerServiceImpl(new WorkerRepository(ctx), new PermissiveAuthz(), new UlidIdProvider()));
        var worker = new Worker { EmployeeNo = "R001", FirstName = "Req", LastName = "Worker",
            WorkerType = "employee", Status = "active", SubjectId = "sub-m25-req", TenantId = "m25-requests" };
        ctx.Workers.Add(worker);
        await ctx.SaveChangesAsync();

        // Another worker's request — same tenant, different identity.
        var otherWorker = new Worker { EmployeeNo = "R002", FirstName = "Other", LastName = "Worker",
            WorkerType = "employee", Status = "active", TenantId = "m25-requests" };
        ctx.Workers.Add(otherWorker);
        await ctx.SaveChangesAsync();

        var open = new HrRequest { WorkerId = worker.Id, Category = "profile-update",
            Subject = "NRC correction", Body = "Please update my NRC digits.", Status = "open", Confidentiality = "normal" };
        var theirs = new HrRequest { WorkerId = otherWorker.Id, Category = "enquiry",
            Subject = "Other worker", Body = "A generic enquiry.", Status = "open", Confidentiality = "normal" };
        await repo.CreateRequestAsync(open, CancellationToken.None);
        await repo.CreateRequestAsync(theirs, CancellationToken.None);

        var mine = await service.GetMyRequestsAsync("sub-m25-req", null, CancellationToken.None);
        Assert.Single(mine.Items);
        Assert.Equal("NRC correction", mine.Items[0].Subject);
        var impostor = await service.GetMyRequestsAsync("sub-impostor", null, CancellationToken.None);
        Assert.Empty(impostor.Items);
        Assert.Empty(await service.GetMyRequestsAsync("", null, CancellationToken.None).ContinueWith(t => t.Result.Items));
    }

    private sealed class FakeDocService(string url) : IPayslipDocumentService
    {
        public Task<string> GenerateAsync(Payslip slip, PayrollRunLine line, CancellationToken ct)
            => Task.FromResult(url);
    }

    private sealed class FakeMergeProvider : IMergeDataProvider
    {
        public Task<LetterMergeData> GetMergeDataAsync(Guid workerId, string letterType, CancellationToken ct) =>
            Task.FromResult(new LetterMergeData(
                "Dev Operator", "DEV-001", "HR Administrator", "A",
                new DateOnly(2026, 1, 4), "Zambia Mining Ltd", 12500.00m, "HRM/DEV-001"));
    }
}
