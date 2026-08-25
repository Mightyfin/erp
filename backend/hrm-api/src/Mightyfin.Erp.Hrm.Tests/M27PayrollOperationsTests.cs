using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public class M27PayrollOperationsTests
{
    [Fact]
    public async Task RunPreflight_ReturnsWarningsForPaymentAndStatutoryReadiness()
    {
        var (service, ctx) = PayrollEngineTests.Build(tenant: "m27-run-preflight");
        var (group, _, period, profile, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);
        var worker = await ctx.Workers.SingleAsync(w => w.Id == profile.WorkerId);
        worker.Tpin = null;
        await ctx.SaveChangesAsync();

        var preflight = await service.GetRunPreflightAsync(new PayrollRunCreate(period.Id, group.Id), default);

        Assert.True(preflight.Ready);
        Assert.Equal(1, preflight.IncludedWorkerCount);
        Assert.Contains(preflight.Checks, c => c.Id == "bank" && c.State == "warn");
        Assert.Contains(preflight.Checks, c => c.Id == "statutory" && c.State == "warn");
    }

    [Fact]
    public async Task RunPreflight_BlocksRunCreationWhenPayGroupHasNoPopulation()
    {
        var (service, ctx) = PayrollEngineTests.Build(tenant: "m27-run-preflight-empty");
        await PayrollEngineTests.SeedStackAsync(ctx);
        var emptyGroup = new PayGroup { Code = "EMPTY", Name = "Empty Group", Frequency = "monthly", Currency = "ZMW", CalendarDayOfMonth = 25 };
        ctx.PayGroups.Add(emptyGroup);
        var emptyPeriod = new PayPeriod
        {
            PayGroupId = emptyGroup.Id,
            PeriodLabel = "Aug 2026",
            StartDate = DateOnly.FromDateTime(new DateTime(2026, 8, 1)),
            EndDate = DateOnly.FromDateTime(new DateTime(2026, 8, 31)),
            CutoffDate = DateOnly.FromDateTime(new DateTime(2026, 8, 20)),
            PayDate = DateOnly.FromDateTime(new DateTime(2026, 8, 31)),
            Status = "open",
            IsCurrent = true,
        };
        ctx.PayPeriods.Add(emptyPeriod);
        await ctx.SaveChangesAsync();

        var preflight = await service.GetRunPreflightAsync(new PayrollRunCreate(emptyPeriod.Id, emptyGroup.Id), default);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateRunAsync(new PayrollRunCreate(emptyPeriod.Id, emptyGroup.Id), default, "preparer"));

        Assert.False(preflight.Ready);
        Assert.Contains(preflight.Checks, c => c.Id == "population" && c.State == "fail");
        Assert.Equal("payroll-run-preflight-failed", ex.Code);
    }

    [Fact]
    public async Task RunApproval_EnforcesSegregation_AndOutstandingExceptionDecision()
    {
        var (service, ctx) = PayrollEngineTests.Build(tenant: "m27-controls");
        var (group, _, period, _, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);
        var run = await service.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "preparer-1");
        await service.LockRunAsync(run.Id, default, "preparer-1");
        await service.CalculateRunAsync(run.Id, default, "preparer-1");

        var selfApproval = await Assert.ThrowsAsync<DomainException>(() =>
            service.ApproveRunAsync(run.Id, "looks fine", default, "preparer-1"));
        Assert.Equal("run-self-approval", selfApproval.Code);

        var openException = await Assert.ThrowsAsync<DomainException>(() =>
            service.ApproveRunAsync(run.Id, "reviewed", default, "approver-2"));
        Assert.Equal("run-exceptions-open", openException.Code);

        var line = await ctx.PayrollRunLines.SingleAsync(l => l.RunId == run.Id);
        await service.DecideExceptionAsync(run.Id, line.Id,
            new PayrollExceptionDecisionRequest("waived", "Bank instruction will be handled manually"), default, "approver-2");
        var approved = await service.ApproveRunAsync(run.Id, "controlled exception accepted", default, "approver-2");

        Assert.Equal("approved", approved.Status);
        Assert.Equal("approver-2", approved.ApprovedBySubjectId);
        Assert.Equal(0, approved.ExceptionCount);
    }

    [Fact]
    public async Task Correction_UpdatesControlTotals_AndCreatesAuditEvent()
    {
        var (service, ctx) = PayrollEngineTests.Build(tenant: "m27-correction");
        var (group, _, period, _, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);
        var run = await service.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "preparer");
        await service.LockRunAsync(run.Id, default, "preparer");
        run = await service.CalculateRunAsync(run.Id, default, "preparer");
        var line = await ctx.PayrollRunLines.SingleAsync(l => l.RunId == run.Id);

        var corrected = await service.ApplyCorrectionAsync(run.Id, line.Id,
            new PayrollCorrectionRequest("basic", 26_000m, "Approved salary amendment"), default, "preparer");

        Assert.Equal(run.TotalGross + 1_000m, corrected.TotalGross);
        Assert.Contains(await service.GetRunAuditAsync(run.Id, default), e => e.Action == "correction-applied");
    }

    [Fact]
    public async Task PaymentWorkflow_GeneratesApprovesReleasesReconciles_AndExportsAudit()
    {
        var (service, ctx) = PayrollEngineTests.Build(tenant: "m27-payment");
        var (group, _, period, profile, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);
        ctx.WorkerBankDetails.Add(new WorkerBankDetail
        {
            WorkerId = profile.WorkerId, BankName = "Zanaco", BranchCode = "010001",
            AccountName = "Test Worker", AccountNumber = "001234567890", IsPrimary = true
        });
        await ctx.SaveChangesAsync();

        var run = await service.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "preparer");
        await service.LockRunAsync(run.Id, default, "preparer");
        await service.CalculateRunAsync(run.Id, default, "preparer");
        var line = await ctx.PayrollRunLines.SingleAsync(l => l.RunId == run.Id);
        if (line.HasException)
            await service.DecideExceptionAsync(run.Id, line.Id,
                new PayrollExceptionDecisionRequest("waived", "Test readiness after exception review"), default, "hr-approver");
        await service.ApproveRunAsync(run.Id, "reviewed", default, "hr-approver");
        run = await service.ReleaseRunAsync(run.Id, default, "payroll-releaser");

        var readiness = await service.GetPaymentReadinessAsync(run.Id, default);
        Assert.True(readiness.Ready);
        Assert.Equal(run.TotalNet, readiness.TotalNet);
        Assert.Empty(readiness.Issues);

        run = await service.GeneratePaymentFileAsync(run.Id, default, "payroll-releaser");
        var csv = await service.DownloadPaymentFileAsync(run.Id, default);
        Assert.Contains("001234567890", csv);
        Assert.Contains(run.PaymentFileReference!, csv);

        var selfApproval = await Assert.ThrowsAsync<DomainException>(() =>
            service.ApprovePaymentFileAsync(run.Id, new(), default, "payroll-releaser"));
        Assert.Equal("payment-self-approval", selfApproval.Code);

        await service.ApprovePaymentFileAsync(run.Id, new("finance reviewed"), default, "finance-approver");
        var generatorRelease = await Assert.ThrowsAsync<DomainException>(() =>
            service.ReleasePaymentFileAsync(run.Id, default, "payroll-releaser"));
        Assert.Equal("payment-generator-release", generatorRelease.Code);

        await service.ReleasePaymentFileAsync(run.Id, default, "treasury-releaser");
        run = await service.ReconcileRunAsync(run.Id,
            new PayrollReconciliationRequest("BANK-ACK-009", run.TotalNet, "bank accepted all rows"), default, "reconciler");

        Assert.Equal("closed", run.Status);
        Assert.Equal("reconciled", run.PaymentStatus);
        Assert.Equal("BANK-ACK-009", run.ReconciliationReference);
        var auditCsv = await service.ExportRunAuditAsync(run.Id, default);
        Assert.Contains("payment-file-generated", auditCsv);
        Assert.Contains("reconciled-and-closed", auditCsv);
    }

    [Fact]
    public async Task PaymentReadiness_ListsWorkersMissingPrimaryBankDetails()
    {
        var (service, ctx) = PayrollEngineTests.Build(tenant: "m27-payment-readiness");
        var (group, _, period, _, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);

        var run = await service.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), default, "preparer");
        await service.LockRunAsync(run.Id, default, "preparer");
        await service.CalculateRunAsync(run.Id, default, "preparer");
        var line = await ctx.PayrollRunLines.SingleAsync(l => l.RunId == run.Id);
        if (line.HasException)
            await service.DecideExceptionAsync(run.Id, line.Id,
                new PayrollExceptionDecisionRequest("waived", "Test readiness after exception review"), default, "hr-approver");
        await service.ApproveRunAsync(run.Id, "reviewed", default, "hr-approver");
        run = await service.ReleaseRunAsync(run.Id, default, "payroll-releaser");

        var readiness = await service.GetPaymentReadinessAsync(run.Id, default);

        Assert.False(readiness.Ready);
        Assert.Equal(1, readiness.PayableCount);
        Assert.Equal(run.TotalNet, readiness.TotalNet);
        Assert.Single(readiness.Issues);
        Assert.Equal("Primary bank details are missing.", readiness.Issues[0].Issue);
    }
}
