using System;
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

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M6: payroll engine — run reversal, YTD accumulation on payslips,
/// statutory employer liability aggregation, and payslip document generation.</summary>
public class PayrollEngineTests
{
    private sealed class FakeDocumentService(string url) : IPayslipDocumentService
    {
        public Task<string> GenerateAsync(Payslip slip, PayrollRunLine line, CancellationToken ct)
            => Task.FromResult(url);
    }

    internal static (PayrollServiceImpl service, HrmDbContext ctx) Build(
        string url = "https://storage.example/payslip.pdf",
        string tenant = "test-tenant",
        string[]? roles = null)
    {
        var ctx = TestDbContextFactory.Create(tenant);
        var repo = new PayrollRepository(ctx);
        var service = new PayrollServiceImpl(
            repo,
            new PermissiveAuthz { Roles = roles ?? ["hr_ops", "hr_admin", "payroll", "employee"] },
            new FakeDocumentService(url));
        return (service, ctx);
    }

    private static SalaryComponent Comp(string code, string type, string basis, string? tied = null, decimal? rate = null, bool statutory = false, int priority = 100)
        => new() { Code = code, Name = code, ComponentType = type, CalculationBasis = basis, BasisComponentCode = tied,
            Rate = rate, FixedAmount = null, Ceiling = null, IsTaxable = true, IsStatutory = statutory, Priority = priority,
            Version = 1, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)) };

    private static Worker TestWorker(string empNo = "T001") => new()
    {
        EmployeeNo = empNo, FirstName = "Test", LastName = "Worker", WorkerType = "employee", Status = "active",
        // M24: the lifecycle harness tests release — every test worker must carry
        // the full statutory identity pack or the release gate blocks them.
        Nrc = "123456/10/1", Tpin = "1000000001", NapsaNumber = "NAPSA-1", NhimaNumber = "NHIMA-1",
    };

    /// Seeds a complete payroll stack: worker, pay group, two monthly periods in 2026,
    /// active components, 2026 PAYE slabs, NAPSA/NHIMA rules, a worker profile.
    internal static async Task<(PayGroup Group, PayPeriod P1, PayPeriod P2, WorkerPayrollProfile Profile, SalaryComponent Basic, SalaryComponent Housing, SalaryComponent Paye, SalaryComponent NapsaEe, SalaryComponent NhimaEe, SalaryComponent NapsaEr, SalaryComponent NhimaEr)>
        SeedStackAsync(HrmDbContext ctx)
    {
        var basic = Comp("basic", "earning", "fixed", statutory: false, priority: 10);
        var housing = Comp("housing", "earning", "fixed", statutory: false, priority: 11);
        var paye = Comp("paye", "tax", "slab", tied: "gross", statutory: true, priority: 90);
        var napsaEe = Comp("napsa-ee", "deduction", "percent-of", tied: "gross", rate: 5m, statutory: true, priority: 91);
        var nhimaEe = Comp("nhima-ee", "deduction", "percent-of", tied: "basic", rate: 1m, statutory: true, priority: 92);
        var napsaEr = Comp("napsa-er", "employer-contribution", "percent-of", tied: "gross", rate: 5m, statutory: true, priority: 93);
        var nhimaEr = Comp("nhima-er", "employer-contribution", "percent-of", tied: "basic", rate: 1m, statutory: true, priority: 94);
        foreach (var c in new[] { basic, housing, paye, napsaEe, nhimaEe, napsaEr, nhimaEr }) ctx.SalaryComponents.Add(c);

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
            new ContributionRule { Code = "napsa-ee", Name = "NAPSA EE", Payer = "employee", Rate = 5m, Ceiling = 1861.80m, TiedComponentCode = "gross", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new ContributionRule { Code = "napsa-er", Name = "NAPSA ER", Payer = "employer", Rate = 5m, Ceiling = 1861.80m, TiedComponentCode = "gross", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new ContributionRule { Code = "nhima-ee", Name = "NHIMA EE", Payer = "employee", Rate = 1m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new ContributionRule { Code = "nhima-er", Name = "NHIMA ER", Payer = "employer", Rate = 1m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
        };
        foreach (var r in rules) ctx.ContributionRules.Add(r);

        var group = new PayGroup { Code = "TEST-MONTHLY", Name = "Test Monthly", Frequency = "monthly", Currency = "ZMW", CalendarDayOfMonth = 25 };
        ctx.PayGroups.Add(group);

        var p1 = new PayPeriod { PayGroupId = group.Id, PeriodLabel = "Jun 2026", StartDate = DateOnly.FromDateTime(new DateTime(2026, 6, 1)), EndDate = DateOnly.FromDateTime(new DateTime(2026, 6, 30)), CutoffDate = DateOnly.FromDateTime(new DateTime(2026, 6, 20)), PayDate = DateOnly.FromDateTime(new DateTime(2026, 6, 30)), IsCurrent = false };
        var p2 = new PayPeriod { PayGroupId = group.Id, PeriodLabel = "Jul 2026", StartDate = DateOnly.FromDateTime(new DateTime(2026, 7, 1)), EndDate = DateOnly.FromDateTime(new DateTime(2026, 7, 31)), CutoffDate = DateOnly.FromDateTime(new DateTime(2026, 7, 20)), PayDate = DateOnly.FromDateTime(new DateTime(2026, 7, 31)), IsCurrent = true };
        ctx.PayPeriods.Add(p1); ctx.PayPeriods.Add(p2);

        var worker = TestWorker();
        ctx.Workers.Add(worker);

        var structure = new SalaryStructure { Code = "TEST-STANDARD", Name = "Test Standard" };
        ctx.SalaryStructures.Add(structure);

        var profile = new WorkerPayrollProfile { WorkerId = worker.Id, PayGroupId = group.Id, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), StructureId = structure.Id };
        profile.ComponentValues.Add(new WorkerComponentValue { ComponentId = basic.Id, Component = basic, Amount = 25000m });
        profile.ComponentValues.Add(new WorkerComponentValue { ComponentId = housing.Id, Component = housing, Amount = 5000m });
        ctx.WorkerPayrollProfiles.Add(profile);
        await ctx.SaveChangesAsync();
        return (group, p1, p2, profile, basic, housing, paye, napsaEe, nhimaEe, napsaEr, nhimaEr);
    }

    /// Runs the full lifecycle for one period: create → lock → calculate → approve → release.
    private static async Task<Guid> RunLifecycleAsync(PayrollServiceImpl service, PayrollRunCreate create)
    {
        var run = await service.CreateRunAsync(create, CancellationToken.None);
        run = await service.LockRunAsync(run.Id, CancellationToken.None);
        run = await service.CalculateRunAsync(run.Id, CancellationToken.None);
        run = await service.ApproveRunAsync(run.Id, "ok", CancellationToken.None);
        run = await service.ReleaseRunAsync(run.Id, CancellationToken.None);
        return run.Id;
    }

    [Fact]
    public async Task CalculateRun_IncludesPayrollBenefitAllowancesAsEarnings()
    {
        var (service, ctx) = Build();
        var (group, _, p2, profile, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);

        var lunch = new BenefitType
        {
            Code = "lunch",
            Name = "Lunch",
            AnnualCap = 12000m,
            RequiresEvidence = false,
            IncludeInPayroll = true,
            IsActive = true,
        };
        ctx.BenefitTypes.Add(lunch);
        ctx.WorkerBenefitAllowances.Add(new WorkerBenefitAllowance
        {
            WorkerId = profile.WorkerId,
            BenefitTypeId = lunch.Id,
            BenefitType = lunch,
            AnnualAmount = 12000m,
            Year = 2026,
        });
        await ctx.SaveChangesAsync();

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await service.LockRunAsync(run.Id, CancellationToken.None);
        await service.CalculateRunAsync(run.Id, CancellationToken.None);

        var line = await ctx.PayrollRunLines
            .Include(l => l.Components)
            .SingleAsync(l => l.RunId == run.Id);
        Assert.Equal(31000m, line.GrossPay);
        var benefit = line.Components.Single(c => c.ComponentCode == "benefit-lunch");
        Assert.Equal("earning", benefit.ComponentType);
        Assert.Equal(1000m, benefit.Amount);
        Assert.Contains("annual allowance K12,000.00 / 12 months", benefit.Explanation);
    }

    [Fact]
    public async Task CalculateRun_ExcludesArchivedWorkersEvenWhenTheirPayrollProfileRemains()
    {
        var (service, ctx) = Build();
        var (group, _, p2, activeProfile, basic, _, _, _, _, _, _) = await SeedStackAsync(ctx);
        var archivedWorker = TestWorker("ARCHIVED-001");
        archivedWorker.Status = "archived";
        archivedWorker.IsArchived = true;
        ctx.Workers.Add(archivedWorker);

        var archivedProfile = new WorkerPayrollProfile
        {
            WorkerId = archivedWorker.Id,
            PayGroupId = group.Id,
            EffectiveFrom = activeProfile.EffectiveFrom,
            StructureId = activeProfile.StructureId,
        };
        archivedProfile.ComponentValues.Add(new WorkerComponentValue
        {
            ComponentId = basic.Id,
            Component = basic,
            Amount = 99999m,
        });
        ctx.WorkerPayrollProfiles.Add(archivedProfile);
        await ctx.SaveChangesAsync();

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await service.LockRunAsync(run.Id, CancellationToken.None);
        var calculated = await service.CalculateRunAsync(run.Id, CancellationToken.None);
        var lines = await ctx.PayrollRunLines.Where(line => line.RunId == run.Id).ToListAsync();

        Assert.Equal(1, calculated.EmployeeCount);
        Assert.Single(lines);
        Assert.Equal(activeProfile.WorkerId, lines[0].WorkerId);
        Assert.DoesNotContain(lines, line => line.WorkerId == archivedWorker.Id);
    }

    [Fact]
    public async Task MissingBankDetails_WarnsButDoesNotBlockPayrollCalculationOrApproval()
    {
        var (service, ctx) = Build();
        var (group, _, p2, _, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None, "preparer");
        await service.LockRunAsync(run.Id, CancellationToken.None, "preparer");

        var readiness = await service.GetCalculationReadinessAsync(run.Id, CancellationToken.None);
        var bankCheck = Assert.Single(readiness.Checks.Where(check => check.Id == "bank-details"));
        Assert.Equal("warn", bankCheck.State);
        Assert.True(readiness.Ready);

        var calculated = await service.CalculateRunAsync(run.Id, CancellationToken.None, "preparer");
        var line = await ctx.PayrollRunLines.SingleAsync(item => item.RunId == run.Id);
        Assert.False(line.HasException);
        Assert.Equal(0, calculated.ExceptionCount);

        var approved = await service.ApproveRunAsync(run.Id, "Payment details will be completed before bank-file generation.", CancellationToken.None, "reviewer");
        Assert.Equal("approved", approved.Status);
    }

    [Fact]
    public async Task CalculateRun_UsesRecordedOvertimeDivisorForWatchpersonGuard()
    {
        var (service, ctx) = Build(tenant: "payroll-overtime-divisor");
        var (group, _, p2, profile, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);
        profile.OvertimeCategory = "watchperson-guard";
        profile.WeeklyOvertimeThresholdHours = 60m;
        profile.MonthlyOvertimeDivisor = 240m;
        ctx.AttendanceRecords.Add(new AttendanceRecord
        {
            WorkerId = profile.WorkerId,
            WorkDate = new DateOnly(2026, 7, 10),
            ClockIn = new TimeOnly(8, 0),
            ClockOut = new TimeOnly(18, 0),
            Source = "device-import",
            DerivedStatus = "present",
            TotalHours = 10m,
            RegularHours = 8m,
            OvertimeHours = 2m,
            OvertimeMultiplier = 1.5m,
            OvertimeHourlyDivisor = 240m,
            OvertimeRuleCode = "watchperson-guard",
            OvertimeStatus = "approved",
        });
        await ctx.SaveChangesAsync();

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await service.LockRunAsync(run.Id, CancellationToken.None);
        await service.CalculateRunAsync(run.Id, CancellationToken.None);

        var line = await ctx.PayrollRunLines
            .Include(l => l.Components)
            .SingleAsync(l => l.RunId == run.Id);
        var overtime = line.Components.Single(c => c.ComponentCode == "overtime");
        Assert.Equal(312.50m, overtime.Amount);
        Assert.Equal(30312.50m, line.GrossPay);
        Assert.Contains("configured hourly divisor", overtime.Explanation);
    }

    [Fact]
    public async Task ReleaseRun_BlocksPayrollOfficerWhoApprovedSameRun()
    {
        var (service, ctx) = Build(tenant: "payroll-self-release", roles: ["payroll"]);
        var (group, _, p2, _, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None, "preparer");
        await service.LockRunAsync(run.Id, CancellationToken.None, "preparer");
        await service.CalculateRunAsync(run.Id, CancellationToken.None, "preparer");
        var line = await ctx.PayrollRunLines.SingleAsync(l => l.RunId == run.Id);
        if (line.HasException)
            await service.DecideExceptionAsync(run.Id, line.Id,
                new PayrollExceptionDecisionRequest("waived", "Test release guard after exception review"), CancellationToken.None, "payroll-user");
        await service.ApproveRunAsync(run.Id, "reviewed", CancellationToken.None, "payroll-user");

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            service.ReleaseRunAsync(run.Id, CancellationToken.None, "payroll-user"));
        Assert.Equal("run-self-release", error.Code);
    }

    [Fact]
    public async Task ReleaseRun_AllowsHrAdminToApproveAndReleaseSameRun()
    {
        var (service, ctx) = Build(tenant: "admin-self-release", roles: ["hr_admin"]);
        var (group, _, p2, _, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);

        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None, "preparer");
        await service.LockRunAsync(run.Id, CancellationToken.None, "preparer");
        await service.CalculateRunAsync(run.Id, CancellationToken.None, "preparer");
        var line = await ctx.PayrollRunLines.SingleAsync(l => l.RunId == run.Id);
        if (line.HasException)
            await service.DecideExceptionAsync(run.Id, line.Id,
                new PayrollExceptionDecisionRequest("waived", "Top admin reviewed generated exception"), CancellationToken.None, "admin-user");
        await service.ApproveRunAsync(run.Id, "top admin reviewed", CancellationToken.None, "admin-user");
        var released = await service.ReleaseRunAsync(run.Id, CancellationToken.None, "admin-user");

        Assert.Equal("released", released.Status);
        Assert.Equal("admin-user", released.ApprovedBySubjectId);
        Assert.Equal("admin-user", released.ReleasedBySubjectId);
    }

    [Fact]
    public async Task Ytd_AccumulatesAcrossReleasedRunsInSameTaxYear()
    {
        var (service, ctx) = Build();
        var (group, p1, p2, _, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);
        var repo = new PayrollRepository(ctx);

        await RunLifecycleAsync(service, new PayrollRunCreate(p1.Id, group.Id));

        // July: gross 30,000 with deductions 10,476 (PAYE 8,726 + NAPSA 1,500 + NHIMA 250).
        await RunLifecycleAsync(service, new PayrollRunCreate(p2.Id, group.Id));

        var slips = await ctx.Payslips.ToListAsync();
        var july = slips.Single(s => s.YtdGross == "60000.00");
        // YTD includes June's released run: gross 60,000, deductions 20,952, net 39,048.
        Assert.Equal("60000.00", july.YtdGross);
        Assert.Equal("20952.00", july.YtdTax);
        Assert.Equal("39048.00", july.YtdNet);

        var june = slips.Single(s => s.YtdGross == "30000.00");
        Assert.Equal("30000.00", june.YtdGross);
    }

    [Fact]
    public async Task Reversal_CreatesReversalRunAndOriginalStatusMovesToReversed()
    {
        var (service, ctx) = Build();
        var (group, _, p2, _, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);

        var runId = await RunLifecycleAsync(service, new PayrollRunCreate(p2.Id, group.Id));
        var slipCount = await ctx.Payslips.CountAsync();
        Assert.Equal(1, slipCount);

        var reversed = await service.ReverseRunAsync(runId, new PayrollRunReverseCreate("payroll correction"), CancellationToken.None);
        Assert.True(reversed.IsReversal);
        Assert.Equal(runId, reversed.ReversesRunId);
        Assert.Equal("draft", reversed.Status);

        var original = await ctx.PayrollRuns.FirstAsync(r => r.Id == runId);
        Assert.Equal("reversed", original.Status);

        // Idempotency: a second reversal attempt is rejected.
        await Assert.ThrowsAsync<DomainException>(() => service.ReverseRunAsync(runId, new PayrollRunReverseCreate(), CancellationToken.None));
    }

    [Fact]
    public async Task Reversal_Release_SupersedesOriginalPayslipsAndClosesOriginal()
    {
        var (service, ctx) = Build();
        var (group, _, p2, _, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);
        var runId = await RunLifecycleAsync(service, new PayrollRunCreate(p2.Id, group.Id));
        var originalSlip = (await ctx.Payslips.ToListAsync()).Single();

        var reversed = await service.ReverseRunAsync(runId, new PayrollRunReverseCreate(), CancellationToken.None);
        var revId = reversed.Id;

        // Release the reversal: original payslip gets superseded, original run closes.
        await service.LockRunAsync(revId, CancellationToken.None);
        await service.CalculateRunAsync(revId, CancellationToken.None);
        await service.ApproveRunAsync(revId, null, CancellationToken.None);
        await service.ReleaseRunAsync(revId, CancellationToken.None);

        await ctx.Entry(originalSlip).ReloadAsync();
        Assert.Equal("superseded", originalSlip.Status);
        var original = await ctx.PayrollRuns.FirstAsync(r => r.Id == runId);
        Assert.Equal("closed", original.Status);

        // A new (replacement) payslip was generated for the same worker.
        var slips = await ctx.Payslips.Where(s => s.WorkerId == originalSlip.WorkerId).ToListAsync();
        Assert.Equal(2, slips.Count);
        var current = slips.Single(s => s.Status == "final");
        Assert.Equal(originalSlip.Id, current.SupersedesId);
    }

    [Fact]
    public async Task Ytd_ExcludesReversedRuns()
    {
        var (service, ctx) = Build();
        var (group, _, p2, _, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);
        var runId = await RunLifecycleAsync(service, new PayrollRunCreate(p2.Id, group.Id));

        // Reverse and release — the reversed run must not count towards YTD.
        var reversed = await service.ReverseRunAsync(runId, new PayrollRunReverseCreate(), CancellationToken.None);
        await service.LockRunAsync(reversed.Id, CancellationToken.None);
        await service.CalculateRunAsync(reversed.Id, CancellationToken.None);
        await service.ApproveRunAsync(reversed.Id, null, CancellationToken.None);
        await service.ReleaseRunAsync(reversed.Id, CancellationToken.None);

        // Now run a fresh replacement run in the same period and check its YTD
        // counts only the fresh run (the reversed original contributes nothing).
        var repo = new PayrollRepository(ctx);
        var fresh = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await service.LockRunAsync(fresh.Id, CancellationToken.None);
        await service.CalculateRunAsync(fresh.Id, CancellationToken.None);
        await service.ApproveRunAsync(fresh.Id, null, CancellationToken.None);
        await service.ReleaseRunAsync(fresh.Id, CancellationToken.None);

        var slips = (await ctx.Payslips.ToListAsync()).OrderByDescending(s => s.ReleasedAt).ToList();
        Assert.Equal(3, slips.Count); // original + reversal + replacement
        var replacement = slips.First();
        // YTD = this run only (prior run reversed): 30,000 gross.
        Assert.Equal("30000.00", replacement.YtdGross);
        Assert.Equal("10476.00", replacement.YtdTax);
        Assert.Equal("19524.00", replacement.YtdNet);
    }

    [Fact]
    public async Task LiabilityReport_AggregatesStatutoryComponentsByPayer()
    {
        var (service, ctx) = Build();
        var (group, _, p2, _, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);
        var runId = await RunLifecycleAsync(service, new PayrollRunCreate(p2.Id, group.Id));

        var report = await service.EmployerLiabilityReportAsync(p2.Id, CancellationToken.None);
        Assert.Equal("Jul 2026", report.PeriodLabel);
        Assert.Equal("2026", report.TaxYear);
        Assert.Equal(5, report.Rows.Count); // paye + napsa-ee + nhima-ee (employee), napsa-er + nhima-er (employer)

        var paye = report.Rows.Single(r => r.ComponentCode == "paye");
        Assert.Equal(8726m, paye.TotalAmount);
        var napsaEr = report.Rows.Single(r => r.ComponentCode == "napsa-er");
        Assert.Equal(1500m, napsaEr.TotalAmount);
        Assert.Equal("employer", napsaEr.Payer);
        // Employer liability total = NAPSA ER 1500 + NHIMA ER 250 = 1750.
        Assert.Equal(1750m, report.TotalStatutory);
    }

    [Fact]
    public async Task LiabilityReport_ExcludesReversedRuns()
    {
        var (service, ctx) = Build();
        var (group, _, p2, _, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);
        var runId = await RunLifecycleAsync(service, new PayrollRunCreate(p2.Id, group.Id));

        var reversed = await service.ReverseRunAsync(runId, new PayrollRunReverseCreate(), CancellationToken.None);
        await service.LockRunAsync(reversed.Id, CancellationToken.None);
        await service.CalculateRunAsync(reversed.Id, CancellationToken.None);
        await service.ApproveRunAsync(reversed.Id, null, CancellationToken.None);
        await service.ReleaseRunAsync(reversed.Id, CancellationToken.None);

        var report = await service.EmployerLiabilityReportAsync(p2.Id, CancellationToken.None);
        Assert.Empty(report.Rows);
        Assert.Equal(0m, report.TotalStatutory);
    }

    [Fact]
    public async Task PayslipDocument_StoresGeneratedUrlOnPayslip()
    {
        var (service, ctx) = Build("https://storage.example/doc.pdf");
        var (group, _, p2, _, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);
        await RunLifecycleAsync(service, new PayrollRunCreate(p2.Id, group.Id));

        var slip = (await ctx.Payslips.ToListAsync()).Single();
        Assert.Null(slip.DocumentUrl);

        var updated = await service.GeneratePayslipDocumentAsync(slip.Id, CancellationToken.None);
        Assert.Equal("https://storage.example/doc.pdf", updated.DocumentUrl);
        Assert.Equal("final", updated.Status);

        // Idempotent: document URL can be re-generated.
        var updated2 = await service.GeneratePayslipDocumentAsync(slip.Id, CancellationToken.None);
        Assert.Equal("https://storage.example/doc.pdf", updated2.DocumentUrl);
    }

    [Fact]
    public async Task ReversalOfNonReleasedRun_IsRejected()
    {
        var (service, ctx) = Build();
        var (group, _, p2, _, _, _, _, _, _, _, _) = await SeedStackAsync(ctx);
        var run = await service.CreateRunAsync(new PayrollRunCreate(p2.Id, group.Id), CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(() => service.ReverseRunAsync(run.Id, new PayrollRunReverseCreate(), CancellationToken.None));
    }
}


