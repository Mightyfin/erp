using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M24: statutory identity enforcement — a payroll run cannot be
/// released while any of its workers is missing NRC, TPIN, NAPSA or NHIMA
/// numbers; released payslips snapshot the four references.</summary>
public class PayslipStatutoryTests
{
    private static Worker TestWorker(string empNo = "T001", string? nrc = "123456/10/1",
        string? tpin = "1000000001", string? napsa = "NAPSA-1", string? nhima = "NHIMA-1") => new()
    {
        EmployeeNo = empNo, FirstName = "Test", LastName = "Worker", WorkerType = "employee", Status = "active",
        Nrc = nrc, Tpin = tpin, NapsaNumber = napsa, NhimaNumber = nhima,
    };

    [Fact]
    public async Task Release_BlockedWhenAnyWorkerMissingStatutoryReferences()
    {
        var ctx = TestDbContextFactory.Create("m24-block");
        var service = new PayrollServiceImpl(new PayrollRepository(ctx), new PermissiveAuthz(),
            new FakeDocService("https://storage.example/x.pdf"));
        var (group, _, p2, _, basic, housing, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);

        // Add a second worker with no statutory IDs (the SMK001-shaped case).
        var worker2 = TestWorker(empNo: "T002", nrc: null, tpin: null, napsa: null, nhima: null);
        ctx.Workers.Add(worker2);
        await ctx.SaveChangesAsync();
        // Run lines come from payroll profiles, so the second worker needs one too.
        var structure2 = new SalaryStructure { Code = "TEST-STD-2", Name = "Test Standard 2" };
        ctx.SalaryStructures.Add(structure2);
        var profile2 = new WorkerPayrollProfile { WorkerId = worker2.Id, PayGroupId = group.Id,
            StructureId = structure2.Id, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)) };
        profile2.ComponentValues.Add(new WorkerComponentValue { ComponentId = basic.Id, Component = basic, Amount = 25000m });
        profile2.ComponentValues.Add(new WorkerComponentValue { ComponentId = housing.Id, Component = housing, Amount = 5000m });
        ctx.WorkerPayrollProfiles.Add(profile2);
        await ctx.SaveChangesAsync();

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await service.LockRunAsync(run.Id, CancellationToken.None);
        await service.CalculateRunAsync(run.Id, CancellationToken.None);
        run = await service.ApproveRunAsync(run.Id, "ok", CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.ReleaseRunAsync(run.Id, CancellationToken.None));
        Assert.Equal("run-statutory-readiness", ex.Code);
        Assert.Contains("T002", ex.Message);
        Assert.Contains("NRC", ex.Message);
        Assert.Contains("TPIN", ex.Message);
        // Release gate is a hard block — the run stays approved, no payslips.
        var dbRun = await ctx.PayrollRuns.FindAsync(run.Id);
        Assert.Equal("approved", dbRun!.Status);
        Assert.Empty(await ctx.Payslips.ToListAsync());
    }

    [Fact]
    public async Task Release_AllowedWhenAllWorkersCarryFullStatutoryPack()
    {
        var ctx = TestDbContextFactory.Create("m24-allow");
        var service = new PayrollServiceImpl(new PayrollRepository(ctx), new PermissiveAuthz(),
            new FakeDocService("https://storage.example/x.pdf"));
        var (group, _, p2, _, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await service.LockRunAsync(run.Id, CancellationToken.None);
        await service.CalculateRunAsync(run.Id, CancellationToken.None);
        await service.ApproveRunAsync(run.Id, "ok", CancellationToken.None);
        var released = await service.ReleaseRunAsync(run.Id, CancellationToken.None);
        Assert.Equal("released", released.Status);
        Assert.Single(await ctx.Payslips.ToListAsync());
    }

    [Fact]
    public async Task ReadinessEndpoint_ReportsPerWorkerMissingReferences()
    {
        var ctx = TestDbContextFactory.Create("m24-readiness");
        var service = new PayrollServiceImpl(new PayrollRepository(ctx), new PermissiveAuthz(),
            new FakeDocService("https://storage.example/x.pdf"));
        var (group, _, p2, _, basic, housing, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);

        // First worker has full pack; second is missing TPIN and NHIMA only.
        var worker2 = TestWorker(empNo: "T002", nrc: "789012/10/1", tpin: null,
            napsa: "NAPSA-2", nhima: null);
        ctx.Workers.Add(worker2);
        await ctx.SaveChangesAsync();
        var structure2 = new SalaryStructure { Code = "TEST-STD-2", Name = "Test Standard 2" };
        ctx.SalaryStructures.Add(structure2);
        var profile2 = new WorkerPayrollProfile { WorkerId = worker2.Id, PayGroupId = group.Id,
            StructureId = structure2.Id, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)) };
        profile2.ComponentValues.Add(new WorkerComponentValue { ComponentId = basic.Id, Component = basic, Amount = 25000m });
        profile2.ComponentValues.Add(new WorkerComponentValue { ComponentId = housing.Id, Component = housing, Amount = 5000m });
        ctx.WorkerPayrollProfiles.Add(profile2);
        await ctx.SaveChangesAsync();

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await service.LockRunAsync(run.Id, CancellationToken.None);
        await service.CalculateRunAsync(run.Id, CancellationToken.None);

        var ready = await service.GetRunStatutoryReadinessAsync(run.Id, CancellationToken.None);
        Assert.Equal(2, ready.WorkerCount);
        Assert.False(ready.IsReady);
        Assert.Equal("Jul 2026", ready.PeriodLabel);

        var incomplete = ready.Workers.Single(w => !w.Ready);
        Assert.Equal("T002", incomplete.EmployeeNo);
        Assert.True(incomplete.HasNrc);
        Assert.False(incomplete.HasTpin);
        Assert.True(incomplete.HasNapsaNumber);
        Assert.False(incomplete.HasNhimaNumber);
        Assert.True(ready.Workers.First(w => w.EmployeeNo == "T001").Ready);
    }

    [Fact]
    public async Task ReleasedPayslips_SnapshotStatutoryReferencesFromWorker()
    {
        var ctx = TestDbContextFactory.Create("m24-snapshot");
        var service = new PayrollServiceImpl(new PayrollRepository(ctx), new PermissiveAuthz(),
            new FakeDocService("https://storage.example/x.pdf"));
        var (group, _, p2, _, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);

        // Worker statutory values at release time.
        var worker = await ctx.Workers.FirstAsync();
        worker.Tpin = "TPIN-AT-RELEASE";
        await ctx.SaveChangesAsync();

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await service.LockRunAsync(run.Id, CancellationToken.None);
        await service.CalculateRunAsync(run.Id, CancellationToken.None);
        await service.ApproveRunAsync(run.Id, "ok", CancellationToken.None);
        await service.ReleaseRunAsync(run.Id, CancellationToken.None);

        var slip = (await ctx.Payslips.ToListAsync()).Single();
        Assert.Equal("123456/10/1", slip.WorkerNrc);
        Assert.Equal("TPIN-AT-RELEASE", slip.WorkerTpin);
        Assert.Equal("NAPSA-1", slip.WorkerNapsaNumber);
        Assert.Equal("NHIMA-1", slip.WorkerNhimaNumber);

        // Later edits to the worker never touch the historical payslip.
        worker.Tpin = "TPIN-CHANGED-LATER";
        await ctx.SaveChangesAsync();
        await ctx.Entry(slip).ReloadAsync();
        Assert.Equal("TPIN-AT-RELEASE", slip.WorkerTpin);

        // The DTO exposes the snapshot too (no subject → broad HR-style read).
        var dto = await service.GetPayslipByIdAsync(slip.Id, null, CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Equal("TPIN-AT-RELEASE", dto!.WorkerTpin);
        Assert.Equal("NHIMA-1", dto.WorkerNhimaNumber);
    }

    [Fact]
    public async Task ReadinessEndpoint_NotFoundForUnknownRun()
        => await Assert.ThrowsAsync<DomainException>(() =>
            new PayrollServiceImpl(new PayrollRepository(TestDbContextFactory.Create("m24-404")),
                new PermissiveAuthz(), new FakeDocService("https://storage.example/x.pdf"))
            .GetRunStatutoryReadinessAsync(Guid.NewGuid(), CancellationToken.None));

    private sealed class FakeDocService(string url) : IPayslipDocumentService
    {
        public Task<string> GenerateAsync(Payslip slip, PayrollRunLine line, CancellationToken ct)
            => Task.FromResult(url);
    }
}
