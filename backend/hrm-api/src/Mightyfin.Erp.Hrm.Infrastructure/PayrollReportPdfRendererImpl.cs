using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Mightyfin.Erp.Hrm.Application.Payroll;

namespace Mightyfin.Erp.Hrm.Infrastructure;

/// <summary>Default report PDF renderer: converts the report HTML to PDF with
/// weasyprint and returns the bytes in memory. The payslip renderer uploads to
/// storage for preview; accounting reports are downloaded directly, so raw
/// bytes are returned instead of a storage URL.</summary>
public sealed class PayrollReportPdfRendererImpl : IPayrollReportPdfRenderer
{
    public async Task<byte[]> RenderPdfAsync(string html, CancellationToken ct)
    {
        var tmp = Path.GetTempFileName();
        var htmlPath = tmp + ".html";
        try
        {
            await File.WriteAllTextAsync(htmlPath, html, ct);
            var psi = new ProcessStartInfo("weasyprint", $"\"{htmlPath}\" \"{tmp}.pdf\"")
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
            return await File.ReadAllBytesAsync(tmp + ".pdf", ct);
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* best effort */ }
            try { File.Delete(htmlPath); } catch { /* best effort */ }
            try { File.Delete(tmp + ".pdf"); } catch { /* best effort */ }
        }
    }
}
