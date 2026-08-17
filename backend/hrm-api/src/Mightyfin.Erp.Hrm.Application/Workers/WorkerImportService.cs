// M26 audit — bulk employee import. An HR administrator uploads one CSV file and
// every valid row becomes a worker; invalid rows are reported per row so a failed
// header or a single bad phone number never aborts the whole batch.

using System.Text;
using System.Text.RegularExpressions;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Workers;

/// One row that could not be imported. Row is the 1-based CSV row number.
public sealed record WorkerImportError(int Row, string Detail);

/// Outcome of a bulk import run: best-effort, row-level failures are reported,
/// never thrown, so HR gets the largest correct subset they submitted.
public sealed record WorkerImportResult(int Created, int Skipped, List<WorkerImportError> Errors);

public interface IWorkerImportService
{
    Task<WorkerImportResult> ImportCsvAsync(Stream csv, CancellationToken ct);
}

public sealed partial class WorkerImportService : IWorkerImportService
{
    private readonly IWorkerRepository repo;
    private readonly IWorkerService workers;
    private readonly IAuthzService authz;

    // The columns HR actually needs on day one. Everything else is optional.
    // orgUnitName is looked up by display name within the tenant; unknown names
    // are reported as a row error rather than silently dropped into no-man's-land.
    public WorkerImportService(IWorkerRepository repo, IWorkerService workers, IAuthzService authz)
    {
        this.repo = repo;
        this.workers = workers;
        this.authz = authz;
    }

    public async Task<WorkerImportResult> ImportCsvAsync(Stream csv, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var errors = new List<WorkerImportError>();
        var created = 0;
        var skipped = 0;

        using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var (headers, rows) = ParseCsv(reader);
        if (headers.Count == 0)
            return new WorkerImportResult(0, 0, [new WorkerImportError(0, "The file is empty or has no header row.")]);

        var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++) col[headers[i].Trim()] = i;

        var orgUnits = await repo.ListAllOrgUnitsAsync(ct);
        // Pre-load existing workers so duplicates (email, NRC, NAPSA) are caught
        // per row before CreateAsync runs. ListAsync is paged, so page until done.
        var existing = new List<Worker>();
        for (var page = 1; ; page++)
        {
            var (batch, total) = await repo.ListAsync(
                new WorkerListFilters(null, null, null, null, null, null, true, page, 100), ct);
            existing.AddRange(batch);
            if (existing.Count >= total) break;
        }
        var existingEmails = existing.Where(w => w.Email is not null).Select(w => w.Email!.ToLowerInvariant()).ToHashSet();
        var existingNrcs = existing.Where(w => w.Nrc is not null).Select(w => w.Nrc.ToLowerInvariant()).ToHashSet();
        var existingNapsas = existing.Where(w => w.NapsaNumber is not null).Select(w => w.NapsaNumber.ToLowerInvariant()).ToHashSet();
        var seenEmails = new HashSet<string>();
        var emailRx = EmailRegex();

        foreach (var (row, index) in rows.Select((r, i) => (r, i)))
        {
            var rowNo = index + 2; // header is row 1
            string Get(string name) => col.TryGetValue(name, out var c) && c < row.Count ? (row[c] ?? "").Trim() : "";

            var firstName = Get("firstName");
            var lastName = Get("lastName");
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                errors.Add(new WorkerImportError(rowNo, "firstName and lastName are required."));
                skipped++;
                continue;
            }
            var email = Get("email");
            if (!string.IsNullOrWhiteSpace(email) && !emailRx.IsMatch(email))
            {
                errors.Add(new WorkerImportError(rowNo, $"email '{email}' is not valid."));
                skipped++;
                continue;
            }
            var nrc = Get("nrc");
            var napsa = Get("napsaNumber");
            if (!string.IsNullOrWhiteSpace(email))
            {
                if (existingEmails.Contains(email.ToLowerInvariant()) || !seenEmails.Add(email.ToLowerInvariant()))
                {
                    errors.Add(new WorkerImportError(rowNo, $"email '{email}' is already in use."));
                    skipped++;
                    continue;
                }
            }
            if (!string.IsNullOrWhiteSpace(nrc) && existingNrcs.Contains(nrc.ToLowerInvariant()))
            {
                errors.Add(new WorkerImportError(rowNo, $"NRC '{nrc}' is already in use."));
                skipped++;
                continue;
            }
            if (!string.IsNullOrWhiteSpace(napsa) && existingNapsas.Contains(napsa.ToLowerInvariant()))
            {
                errors.Add(new WorkerImportError(rowNo, $"NAPSA number '{napsa}' is already in use."));
                skipped++;
                continue;
            }

            var workerType = Get("workerType");
            if (workerType is not ("employee" or "contingent" or "intern" or "volunteer"))
                workerType = "employee";
            var orgUnitName = Get("orgUnitName");
            Guid? orgUnitId = null;
            if (!string.IsNullOrWhiteSpace(orgUnitName))
                orgUnitId = orgUnits.FirstOrDefault(u =>
                    u.Name.Equals(orgUnitName, StringComparison.OrdinalIgnoreCase))?.Id;
            if (!string.IsNullOrWhiteSpace(orgUnitName) && orgUnitId is null)
            {
                errors.Add(new WorkerImportError(rowNo, $"No org unit named '{orgUnitName}' exists."));
                skipped++;
                continue;
            }

            var request = new WorkerCreateRequest(
                Get("employeeNo"), firstName, lastName,
                MiddleName: OrNull(Get("middleName")),
                Email: OrNull(email),
                Phone: OrNull(Get("phone")),
                Nrc: OrNull(nrc),
                Tpin: OrNull(Get("tpin")),
                NapsaNumber: OrNull(napsa),
                NhimaNumber: OrNull(Get("nhimaNumber")),
                Grade: OrNull(Get("grade")),
                JobTitle: OrNull(Get("jobTitle")),
                StartDate: OrNull(Get("startDate")),
                WorkerType: workerType,
                OrgUnitId: orgUnitId);

            try
            {
                await workers.CreateAsync(request, ct);
                created++;
            }
            catch (DomainException ex)
            {
                errors.Add(new WorkerImportError(rowNo, ex.Message));
                skipped++;
            }
        }

        return new WorkerImportResult(created, skipped, errors);
    }

    private static string? OrNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // RFC 4180-lite: commas inside double-quoted cells are kept, a doubled quote
    // inside quotes becomes a single quote, and the header row is normalised to
    // lower-case so the CSV is forgiving about casing and spacing.
    private static (List<string> Headers, List<List<string>> Rows) ParseCsv(TextReader reader)
    {
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            lines.Add(line);
        }
        if (lines.Count == 0) return ([], []);

        static List<string> SplitLine(string line)
        {
            var cells = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else current.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { cells.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            cells.Add(current.ToString());
            return cells;
        }

        var headers = SplitLine(lines[0]).Select(h => h.ToLowerInvariant()).ToList();
        var rows = lines.Skip(1).Select(SplitLine).ToList();
        return (headers, rows);
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();
}
