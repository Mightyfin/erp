using System.Globalization;
// M31b — attendance schema for the shared import tool. Adapts the existing
// ITimeService.ImportAttendanceAsync so attendance logs can be uploaded via
// the same column-mapping UI as employees.
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Shared;

public sealed class AttendanceImportSchema : IImportSchemaWithExport
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
        new("workerName", "Employee name", false),
        new("workDate", "Date", true, FormatNote: "DD-MM-YYYY"),
        new("clockIn", "Clock in", false, FormatNote: "HH:mm"),
        new("clockOut", "Clock out", false, FormatNote: "HH:mm"),
        new("source", "Source", false),
        new("derivedStatus", "Attendance status", false),
        new("totalHours", "Total hours", false),
        new("scheduledHours", "Scheduled hours", false),
        new("regularHours", "Regular hours", false),
        new("overtimeHours", "Overtime hours", false),
        new("overtimeMultiplier", "Overtime multiplier", false),
        new("overtimeStatus", "Overtime lifecycle", false),
        new("overtimeDecisionReason", "Overtime decision reason", false),
        new("overtimeDecidedAt", "Overtime decision time", false),
        new("overtimePayrollRunId", "Payroll run", false),
        new("overtimePayrollLineId", "Payroll line", false),
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

        if (!TryParseImportDate(workDateStr, out var date))
            return new ImportRowOutcome("error", $"Date '{workDateStr}' is invalid — use DD-MM-YYYY.");

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
                    NormalizeImportDate(row.Get("workDate")) ?? row.Get("workDate").Trim(),
                    OrNull(row.Get("clockIn")),
                    OrNull(row.Get("clockOut")))
            });

        await timeService.ImportAttendanceAsync(request, authz.CurrentSubjectId ?? "system", ct);
    }

    public async Task<List<Dictionary<string, string>>> ExportRowsAsync(string? filter, CancellationToken ct)
    {
        var from = FilterValue(filter, "from");
        var to = FilterValue(filter, "to");
        var rows = await timeService.ListAttendanceForScopeAsync(from, to, ct);
        return rows.Select(row => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["employeeNo"] = row.WorkerEmployeeNo,
            ["workerName"] = row.WorkerName,
            ["workDate"] = NormalizeDate(row.WorkDate),
            ["clockIn"] = row.ClockIn ?? "",
            ["clockOut"] = row.ClockOut ?? "",
            ["source"] = row.Source,
            ["derivedStatus"] = row.DerivedStatus,
            ["totalHours"] = row.TotalHours.ToString(CultureInfo.InvariantCulture),
            ["scheduledHours"] = row.ScheduledHours.ToString(CultureInfo.InvariantCulture),
            ["regularHours"] = row.RegularHours.ToString(CultureInfo.InvariantCulture),
            ["overtimeHours"] = row.OvertimeHours.ToString(CultureInfo.InvariantCulture),
            ["overtimeMultiplier"] = row.OvertimeMultiplier.ToString(CultureInfo.InvariantCulture),
            ["overtimeStatus"] = row.OvertimeStatus,
            ["overtimeDecisionReason"] = row.OvertimeDecisionReason ?? "",
            ["overtimeDecidedAt"] = row.OvertimeDecidedAt?.ToString("O", CultureInfo.InvariantCulture) ?? "",
            ["overtimePayrollRunId"] = row.OvertimePayrollRunId?.ToString() ?? "",
            ["overtimePayrollLineId"] = row.OvertimePayrollLineId?.ToString() ?? "",
        }).ToList();
    }

    private static string? FilterValue(string? filter, string key)
    {
        if (string.IsNullOrWhiteSpace(filter)) return null;
        foreach (var part in filter.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = part.Split('=', 2);
            if (bits.Length == 2 && bits[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(bits[1]);
        }
        return null;
    }

    private static string NormalizeDate(string value)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return value;
    }

    private static readonly string[] ImportDateFormats =
    [
        "dd-MM-yyyy",
        "dd/MM/yyyy",
        "dd.MM.yyyy",
        "yyyy-MM-dd",
    ];

    private static bool TryParseImportDate(string? value, out DateOnly date)
    {
        var t = value?.Trim();
        if (string.IsNullOrWhiteSpace(t))
        {
            date = default;
            return false;
        }

        return DateOnly.TryParseExact(t, ImportDateFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    private static string? NormalizeImportDate(string? value) =>
        TryParseImportDate(value, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

    private static string? OrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
