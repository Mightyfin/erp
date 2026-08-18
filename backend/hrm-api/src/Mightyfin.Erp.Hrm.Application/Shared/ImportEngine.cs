// M31 — shared generic import engine. Every importable type (workers, positions,
// org units, ...) registers a schema contract here and routes its row creation
// through the same engine, which parses CSV/XLSX-safe rows, maps arbitrary file
// columns via the user's mapping, validates per row, and records per-row
// outcomes so a broken row never aborts the batch. Insert and Update modes are
// both handled by the registering service.
using System.Text;

namespace Mightyfin.Erp.Hrm.Application.Shared;

/// Describes one mappable field of an importable type.
public sealed record ImportFieldDef(
    string Key,              // canonical column key, e.g. "firstName"
    string Label,            // human label for dropdowns, e.g. "First name"
    bool Required,
    bool NaturalKey = false, // used to match existing records in Update mode
    string? Example = null,
    string? FormatNote = null);

/// One importable type's contract: fields + preview/apply handlers.
public interface IImportSchema
{
    string TypeKey { get; }                       // e.g. "workers"
    string DisplayName { get; }                   // e.g. "Employees"
    List<ImportFieldDef> Fields { get; }
    /// Validate one mapped row against tenant data without persisting.
    Task<ImportRowOutcome> PreviewRowAsync(IDictionary<string, string> row, CancellationToken ct);
    /// Persist one validated row (create in Insert mode, update in Update mode).
    Task ApplyRowAsync(IDictionary<string, string> row, CancellationToken ct);
}

public sealed record ImportRowOutcome(
    string Status,              // "create" | "update" | "skip" | "error"
    string? Message = null,     // reason when skip/error
    IDictionary<string, string>? ResolvedRow = null); // canonical row keyed by field key

public sealed record ImportPreviewResult(
    int TotalRows,
    int WillCreate, int WillUpdate, int WillSkip, int WillError,
    List<ImportRowOutcome> RowOutcomes); // same length as input rows

public static class ImportRowParser
{
    /// Parse one CSV line into cells, honoring double-quoted values with
    /// embedded commas and escaped quotes. UTF-8 is assumed.
    public static List<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') inQuotes = false;
                else cell.Append(ch);
            }
            else
            {
                if (ch == '"') inQuotes = true;
                else if (ch == ',') { cells.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(ch);
            }
        }
        cells.Add(cell.ToString());
        return cells;
    }
}
