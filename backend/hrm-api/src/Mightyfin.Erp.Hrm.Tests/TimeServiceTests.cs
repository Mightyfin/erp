using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M3: time module tests (attendance punches, roster, leave balances,
/// correction and leave decisions) over EF InMemory with a fixed tenant.</summary>
public class TimeServiceTests
{
    /// <summary>No-op double for the workflow effect applier (leave decisions are
    /// applied directly by the time service; the workflow engine applies its own
    /// effects through this interface but tests run the direct service path).</summary>
    private sealed class NoOpLeaveEffectApplier : ILeaveEffectApplier
    {
        public Task ApplyAsync(WorkflowRequest request, string decisionAction, CancellationToken ct) => Task.CompletedTask;
    }

    private static (TimeServiceImpl service, HrmDbContext ctx, Worker worker, WorkflowServiceImpl wf, WorkflowRepository wfRepo) Build(
        string tenant = "test-tenant")
    {
        var ctx = TestDbContextFactory.Create(tenant);
        var wfRepo = new WorkflowRepository(ctx);
        var wf = new WorkflowServiceImpl(wfRepo, new PermissiveAuthz(), new NoOpLeaveEffectApplier());
        var repo = new TimeRepository(ctx);
        var service = new TimeServiceImpl(repo, new PermissiveAuthz(), wf);
        var worker = new Worker
        {
            EmployeeNo = "EMP-TM-001", FirstName = "Time", LastName = "Worker",
            WorkerType = "employee", Status = "active", Nationality = "ZM",
            TenantId = "test-tenant",
        };
        ctx.Workers.Add(worker);
        // seed calendar: weekend days sat/sun, default
        var calendar = new WorkCalendar
        {
            Name = "Test Calendar", CountryCode = "ZM", StandardWeeklyHours = 45,
            WeekendDays = "sat,sun", IsDefault = true,
            TenantId = "test-tenant",
        };
        ctx.WorkCalendars.Add(calendar);
        ctx.LeaveTypes.Add(new LeaveType
        {
            Code = "annual", Name = "Annual Leave", Category = "annual",
            DefaultDaysPerYear = 12, IsActive = true, AllowNegative = false,
            MaxConsecutiveDays = 999, RequiresEvidence = false, MinNoticeDays = 0,
            AllowsPartialDays = false, CarryForwardDays = 0, CarryForwardExpiryMonths = 0,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            TenantId = "test-tenant",
        });
        ctx.SaveChanges();
        return (service, ctx, worker, wf, wfRepo);
    }

    [Fact]
    public async Task ClockIn_CreatesRecordWithStateIn()
    {
        var (service, ctx, worker, _, _) = Build();
        var result = await service.ClockInAsync(worker.Id, CancellationToken.None);
        var record = await ctx.AttendanceRecords.FirstAsync(a => a.WorkerId == worker.Id);
        Assert.Equal("in", result.State);
        Assert.NotNull(record.ClockIn);
        Assert.Null(record.ClockOut);
        Assert.Equal("self-service", record.Source);
    }

    [Fact]
    public async Task ClockOut_MarksRecordDoneWithPositiveHours()
    {
        var (service, ctx, worker, _, _) = Build();
        await service.ClockInAsync(worker.Id, CancellationToken.None);
        // simulate a gap by faking the clock-in time back 2 hours
        var rec = await ctx.AttendanceRecords.FirstAsync(a => a.WorkerId == worker.Id);
        rec.ClockIn = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-2));
        ctx.SaveChanges();
        var result = await service.ClockOutAsync(worker.Id, CancellationToken.None);
        Assert.Equal("done", result.State);
        Assert.True(result.TotalHours > 1.9m && result.TotalHours < 2.1m, $"expected ~2h, got {result.TotalHours}");
        var record = await ctx.AttendanceRecords.FirstAsync(a => a.WorkerId == worker.Id);
        Assert.NotNull(record.ClockOut);
        Assert.Equal("present", record.DerivedStatus);
    }

    [Fact]
    public async Task GetRoster_ClassifiesWorkingDays_Weekends_AndMissingPunch()
    {
        var (service, ctx, worker, _, _) = Build();
        // punch only on Monday (today may be any day — punch today explicitly)
        await service.ClockInAsync(worker.Id, CancellationToken.None);
        await service.ClockOutAsync(worker.Id, CancellationToken.None);

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var roster = await service.GetRosterAsync(worker.Id,
            from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"), CancellationToken.None);
        Assert.Equal(11, roster.Count);

        var punched = roster.Single(r => r.ClockIn is not null);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.Parse(punched.Date));
        Assert.Equal("present", punched.Status);

        var weekends = roster.Where(r => !r.IsWorkingDay).ToList();
        Assert.True(weekends.Count >= 2, $"expected at least 2 weekend days, got {weekends.Count}");

        // past working days (excluding today, which was punched) must be flagged missing-punch
        var missing = roster.Where(r => r.Status == "missing-punch").ToList();
        Assert.True(missing.Count > 0, "working days without punches should be flagged missing-punch");
    }

    [Fact]
    public async Task DecideCorrection_Approve_AppliesProposedValues()
    {
        var (service, ctx, worker, _, _) = Build();
        var repo = new TimeRepository(ctx);
        var correction = new AttendanceCorrection
        {
            WorkerId = worker.Id, WorkDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IssueType = "missed-punch", Reason = "forgot badge", Status = "submitted",
            ProposedClockIn = new TimeOnly(8, 0), ProposedClockOut = new TimeOnly(16, 0),
        };
        await repo.CreateCorrectionAsync(correction, CancellationToken.None);

        var decided = await service.DecideCorrectionAsync(correction.Id,
            new TimeDecisionRequest("approve", null), CancellationToken.None);
        Assert.Equal("approved", decided.Status);

        var record = await repo.GetAttendanceAsync(worker.Id, correction.WorkDate, CancellationToken.None);
        Assert.NotNull(record);
        Assert.Equal("corrected", record!.Source);
        Assert.Equal(new TimeOnly(8, 0), record.ClockIn);
        Assert.Equal(8m, record.TotalHours);
    }

    [Fact]
    public async Task CreateLeave_InsufficientBalance_Throws()
    {
        var (service, ctx, worker, _, _) = Build();
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateLeaveAsync(
            new LeaveRequestCreate(worker.Id, "annual",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)).ToString("yyyy-MM-dd")),
            CancellationToken.None));
        Assert.Equal("leave-insufficient-balance", ex.Code);
    }

    [Fact]
    public async Task CreateLeave_ReservesBalance()
    {
        var (service, ctx, worker, _, _) = Build();
        var repo = new TimeRepository(ctx);
        // accrue 12 days
        ctx.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = worker.Id, LeaveTypeCode = "annual", Days = 12m,
            Reason = "accrual", ReferenceType = "", ForDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = "test-tenant",
        });
        ctx.SaveChanges();

        var created = await service.CreateLeaveAsync(new LeaveRequestCreate(
            worker.Id, "annual",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")),
            CancellationToken.None);
        Assert.Equal("submitted", created.Status);
        Assert.False(created.BalanceReserved);

        var ledger = await repo.GetLedgerAsync(worker.Id, CancellationToken.None);
        var reservation = ledger.Single(l => l.Reason == "request");
        Assert.Equal(-1m, reservation.Days);
        Assert.NotNull(reservation.ReferenceId);

        var balances = await service.GetBalancesAsync(worker.Id, CancellationToken.None);
        var annual = balances.Single(b => b.LeaveTypeCode == "annual");
        Assert.Equal(12m, annual.Accrued);
        Assert.Equal(1m, annual.Reserved);
        Assert.Equal(11m, annual.Available);
    }

    [Fact]
    public async Task DecideLeave_Approve_ConvertsReservationToTaken()
    {
        var (service, ctx, worker, _, _) = Build();
        var repo = new TimeRepository(ctx);
        ctx.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = worker.Id, LeaveTypeCode = "annual", Days = 12m,
            Reason = "accrual", ReferenceType = "", ForDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = "test-tenant",
        });
        ctx.SaveChanges();

        var created = await service.CreateLeaveAsync(new LeaveRequestCreate(
            worker.Id, "annual",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")),
            CancellationToken.None);

        var decided = await service.DecideLeaveAsync(created.Id,
            new TimeDecisionRequest("approve", null), CancellationToken.None);
        Assert.Equal("approved", decided.Status);
        Assert.True(decided.BalanceReserved);

        var balances = await service.GetBalancesAsync(worker.Id, CancellationToken.None);
        var annual = balances.Single(b => b.LeaveTypeCode == "annual");
        Assert.Equal(1m, annual.Taken);
        Assert.Equal(0m, annual.Reserved);
        Assert.Equal(11m, annual.Available);
    }

    [Fact]
    public async Task DecideLeave_Reject_ReleasesReservation()
    {
        var (service, ctx, worker, _, _) = Build();
        var repo = new TimeRepository(ctx);
        ctx.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = worker.Id, LeaveTypeCode = "annual", Days = 12m,
            Reason = "accrual", ReferenceType = "", ForDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = "test-tenant",
        });
        ctx.SaveChanges();

        var created = await service.CreateLeaveAsync(new LeaveRequestCreate(
            worker.Id, "annual",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")),
            CancellationToken.None);

        var decided = await service.DecideLeaveAsync(created.Id,
            new TimeDecisionRequest("reject", "not this time"), CancellationToken.None);
        Assert.Equal("rejected", decided.Status);

        var ledger = await repo.GetLedgerAsync(worker.Id, CancellationToken.None);
        Assert.DoesNotContain(ledger, l => l.Days < 0);
        var balances = await service.GetBalancesAsync(worker.Id, CancellationToken.None);
        var annual = balances.Single(b => b.LeaveTypeCode == "annual");
        Assert.Equal(0m, annual.Reserved);
        Assert.Equal(12m, annual.Available);
    }
}
