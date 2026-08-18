// M31 — the shared import/export surface. A single service owns schema
// registration, the map-columns+preview flow and the type-aware CSV export,
// so every CRUD page reuses the same tooling with no per-page duplication.
using System.Text;

namespace Mightyfin.Erp.Hrm.Application.Shared;

public sealed record ImportFieldSchemaDto(
    string Key, string Label, bool Required, bool NaturalKey, string? Example, string? FormatNote);

public sealed record ImportTypeSchemaDto(
    string TypeKey, string DisplayName, List<ImportFieldSchemaDto> Fields);

public sealed record ImportRowPreviewDto(int Row, string Status, string? Message, IDictionary<string, string>? Resolved = null);

public sealed record ImportPreviewDto(
    string TypeKey, string FileName, string Mode,
    int TotalRows, int WillCreate, int WillUpdate, int WillSkip, int WillError,
    List<ImportRowPreviewDto> Rows)
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed record ImportApplyDto(string TypeKey, Guid PreviewId, List<int> ApplyRowIndexes);

public sealed record ImportApplyResult(int Created, int Updated, int Skipped, List<ImportRowPreviewDto> RowOutcomes);

public interface IImportExportService
{
    List<ImportTypeSchemaDto> ListSchemas();
    /// Preview: rows are already client-mapped to canonical keys (column mapping
    /// happens in the UI). The engine re-runs server-side validation and status
    /// resolution per row. Idempotent — returns a stable preview keyed by hash.
    Task<ImportPreviewDto> PreviewAsync(string typeKey, string fileName, string mode, List<Dictionary<string, string>> rows, CancellationToken ct);
    /// Apply a preview by re-running PreviewRow/ApplyRow for each accepted row.
    Task<ImportApplyResult> ApplyAsync(Guid previewId, List<int> rowIndexes, CancellationToken ct);
    /// Type-aware CSV export. Header uses canonical keys but is round-trip safe
    /// for the importer (importer maps keys to fields by key, not label).
    Task<byte[]> ExportAsync(string typeKey, string? filter, CancellationToken ct);
}

public sealed class ImportExportServiceImpl : IImportExportService
{
    private readonly IEnumerable<IImportSchema> schemas;
    private static readonly Dictionary<Guid, ImportPreviewDto> Previews = new(); // in-memory preview cache

    public ImportExportServiceImpl(IEnumerable<IImportSchema> schemas)
    {
        this.schemas = schemas;
    }

    private IImportSchema Schema(string typeKey) =>
        schemas.FirstOrDefault(s => s.TypeKey.Equals(typeKey, StringComparison.OrdinalIgnoreCase))
        ?? throw new DomainException("import-schema-not-found", $"No import schema registered for '{typeKey}'.");

    public List<ImportTypeSchemaDto> ListSchemas() =>
        schemas.Select(s => new ImportTypeSchemaDto(
            s.TypeKey, s.DisplayName,
            s.Fields.Select(f => new ImportFieldSchemaDto(
                f.Key, f.Label, f.Required, f.NaturalKey, f.Example, f.FormatNote)).ToList())).ToList();

    public async Task<ImportPreviewDto> PreviewAsync(
        string typeKey, string fileName, string mode, List<Dictionary<string, string>> rows, CancellationToken ct)
    {
        var schema = Schema(typeKey);
        var isUpdate = mode.Equals("update", StringComparison.OrdinalIgnoreCase);
        var outcomes = new List<ImportRowPreviewDto>(rows.Count);
        var willCreate = 0; var willUpdate = 0; var willSkip = 0; var willError = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            ImportRowOutcome r;
            try { r = await schema.PreviewRowAsync(row, mode, ct); }
            catch (DomainException ex) { r = new ImportRowOutcome("error", ex.Message); }
            catch (Exception ex) { r = new ImportRowOutcome("error", ex.Message); }
            outcomes.Add(new ImportRowPreviewDto(i + 2, r.Status, r.Message, r.ResolvedRow ?? row));
            if (r.Status == "create") willCreate++;
            else if (r.Status == "update") willUpdate++;
            else if (r.Status == "skip") willSkip++;
            else willError++;
        }
        var preview = new ImportPreviewDto(
            typeKey, fileName, mode,
            rows.Count, willCreate, willUpdate, willSkip, willError, outcomes);
        // Stable id so the UI can POST apply for the exact preview shown.
        Previews[preview.Id] = preview;
        while (Previews.Count > 1000) { var oldest = Previews.Keys.First(); Previews.Remove(oldest); }
        return preview;
    }

    public async Task<ImportApplyResult> ApplyAsync(Guid previewId, List<int> rowIndexes, CancellationToken ct)
    {
        if (!Previews.TryGetValue(previewId, out var preview))
            throw new DomainException("import-preview-expired", "The import preview has expired. Preview the file again.");
        var schema = Schema(preview.TypeKey);
        var created = 0; var updated = 0; var skipped = 0;
        var rowOutcomes = new List<ImportRowPreviewDto>();
        foreach (var idx in rowIndexes)
        {
            if (idx < 0 || idx >= preview.Rows.Count) continue;
            var rowPreview = preview.Rows[idx];
            var status = rowPreview.Status;
            if (status is "create" or "update")
            {
                try
                {
                    await schema.ApplyRowAsync(preview.Rows[idx].Resolved ?? new Dictionary<string, string>(), ct);
                    if (status == "create") created++; else updated++;
                    rowOutcomes.Add(new ImportRowPreviewDto(idx + 2, "ok", null));
                }
                catch (DomainException ex) { rowOutcomes.Add(new ImportRowPreviewDto(idx + 2, "error", ex.Message)); skipped++; }
                catch (Exception ex) { rowOutcomes.Add(new ImportRowPreviewDto(idx + 2, "error", ex.Message)); skipped++; }
            }
            else if (status == "skip")
            {
                skipped++;
                rowOutcomes.Add(new ImportRowPreviewDto(idx + 2, "skip", rowPreview.Message));
            }
            else
            {
                rowOutcomes.Add(new ImportRowPreviewDto(idx + 2, "error", rowPreview.Message));
            }
        }
        return new ImportApplyResult(created, updated, skipped, rowOutcomes);
    }

    public async Task<byte[]> ExportAsync(string typeKey, string? filter, CancellationToken ct)
    {
        var schema = Schema(typeKey);
        if (schema is not IImportSchemaWithExport withExport)
            throw new DomainException("import-export-not-supported", $"'{typeKey}' does not support export yet.");
        var rows = await withExport.ExportRowsAsync(filter, ct);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", schema.Fields.Select(f => CsvQuote(f.Key))));
        foreach (var row in rows)
        {
            var cells = schema.Fields.Select(f => row.TryGetValue(f.Key, out var v) ? CsvQuote(v) : "");
            sb.AppendLine(string.Join(",", cells));
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string CsvQuote(string value) =>
        value == null ? "" : (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}

/// Per-row export source for a schema — types opt in to the export side.
public interface IImportSchemaWithExport : IImportSchema
{
    Task<List<Dictionary<string, string>>> ExportRowsAsync(string? filter, CancellationToken ct);
}
