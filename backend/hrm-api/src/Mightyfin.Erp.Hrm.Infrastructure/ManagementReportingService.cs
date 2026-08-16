using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

/// <summary>
/// M35 certified management reporting. Financial measures are read from released
/// payroll snapshots; workforce dimensions resolve effective-dated assignments.
/// Every query remains tenant-scoped by HrmDbContext's global filters.
/// </summary>
public sealed class ManagementReportingService(HrmDbContext db, IAuthzService authz) : IManagementReportingService
{
    private static readonly string[] ReleasedStatuses = ["released", "closed"];

    private static readonly List<ReportCatalogueItemDto> Catalogue =
    [
        new("workforce-summary", "Headcount and workforce movements", "Workforce", "Point-in-time headcount, hires, leavers and movements by organisation.", "HR operations", true, false, "Workers and effective-dated assignments"),
        new("payroll-department", "Payroll by department", "Payroll and cost", "Released gross-to-net and employer cost controls by department.", "Payroll", true, true, "Released payroll run lines"),
        new("payroll-detail", "Employee payroll detail", "Payroll and cost", "Released employee gross-to-net register with assignment-at-period dimensions.", "Payroll", true, true, "Released payroll run lines"),
        new("payroll-journal", "Payroll journal voucher", "Payroll and cost", "Balanced payroll expense, liability and net-pay journal control.", "Finance", true, true, "Released payroll run lines"),
        new("statutory-liability", "Statutory liability summary", "Compliance", "ZRA PAYE, NAPSA and NHIMA liabilities from released component snapshots.", "Payroll", true, true, "Released payroll line components"),
        new("leave-attendance", "Leave and attendance operations", "Time and absence", "Approved and pending leave with attendance and overtime controls.", "HR operations", true, false, "Leave requests and attendance records"),
        new("recruitment-funnel", "Recruitment funnel", "Recruitment", "Candidate pipeline from application through hire.", "Recruitment", true, false, "Vacancies and candidate stage records"),
        new("workforce-movements", "Workforce movement register", "Workforce", "Approved and executed transfers, promotions and other movements.", "HR operations", true, false, "Effective-dated worker movements")
    ];

