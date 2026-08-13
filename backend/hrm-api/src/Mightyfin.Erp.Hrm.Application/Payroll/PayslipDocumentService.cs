using System.Threading;
using System.Threading.Tasks;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Payroll;

/// <summary>Renders a payslip as a PDF document and returns a durable URL
/// where the PDF can be fetched. Implementations own the rendering pipeline
/// (template → PDF → storage).</summary>
public interface IPayslipDocumentService
{
    Task<string> GenerateAsync(Payslip slip, PayrollRunLine line, CancellationToken ct);
}
