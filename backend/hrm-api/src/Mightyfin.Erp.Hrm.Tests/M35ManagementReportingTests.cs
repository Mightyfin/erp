using System.Text;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class M35ManagementReportingTests
{
    [Fact]
    public async Task Dashboard_ReconcilesReleasedPayrollAndOperationalLedgers()
    {
        await using var db = TestDbContextFactory.Create("m35-dashboard");
        var seed = await SeedAsync(db);
        var service = new ManagementReportingService(db, new PermissiveAuthz());

        var result = await service.GetDashboardAsync(new ManagementReportQuery("2026-06-01", "2026-06-30"), CancellationToken.None);

        Assert.Equal(2, result.Kpis.Single(x => x.Code == "headcount").Value);
        Assert.Equal(1150m, result.Kpis.Single(x => x.Code == "employer-cost").Value);
        Assert.Equal(800m, result.Kpis.Single(x => x.Code == "net-pay").Value);
        Assert.Equal(1, result.Departments.Single().PayrollWorkers);
        Assert.Equal(1000m, result.Departments.Single().GrossPay);
        Assert.Equal(150m, result.Departments.Single().EmployerContributions);
        Assert.Equal(100m, result.StatutoryLiability.Paye);
        Assert.Equal(24m, result.Leave.Single().ApprovedDays);
        Assert.Equal(2m, result.Attendance.Single().OvertimeHours);
        Assert.Equal(1, result.Recruitment.Single(x => x.Stage == "shortlisted").Candidates);
        Assert.Equal(1, result.Movements.Single(x => x.MovementType == "promotion").Movements);
        Assert.Contains(result.Filters.OrgUnits, x => x.Id == seed.OrgUnitId);
    }

    [Fact]
    public async Task Dashboard_AppliesOrganisationFilterAndExcludesReversalRuns()
    {
        await using var db = TestDbContextFactory.Create("m35-filter");
        var seed = await SeedAsync(db);
        var other = new OrgUnit { Code = "OPS", Name = "Operations", LegalEntityId = seed.LegalEntityId, EffectiveFrom = new DateOnly(2020, 1, 1) };
        db.OrgUnits.Add(other);
        var reversal = new PayrollRun { PayGroupId = seed.PayGroupId, PayPeriodId = seed.PayPeriodId, Status = "released", IsReversal = true };
        db.PayrollRuns.Add(reversal);
        db.PayrollRunLines.Add(new PayrollRunLine { RunId = reversal.Id, WorkerId = seed.WorkerId, GrossPay = 999m, TotalDeductions = 1m, NetPay = 998m, EmployerCost = 5m });
        await db.SaveChangesAsync();
        var service = new ManagementReportingService(db, new PermissiveAuthz());

        var result = await service.GetDashboardAsync(new ManagementReportQuery("2026-06-01", "2026-06-30", OrgUnitId: other.Id), CancellationToken.None);

        Assert.Equal(0, result.Kpis.Single(x => x.Code == "headcount").Value);
        Assert.Equal(0, result.Kpis.Single(x => x.Code == "employer-cost").Value);
        var unfiltered = await service.GetDashboardAsync(new ManagementReportQuery("2026-06-01", "2026-06-30"), CancellationToken.None);
        Assert.Equal(1150m, unfiltered.Kpis.Single(x => x.Code == "employer-cost").Value);
    }

    [Fact]
    public async Task JournalExport_IsBalancedAndPayrollRestricted()
    {
        await using var db = TestDbContextFactory.Create("m35-export");
        await SeedAsync(db);
        var service = new ManagementReportingService(db, new PermissiveAuthz());
        var export = await service.ExportAsync("payroll-journal", new ManagementReportQuery("2026-06-01", "2026-06-30"), CancellationToken.None);
        var csv = Encoding.UTF8.GetString(export.Content);
        Assert.Contains("CONTROL_TOTAL,1150.00,1150.00", csv);
        Assert.EndsWith(".csv", export.FileName);

        var hrOnly = new ManagementReportingService(db, new PermissiveAuthz { Roles = ["hr_ops"] });
        var error = await Assert.ThrowsAsync<DomainException>(() => hrOnly.ExportAsync("payroll-detail", new ManagementReportQuery("2026-06-01", "2026-06-30"), CancellationToken.None));
        Assert.Equal("report-forbidden", error.Code);
    }

    [Fact]
    public async Task InvalidDateRange_IsRejected()
    {
        await using var db = TestDbContextFactory.Create("m35-date");
        var service = new ManagementReportingService(db, new PermissiveAuthz());
        var error = await Assert.ThrowsAsync<DomainException>(() => service.GetDashboardAsync(new ManagementReportQuery("2026-07-01", "2026-06-01"), CancellationToken.None));
        Assert.Equal("report-date-range", error.Code);
    }

    private static async Task<SeedIds> SeedAsync(Infrastructure.Data.HrmDbContext db)
    {
        var entity = new LegalEntity { Code = "MF", RegisteredName = "Mightyfin", IsDefault = true };
        var unit = new OrgUnit { Code = "FIN", Name = "Finance", LegalEntityId = entity.Id, EffectiveFrom = new DateOnly(2020, 1, 1) };
        var location = new WorkLocation { Code = "LUS", Name = "Lusaka", LegalEntityId = entity.Id };
        var worker = new Worker { EmployeeNo = "EMP-001", FirstName = "George", LastName = "Munganga", Status = "active", StartDate = new DateOnly(2025, 1, 1), OrgUnitId = unit.Id, LocationId = location.Id };
        var hire = new Worker { EmployeeNo = "EMP-002", FirstName = "June", LastName = "Hire", Status = "active", StartDate = new DateOnly(2026, 6, 10), OrgUnitId = unit.Id, LocationId = location.Id };
        db.AddRange(entity, unit, location, worker, hire);
        db.Assignments.AddRange(
            new Assignment { WorkerId = worker.Id, LegalEntityId = entity.Id, OrgUnitId = unit.Id, LocationId = location.Id, StartDate = new DateOnly(2025, 1, 1), EffectiveFrom = new DateOnly(2025, 1, 1) },
            new Assignment { WorkerId = hire.Id, LegalEntityId = entity.Id, OrgUnitId = unit.Id, LocationId = location.Id, StartDate = new DateOnly(2026, 6, 10), EffectiveFrom = new DateOnly(2026, 6, 10) });
        var group = new PayGroup { Code = "MONTHLY", Name = "Monthly" };
        var period = new PayPeriod { PayGroupId = group.Id, PeriodLabel = "Jun 2026", StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 6, 30), CutoffDate = new DateOnly(2026, 6, 25), PayDate = new DateOnly(2026, 6, 30) };
        var run = new PayrollRun { PayGroupId = group.Id, PayPeriodId = period.Id, PayPeriod = period, Status = "released" };
        var line = new PayrollRunLine { RunId = run.Id, WorkerId = worker.Id, GrossPay = 1000m, TotalDeductions = 200m, NetPay = 800m, EmployerCost = 150m };
        line.Components.Add(new PayrollLineComponent { ComponentCode = "paye", ComponentName = "PAYE", ComponentType = "tax", Amount = 100m, IsStatutory = true });
        line.Components.Add(new PayrollLineComponent { ComponentCode = "napsa-er", ComponentName = "NAPSA ER", ComponentType = "employer-contribution", Amount = 150m, IsStatutory = true });
        db.AddRange(group, period, run, line);
        db.LeaveRequests.Add(new LeaveRequest { WorkerId = worker.Id, LeaveTypeCode = "annual", StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 6, 30), RequestedDays = 24, Status = "approved", CreatedForPeriod = new DateOnly(2026, 6, 1) });
        db.AttendanceRecords.Add(new AttendanceRecord { WorkerId = worker.Id, WorkDate = new DateOnly(2026, 6, 2), DerivedStatus = "present", ScheduledHours = 8, TotalHours = 10, RegularHours = 8, OvertimeHours = 2 });
        var vacancy = new Vacancy { OrgUnitId = unit.Id, JobTitle = "Analyst", Status = "published", CreatedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero) };
        db.AddRange(vacancy, new Candidate { VacancyId = vacancy.Id, FullName = "Candidate", Stage = "shortlisted", CreatedAt = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero) });
        db.Movements.Add(new Movement { WorkerId = worker.Id, MovementType = "promotion", Status = "executed", EffectiveDate = new DateOnly(2026, 6, 15), Reason = "Merit" });
        await db.SaveChangesAsync();
        return new(entity.Id, unit.Id, location.Id, worker.Id, group.Id, period.Id);
    }

    private sealed record SeedIds(Guid LegalEntityId, Guid OrgUnitId, Guid LocationId, Guid WorkerId, Guid PayGroupId, Guid PayPeriodId);
}