    public async Task<ManagementDashboardDto> GetDashboardAsync(ManagementReportQuery query, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var (from, to) = ParseWindow(query);

        var legalEntities = await db.LegalEntities.AsNoTracking().OrderBy(x => x.RegisteredName).ToListAsync(ct);
        var orgUnits = await db.OrgUnits.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var locations = await db.WorkLocations.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var assignments = await db.Assignments.AsNoTracking().ToListAsync(ct);
        var workers = await db.Workers.AsNoTracking().Where(x => !x.IsArchived).ToListAsync(ct);

        Assignment? At(Guid workerId, DateOnly date) => assignments
            .Where(x => x.WorkerId == workerId && x.EffectiveFrom <= date && (x.EffectiveTo == null || x.EffectiveTo >= date))
            .OrderByDescending(x => x.EffectiveFrom).FirstOrDefault();
        Guid? Unit(Guid workerId, DateOnly date) => At(workerId, date)?.OrgUnitId ?? workers.FirstOrDefault(x => x.Id == workerId)?.OrgUnitId;
        Guid? Location(Guid workerId, DateOnly date) => At(workerId, date)?.LocationId ?? workers.FirstOrDefault(x => x.Id == workerId)?.LocationId;
        Guid? Entity(Guid workerId, DateOnly date)
        {
            var assignment = At(workerId, date);
            if (assignment is not null) return assignment.LegalEntityId;
            var unitId = Unit(workerId, date);
            return orgUnits.FirstOrDefault(x => x.Id == unitId)?.LegalEntityId;
        }
        bool InScope(Guid workerId, DateOnly date) =>
            (query.LegalEntityId is null || Entity(workerId, date) == query.LegalEntityId) &&
            (query.OrgUnitId is null || Unit(workerId, date) == query.OrgUnitId) &&
            (query.LocationId is null || Location(workerId, date) == query.LocationId);
        bool ActiveAt(Worker worker, DateOnly date) => !worker.IsArchived && worker.StartDate <= date && (worker.EndDate is null || worker.EndDate >= date);

        var scopedAtEnd = workers.Where(x => InScope(x.Id, to)).ToList();
        var activeEnd = scopedAtEnd.Where(x => ActiveAt(x, to)).ToList();
        var activeStart = workers.Where(x => InScope(x.Id, from) && ActiveAt(x, from)).ToList();
        var hires = scopedAtEnd.Where(x => x.StartDate >= from && x.StartDate <= to).ToList();
        var leavers = workers.Where(x => x.EndDate >= from && x.EndDate <= to && InScope(x.Id, x.EndDate!.Value)).ToList();
        var averageHeadcount = (activeStart.Count + activeEnd.Count) / 2m;
        var turnover = averageHeadcount == 0 ? 0 : Math.Round(leavers.Count / averageHeadcount * 100m, 2);

        var runs = await db.PayrollRuns.AsNoTracking()
            .Include(x => x.PayPeriod)
            .Where(x => ReleasedStatuses.Contains(x.Status) && !x.IsReversal && x.PayPeriod != null && x.PayPeriod.EndDate >= from && x.PayPeriod.EndDate <= to)
            .ToListAsync(ct);
        var runIds = runs.Select(x => x.Id).ToHashSet();
        var lines = await db.PayrollRunLines.AsNoTracking().Where(x => runIds.Contains(x.RunId) && !x.IsExcluded).ToListAsync(ct);
        var runById = runs.ToDictionary(x => x.Id);
        var scopedLines = lines.Where(x =>
        {
            var periodEnd = runById[x.RunId].PayPeriod!.EndDate;
            return InScope(x.WorkerId, periodEnd);
        }).ToList();
        var lineIds = scopedLines.Select(x => x.Id).ToHashSet();
        var components = await db.PayrollLineComponents.AsNoTracking().Where(x => lineIds.Contains(x.RunLineId)).ToListAsync(ct);

        var leaveRequests = await db.LeaveRequests.AsNoTracking()
            .Where(x => x.StartDate <= to && x.EndDate >= from).ToListAsync(ct);
        leaveRequests = leaveRequests.Where(x => InScope(x.WorkerId, x.StartDate)).ToList();
        var attendance = await db.AttendanceRecords.AsNoTracking()
            .Where(x => x.WorkDate >= from && x.WorkDate <= to).ToListAsync(ct);
        attendance = attendance.Where(x => InScope(x.WorkerId, x.WorkDate)).ToList();
        var movementRows = await db.Movements.AsNoTracking()
            .Where(x => x.EffectiveDate >= from && x.EffectiveDate <= to && (x.Status == "approved" || x.Status == "executed"))
            .ToListAsync(ct);
        movementRows = movementRows.Where(x => InScope(x.WorkerId, x.EffectiveDate)).ToList();

        // DateTimeOffset comparison is evaluated after the tenant-scoped read
        // so the service behaves identically on PostgreSQL and the SQLite
        // acceptance-test provider.
        var candidateRows = await db.Candidates.AsNoTracking().Include(x => x.Vacancy).ToListAsync(ct);
        var candidateFrom = ToUtc(from);
        var candidateTo = ToUtc(to.AddDays(1));
        candidateRows = candidateRows.Where(x => x.CreatedAt >= candidateFrom && x.CreatedAt < candidateTo && x.Vacancy is not null &&
            (query.OrgUnitId is null || x.Vacancy.OrgUnitId == query.OrgUnitId) &&
            (query.LegalEntityId is null || orgUnits.FirstOrDefault(o => o.Id == x.Vacancy.OrgUnitId)?.LegalEntityId == query.LegalEntityId)).ToList();

        var departmentRows = BuildDepartments(activeEnd, scopedLines, runById, orgUnits, Unit, to);
        var statutory = BuildStatutory(components);
        var scheduled = attendance.Sum(x => x.ScheduledHours);
        var absenceHours = attendance.Where(x => x.DerivedStatus == "absent").Sum(x => x.ScheduledHours);
        var absenceRate = scheduled == 0 ? 0 : Math.Round(absenceHours / scheduled * 100m, 2);
        var approvedLeave = leaveRequests.Where(x => x.Status == "approved").Sum(x => x.RequestedDays);
        var overtime = attendance.Sum(x => x.OvertimeHours);

        var kpis = new List<ManagementKpiDto>
        {
            new("headcount", "Active headcount", activeEnd.Count, "people", "Workers active on the reporting end date.", "Workers + effective-dated assignments"),
            new("turnover", "Turnover", turnover, "percent", "Leavers in window divided by average opening and closing headcount.", "Worker start/end dates"),
            new("employer-cost", "Employer cost", scopedLines.Sum(x => x.GrossPay + x.EmployerCost), "ZMW", "Released gross pay plus employer contributions.", "Released payroll run lines"),
            new("net-pay", "Net pay", scopedLines.Sum(x => x.NetPay), "ZMW", "Net pay from released payroll run lines.", "Released payroll run lines"),
            new("absence-rate", "Absence rate", absenceRate, "percent", "Scheduled hours on records marked absent divided by scheduled recorded hours.", "Attendance records"),
            new("overtime", "Overtime", overtime, "hours", "Calculated overtime hours in the selected window.", "Attendance records"),
            new("approved-leave", "Approved leave", approvedLeave, "days", "Requested days on approved leave requests overlapping the window.", "Leave requests"),
            new("hires", "New hires", hires.Count, "people", "Workers whose start date falls in the selected window.", "Worker records")
        };

        var filters = new ManagementReportFiltersDto(from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"),
            legalEntities.Select(x => new ReportDimensionDto(x.Id, x.Code, x.TradingName ?? x.RegisteredName)).ToList(),
            orgUnits.Where(x => query.LegalEntityId is null || x.LegalEntityId == query.LegalEntityId).Select(x => new ReportDimensionDto(x.Id, x.Code, x.Name)).ToList(),
            locations.Where(x => query.LegalEntityId is null || x.LegalEntityId == query.LegalEntityId).Select(x => new ReportDimensionDto(x.Id, x.Code, x.Name)).ToList());

        return new ManagementDashboardDto(
            DateTimeOffset.UtcNow.ToString("o"), to.ToString("yyyy-MM-dd"), filters, kpis,
            BuildTrend(from, to, workers, scopedLines, runById, InScope, ActiveAt), departmentRows,
            leaveRequests.GroupBy(x => x.LeaveTypeCode).OrderBy(x => x.Key).Select(g => new LeaveReportDto(g.Key, g.Count(),
                g.Where(x => x.Status == "approved").Sum(x => x.RequestedDays),
                g.Where(x => x.Status is "submitted" or "in-review").Sum(x => x.RequestedDays))).ToList(),
            attendance.GroupBy(x => x.DerivedStatus).OrderBy(x => x.Key).Select(g => new AttendanceReportDto(g.Key, g.Count(),
                g.Sum(x => x.ScheduledHours), g.Sum(x => x.TotalHours), g.Sum(x => x.OvertimeHours))).ToList(),
            BuildRecruitment(candidateRows),
            movementRows.GroupBy(x => x.MovementType).OrderBy(x => x.Key).Select(g => new MovementReportDto(g.Key, g.Count())).ToList(),
            statutory, Catalogue,
            [
                "Payroll totals include released and closed runs only; reversed originals are excluded by lifecycle status.",
                "Department payroll uses the worker assignment effective on each pay-period end date.",
                "Employer cost reconciles to gross pay plus employer contribution components; statutory totals use immutable line components."
            ]);
    }

