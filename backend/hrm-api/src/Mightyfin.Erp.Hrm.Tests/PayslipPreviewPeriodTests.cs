using Mightyfin.Erp.Hrm.Domain.Entities;
namespace Mightyfin.Erp.Hrm.Tests;

public class PayslipPreviewPeriodTests
{
    [Fact]
    public async Task NewStarterPreviewUsesTodaysPeriodDespiteStaleCurrentFlagWithoutCreatingRun()
    {
        var (service, db) = PayrollEngineTests.Build();
        var stack = await PayrollEngineTests.SeedStackAsync(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = new DateOnly(today.Year, today.Month, 1);
        stack.P1.StartDate = start.AddMonths(-1);
        stack.P1.EndDate = start.AddDays(-1);
        stack.P1.IsCurrent = true;
        stack.P1.PeriodLabel = "Previous month";
        stack.P2.StartDate = start;
        stack.P2.EndDate = start.AddMonths(1).AddDays(-1);
        stack.P2.IsCurrent = false;
        stack.P2.PeriodLabel = "This month";
        stack.Profile.Worker!.StartDate = start;
        stack.Profile.EffectiveFrom = start;
        db.PayrollRuns.Add(new PayrollRun { PayPeriodId = stack.P1.Id, PayGroupId = stack.Group.Id, Status = "released", TotalGross = 1234m });
        await db.SaveChangesAsync();
        var preview = await service.PreviewWorkerPayslipAsync(stack.Profile.WorkerId, default);
        Assert.Equal("This month", preview.PeriodLabel);
        Assert.NotNull(preview.Line);
        Assert.True(preview.Line.GrossPay > 0);
        Assert.True(preview.Line.NetPay > 0);
        Assert.DoesNotContain(preview.Guardrails, g => g.StartsWith("Gross pay is zero") || g.StartsWith("Net pay is zero"));
        Assert.Single(db.PayrollRuns);
        Assert.Empty(db.PayrollRunLines);
        Assert.Equal(1234m, db.PayrollRuns.Single().TotalGross);
        Assert.True(stack.P1.IsCurrent);
    }

    [Fact]
    public async Task PreviewRetainsConfiguredCurrentPeriodWhenNoPeriodContainsToday()
    {
        var (service, db) = PayrollEngineTests.Build();
        var stack = await PayrollEngineTests.SeedStackAsync(db);
        var start = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        stack.P1.StartDate = start.AddMonths(-2);
        stack.P1.EndDate = start.AddMonths(-1).AddDays(-1);
        stack.P1.IsCurrent = true;
        stack.P2.StartDate = start.AddMonths(-1);
        stack.P2.EndDate = start.AddDays(-1);
        stack.P2.IsCurrent = false;
        await db.SaveChangesAsync();
        var preview = await service.PreviewWorkerPayslipAsync(stack.Profile.WorkerId, default);
        Assert.Equal(stack.P1.PeriodLabel, preview.PeriodLabel);
    }
}
