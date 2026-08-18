// M31 — workers (employees) schema for the shared import/export engine.
// Reuses the existing worker creation service so imports go through the same
// lifecycle as the UI form (naming rules, entity/unit resolution, validations),
// and adds Update mode matched on employee number with NRC/NAPSA fallback.
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Shared;

public sealed class WorkersImportSchema : IImportSchemaWithExport
{
    private readonly IWorkerRepository repo;
    private readonly IWorkerService workers;
    private readonly IAuthzService authz;

    public WorkersImportSchema(IWorkerRepository repo, IWorkerService workers, IAuthzService authz)
    {
        this.repo = repo;
        this.workers = workers;
        this.authz = authz;
    }

    public string TypeKey => "workers";
    public string DisplayName => "Employees";

    public List<ImportFieldDef> Fields =>
    [
        new("employeeNo", "Employee number", false, NaturalKey: true, Example: "EMP-0008"),
        new("firstName", "First name", true, Example: "Mary"),
        new("lastName", "Last name", true, Example: "Bwalya"),
        new("middleName", "Middle name", false, Example: "Chileshe"),
        new("email", "Work email", false, FormatNote: "e.g. mary@example.com"),
        new("phone", "Phone", false, FormatNote: "e.g. 0971234567"),
        new("nrc", "NRC", false, FormatNote: "e.g. 123456/78/1"),
        new("tpin", "TPIN", false, FormatNote: "10 digits"),
        new("napsaNumber", "NAPSA number", false, FormatNote: "e.g. NAPSA-001"),
        new("nhimaNumber", "NHIMA number", false, FormatNote: "e.g. NHIMA-001"),
        new("grade", "Grade", false, Example: "G5"),
        new("jobTitle", "Job title", false, Example: "Accounts Officer"),
        new("startDate", "Start date", false, FormatNote: "YYYY-MM-DD"),
        new("workerType", "Employment type", false, Example: "employee | contingent | intern | volunteer"),
        new("orgUnitName", "Department", false, FormatNote: "exact department name, e.g. Finance"),
    ];