    public async Task<ManagementReportExport> ExportAsync(string reportType, ManagementReportQuery query, CancellationToken ct)
    {
        var dashboard = await GetDashboardAsync(query, ct);
        var payrollReport = reportType is "payroll-department" or "payroll-detail" or "payroll-journal" or "statutory-liability";
        if (payrollReport && !authz.IsRole("payroll", "hr_admin"))
            throw new DomainException("report-forbidden", "Payroll and statutory exports require Payroll or HR Admin access.");

        var (from, to) = ParseWindow(query);
        var csv = reportType switch
        {
            "workforce-summary" => ExportWorkforce(dashboard),
            "payroll-department" => ExportDepartments(dashboard.Departments),
            "payroll-detail" => await ExportPayrollDetail(query, from, to, ct),
            "payroll-journal" => ExportJournal(dashboard),
            "statutory-liability" => ExportStatutory(dashboard.StatutoryLiability),
            "leave-attendance" => ExportTime(dashboard),
            "recruitment-funnel" => ExportRecruitment(dashboard.Recruitment),
            "workforce-movements" => ExportMovements(dashboard.Movements),
            _ => throw new DomainException("report-not-found", $"Report type {reportType} is not available.")
        };
        return new ManagementReportExport($"{reportType}-{from:yyyyMMdd}-{to:yyyyMMdd}.csv", "text/csv; charset=utf-8", Encoding.UTF8.GetBytes(csv));
    }

