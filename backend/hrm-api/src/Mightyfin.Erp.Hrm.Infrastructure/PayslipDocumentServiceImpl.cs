using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

/// <summary>Default payslip document renderer: builds an HTML payslip and
/// converts it to PDF with weasyprint, then uploads to durable object storage.
/// This is a v1 reference implementation — a template service could swap in a
/// different renderer without touching the payroll domain.</summary>
public sealed class PayslipDocumentServiceImpl(HrmDbContext db) : IPayslipDocumentService
{
    public async Task<string> GenerateAsync(Payslip slip, PayrollRunLine line, CancellationToken ct)
    {
        var run = await db.PayrollRuns.Include(r => r.PayPeriod)
            .FirstAsync(r => r.Id == line.RunId, ct);
        var worker = await db.Workers.FirstAsync(w => w.Id == line.WorkerId, ct);
        var components = await db.PayrollLineComponents
            .Where(c => c.RunLineId == line.Id).ToListAsync(ct);
        var periodLabel = run.PayPeriod?.PeriodLabel ?? "";

        var html = RenderPayslipHtml(worker.FullName, worker.EmployeeNo ?? "", periodLabel,
            slip.PayslipNo, components, slip.GrossPay, slip.TotalDeductions, slip.NetPay,
            slip.YtdGross, slip.YtdTax, slip.YtdNet,
            slip.WorkerNrc, slip.WorkerTpin, slip.WorkerNapsaNumber, slip.WorkerNhimaNumber);

        var pdfPath = Path.Combine(Path.GetTempPath(), $"payslip-{slip.Id:D}.pdf");
        try
        {
            var htmlPath = pdfPath + ".html";
            await File.WriteAllTextAsync(htmlPath, html, ct);
            var psi = new ProcessStartInfo("weasyprint", $"\"{htmlPath}\" \"{pdfPath}\"")
            {
                RedirectStandardError = true, RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("weasyprint not found on this host");
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0)
            {
                var err = await proc.StandardError.ReadToEndAsync(ct);
                throw new InvalidOperationException($"PDF rendering failed: {err.Trim()}");
            }
            return await UploadAsync(pdfPath, ct);
        }
        finally
        {
            try { File.Delete(pdfPath); } catch { /* best effort */ }
            try { File.Delete(pdfPath + ".html"); } catch { /* best effort */ }
        }
    }

    private static async Task<string> UploadAsync(string pdfPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("manus-upload-file", $"\"{pdfPath}\"")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("manus-upload-file not found on this host");
        var outText = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"Payslip upload failed: {err.Trim()}");
        }
        // Output is a JSON-ish wrapper; the URL is the first http(s) token.
        var match = System.Text.RegularExpressions.Regex.Match(outText, @"https://[^\s""',]+");
        return match.Success ? match.Value : outText.Trim();
    }

    private static string RenderPayslipHtml(string workerName, string employeeNo, string periodLabel,
        string payslipNo, List<PayrollLineComponent> components, decimal gross, decimal deductions,
        decimal net, string? ytdGross, string? ytdTax, string? ytdNet,
        string? workerNrc = null, string? workerTpin = null,
        string? workerNapsaNumber = null, string? workerNhimaNumber = null)
    {
        var sb = new StringBuilder();
        sb.Append("<html><head><meta charset='utf-8'><style>")
          .Append("body{font-family:DejaVu Sans,sans-serif;font-size:11px;color:#1a1a1a;margin:24px}")
          .Append("table{width:100%;border-collapse:collapse;margin:8px 0}")
          .Append("td,th{border:1px solid #ccc;padding:4px 6px;text-align:left}")
          .Append(".right{text-align:right} h1{font-size:15px;margin:0}")
          .Append("h2{font-size:12px;margin:14px 0 4px} .muted{color:#666;font-size:10px}")
          .Append("</style></head><body>")
          .Append("<h1>Payslip</h1>")
          .Append($"<div class='muted'>{Escape(payslipNo)} &middot; Period {Escape(periodLabel)}</div>")
          .Append("<h2>Employee</h2><table>")
          .Append($"<tr><td>Name</td><td>{Escape(workerName)}</td><td>Employee No</td><td>{Escape(employeeNo)}</td></tr>")
          .Append("</table>")
          // M24: statutory identity pack snapshotted at payment time
          .Append("<h2>Statutory references</h2><table>")
          .Append($"<tr><td>NRC</td><td>{Escape(workerNrc)}</td><td>TPIN</td><td>{Escape(workerTpin)}</td></tr>")
          .Append($"<tr><td>NAPSA no.</td><td>{Escape(workerNapsaNumber)}</td><td>NHIMA no.</td><td>{Escape(workerNhimaNumber)}</td></tr>")
          .Append("</table>")
          .Append("<h2>Earnings &amp; Deductions</h2><table>")
          .Append("<tr><th>Component</th><th>Type</th><th class='right'>Amount</th><th>Explanation</th></tr>");
        foreach (var c in components)
        {
            var cls = c.ComponentType == "earning" ? "" : " class='right'";
            sb.Append($"<tr><td>{Escape(c.ComponentName)}</td><td>{Escape(c.ComponentType)}</td>")
              .Append($"<td{cls}>{c.Amount.ToString("N2", CultureInfo.InvariantCulture)}</td>")
              .Append($"<td class='muted'>{Escape(c.Explanation)}</td></tr>");
        }
        sb.Append("</table>")
          .Append("<h2>Summary</h2><table>")
          .Append($"<tr><td>Gross pay</td><td class='right'>{gross:N2}</td></tr>")
          .Append($"<tr><td>Total deductions</td><td class='right'>{deductions:N2}</td></tr>")
          .Append($"<tr><th>Net pay</th><th class='right'>{net:N2}</th></tr>")
          .Append("</table>")
          .Append("<h2>Year to date</h2><table>")
          .Append($"<tr><td>YTD gross</td><td class='right'>{Escape(ytdGross ?? "")}</td>")
          .Append($"<td>YTD tax</td><td class='right'>{Escape(ytdTax ?? "")}</td>")
          .Append($"<td>YTD net</td><td class='right'>{Escape(ytdNet ?? "")}</td></tr>")
          .Append("</table>")
          .Append("<div class='muted'>Generated by the HRM payroll module. Statutory calculations follow ZRA PAYE 2026, NAPSA and NHIMA rules.</div>")
          .Append("</body></html>");
        return sb.ToString();
    }

    private static string Escape(string? s) => (s ?? "")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
