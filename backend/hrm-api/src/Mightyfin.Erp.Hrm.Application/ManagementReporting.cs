namespace Mightyfin.Erp.Hrm.Application;

public sealed record ManagementReportQuery(
    string? FromDate = null,
    string? ToDate = null,
    Guid? LegalEntityId = null,
    Guid? OrgUnitId = null,
    Guid? LocationId = null);

public sealed record ReportDimensionDto(Guid Id, string Code, string Name);
public sealed record ManagementReportFiltersDto(
    string FromDate,
    string ToDate,
    List<ReportDimensionDto> LegalEntities,
    List<ReportDimensionDto> OrgUnits,
    List<ReportDimensionDto> Locations);
public sealed record ManagementKpiDto(
    string Code,
    string Label,
    decimal Value,
    string Unit,
    string Definition,
    string Source);
public sealed record WorkforceTrendDto(
    string Period,
    int Headcount,
    int Hires,
    int Leavers,
    decimal GrossPay,
    decimal EmployerCost);
public sealed record DepartmentReportDto(
    Guid? OrgUnitId,
    string Department,
    int Headcount,
    int PayrollWorkers,
    decimal GrossPay,
    decimal Deductions,
    decimal NetPay,
    decimal EmployerContributions,
    decimal EmployerCost);
public sealed record LeaveReportDto(string LeaveType, int Requests, decimal ApprovedDays, decimal PendingDays);
public sealed record AttendanceReportDto(string Status, int Records, decimal ScheduledHours, decimal WorkedHours, decimal OvertimeHours);
public sealed record RecruitmentReportDto(string Stage, int Candidates, decimal Percentage);
public sealed record MovementReportDto(string MovementType, int Movements);
public sealed record StatutoryLiabilityReportDto(
    decimal Paye,
    decimal NapsaEmployee,
    decimal NapsaEmployer,
    decimal NhimaEmployee,
    decimal NhimaEmployer,
    decimal Total);
public sealed record ReportCatalogueItemDto(
    string Code,
    string Name,
    string Category,
    string Description,
    string Owner,
    bool Certified,
    bool PayrollRestricted,
    string Source);
public sealed record ManagementDashboardDto(
    string GeneratedAt,
    string DataThrough,
    ManagementReportFiltersDto Filters,
    List<ManagementKpiDto> Kpis,
    List<WorkforceTrendDto> Trend,
    List<DepartmentReportDto> Departments,
    List<LeaveReportDto> Leave,
    List<AttendanceReportDto> Attendance,
    List<RecruitmentReportDto> Recruitment,
    List<MovementReportDto> Movements,
    StatutoryLiabilityReportDto StatutoryLiability,
    List<ReportCatalogueItemDto> Catalogue,
    List<string> ReconciliationNotes);
public sealed record ManagementReportExport(string FileName, string ContentType, byte[] Content);

public interface IManagementReportingService
{
    Task<ManagementDashboardDto> GetDashboardAsync(ManagementReportQuery query, CancellationToken ct);
    Task<ManagementReportExport> ExportAsync(string reportType, ManagementReportQuery query, CancellationToken ct);
}
