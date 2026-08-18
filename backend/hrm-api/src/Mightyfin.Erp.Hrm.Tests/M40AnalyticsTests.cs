using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Analytics;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M40 tests: read-only HR analytics dashboard service.</summary>
public class M40AnalyticsTests
{
    private static HrmDbContext NewContext(string tenant = "test-tenant") => TestDbContextFactory.Create(tenant);

    private static (AnalyticsServiceImpl Svc, HrmDbContext Ctx) Build()
    {
        var ctx = NewContext();
        var authz = new PermissiveAuthz();
        var svc = new AnalyticsServiceImpl(new AnalyticsRepository(ctx), authz);
        return (svc, ctx);
    }

    [Fact]
    public async Task Dashboard_WithEmptyDatabase_ReturnsZeroedPanels()
    {
        var (svc, _) = Build();
        var dash = await svc.GetDashboardAsync(CancellationToken.None);
        Assert.Equal(0, dash.Workforce.ActiveCount);
        Assert.Equal(0, dash.Workforce.ArchivedCount);
        Assert.Equal(0, dash.Leave.ByType.Count);
        Assert.Equal(0, dash.Payroll.Runs.Count);
        Assert.Equal(0, dash.Performance.Finalized);
        Assert.Equal(0, dash.Recruitment.CandidatesInPipeline);
        Assert.Equal(0, dash.Attendance.ByStatus.Count);
        Assert.Equal(12, dash.Workforce.MonthlyTrend.Count);
    }

    [Fact]
    public async Task Dashboard_RequiresHrRole_UnauthenticatedThrows()
    {
        var ctx = NewContext();
        var authz = new ForbiddenAuthz();
        var svc = new AnalyticsServiceImpl(new AnalyticsRepository(ctx), authz);
        await Assert.ThrowsAsync<DomainException>(() => svc.GetDashboardAsync(CancellationToken.None));
    }

    [Fact]
    public async Task WorkerCounts_GroupsByStatus_IgnoresArchived()
    {
        var (svc, ctx) = Build();
        ctx.Set<Worker>().AddRange(
            new Worker { EmployeeNo = "E1", FirstName = "A", LastName = "B", Status = "active" },
            new Worker { EmployeeNo = "E2", FirstName = "C", LastName = "D", Status = "active" },
            new Worker { EmployeeNo = "E3", FirstName = "E", LastName = "F", Status = "pre-hire" },
            new Worker { EmployeeNo = "E4", FirstName = "G", LastName = "H", Status = "active", IsArchived = true });
        await ctx.SaveChangesAsync();
        var dash = await svc.GetDashboardAsync(CancellationToken.None);
        Assert.Equal(2, dash.Workforce.ActiveCount);
        Assert.Equal(1, dash.Workforce.PreHireCount);
        Assert.Equal(1, dash.Workforce.ArchivedCount);
    }

    [Fact]
    public async Task LeaveByType_AggregatesRequestedAndApproved()
    {
        var (svc, ctx) = Build();
        var worker = new Worker { EmployeeNo = "E1", FirstName = "A", LastName = "B", Status = "active" };
        ctx.Set<Worker>().Add(worker);
        ctx.Set<LeaveRequest>().AddRange(
            new LeaveRequest { WorkerId = worker.Id, LeaveTypeCode = "AL", RequestedDays = 3, Status = "approved" },
            new LeaveRequest { WorkerId = worker.Id, LeaveTypeCode = "AL", RequestedDays = 2, Status = "rejected" },
            new LeaveRequest { WorkerId = worker.Id, LeaveTypeCode = "SL", RequestedDays = 1, Status = "approved" },
            new LeaveRequest { WorkerId = worker.Id, LeaveTypeCode = "ML", RequestedDays = 5, Status = "draft" });
        await ctx.SaveChangesAsync();
        var dash = await svc.GetDashboardAsync(CancellationToken.None);
        var al = dash.Leave.ByType.First(t => t.LeaveType == "AL");
        Assert.Equal(5, al.RequestedDays);
        Assert.Equal(3, al.ApprovedDays);
        Assert.Equal(2, al.Requests);
        Assert.Equal(1, al.Approved);
        // Draft requests excluded entirely.
        Assert.DoesNotContain(dash.Leave.ByType, t => t.LeaveType == "ML");
    }