    private async Task<string> ExportPayrollDetail(ManagementReportQuery query, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.PayrollRunLines.AsNoTracking().Include(x => x.Worker).Include(x => x.Run)!.ThenInclude(x => x!.PayPeriod)
            .Where(x => !x.IsExcluded && x.Run != null && ReleasedStatuses.Contains(x.Run.Status) && !x.Run.IsReversal && x.Run.PayPeriod != null && x.Run.PayPeriod.EndDate >= from && x.Run.PayPeriod.EndDate <= to)
            .OrderBy(x => x.Run!.PayPeriod!.EndDate).ThenBy(x => x.Worker!.EmployeeNo).ToListAsync(ct);
        var assignments = await db.Assignments.AsNoTracking().ToListAsync(ct);
        var units = await db.OrgUnits.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        var scoped = rows.Where(x =>
        {
            var date = x.Run!.PayPeriod!.EndDate;
            var assignment = assignments.Where(a => a.WorkerId == x.WorkerId && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date)).OrderByDescending(a => a.EffectiveFrom).FirstOrDefault();
            var unitId = assignment?.OrgUnitId ?? x.Worker?.OrgUnitId;
            var entityId = assignment?.LegalEntityId ?? (unitId.HasValue && units.TryGetValue(unitId.Value, out var unit) ? unit.LegalEntityId : (Guid?)null);
            var locationId = assignment?.LocationId ?? x.Worker?.LocationId;
            return (query.LegalEntityId is null || entityId == query.LegalEntityId) && (query.OrgUnitId is null || unitId == query.OrgUnitId) && (query.LocationId is null || locationId == query.LocationId);
        });
        var output = new StringBuilder("period,employee_no,employee,department,gross,deductions,net,employer_contributions,employer_cost\n");
        foreach (var x in scoped)
        {
            var date = x.Run!.PayPeriod!.EndDate;
            var assignment = assignments.Where(a => a.WorkerId == x.WorkerId && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date)).OrderByDescending(a => a.EffectiveFrom).FirstOrDefault();
            var unitId = assignment?.OrgUnitId ?? x.Worker?.OrgUnitId;
            output.AppendLine(string.Join(',', Csv(x.Run.PayPeriod.PeriodLabel), Csv(x.Worker?.EmployeeNo), Csv(x.Worker?.FullName), Csv(unitId.HasValue && units.TryGetValue(unitId.Value, out var unit) ? unit.Name : "Unassigned"), Num(x.GrossPay), Num(x.TotalDeductions), Num(x.NetPay), Num(x.EmployerCost), Num(x.GrossPay + x.EmployerCost)));
        }
        return output.ToString();
    }

    private static List<DepartmentReportDto> BuildDepartments(List<Worker> active, List<PayrollRunLine> lines,
        Dictionary<Guid, PayrollRun> runs, List<OrgUnit> units, Func<Guid, DateOnly, Guid?> unitAt, DateOnly asOf)
    {
        var ids = active.Select(x => unitAt(x.Id, asOf)).Concat(lines.Select(x => unitAt(x.WorkerId, runs[x.RunId].PayPeriod!.EndDate))).Distinct().ToList();
        return ids.Select(id =>
        {
            var departmentLines = lines.Where(x => unitAt(x.WorkerId, runs[x.RunId].PayPeriod!.EndDate) == id).ToList();
            return new DepartmentReportDto(id, units.FirstOrDefault(x => x.Id == id)?.Name ?? "Unassigned",
                active.Count(x => unitAt(x.Id, asOf) == id), departmentLines.Select(x => x.WorkerId).Distinct().Count(),
                departmentLines.Sum(x => x.GrossPay), departmentLines.Sum(x => x.TotalDeductions), departmentLines.Sum(x => x.NetPay),
                departmentLines.Sum(x => x.EmployerCost), departmentLines.Sum(x => x.GrossPay + x.EmployerCost));
        }).OrderByDescending(x => x.EmployerCost).ThenBy(x => x.Department).ToList();
    }

    private static List<WorkforceTrendDto> BuildTrend(DateOnly from, DateOnly to, List<Worker> workers,
        List<PayrollRunLine> lines, Dictionary<Guid, PayrollRun> runs, Func<Guid, DateOnly, bool> inScope, Func<Worker, DateOnly, bool> activeAt)
    {
        var result = new List<WorkforceTrendDto>();
        var cursor = new DateOnly(from.Year, from.Month, 1);
        while (cursor <= to)
        {
            var monthEnd = new DateOnly(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            if (monthEnd > to) monthEnd = to;
            var monthStart = cursor < from ? from : cursor;
            var monthLines = lines.Where(x => { var d = runs[x.RunId].PayPeriod!.EndDate; return d.Year == cursor.Year && d.Month == cursor.Month; }).ToList();
            result.Add(new WorkforceTrendDto(cursor.ToString("yyyy-MM"),
                workers.Count(x => activeAt(x, monthEnd) && inScope(x.Id, monthEnd)),
                workers.Count(x => x.StartDate >= monthStart && x.StartDate <= monthEnd && inScope(x.Id, x.StartDate!.Value)),
                workers.Count(x => x.EndDate >= monthStart && x.EndDate <= monthEnd && inScope(x.Id, x.EndDate!.Value)),
                monthLines.Sum(x => x.GrossPay), monthLines.Sum(x => x.GrossPay + x.EmployerCost)));
            cursor = cursor.AddMonths(1);
        }
        return result.TakeLast(24).ToList();
    }

    private static List<RecruitmentReportDto> BuildRecruitment(List<Candidate> candidates)
    {
        string[] stages = ["applied", "screening", "shortlisted", "interviewing", "interviewed", "offered", "preboarding", "hired", "rejected"];
        return stages.Select(stage => new RecruitmentReportDto(stage, candidates.Count(x => x.Stage == stage),
            candidates.Count == 0 ? 0 : Math.Round(candidates.Count(x => x.Stage == stage) * 100m / candidates.Count, 1)))
            .Where(x => x.Candidates > 0).ToList();
    }

    private static StatutoryLiabilityReportDto BuildStatutory(List<PayrollLineComponent> components)
    {
        decimal Sum(params string[] codes) => components.Where(x => codes.Contains(x.ComponentCode, StringComparer.OrdinalIgnoreCase)).Sum(x => Math.Abs(x.Amount));
        var paye = Sum("paye");
        var napsaEe = Sum("napsa-ee");
        var napsaEr = Sum("napsa-er");
        var nhimaEe = Sum("nhima-ee", "nhima");
        var nhimaEr = Sum("nhima-er");
        return new(paye, napsaEe, napsaEr, nhimaEe, nhimaEr, paye + napsaEe + napsaEr + nhimaEe + nhimaEr);
    }

    private static string ExportWorkforce(ManagementDashboardDto d) =>
        "section,metric,value,unit\n" +
        string.Join('\n', d.Kpis.Where(x => x.Code is "headcount" or "turnover" or "hires").Select(x => $"workforce,{Csv(x.Label)},{Num(x.Value)},{Csv(x.Unit)}")) + "\n" +
        string.Join('\n', d.Movements.Select(x => $"movement,{Csv(x.MovementType)},{x.Movements},count")) + "\n";
    private static string ExportDepartments(List<DepartmentReportDto> rows) =>
        "department,headcount,payroll_workers,gross,deductions,net,employer_contributions,employer_cost\n" +
        string.Join('\n', rows.Select(x => $"{Csv(x.Department)},{x.Headcount},{x.PayrollWorkers},{Num(x.GrossPay)},{Num(x.Deductions)},{Num(x.NetPay)},{Num(x.EmployerContributions)},{Num(x.EmployerCost)}")) + "\n";
    private static string ExportJournal(ManagementDashboardDto d)
    {
        var gross = d.Departments.Sum(x => x.GrossPay); var deductions = d.Departments.Sum(x => x.Deductions); var net = d.Departments.Sum(x => x.NetPay); var employer = d.Departments.Sum(x => x.EmployerContributions); var total = gross + employer;
        return "account,debit,credit\n" + $"PAYROLL_EXPENSE,{Num(total)},0.00\nNET_PAY_CLEARING,0.00,{Num(net)}\nEMPLOYEE_DEDUCTIONS_PAYABLE,0.00,{Num(deductions)}\nEMPLOYER_CONTRIBUTIONS_PAYABLE,0.00,{Num(employer)}\nCONTROL_TOTAL,{Num(total)},{Num(net + deductions + employer)}\n";
    }
    private static string ExportStatutory(StatutoryLiabilityReportDto x) =>
        $"liability,amount\nPAYE,{Num(x.Paye)}\nNAPSA employee,{Num(x.NapsaEmployee)}\nNAPSA employer,{Num(x.NapsaEmployer)}\nNHIMA employee,{Num(x.NhimaEmployee)}\nNHIMA employer,{Num(x.NhimaEmployer)}\nTOTAL,{Num(x.Total)}\n";
    private static string ExportTime(ManagementDashboardDto d) =>
        "section,type,records_or_requests,days_or_scheduled_hours,worked_hours,overtime_hours\n" +
        string.Join('\n', d.Leave.Select(x => $"leave,{Csv(x.LeaveType)},{x.Requests},{Num(x.ApprovedDays)},,")) + "\n" +
        string.Join('\n', d.Attendance.Select(x => $"attendance,{Csv(x.Status)},{x.Records},{Num(x.ScheduledHours)},{Num(x.WorkedHours)},{Num(x.OvertimeHours)}")) + "\n";
    private static string ExportRecruitment(List<RecruitmentReportDto> rows) =>
        "stage,candidates,percentage\n" + string.Join('\n', rows.Select(x => $"{Csv(x.Stage)},{x.Candidates},{Num(x.Percentage)}")) + "\n";
    private static string ExportMovements(List<MovementReportDto> rows) =>
        "movement_type,movements\n" + string.Join('\n', rows.Select(x => $"{Csv(x.MovementType)},{x.Movements}")) + "\n";

    private static (DateOnly From, DateOnly To) ParseWindow(ManagementReportQuery query)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = ParseDate(query.ToDate) ?? today;
        var from = ParseDate(query.FromDate) ?? new DateOnly(to.Year, 1, 1);
        if (from > to) throw new DomainException("report-date-range", "From date cannot be after to date.");
        if (to.DayNumber - from.DayNumber > 3660) throw new DomainException("report-date-range", "Reporting windows cannot exceed ten years.");
        return (from, to);
    }
    private static DateOnly? ParseDate(string? value) => string.IsNullOrWhiteSpace(value) ? null :
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : throw new DomainException("report-date", $"'{value}' is not a valid ISO date.");
    private static DateTimeOffset ToUtc(DateOnly value) => new(value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    private static string Num(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Csv(object? value) => $"\"{(value?.ToString() ?? "").Replace("\"", "\"\"")}\"";
}
