// M31b — minimal zero-dependency XLSX writer. Produces a valid
// OfficeOpenXML workbook (single sheet, shared strings, UTF-8) so the
// import/export tool can deliver .xlsx exports without taking a new NuGet
// dependency. Round-trip-safe: the importer reads any .xlsx SheetJS can parse.
using System.IO.Compression;
using System.Text;

namespace Mightyfin.Erp.Hrm.Application.Shared;

public static class MinimalXlsx
{
    /// Build a single-sheet .xlsx workbook in memory. [header, ...rows] are
    /// cell values; nulls become empty cells.
    public static byte[] Build(IEnumerable<IEnumerable<string?>> sheet)
    {
        var rows = sheet.Select(r => r.Select(c => c ?? "").ToList()).ToList();
        if (rows.Count == 0) rows.Add([]);

        // Shared-string table: dedupe cell texts in document order.
        var sst = new List<string>();
        var sstIdx = new Dictionary<string, int>(StringComparer.Ordinal);
        var sstRefs = new List<List<int?>>(); // shared-string index per cell, null = empty
        foreach (var row in rows)
        {
            var rowRefs = new List<int?>();
            foreach (var cell in row)
            {
                if (cell == "") { rowRefs.Add(null); continue; }
                if (!sstIdx.TryGetValue(cell, out var idx))
                {
                    idx = sst.Count;
                    sst.Add(cell);
                    sstIdx[cell] = idx;
                }
                rowRefs.Add(idx);
            }
            sstRefs.Add(rowRefs);
        }

        var rowCount = rows.Count;
        var maxCol = rows.Max(r => r.Count);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                "<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>" +
                "</Types>");
            WriteEntry(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>");
            WriteEntry(zip, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>" +
                "</Relationships>");
            WriteEntry(zip, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                "<sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            WriteEntry(zip, "xl/styles.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
                "</styleSheet>");

            var sstXml = new StringBuilder();
            sstXml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                $"<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"{sst.Count}\" uniqueCount=\"{sst.Count}\">");
            foreach (var s in sst)
            {
                sstXml.Append("<si><t");
                if (s.StartsWith(' ') || s.EndsWith(' ') || s.Contains("\r\n") || s.Contains('\n'))
                    sstXml.Append(" xml:space=\"preserve\"");
                sstXml.Append('>');
                sstXml.Append(XMLEsc(s));
                sstXml.Append("</t></si>");
            }
            sstXml.Append("</sst>");
            WriteEntry(zip, "xl/sharedStrings.xml", sstXml.ToString());

            var ws = new StringBuilder();
            ws.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
                "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<sheetData>");
            for (var r = 0; r < rowCount; r++)
            {
                ws.Append($"<row r=\"{r + 1}\">");
                var refs = sstRefs[r];
                for (var c = 0; c < maxCol; c++)
                {
                    if (c < refs.Count && refs[c].HasValue)
                        ws.Append($"<c r=\"{ColRef(c)}{r + 1}\" t=\"s\"><v>{refs[c].Value}</v></c>");
                    else
                        ws.Append($"<c r=\"{ColRef(c)}{r + 1}\"/>");
                }
                ws.Append("</row>");
            }
            ws.Append("</sheetData></worksheet>");
            WriteEntry(zip, "xl/worksheets/sheet1.xml", ws.ToString());
        }
        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var w = new StreamWriter(entry.Open(), Encoding.UTF8);
        w.Write(content);
    }

    private static string XMLEsc(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    /// Column index → Excel column reference (0 → "A", 26 → "AA").
    private static string ColRef(int idx)
    {
        var sb = new StringBuilder();
        idx++;
        while (idx > 0)
        {
            idx--;
            sb.Insert(0, (char)('A' + idx % 26));
            idx /= 26;
        }
        return sb.ToString();
    }
}