    [Fact]
    public async Task PayrollRuns_LatestSixByStartDate()
    {
        var (svc, ctx) = Build();
        var payGroup = new PayGroup { Code = "PG1", Name = "Monthly", Frequency = "monthly", IsDefault = false };
        ctx.Set<PayGroup>().Add(payGroup);
        await ctx.SaveChangesAsync();
        for (int i = 0; i < 8; i++)
        {
            var period = new PayPeriod { PayGroupId = payGroup.Id, PeriodLabel = $"P{i}", StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i + 25)), CutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i + 24)), PayDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i + 26)) };
            ctx.Set<PayPeriod>().Add(period);
            await ctx.SaveChangesAsync();
            ctx.Set<PayrollRun>().Add(new PayrollRun { PayPeriodId = period.Id, PayGroupId = payGroup.Id, Status = "released", TotalGross = 1000m + i, TotalDeductions = 100m, TotalNet = 900m + i, TotalEmployerCost = 1100m, EmployeeCount = 5 });
            await ctx.SaveChangesAsync();
        }
        var dash = await svc.GetDashboardAsync(CancellationToken.None);
        Assert.Equal(6, dash.Payroll.Runs.Count);
        // Descending by PayPeriod.StartDate: most recent run first.
        Assert.Contains("P0", dash.Payroll.Runs[0].PeriodLabel);
    }

    private static async Task<Vacancy> SeedVacancyAsync(HrmDbContext ctx, string status = "published")
    {
        var legalEntity = new LegalEntity { Code = "TE", RegisteredName = "Test Entity Ltd", TradingName = "TE" };
        ctx.Set<LegalEntity>().Add(legalEntity);
        await ctx.SaveChangesAsync();
        var orgUnit = new OrgUnit { Code = "OPS", Name = "Operations", LegalEntityId = legalEntity.Id };
        ctx.Set<OrgUnit>().Add(orgUnit);
        await ctx.SaveChangesAsync();
        var vacancy = new Vacancy { OrgUnitId = orgUnit.Id, JobTitle = "Teller", Status = status };
        ctx.Set<Vacancy>().Add(vacancy);
        await ctx.SaveChangesAsync();
        return vacancy;
    }

    [Fact]
    public async Task RecruitmentFunnel_UsesLatestStageEvent_FallbackToStage()
    {
        var (svc, ctx) = Build();
        var vacancy = await SeedVacancyAsync(ctx);
        await ctx.SaveChangesAsync();
        var c1 = new Candidate { VacancyId = vacancy.Id, FullName = "X Y", Stage = "screening" };
        var c2 = new Candidate { VacancyId = vacancy.Id, FullName = "Z W", Stage = "applied" };
        ctx.Set<Candidate>().AddRange(c1, c2);
        await ctx.SaveChangesAsync();
        ctx.Set<CandidateStageEvent>().Add(new CandidateStageEvent { CandidateId = c2.Id, FromStage = "applied", ToStage = "interviewing" });
        await ctx.SaveChangesAsync();
        var dash = await svc.GetDashboardAsync(CancellationToken.None);
        Assert.Equal(2, dash.Recruitment.CandidatesInPipeline);
        Assert.Contains(dash.Recruitment.StageFunnel, f => f.Stage == "screening" && f.Count == 1);
        Assert.Contains(dash.Recruitment.StageFunnel, f => f.Stage == "interviewing" && f.Count == 1);
    }

    [Fact]
    public async Task RecruitmentCounts_TerminalStagesExcludeFromPipeline()
    {
        var (svc, ctx) = Build();
        var vacancy = await SeedVacancyAsync(ctx);
        await ctx.SaveChangesAsync();
        var hired = new Candidate { VacancyId = vacancy.Id, FullName = "A B", Stage = "hired" };
        var rejected = new Candidate { VacancyId = vacancy.Id, FullName = "C D", Stage = "rejected" };
        var active = new Candidate { VacancyId = vacancy.Id, FullName = "E F", Stage = "shortlisted" };
        ctx.Set<Candidate>().AddRange(hired, rejected, active);
        await ctx.SaveChangesAsync();
        var dash = await svc.GetDashboardAsync(CancellationToken.None);
        Assert.Equal(1, dash.Recruitment.CandidatesInPipeline);
    }

    [Fact]
    public async Task AttendanceByStatus_TrailingWindowOnly()
    {
        var (svc, ctx) = Build();
        var worker = new Worker { EmployeeNo = "E1", FirstName = "A", LastName = "B", Status = "active" };
        ctx.Set<Worker>().Add(worker);
        await ctx.SaveChangesAsync();
        ctx.Set<AttendanceRecord>().AddRange(
            new AttendanceRecord { WorkerId = worker.Id, WorkDate = DateOnly.FromDateTime(DateTime.UtcNow), DerivedStatus = "present", TotalHours = 8 },
            new AttendanceRecord { WorkerId = worker.Id, WorkDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)), DerivedStatus = "present", TotalHours = 8 });
        await ctx.SaveChangesAsync();
        var dash = await svc.GetDashboardAsync(CancellationToken.None);
        Assert.Single(dash.Attendance.ByStatus);
        Assert.Equal(8, dash.Attendance.AverageDailyHours);
    }
}

/// <summary>Authz double that denies every call.</summary>
internal sealed class ForbiddenAuthz : IAuthzService
{
    public void RequireAnyRole(params string[] roles) => throw new DomainException("forbidden", "not allowed");
    public bool IsRole(params string[] roles) => false;
    public bool CanAccessSensitive(string category) => false;
    public string CurrentSubjectId => "test-subject";
}
