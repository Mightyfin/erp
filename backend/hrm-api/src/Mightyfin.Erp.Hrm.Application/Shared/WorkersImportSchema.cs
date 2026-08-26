// M31 — workers (employees) schema for the shared import/export engine.
// Reuses the existing worker creation service so imports go through the same
// lifecycle as the UI form (naming rules, entity/unit resolution, validations),
// and adds Update mode matched on employee number with NRC/NAPSA fallback.
using System.Globalization;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Shared;

public sealed class WorkersImportSchema : IImportSchemaWithExport
{
    private readonly IWorkerRepository repo;
    private readonly IWorkerService workers;
    private readonly IAuthzService authz;
    private readonly ShellContext scope;

    public WorkersImportSchema(IWorkerRepository repo, IWorkerService workers, IAuthzService authz, ShellContext scope)
    {
        this.repo = repo;
        this.workers = workers;
        this.authz = authz;
        this.scope = scope;
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
        new("startDate", "Start date", false, FormatNote: "DD-MM-YYYY"),
        new("locationId", "Branch", false, FormatNote: "guid of the work location, optional — current work scope is used when empty"),
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
        // ---- Child rows (education / previous employment / internal moves) —
        // optional, but when supplied they must be self-consistent (e.g. an
        // education end year may not precede its start year). ----
        if (IsNonBlank(row.Get("edu.institution")) || IsNonBlank(row.Get("edu.qualification")) ||
            IsNonBlank(row.Get("edu.startYear")) || IsNonBlank(row.Get("edu.endYear")))
        {
            var inst = row.Get("edu.institution").Trim();
            var qual = row.Get("edu.qualification").Trim();
            if (IsNonBlank(inst) || IsNonBlank(qual))
            {
                if (string.IsNullOrWhiteSpace(inst) || string.IsNullOrWhiteSpace(qual))
                    return new ImportRowOutcome("error",
                        string.IsNullOrWhiteSpace(inst) ? "Education row: Institution is empty — both Institution and Qualification must be filled for an education record."
                        : "Education row: Qualification is empty — both Institution and Qualification must be filled for an education record.");
                if (int.TryParse(row.Get("edu.startYear").Trim(), out var sy) &&
                    int.TryParse(row.Get("edu.endYear").Trim(), out var ey) && ey < sy)
                    return new ImportRowOutcome("error",
                        $"Education row: end year '{ey}' is before start year '{sy}' — an education record cannot end before it begins.");
            }
        }

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
        if (!string.IsNullOrWhiteSpace(startDate) && !TryParseImportDate(startDate, out _))
            return new ImportRowOutcome("error", $"Start date '{startDate}' is not valid — use DD-MM-YYYY, e.g. 02-01-2026.");

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

    /// Locates a worker by the row's natural keys, falling back to the work
    /// email when none of the keys were supplied. Preview already refused
    /// duplicate emails inside the file and against existing records, so the
    /// email uniquely identifies the just-created row.
    private async Task<Worker?> FindByNaturalOrEmailAsync(string? email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return await repo.FindByEmailAsync(email, ct);
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
            await ApplyChildRowsAsync(target.Id, row, orgUnits, ct);
        }

        // NOTE on blank-update semantics: ApplyRowAsync is only ever reached for
        // rows the preview approved. The preview already refused blank emails in
        // insert mode and treats blank optional values in update mode as
        // "unchanged", so no existing value is silently cleared here.
        else
        {
            // Insert mode — full lifecycle via the same service the UI uses.
            // M54 / M54.3: an explicit `locationId` column wins; otherwise the
            // row is hired into the operator's current work scope (branch
            // switcher / confinement — which is an ORG UNIT, not a work
            // location; the header may carry either).
            Guid? locationId = Guid.TryParse(OrNull(row.Get("locationId")), out var lid)
                ? lid
                : scope.LocationId;
            orgUnitId = orgUnitId ?? scope.OrgUnitId;
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
                StartDate: NormalizedImportDate(row.Get("startDate")),
                WorkerType: workerType,
                OrgUnitId: orgUnitId,
                LocationId: locationId);
            await workers.CreateAsync(request, ct);
            // The freshly created worker is located again by the same natural
            // keys the import row was built with (CreateAsync returns a DTO,
            // not the id). Insert mode always carries a required email, so a
            // row without any natural keys falls back to locating by email.
            var created = await repo.FindByNaturalKeyAsync(
                OrNull(row.Get("employeeNo")), OrNull(row.Get("nrc")),
                OrNull(row.Get("napsaNumber")), ct)
                ?? await FindByNaturalOrEmailAsync(OrNull(row.Get("email")), ct)
                ?? throw new DomainException("import-create-lost",
                    "The worker row was applied but could not be re-located for history records — report this to support.");
            await ApplyChildRowsAsync(created.Id, row, orgUnits, ct);
        }
    }

    /// Applies the optional education / external / internal history child rows
    /// that the flattened import layout exposes (edu.*, ext.*, int.* keys).
    /// Only creates — never deletes — matching the blank-values-mean-unchanged
    /// contract documented above. A single child row per type is supported per
    /// import row, which matches the flattened export shape this schema reads.
    private async Task ApplyChildRowsAsync(Guid workerId, IDictionary<string, string> row,
        List<OrgUnit> orgUnits, CancellationToken ct)
    {
        // Education: both of the two visible columns must be present — a
        // half-filled education row silently imported would produce junk data.
        var eduInst = OrNull(row.Get("edu.institution"));
        var eduQual = OrNull(row.Get("edu.qualification"));
        if (eduInst is not null && eduQual is not null)
        {
            int.TryParse(OrNull(row.Get("edu.startYear")), out var sy);
            int.TryParse(OrNull(row.Get("edu.endYear")), out var ey);
            await repo.AddEducationAsync(new WorkerEducation
            {
                WorkerId = workerId,
                Institution = eduInst.Trim(),
                Qualification = eduQual.Trim(),
                StartYear = sy != 0 ? sy : null,
                EndYear = ey != 0 ? ey : null,
            }, ct);
        }

        // Previous employment (external work history) — company is the anchor.
        var extCompany = OrNull(row.Get("ext.company"));
        if (extCompany is not null)
        {
            await repo.AddExternalWorkHistoryAsync(new ExternalWorkHistory
            {
                WorkerId = workerId,
                Company = extCompany.Trim(),
                Role = OrNull(row.Get("ext.role"))?.Trim(),
                StartDate = NormalizedImportDate(row.Get("ext.startDate")),
                EndDate = NormalizedImportDate(row.Get("ext.endDate")),
            }, ct);
        }

        // Internal move — the resolved department name is what history stores.
        var intOrg = OrNull(row.Get("int.orgUnitName"));
        var intRole = OrNull(row.Get("int.role"));
        if (intOrg is not null || intRole is not null)
        {
            var resolved = orgUnits
                .FirstOrDefault(u => u.Name.Equals(intOrg ?? "", StringComparison.OrdinalIgnoreCase))?.Name ?? intOrg;
            if (string.IsNullOrWhiteSpace(resolved)) return; // nothing meaningful to record
            await repo.AddInternalWorkHistoryAsync(new InternalWorkHistory
            {
                WorkerId = workerId,
                OrgUnitName = resolved.Trim(),
                Role = intRole?.Trim(),
                StartDate = NormalizedImportDate(row.Get("int.startDate")),
            }, ct);
        }
    }

    private static bool IsNonBlank(string v) => !string.IsNullOrWhiteSpace(v);

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

    /// Accepts day-first import dates and returns the ISO value the domain services use.
    private static string? NormalizedImportDate(string? v)
    {
        return TryParseImportDate(v, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
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
