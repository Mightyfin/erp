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
        new("email", "Work email", true, FormatNote: "e.g. mary@example.com"),
        new("phone", "Phone", true, FormatNote: "e.g. 0971234567"),
        new("nrc", "NRC", false, FormatNote: "e.g. 123456/78/1"),
        new("tpin", "TPIN", false, FormatNote: "10 digits"),
        new("napsaNumber", "NAPSA number", false, FormatNote: "e.g. NAPSA-001"),
        new("nhimaNumber", "NHIMA number", false, FormatNote: "e.g. NHIMA-001"),
        new("grade", "Grade", false, Example: "G5"),
        new("jobTitle", "Job title", false, Example: "Accounts Officer"),
        new("startDate", "Start date", false, FormatNote: "YYYY-MM-DD"),
        new("workerType", "Employment type", true, Example: "employee | contingent | intern | volunteer"),
        new("orgUnitName", "Department", false, FormatNote: "exact department name, e.g. Finance"),

        // M31b flattening: history child fields (export-only in v1; import
        // matches parent and ignores these or uses them for bulk child init)
        new("edu.institution", "Education: Institution", false),
        new("edu.qualification", "Education: Qualification", false),
        new("edu.startYear", "Education: Start Year", false),
        new("edu.endYear", "Education: End Year", false),
        new("ext.company", "Previous Employer: Company", false),
        new("ext.role", "Previous Employer: Role", false),
        new("ext.startDate", "Previous Employer: Start Date", false),
        new("ext.endDate", "Previous Employer: End Date", false),
        new("int.orgUnitName", "Internal Move: Dept", false),
        new("int.role", "Internal Move: Role", false),
        new("int.startDate", "Internal Move: Start Date", false),
    ];

    public async Task<ImportRowOutcome> PreviewRowAsync(IDictionary<string, string> row, string mode, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var isUpdate = mode.Equals("update", StringComparison.OrdinalIgnoreCase);

        // ---- Non-negotiable required fields. Nothing processes without them —
        // except Update mode on an existing record: it already carries its
        // identity on file, so the import patches only the supplied fields. ----
        var employeeNo = row.Get("employeeNo").Trim();
        var nrc = row.Get("nrc").Trim();
        var napsa = row.Get("napsaNumber").Trim();
        var target = await repo.FindByNaturalKeyAsync(employeeNo, nrc, napsa, ct);
        if (!isUpdate && target is not null)
            return new ImportRowOutcome("error",
                !string.IsNullOrWhiteSpace(employeeNo) ? $"Employee number '{employeeNo}' is already assigned to an existing employee ({target.FullName}). Insert mode never overwrites — review the spreadsheet or switch to Update mode."
                : !string.IsNullOrWhiteSpace(nrc) ? $"NRC '{nrc}' is already registered to an existing employee ({target.FullName}). Insert mode never overwrites — review the spreadsheet or switch to Update mode."
                : $"NAPSA number '{napsa}' is already registered to an existing employee ({target.FullName}). Insert mode never overwrites — review the spreadsheet or switch to Update mode.");
        if (isUpdate && target is null)
        {
            var key = !string.IsNullOrWhiteSpace(employeeNo) ? $"employee number '{employeeNo}'"
                : !string.IsNullOrWhiteSpace(nrc) ? $"NRC '{nrc}'"
                : !string.IsNullOrWhiteSpace(napsa) ? $"NAPSA number '{napsa}'"
                : "employee number, NRC or NAPSA number";
            return new ImportRowOutcome("error", $"No existing employee matches {key}. Check the identifier or switch to Insert mode.");
        }
        if (target is null)
        {
            var missing = new List<string>();
            foreach (var field in Fields)
                if (field.Required && string.IsNullOrWhiteSpace(row.Get(field.Key)))
                    missing.Add(field.Label);
            if (missing.Count > 0)
                return new ImportRowOutcome("error",
                    $"Missing required { (missing.Count == 1 ? "field" : "fields") }: {string.Join(", ", missing)}. The row cannot be imported until they are filled in.");
        }

        var firstName = row.Get("firstName");
        var lastName = row.Get("lastName");
        var email = row.Get("email").Trim();
        var phone = row.Get("phone").Trim();
        var workerType = row.Get("workerType").Trim().ToLowerInvariant();

        // ---- Format checks with explicit what-is-wrong wording.
        // In Update mode a blank value means "leave the record's existing
        // value untouched", so only non-blank values are checked. ----
        if (EmailInvalid(email))
            return new ImportRowOutcome("error", $"Work email '{email}' is not a valid email address.");
        if (isUpdate && string.IsNullOrWhiteSpace(phone))
            phone = null; // blank in Update mode means "keep the existing phone"
        if (!string.IsNullOrWhiteSpace(phone) && !PhoneValid(phone))
            return new ImportRowOutcome("error", $"Phone '{phone}' is not valid — use 9–15 digits, e.g. 0971234567.");
        if (!string.IsNullOrWhiteSpace(workerType) &&
            workerType is not ("employee" or "contingent" or "intern" or "volunteer"))
            return new ImportRowOutcome("error", $"Employment type '{row.Get("workerType")}' is not valid — use one of: employee, contingent, intern, volunteer.");
        var tpin = row.Get("tpin").Trim();
        if (!string.IsNullOrWhiteSpace(tpin) && !TpinValid(tpin))
            return new ImportRowOutcome("error", $"TPIN '{tpin}' is not valid — use exactly 10 digits.");
        var startDate = row.Get("startDate").Trim();
        if (!string.IsNullOrWhiteSpace(startDate) && !DateTime.TryParseExact(startDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _))
            return new ImportRowOutcome("error", $"Start date '{startDate}' is not valid — use YYYY-MM-DD, e.g. 2026-01-02.");

        // ---- Cross-field uniqueness: never import two rows that collide with each
        // other, so a bad spreadsheet cannot mass-duplicate identities in one batch.
        // Existing-identity sets are loaded once per preview pass (small scans),
        // then each row checks against them plus the in-file Seen sets. ----
        if (!IdentitiesLoaded)
        {
            IdentitiesLoaded = true;
            var all = await repo.ListAllWorkersAsync(null, ct);
            foreach (var w in all)
            {
                if (!string.IsNullOrWhiteSpace(w.Email)) ExistingEmails.Add(w.Email.ToLowerInvariant());
                if (!string.IsNullOrWhiteSpace(w.Nrc)) ExistingNrcs.Add(w.Nrc.ToLowerInvariant());
                if (!string.IsNullOrWhiteSpace(w.EmployeeNo)) ExistingEmployeeNos.Add(w.EmployeeNo.ToLowerInvariant());
            }
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            var targetOwnsThis = target is not null &&
                !string.IsNullOrWhiteSpace(target.Email) &&
                target.Email.Equals(email, StringComparison.OrdinalIgnoreCase);
            if (!targetOwnsThis && ExistingEmails.Contains(email.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"Work email '{email}' is already in use by an existing employee.");
            if (!targetOwnsThis && !SeenEmails.Add(email.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"Work email '{email}' appears twice in this file. Each employee must have a unique email.");
        }
        // Update mode matched THIS record — its own identifiers are exempt
        // from the within-file uniqueness check (updating yourself is allowed).
        if (target is not null)
        {
            if (!string.IsNullOrWhiteSpace(target.Email)) _ = SeenEmails.Add(target.Email.ToLowerInvariant());
            if (!string.IsNullOrWhiteSpace(target.EmployeeNo)) _ = SeenEmployeeNos.Add(target.EmployeeNo.ToLowerInvariant());
            if (!string.IsNullOrWhiteSpace(target.Nrc)) _ = SeenNrcs.Add(target.Nrc.ToLowerInvariant());
        }
        if (!string.IsNullOrWhiteSpace(employeeNo))
        {
            var targetOwnsThis = target is not null &&
                !string.IsNullOrWhiteSpace(target.EmployeeNo) &&
                target.EmployeeNo.Equals(employeeNo, StringComparison.OrdinalIgnoreCase);
            if (!targetOwnsThis && ExistingEmployeeNos.Contains(employeeNo.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"Employee number '{employeeNo}' is already assigned to an existing employee.");
            // The matched record's own key was already exempted below;
            // only flag genuine within-file duplicates.
            if (!targetOwnsThis && !SeenEmployeeNos.Add(employeeNo.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"Employee number '{employeeNo}' appears twice in this file. Each employee must have a unique number.");
        }
        if (!string.IsNullOrWhiteSpace(nrc))
        {
            var targetOwnsThis = target is not null &&
                !string.IsNullOrWhiteSpace(target.Nrc) &&
                target.Nrc.Equals(nrc, StringComparison.OrdinalIgnoreCase);
            if (!targetOwnsThis && ExistingNrcs.Contains(nrc.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"NRC '{nrc}' is already in use by an existing employee.");
            if (!targetOwnsThis && !SeenNrcs.Add(nrc.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"NRC '{nrc}' appears twice in this file. Each employee must have a unique NRC.");
        }

        // ---- Reference data that must exist before payroll-relevant rows land. ----
        var orgUnits = await repo.ListAllOrgUnitsAsync(ct);
        var orgUnitName = row.Get("orgUnitName");
        if (!string.IsNullOrWhiteSpace(orgUnitName) &&
            !orgUnits.Any(u => u.Name.Equals(orgUnitName, StringComparison.OrdinalIgnoreCase)))
            return new ImportRowOutcome("error", $"No department named '{orgUnitName}' exists. Create it under Organisation structure first.");

        // ---- Natural-key match drives the Insert-vs-Update status in the preview. ----
        if (target is not null)
            return new ImportRowOutcome("update", $"Existing record: {target.FullName}", row);
        return new ImportRowOutcome("create", null, row);
    }

    // --- Batch-level identity uniqueness sets (cleared per schema instance).
    private readonly HashSet<string> SeenEmails = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> SeenEmployeeNos = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> SeenNrcs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ExistingEmails = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ExistingEmployeeNos = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ExistingNrcs = new(StringComparer.OrdinalIgnoreCase);
    private bool IdentitiesLoaded;

    private static readonly System.Text.RegularExpressions.Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static bool EmailInvalid(string email) => !string.IsNullOrWhiteSpace(email) && !EmailRx.IsMatch(email);
    // Zambian-style mobile landline numbers: digits only, 9-15 long (0971234567 style).
    private static bool PhoneValid(string phone) => !string.IsNullOrWhiteSpace(phone) && System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+?\d{9,15}$");
    // ZRA TPIN: exactly 10 digits.
    private static bool TpinValid(string tpin) => System.Text.RegularExpressions.Regex.IsMatch(tpin, @"^\d{10}$");

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
            // Update mode — patch only the fields the row actually supplies;
            // blank values leave the record's existing values untouched.
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

        // NOTE on blank-update semantics: ApplyRowAsync is only ever reached for
        // rows the preview approved. The preview already refused blank emails in
        // insert mode and treats blank optional values in update mode as
        // "unchanged", so no existing value is silently cleared here.
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
        // M31b: filter support (status=Active) + child-table flattening.
        // If a worker has multiple history records, we repeat the parent row.
        var status = filter?.StartsWith("status=") == true ? filter[7..] : null;
        var items = await repo.ListAllWorkersWithDetailsAsync(status, ct);

        var rows = new List<Dictionary<string, string>>();
        foreach (var w in items)
        {
            var baseRow = new Dictionary<string, string>
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
                ["orgUnitName"] = w.OrgUnit?.Name ?? "",
            };

            // Flatten child records: repeat the parent row for each child record
            // across the three types. If no children, just the base row.
            var max = Math.Max(w.Education.Count, Math.Max(w.ExternalWorkHistory.Count, w.InternalWorkHistory.Count));
            if (max == 0)
            {
                rows.Add(baseRow);
                continue;
            }

            var edus = w.Education.ToList();
            var exts = w.ExternalWorkHistory.ToList();
            var ints = w.InternalWorkHistory.ToList();

            for (var i = 0; i < max; i++)
            {
                var row = new Dictionary<string, string>(baseRow);
                if (i < edus.Count)
                {
                    row["edu.institution"] = edus[i].Institution;
                    row["edu.qualification"] = edus[i].Qualification;
                    row["edu.startYear"] = edus[i].StartYear?.ToString() ?? "";
                    row["edu.endYear"] = edus[i].EndYear?.ToString() ?? "";
                }
                if (i < exts.Count)
                {
                    row["ext.company"] = exts[i].Company;
                    row["ext.role"] = exts[i].Role ?? "";
                    row["ext.startDate"] = exts[i].StartDate ?? "";
                    row["ext.endDate"] = exts[i].EndDate ?? "";
                }
                if (i < ints.Count)
                {
                    row["int.orgUnitName"] = ints[i].OrgUnitName;
                    row["int.role"] = ints[i].Role ?? "";
                    row["int.startDate"] = ints[i].StartDate ?? "";
                }
                rows.Add(row);
            }
        }
        return rows;
    }

    private static string? OrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
}

/// Convenience: dictionary lookup returning "" for missing keys.
internal static class ImportRowExtensions
{
    public static string Get(this IDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var v) ? (v ?? "") : "";
}
