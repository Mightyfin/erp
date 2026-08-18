// M31b — attendance schema for the shared import tool. Adapts the existing
// ITimeService.ImportAttendanceAsync so attendance logs can be uploaded via
// the same column-mapping UI as employees.
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Shared;

public sealed class AttendanceImportSchema : IImportSchema
{
    private readonly IWorkerRepository workerRepo;
    private readonly ITimeService timeService;
    private readonly IAuthzService authz;

    public AttendanceImportSchema(IWorkerRepository workerRepo, ITimeService timeService, IAuthzService authz)
    {
        this.workerRepo = workerRepo;
        this.timeService = timeService;
        this.authz = authz;
    }

    public string TypeKey => "attendance";
    public string DisplayName => "Attendance logs";

    public List<ImportFieldDef> Fields =>
    [
        new("employeeNo", "Employee number", true, Example: "EMP-0001"),
        new("workDate", "Date", true, FormatNote: "YYYY-MM-DD"),
        new("clockIn", "Clock in", false, FormatNote: "HH:mm"),
        new("clockOut", "Clock out", false, FormatNote: "HH:mm"),
    ];

    public async Task<ImportRowOutcome> PreviewRowAsync(IDictionary<string, string> row, string mode, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");

        var employeeNo = row.Get("employeeNo").Trim();
        var workDateStr = row.Get("workDate").Trim();
        var clockInStr = row.Get("clockIn").Trim();
        var clockOutStr = row.Get("clockOut").Trim();

        if (string.IsNullOrWhiteSpace(employeeNo))
            return new ImportRowOutcome("error", "Employee number is required.");

        var worker = await workerRepo.FindByNaturalKeyAsync(employeeNo, null, null, ct);
        if (worker is null)
            return new ImportRowOutcome("error", $"Employee '{employeeNo}' not found.");

        if (!DateOnly.TryParse(workDateStr, out var date))
            return new ImportRowOutcome("error", $"Date '{workDateStr}' is invalid — use YYYY-MM-DD.");

        if (!string.IsNullOrWhiteSpace(clockInStr) && !TimeOnly.TryParse(clockInStr, out _))
            return new ImportRowOutcome("error", $"Clock-in time '{clockInStr}' is invalid — use HH:mm.");

        if (!string.IsNullOrWhiteSpace(clockOutStr) && !TimeOnly.TryParse(clockOutStr, out _))
            return new ImportRowOutcome("error", $"Clock-out time '{clockOutStr}' is invalid — use HH:mm.");

        return new ImportRowOutcome("create", "Row is valid and will be imported.");
    }

    public async Task ApplyRowAsync(IDictionary<string, string> row, CancellationToken ct)
    {
        // ITimeService.ImportAttendanceAsync already handles the batch and persistence.
        // For the shared tool, we call it with a single-row request.
        var request = new AttendanceImportRequest(
            "shared-tool-import",
            new List<AttendanceImportRow>
            {
                new AttendanceImportRow(
                    row.Get("employeeNo").Trim(),
                    row.Get("workDate").Trim(),
                    OrNull(row.Get("clockIn")),
                    OrNull(row.Get("clockOut")))
            });

        await timeService.ImportAttendanceAsync(request, authz.CurrentSubjectId ?? "system", ct);
    }

    private static string? OrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
