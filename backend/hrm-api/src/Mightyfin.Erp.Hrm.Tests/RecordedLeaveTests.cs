using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Tests;
public class RecordedLeaveTests
{
    private static async Task<(HrmDbContext Db, RecordedLeaveService Service, Worker Worker)> Build()
    {
        var db = TestDbContextFactory.Create();
        var worker = new Worker { EmployeeNo = "T01", FirstName = "Test", LastName = "Worker", Status = "active", StartDate = new(2020,1,1) };
        db.Workers.Add(worker);
        db.LeaveTypes.Add(new LeaveType { Code = "annual", Name = "Annual", EffectiveFrom = new(2020,1,1), MaxConsecutiveDays = 30 });
        var entity = new LegalEntity { Code = "test", RegisteredName = "Test", CountryCode = "ZM" }; db.LegalEntities.Add(entity);
        db.WorkCalendars.Add(new WorkCalendar { Name = "Sunday rest", LegalEntityId = entity.Id, IsDefault = true, WeekendDays = "sun" });
        db.LeaveBalanceLedgers.Add(new LeaveBalanceLedger { WorkerId = worker.Id, LeaveTypeCode = "annual", Days = 24, Reason = "annual-accrual" });
        await db.SaveChangesAsync();
        return (db, new RecordedLeaveService(db, new PermissiveAuthz(), new ShellContext()), worker);
    }
    private static RecordedLeaveInput Input(Worker worker, string start = "2026-09-18", string end = "2026-09-19") => new(worker.Id, "annual", start, end, "Recorded from approved September leave sheet");
    [Fact]
    public async Task CalendarCountsMapaloAndRoseWithoutGuessingSevenDays()
    {
        var (_, svc, w) = await Build();
        var mapalo = await svc.QuoteAsync(Input(w,"2026-09-22","2026-09-29"), default);
        Assert.Equal(7, mapalo.Days); Assert.Contains("2026-09-27", mapalo.ExcludedDates);
        Assert.Equal(8, (await svc.QuoteAsync(Input(w,"2026-09-17","2026-09-25"),default)).Days);
    }
    [Fact]
    public async Task CreateEditCancelUpdateBalanceOnceAndKeepAudit()
    {
        var (db, svc, w) = await Build();
        var created = await svc.SaveAsync(null,Input(w),default);
        Assert.Equal("approved",created.Status); Assert.Equal(22,await db.LeaveBalanceLedgers.SumAsync(b=>b.Days));
        await svc.SaveAsync(created.Id,Input(w,"2026-09-18","2026-09-18") with { Note="Corrected to one day" },default);
        Assert.Equal(23,await db.LeaveBalanceLedgers.SumAsync(b=>b.Days));
        await svc.CancelAsync(created.Id,"Entered in error",default);
        Assert.Equal(24,await db.LeaveBalanceLedgers.SumAsync(b=>b.Days));
        Assert.Equal("cancelled",(await svc.ListAsync(default)).Single().Status);
        Assert.Equal(3,await db.AuditEntries.CountAsync(a=>a.EntityType=="time.recorded-leave"));
        await Assert.ThrowsAsync<DomainException>(()=>svc.CancelAsync(created.Id,"Again",default));
    }
    [Fact]
    public async Task OverlapAndInsufficientBalanceDoNotCreateRecords()
    {
        var (db,svc,w)=await Build(); await svc.SaveAsync(null,Input(w),default);
        await Assert.ThrowsAsync<DomainException>(()=>svc.SaveAsync(null,Input(w),default));
        Assert.Single(db.LeaveRequests);
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<DomainException>(()=>svc.SaveAsync(null,Input(w,"2026-08-01","2026-08-29"),default));
        Assert.Single(await db.LeaveRequests.ToListAsync());
    }
    [Fact]
    public async Task ScopeAndLockedPayrollAreEnforced()
    {
        var (db,svc,w)=await Build();
        var scoped=new RecordedLeaveService(db,new PermissiveAuthz(),new ShellContext { LocationId=Guid.NewGuid() });
        await Assert.ThrowsAsync<DomainException>(()=>scoped.SaveAsync(null,Input(w),default));
        var group=new PayGroup { Code="pay", Name="Pay" }; var structure=new SalaryStructure { Code="std", Name="Standard" };
        var period=new PayPeriod { PayGroup=group, PeriodLabel="Sep", StartDate=new(2026,9,1),EndDate=new(2026,9,30),Status="closed" };
        db.AddRange(group,structure,period);
        db.WorkerPayrollProfiles.Add(new WorkerPayrollProfile { Worker=w,PayGroup=group,Structure=structure,EffectiveFrom=new(2020,1,1) });await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(()=>svc.SaveAsync(null,Input(w),default));
        Assert.Empty(await db.LeaveRequests.ToListAsync());
    }
    [Fact]
    public async Task OpenPeriodAllowsLeaveThenRequiresFreshPayrollCalculation()
    {
        var (db, svc, worker) = await Build();
        var group = new PayGroup { Code = "sep", Name = "September" };
        var structure = new SalaryStructure { Code = "std-sep", Name = "Standard" };
        var period = new PayPeriod { PayGroup = group, PeriodLabel = "Sep 2026", StartDate = new(2026,9,1), EndDate = new(2026,9,30), Status = "open" };
        var run = new PayrollRun { PayPeriod = period, PayGroupId = group.Id, Status = "calculated" };
        db.AddRange(group, structure, period, run);
        db.WorkerPayrollProfiles.Add(new WorkerPayrollProfile { Worker = worker, PayGroup = group, Structure = structure, EffectiveFrom = new(2020,1,1) });
        await db.SaveChangesAsync();

        var leave = await svc.SaveAsync(null, Input(worker), default);
        Assert.Equal("approved", leave.Status);
        Assert.Equal("locked", run.Status);
        Assert.Contains(await db.PayrollRunEvents.ToListAsync(), e => e.RunId == run.Id && e.Action == "leave-inputs-updated");

        run.Status = "released";
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<DomainException>(() => svc.CancelAsync(leave.Id, "Too late", default));
        Assert.Equal("leave-payroll-locked", error.Code);
        Assert.Equal("approved", (await svc.ListAsync(default)).Single().Status);
    }
    private sealed class EmployeeOnly : IAuthzService
    {
        public string CurrentSubjectId => "employee";
        public bool IsRole(params string[] roles) => roles.Contains("employee");
        public bool CanAccessSensitive(string category) => false;
        public void RequireAnyRole(params string[] roles) { if (!IsRole(roles)) throw new DomainException("forbidden", "Denied"); }
    }
    [Fact]
    public async Task EmployeesCannotBypassWorkflowAndFutureLeaveCannotBeRecorded()
    {
        var (db,svc,w)=await Build();
        var employee=new RecordedLeaveService(db,new EmployeeOnly(),new ShellContext());
        await Assert.ThrowsAsync<DomainException>(()=>employee.ListAsync(default));
        await Assert.ThrowsAsync<DomainException>(()=>employee.SaveAsync(null,Input(w),default));
        var tomorrow=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd");
        await Assert.ThrowsAsync<DomainException>(()=>svc.SaveAsync(null,Input(w,tomorrow,tomorrow),default));
        Assert.Empty(await db.LeaveRequests.ToListAsync());
    }
}
