using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M41 Gap 2: payment-days proration — mid-month starters, leavers,
/// approved unpaid leave, and full-month workers stay untouched.</summary>
public class M41Gap2ProrationTests : IDisposable
{
    private readonly HrmDbContext _db;

    public M41Gap2ProrationTests()
    {
        _db = TestDbContextFactory.Create();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    // ---------------- domain-level (pure) tests ----------------

    private static PayrollProrationInputs July2026(params ApprovedUnpaidLeave[] leaves)
        => new PayrollProrationInputs(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), leaves.ToList(),
            [new DateOnly(2026, 7, 6)]);

    [Fact]
    public void FullMonthWorker_NoProration()
    {
        var (working, payment, note) = PaymentDaysCalculator.For(
            July2026(), new Worker { StartDate = new DateOnly(2025, 1, 1) }, []);
        Assert.Equal(23, working);
        Assert.Equal(23, payment);
        Assert.Null(note);
    }

    [Fact]
    public void MidMonthStarter_Proration()
    {
        var (working, payment, note) = PaymentDaysCalculator.For(
            July2026(), new Worker { StartDate = new DateOnly(2026, 7, 16) }, []);
        Assert.Equal(23, working);
        Assert.Equal(12, payment); // 16th..31st, excluding weekends
        Assert.Contains("started 16 Jul 2026", note);
    }

    [Fact]
    public void WeekdayCalendar_MidMonthStarter_ExcludesSaturdayAndSunday()
    {
        var (working, payment, note) = PaymentDaysCalculator.For(
            July2026(), new Worker { StartDate = new DateOnly(2026, 7, 10) }, []);
        // July 2026 contains 23 Mon–Fri workdays. From Friday 10th through
        // Friday 31st, the employee has 16 scheduled workdays.
        Assert.Equal(23, working);
        Assert.Equal(16, payment);
        Assert.Contains("started 10 Jul 2026", note);
    }

    [Fact]
    public void LeaverMidMonth_Proration()
    {
        var (working, payment, note) = PaymentDaysCalculator.For(
            July2026(),
            new Worker { StartDate = new DateOnly(2025, 1, 1), EndDate = new DateOnly(2026, 7, 15) }, []);
        Assert.Equal(23, working);
        Assert.Equal(11, payment);
        Assert.Contains("ended", note);
    }