    public async Task<ImportRowOutcome> PreviewRowAsync(IDictionary<string, string> row, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var firstName = row.Get("firstName");
        var lastName = row.Get("lastName");
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return new ImportRowOutcome("error", "First name and last name are required.");
        var email = row.Get("email");
        var emailRx = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        if (!string.IsNullOrWhiteSpace(email) && !emailRx.IsMatch(email))
            return new ImportRowOutcome("error", $"Email '{email}' is not valid.");
        var orgUnits = await repo.ListAllOrgUnitsAsync(ct);
        var orgUnitName = row.Get("orgUnitName");
        if (!string.IsNullOrWhiteSpace(orgUnitName) &&
            !orgUnits.Any(u => u.Name.Equals(orgUnitName, StringComparison.OrdinalIgnoreCase)))
            return new ImportRowOutcome("error", $"No department named '{orgUnitName}' exists.");
        // Natural-key match drives the Insert-vs-Update status in the preview.
        var target = await repo.FindByNaturalKeyAsync(row.Get("employeeNo"), row.Get("nrc"), row.Get("napsaNumber"), ct);
        if (target is not null)
            return new ImportRowOutcome("update", $"Existing record: {target.FullName}", row);
        // Cheap duplicate scan for the keys that would collide on insert.
        var emailHits = await repo.ListAsync(new WorkerListFilters(email?.ToLower(), null, null, null, null, null, true, 1, 25), ct);
        if (!string.IsNullOrWhiteSpace(email) &&
            emailHits.Items.Any(w => w.Email is not null && w.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            return new ImportRowOutcome("error", $"Email '{email}' is already in use.");
        return new ImportRowOutcome("create", null, row);
    }

    public async Task ApplyRowAsync(IDictionary<string, string> row, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var target = await repo.FindByNaturalKeyAsync(row.Get("employeeNo"), row.Get("nrc"), row.Get("napsaNumber"), ct);
        var orgUnits = await repo.ListAllOrgUnitsAsync(ct);
        var orgUnitName = row.Get("orgUnitName");
        Guid? orgUnitId = null;
        if (!string.IsNullOrWhiteSpace(orgUnitName))
            orgUnitId = orgUnits.FirstOrDefault(u => u.Name.Equals(orgUnitName, StringComparison.OrdinalIgnoreCase))?.Id;
        if (!string.IsNullOrWhiteSpace(orgUnitName) && orgUnitId is null)
            throw new DomainException("org-unit-not-found", $"No department named '{orgUnitName}' exists.");
        var workerType = row.Get("workerType");
        if (workerType is not ("employee" or "contingent" or "intern" or "volunteer")) workerType = "employee";

        if (target is not null)
        {
            // Update mode — patch the mutable fields only.
            var patch = new WorkerUpdateRequest(
                FirstName: OrNull(row.Get("firstName")),
                MiddleName: OrNull(row.Get("middleName")),
                LastName: OrNull(row.Get("lastName")),
                Email: OrNull(row.Get("email")),
                Phone: OrNull(row.Get("phone")),
                Grade: OrNull(row.Get("grade")),
                JobTitle: OrNull(row.Get("jobTitle")),
                OrgUnitId: orgUnitId);
            await workers.UpdateAsync(target.Id, patch, ct);
        }
        else
        {
            // Insert mode — full lifecycle via the same service the UI uses.
            var request = new WorkerCreateRequest(
                OrNull(row.Get("employeeNo")),
                OrNull(row.Get("firstName")), OrNull(row.Get("lastName")),
                MiddleName: OrNull(row.Get("middleName")),
                Email: OrNull(row.Get("email")),
                Phone: OrNull(row.Get("phone")),
                Nrc: OrNull(row.Get("nrc")),
                Tpin: OrNull(row.Get("tpin")),
                NapsaNumber: OrNull(row.Get("napsaNumber")),
                NhimaNumber: OrNull(row.Get("nhimaNumber")),
                Grade: OrNull(row.Get("grade")),
                JobTitle: OrNull(row.Get("jobTitle")),
                StartDate: OrNull(row.Get("startDate")),
                WorkerType: workerType,
                OrgUnitId: orgUnitId);
            await workers.CreateAsync(request, ct);
        }
    }

    public async Task<List<Dictionary<string, string>>> ExportRowsAsync(string? filter, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        // M31: full roster exported so the file round-trips into the importer;
        // status filtering is offered by the export UI as follow-up.
        var items = await repo.ListAllWorkersAsync(null, ct);
        var orgUnits = await repo.ListAllOrgUnitsAsync(ct);
        var byId = new Dictionary<Guid, string>(orgUnits.Count);
        foreach (var u in orgUnits) byId[u.Id] = u.Name;
        return items.Select(w => new Dictionary<string, string>
        {
            ["employeeNo"] = w.EmployeeNo ?? "",
            ["firstName"] = w.FirstName,
            ["lastName"] = w.LastName,
            ["middleName"] = w.MiddleName ?? "",
            ["email"] = w.Email ?? "",
            ["phone"] = w.Phone ?? "",
            ["nrc"] = w.Nrc ?? "",
            ["tpin"] = w.Tpin ?? "",
            ["napsaNumber"] = w.NapsaNumber ?? "",
            ["nhimaNumber"] = w.NhimaNumber ?? "",
            ["grade"] = w.Grade ?? "",
            ["jobTitle"] = w.JobTitle ?? "",
            ["startDate"] = w.StartDate?.ToString("yyyy-MM-dd") ?? "",
            ["workerType"] = w.WorkerType ?? "employee",
            ["orgUnitName"] = w.OrgUnitId.HasValue && byId.TryGetValue(w.OrgUnitId.Value, out var n) ? n : "",
        }).ToList();
    }

    private static string? OrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
}

/// Convenience: dictionary lookup returning "" for missing keys.
internal static class ImportRowExtensions
{
    public static string Get(this IDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var v) ? (v ?? "") : "";
}
