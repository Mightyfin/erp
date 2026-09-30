using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public class PeriodOvertimeTests
{
    private static (HrmDbContext Db, PeriodOvertimeService Service, Worker Worker, PayPeriod August, PayPeriod September) Build()
    {
        var db = TestDbContextFactory.Create();
        var group = new PayGroup { Code = "monthly", Name = "Monthly" };
        var structure = new SalaryStructure { Code = "standard", Name = "Standard" };
        var worker = new Worker { EmployeeNo = "T001", FirstName = "Test", LastName = "Employee", Status = "active", StartDate = new DateOnly(2026, 1, 1) };
        var august = new PayPeriod { PayGroup = group, PeriodLabel = "Aug 2026", StartDate = new(2026,8,1), EndDate = new(2026,8,31) };
        var september = new PayPeriod { PayGroup = group, PeriodLabel = "Sep 2026", StartDate = new(2026,9,1), EndDate = new(2026,9,30) };
        db.AddRange(group, structure, worker, august, september);
        db.WorkerPayrollProfiles.Add(new WorkerPayrollProfile { Worker = worker, PayGroup = group, Structure = structure, EffectiveFrom = new(2026,1,1) });
        db.SaveChanges();
        return (db, new PeriodOvertimeService(db, new PermissiveAuthz(), new ShellContext()), worker, august, september);
    }

    [Fact]
    public async Task BranchScopeCannotReadOrWriteAnotherBranchesEmployee()
    {
        var (db, _, worker, _, period) = Build();
        var service = new PeriodOvertimeService(db, new PermissiveAuthz(), new ShellContext { LocationId = Guid.NewGuid() });
        Assert.Empty((await service.GetAsync(period.Id, default)).Rows);
        await Assert.ThrowsAsync<DomainException>(() => service.SaveAsync(period.Id, worker.Id, new(100, ""), default));
        Assert.Empty(await db.PeriodOvertime.ToListAsync());
    }

    [Fact]
    public async Task CalculateAndRecalculateUsesOnlySelectedPeriodAmount()
    {
        var (payroll, db) = PayrollEngineTests.Build();
        var stack = await PayrollEngineTests.SeedStackAsync(db);
        var entries = new PeriodOvertimeService(db, new PermissiveAuthz(), new ShellContext());
        await entries.SaveAsync(stack.P1.Id, stack.Profile.WorkerId, new(99, "Previous period"), default);
        await entries.SaveAsync(stack.P2.Id, stack.Profile.WorkerId, new(1200, "Current period"), default);
        var run = await payroll.CreateRunAsync(new PayrollRunCreate(stack.P2.Id, stack.Group.Id), default);
        await payroll.LockRunAsync(run.Id, default);
        await payroll.CalculateRunAsync(run.Id, default);
        var line = await db.PayrollRunLines.Include(l => l.Components).SingleAsync(l => l.RunId == run.Id);
        Assert.Equal(31200m, line.GrossPay);
        Assert.Equal(1200m, line.Components.Single(c => c.ComponentCode == "overtime").Amount);
        var originalNet = line.NetPay;
        await payroll.CalculateRunAsync(run.Id, default);
        line = await db.PayrollRunLines.Include(l => l.Components).SingleAsync(l => l.RunId == run.Id);
        Assert.Equal(31200m, line.GrossPay);
        Assert.Equal(originalNet, line.NetPay);
        Assert.Single(line.Components, c => c.ComponentCode == "overtime");
        Assert.Equal(99m, (await entries.GetAsync(stack.P1.Id, default)).Rows.Single().Amount);
        await Assert.ThrowsAsync<DomainException>(() => entries.SaveAsync(stack.P2.Id, stack.Profile.WorkerId, new(999, ""), default));
    }

    [Fact]
    public async Task SeptemberSaveDoesNotChangeAugustAndSurvivesReload()
    {
        var (db, service, worker, august, september) = Build();
        await service.SaveAsync(august.Id, worker.Id, new(150, "August"), default);
        db.PayrollRuns.Add(new PayrollRun { PayPeriodId = august.Id, PayGroupId = august.PayGroupId, Status = "released" });
        await db.SaveChangesAsync();
        await service.SaveAsync(september.Id, worker.Id, new(2750, "September"), default);
        db.ChangeTracker.Clear();
        var repo = new PayrollRepository(db);
        Assert.Equal(150, (await repo.LoadPeriodOvertimeAsync(august.Id, default)).Single().Amount);
        Assert.Equal(2750, (await repo.LoadPeriodOvertimeAsync(september.Id, default)).Single().Amount);
        Assert.False((await service.GetAsync(august.Id, default)).Editable);
        Assert.Equal(2750, (await service.GetAsync(september.Id, default)).Rows.Single().Amount);
    }

    [Theory]
    [InlineData("locked")]
    [InlineData("calculated")]
    [InlineData("approved")]
    [InlineData("released")]
    public async Task CannotEditPayrollAfterInputsLocked(string status)
    {
        var (db, service, worker, _, period) = Build();
        db.PayrollRuns.Add(new PayrollRun { PayPeriodId = period.Id, PayGroupId = period.PayGroupId, Status = status });
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<DomainException>(() => service.SaveAsync(period.Id, worker.Id, new(100, ""), default));
        Assert.Contains("read-only", error.Message);
        Assert.Empty(await db.PeriodOvertime.ToListAsync());
    }

    [Fact]
    public async Task ClearThenReenterDoesNotDuplicateAndZeroIsExplicit()
    {
        var (db, service, worker, _, period) = Build();
        await service.SaveAsync(period.Id, worker.Id, new(100, ""), default);
        await service.SaveAsync(period.Id, worker.Id, new(null, ""), default);
        Assert.Null((await service.GetAsync(period.Id, default)).Rows.Single().Amount);
        await service.SaveAsync(period.Id, worker.Id, new(0, "No overtime"), default);
        Assert.Equal(0, (await service.GetAsync(period.Id, default)).Rows.Single().Amount);
        Assert.Single(await db.PeriodOvertime.Where(o => !o.IsArchived).ToListAsync());
        await Assert.ThrowsAsync<DomainException>(() => service.SaveAsync(period.Id, worker.Id, new(-1, ""), default));
        await Assert.ThrowsAsync<DomainException>(() => service.SaveAsync(period.Id, worker.Id, new(1.001m, ""), default));
        await Assert.ThrowsAsync<DomainException>(() => service.SaveAsync(period.Id, Guid.NewGuid(), new(100, ""), default));
    }

    [Fact]
    public void PeriodAmountReplacesHoursAndIsAddedExactlyOnceToGross()
    {
        var worker = new Worker();
        var context = new CalcContext(worker, new WorkerPayrollProfile(), [], [], []);
        context.AddOvertime([new AttendanceRecord { OvertimeHours = 20, OvertimeMultiplier = 1.5m }], new PeriodOvertime { Amount = 1200 });
        Assert.Equal(1200, context.Gross);
        Assert.Equal(1200, context.Components.Single(c => c.Code == "overtime").Amount);
        var zero = new CalcContext(worker, new WorkerPayrollProfile(), [], [], []);
        zero.AddOvertime([new AttendanceRecord { OvertimeHours = 20 }], new PeriodOvertime { Amount = 0 });
        Assert.Equal(0, zero.Gross);
        Assert.Empty(zero.Components);
    }
}