    [Fact]
    public void ApprovedUnpaidLeave_ReducesPaymentDays()
    {
        var wid = Guid.NewGuid();
        var leaves = new List<ApprovedUnpaidLeave>
        {
            new(wid, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 21), 2),
        };
        var (working, payment, note) = PaymentDaysCalculator.For(
            July2026(), new Worker { Id = wid, StartDate = new DateOnly(2025, 1, 1) }, leaves);
        Assert.Equal(23, working);
        Assert.Equal(21, payment);
        Assert.Contains("unpaid leave days", note);
    }

    [Fact]
    public void UnpaidLeaveOutsidePeriod_NoEffect()
    {
        var wid = Guid.NewGuid();
        var leaves = new List<ApprovedUnpaidLeave>
        {
            new(wid, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 3), 3),
        };
        var (working, payment, note) = PaymentDaysCalculator.For(
            July2026(), new Worker { Id = wid, StartDate = new DateOnly(2025, 1, 1) }, leaves);
        Assert.Equal(23, payment);
        Assert.Null(note);
    }

    [Fact]
    public void StarterPlusUnpaidLeave_Combined()
    {
        var wid = Guid.NewGuid();
        var leaves = new List<ApprovedUnpaidLeave>
        {
            new(wid, new DateOnly(2026, 7, 29), new DateOnly(2026, 7, 30), 2),
        };
        var (working, payment, _) = PaymentDaysCalculator.For(
            July2026(), new Worker { Id = wid, StartDate = new DateOnly(2026, 7, 16) }, leaves);
        Assert.Equal(23, working);
        Assert.Equal(10, payment); // 12 scheduled days minus 2 unpaid
    }

    [Fact]
    public void NotEmployedDuringPeriod_ZeroPaymentDays()
    {
        var (working, payment, note) = PaymentDaysCalculator.For(
            July2026(), new Worker { StartDate = new DateOnly(2026, 8, 1) }, []);
        Assert.Equal(0, payment);
        Assert.Contains("Not employed", note);
    }

    [Fact]
    public void PublicHoliday_NeverReducesPay()
    {
        var (working, payment, note) = PaymentDaysCalculator.For(
            July2026(), new Worker { StartDate = new DateOnly(2025, 1, 1) }, []);
        // July2026 includes Heroes' Day (6 Jul) as a holiday — payment days
        // remain the full 23 scheduled weekdays.
        Assert.Equal(23, working);
        Assert.Equal(23, payment);
        Assert.Null(note);
    }

    // ---------------- engine-level: CalcContext applies the factor ----------------

    [Fact]
    public void CalcContext_ProrationAppliesToFixedEarnings()
    {
        var worker = new Worker { FirstName = "A", LastName = "B", EmployeeNo = "E1" };
        var basic = new SalaryComponent { Code = "basic", Name = "Basic", ComponentType = "earning",
            CalculationBasis = "fixed", FixedAmount = 3100m, IsStatutory = false };
        var profile = new WorkerPayrollProfile { WorkerId = worker.Id,
            ComponentValues = new List<WorkerComponentValue>
            {
                new() { ComponentId = basic.Id, Amount = 3100m },
            } };
        var ctx = new CalcContext(worker, profile, new List<SalaryComponent> { basic }, [], []);
        ctx.SetProration(31, 15, "started 16 Jul");
        ctx.Evaluate(basic);
        Assert.Equal(1500m, ctx.Gross); // 3100 * 15/31
        Assert.Equal(15, ctx.PaymentDays);
    }

    [Fact]
    public void CalcContext_PercentOfEarning_FollowsProratedBasis()
    {
        var worker = new Worker { FirstName = "A", LastName = "B", EmployeeNo = "E1" };
        var basic = new SalaryComponent { Code = "basic", Name = "Basic", ComponentType = "earning",
            CalculationBasis = "fixed", FixedAmount = 3100m, IsStatutory = false };
        var housing = new SalaryComponent { Code = "housing", Name = "Housing", ComponentType = "earning",
            CalculationBasis = "percent-of", BasisComponentCode = "basic", Rate = 10m, IsStatutory = false };
        var profile = new WorkerPayrollProfile { WorkerId = worker.Id,
            ComponentValues = new List<WorkerComponentValue>
            {
                new() { ComponentId = basic.Id, Amount = 3100m },
            } };
        var ctx = new CalcContext(worker, profile,
            new List<SalaryComponent> { basic, housing }, [], []);
        ctx.SetProration(31, 15, "started 16 Jul");
        // basic first (structure priority), then housing reads the already-
        // prorated basic value.
        ctx.Evaluate(basic);
        ctx.Evaluate(housing);
        Assert.Equal(1650m, ctx.Gross);            // 1500 prorated basic + 150 housing
        Assert.Equal(150m, ctx.Components[1].Amount); // 10% of the prorated basic
    }

    [Fact]
    public void CalcContext_NoProrationWhenFullMonth()
    {
        var worker = new Worker { FirstName = "A", LastName = "B", EmployeeNo = "E1" };
        var basic = new SalaryComponent { Code = "basic", Name = "Basic", ComponentType = "earning",
            CalculationBasis = "fixed", FixedAmount = 3100m, IsStatutory = false };
        var profile = new WorkerPayrollProfile { WorkerId = worker.Id,
            ComponentValues = new List<WorkerComponentValue>
            {
                new() { ComponentId = basic.Id, Amount = 3100m },
            } };
        var ctx = new CalcContext(worker, profile, new List<SalaryComponent> { basic }, [], []);
        ctx.SetProration(31, 31, null);
        ctx.Evaluate(basic);
        Assert.Equal(3100m, ctx.Gross);
        Assert.Null(ctx.ProrationNote);
    }
}