/// <summary>M34: admin payslip surface — list by run, bulk PDF generation, preview bytes.</summary>
public class M34RunPayslipTests
{
    private sealed class FakeDocumentService(string url) : IPayslipDocumentService
    {
        public Task<string> GenerateAsync(Payslip slip, PayrollRunLine line, CancellationToken ct)
            => Task.FromResult(url);
    }

    private static SalaryComponent Comp(string code, string type, string basis, string? tied = null, decimal? rate = null, bool statutory = false, int priority = 100)
        => new() { Code = code, Name = code, ComponentType = type, CalculationBasis = basis, BasisComponentCode = tied,
            Rate = rate, FixedAmount = null, Ceiling = null, IsTaxable = true, IsStatutory = statutory, Priority = priority,
            Version = 1, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)) };

    private static Worker TestWorker(string empNo = "T001") => new()
    {
        EmployeeNo = empNo, FirstName = "Test", LastName = "Worker", WorkerType = "employee", Status = "active",
        Nrc = "123456/10/1", Tpin = "1000000001", NapsaNumber = "NAPSA-1", NhimaNumber = "NHIMA-1",
    };

    private static async Task<(PayrollServiceImpl service, HrmDbContext ctx)> BuildWithStackAsync(string docUrl = "https://storage.example/doc.pdf")
    {
        var ctx = TestDbContextFactory.Create("m34-tenant");
        var basic = Comp("basic", "earning", "fixed", statutory: false, priority: 10);
        var paye = Comp("paye", "tax", "slab", tied: "gross", statutory: true, priority: 90);
        var napsaEe = Comp("napsa-ee", "deduction", "percent-of", tied: "gross", rate: 5m, statutory: true, priority: 91);
        var nhimaEe = Comp("nhima-ee", "deduction", "percent-of", tied: "basic", rate: 1m, statutory: true, priority: 92);
        foreach (var c in new[] { basic, paye, napsaEe, nhimaEe }) ctx.SalaryComponents.Add(c);
        foreach (var s in new[]
        {
            new TaxSlab { TaxYear = "2026", MinAmount = 0m, MaxAmount = 5100m, Rate = 0m, Sequence = 10, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new TaxSlab { TaxYear = "2026", MinAmount = 5100m, MaxAmount = 7100m, Rate = 20m, Sequence = 20, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new TaxSlab { TaxYear = "2026", MinAmount = 7100m, MaxAmount = 9200m, Rate = 30m, Sequence = 30, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new TaxSlab { TaxYear = "2026", MinAmount = 9200m, MaxAmount = null, Rate = 37m, Sequence = 40, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
        }) ctx.TaxSlabs.Add(s);
        foreach (var r in new[]
        {
            new ContributionRule { Code = "napsa-ee", Name = "NAPSA EE", Payer = "employee", Rate = 5m, Ceiling = 1861.80m, TiedComponentCode = "gross", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
            new ContributionRule { Code = "nhima-ee", Name = "NHIMA EE", Payer = "employee", Rate = 1m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
        }) ctx.ContributionRules.Add(r);

        var group = new PayGroup { Code = "M34-MONTHLY", Name = "M34 Monthly", Frequency = "monthly", Currency = "ZMW", CalendarDayOfMonth = 25 };
        ctx.PayGroups.Add(group);

        var p1 = new PayPeriod { PayGroupId = group.Id, PeriodLabel = "Aug 2026", StartDate = DateOnly.FromDateTime(new DateTime(2026, 8, 1)), EndDate = DateOnly.FromDateTime(new DateTime(2026, 8, 31)), CutoffDate = DateOnly.FromDateTime(new DateTime(2026, 8, 20)), PayDate = DateOnly.FromDateTime(new DateTime(2026, 8, 31)), IsCurrent = true };
        ctx.PayPeriods.Add(p1);

        var w1 = TestWorker("M34-001");
        var w2 = TestWorker("M34-002");
        ctx.Workers.Add(w1);
        ctx.Workers.Add(w2);

        var structure = new SalaryStructure { Code = "M34-STD", Name = "M34 Standard" };
        ctx.SalaryStructures.Add(structure);

        foreach (var w in new[] { w1, w2 })
        {
            var profile = new WorkerPayrollProfile { WorkerId = w.Id, PayGroupId = group.Id, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), StructureId = structure.Id };
            profile.ComponentValues.Add(new WorkerComponentValue { ComponentId = basic.Id, Component = basic, Amount = 25000m });
            ctx.WorkerPayrollProfiles.Add(profile);
        }
        await ctx.SaveChangesAsync();

        var repo = new PayrollRepository(ctx);
        var service = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakeDocumentService(docUrl));
        return (service, ctx);
    }

    private static async Task<Guid> RunLifecycleAsync(PayrollServiceImpl service, Guid payPeriodId, Guid payGroupId)
    {
        var run = await service.CreateRunAsync(new PayrollRunCreate(payPeriodId, payGroupId), CancellationToken.None);
        run = await service.LockRunAsync(run.Id, CancellationToken.None);
        run = await service.CalculateRunAsync(run.Id, CancellationToken.None);
        run = await service.ApproveRunAsync(run.Id, "ok", CancellationToken.None);
        run = await service.ReleaseRunAsync(run.Id, CancellationToken.None);
        return run.Id;
    }

    [Fact]
    public async Task ListRunPayslips_ReturnsAllSlipsForTheRun()
    {
        var (service, ctx) = await BuildWithStackAsync();
        var group = await ctx.PayGroups.FirstAsync();
        var period = await ctx.PayPeriods.FirstAsync();
        var runId = await RunLifecycleAsync(service, period.Id, group.Id);

        var slips = await service.ListRunPayslipsAsync(runId, CancellationToken.None);
        Assert.Equal(2, slips.Count); // two workers
        Assert.All(slips, s => Assert.Equal("final", s.Status));
        Assert.All(slips, s => Assert.NotNull(s.Id));
        Assert.All(slips, s => Assert.NotNull(s.PayslipNo));
        Assert.All(slips, s => Assert.True(s.NetPay > 0));
    }

    [Fact]
    public async Task GenerateAllPayslipDocuments_SetsDocumentUrlOnAllSlips()
    {
        var (service, ctx) = await BuildWithStackAsync();
        var group = await ctx.PayGroups.FirstAsync();
        var period = await ctx.PayPeriods.FirstAsync();
        var runId = await RunLifecycleAsync(service, period.Id, group.Id);

        // Before generation: no DocumentUrl.
        var before = await ctx.Payslips.Where(s => s.RunLine.RunId == runId).ToListAsync();
        Assert.All(before, s => Assert.Null(s.DocumentUrl));

        var updated = await service.GenerateAllPayslipDocumentsAsync(runId, CancellationToken.None);
        Assert.Equal(2, updated.Count);
        Assert.All(updated, s => Assert.Equal("https://storage.example/doc.pdf", s.DocumentUrl));

        // Idempotent: second call returns same results without changing data.
        var updated2 = await service.GenerateAllPayslipDocumentsAsync(runId, CancellationToken.None);
        Assert.Equal(updated.Count, updated2.Count);
        Assert.All(updated2, s => Assert.Equal("https://storage.example/doc.pdf", s.DocumentUrl));
    }

    [Fact]
    public async Task ListRunPayslips_WithNoRelease_ReturnsEmpty()
    {
        var (service, ctx) = await BuildWithStackAsync();
        var group = await ctx.PayGroups.FirstAsync();
        var period = await ctx.PayPeriods.FirstAsync();

        // Create a run but don't release it.
        var run = await service.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), CancellationToken.None);
        var slips = await service.ListRunPayslipsAsync(run.Id, CancellationToken.None);
        Assert.Empty(slips);
    }

    [Fact]
    public async Task GenerateAllPayslipDocuments_WithNoRelease_Throws()
    {
        var (service, ctx) = await BuildWithStackAsync();
        var group = await ctx.PayGroups.FirstAsync();
        var period = await ctx.PayPeriods.FirstAsync();

        var run = await service.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(() =>
            service.GenerateAllPayslipDocumentsAsync(run.Id, CancellationToken.None));
    }
}
