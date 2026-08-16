using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class M36GoLiveReadinessTests
{
    [Fact]
    public async Task Readiness_FailsClosed_UntilEveryGateAndSignoffPasses()
    {
        await using var db = TestDbContextFactory.Create("m36-ready");
        var service = new GoLiveReadinessService(db, new PermissiveAuthz());
        var initial = await service.GetAsync(default);
        Assert.Equal("blocked", initial.Decision);
        Assert.False(initial.CanGoLive);
        Assert.Contains("Worker statutory identity", initial.Blockers);
        var earlyApproval = await Assert.ThrowsAsync<DomainException>(() => service.RecordSignoffAsync(
            "hr-owner", new("approved", "Too early."), "hr-owner", default));
        Assert.Equal("go-live-not-ready", earlyApproval.Code);

        var entity = new LegalEntity
        {
            Code = "MFL", RegisteredName = "Mighty Finance Limited", IsDefault = true,
            Tpin = "1000000000", NapsaEmployerRef = "NAPSA-1", NhimaEmployerRef = "NHIMA-1",
        };
        var group = new PayGroup { Code = "MONTHLY", Name = "Monthly", IsDefault = true };
        var period = new PayPeriod
        {
            PayGroupId = group.Id, PeriodLabel = "Rehearsal", StartDate = new(2026, 7, 1),
            EndDate = new(2026, 7, 31), CutoffDate = new(2026, 7, 25), PayDate = new(2026, 7, 31),
            Status = "closed",
        };
        db.AddRange(entity, group, period,
            new TaxSlab { TaxYear = "2026", IsActive = true, EffectiveFrom = new(2026, 1, 1) },
            new ContributionRule { Code = "napsa-ee", Name = "NAPSA", Payer = "employee", IsActive = true, EffectiveFrom = new(2026, 1, 1) },
            new ContributionRule { Code = "nhima-ee", Name = "NHIMA", Payer = "employee", IsActive = true, EffectiveFrom = new(2026, 1, 1) },
            new Worker
            {
                EmployeeNo = "M36-001", FirstName = "Ready", LastName = "Worker", Status = "active",
                Nrc = "111111/11/1", Tpin = "2000000000", NapsaNumber = "N1", NhimaNumber = "H1",
            },
            new PayrollRun { PayGroupId = group.Id, PayPeriodId = period.Id, Status = "closed", PaymentStatus = "reconciled" });
        await db.SaveChangesAsync();

        foreach (var key in new[] { "backup-restore", "security-test", "migration-rehearsal", "performance-test",
                     "monitoring-alerts", "incident-runbook", "rollback-rehearsal", "uat-hr", "uat-payroll",
                     "training-hr", "training-payroll" })
            await service.RecordEvidenceAsync(new(key, "passed", $"evidence:{key}", null,
                DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMonths(1)), "go-live-admin", default);

        var ready = await service.GetAsync(default);
        Assert.Equal("ready-for-signoff", ready.Decision);
        Assert.Empty(ready.Blockers);

        foreach (var role in new[] { "hr-owner", "payroll-owner", "finance-owner", "technical-owner", "tenant-owner" })
            await service.RecordSignoffAsync(role, new("approved", "Acceptance completed."), $"{role}-subject", default);
        var approved = await service.GetAsync(default);
        Assert.Equal("approved", approved.Decision);
        Assert.True(approved.CanGoLive);
        Assert.Equal(approved.TotalGates, approved.PassedGates);
    }

    [Fact]
    public async Task Signoffs_AreAppendOnly_AndLatestDecisionWins()
    {
        await using var db = TestDbContextFactory.Create("m36-signoff");
        var service = new GoLiveReadinessService(db, new PermissiveAuthz());
        var withdrawn = await service.RecordSignoffAsync("payroll-owner", new("withdrawn", "Pending review."), "payroll-a", default);
        await service.RecordSignoffAsync("payroll-owner", new("rejected", "Reconciliation variance."), "payroll-b", default);
        Assert.Equal(2, db.GoLiveSignoffs.Count());
        Assert.Equal("withdrawn", withdrawn.Decision);
        db.GoLiveSignoffs.First().Decision = "tampered";
        var error = await Assert.ThrowsAsync<DomainException>(() => db.SaveChangesAsync());
        Assert.Equal("audit-immutable", error.Code);
    }
}
