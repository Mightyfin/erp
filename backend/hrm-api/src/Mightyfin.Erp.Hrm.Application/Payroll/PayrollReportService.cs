using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mightyfin.Erp.Hrm.Application.Payroll;

/// <summary>Accounting-facing payroll reports modeled on the reference JV and
/// payroll-by-department layouts: a detailed JV (one row per employee and
/// component), a JV summary by transaction code (the report the accounts team
/// books from), and payroll reports by department in summary and detail form.
/// CSV and PDF formats are both supported; PDF rendering is delegated to the
/// infrastructure renderer (HTML -> PDF via weasyprint).</summary>
public interface IPayrollReportService
{
    Task<PayrollReportPayload> GetAsync(PayrollReportKind kind, Guid runId, string format, CancellationToken ct);
}

public enum PayrollReportKind
{
    JvDetailed,   // one row per employee + component (payment/deduction columns)
    JvSummary,    // one row per transaction code — the booking report
    DeptSummary,  // per-department: employee, NRC, totals, net pay
    DeptDetailed, // per-department employee blocks with per-component detail + tax block
}

/// <summary>Aggregated report rows ready for CSV or HTML rendering.</summary>
public sealed class PayrollReportPayload
{
    public string CompanyName { get; set; } = "";
    public string ReportTitle { get; set; } = "";
    public string PeriodLabel { get; set; } = "";
    public string PrintDate { get; set; } = "";
    public List<PayrollReportGroup> Groups { get; set; } = [];
    public PayrollReportTotals CompanyTotals { get; set; } = new();
    public bool HasUnmappedAccounts { get; set; }
}

/// <summary>One group in a report: either one employee's rows (JV detailed) or
/// one department (department reports).</summary>
public sealed class PayrollReportGroup
{
    public string GroupLabel { get; set; } = "";
    public List<PayrollReportRow> Rows { get; set; } = [];
    public PayrollReportTotals GroupTotals { get; set; } = new();
    // Dept-detailed extras per employee.
    public List<PayrollReportEmployeeDetail> EmployeeDetails { get; set; } = [];
}

public sealed class PayrollReportRow
{
    public string EmployeeNo { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string TransactionCode { get; set; } = "";
    public string TransactionName { get; set; } = "";
    public decimal Payment { get; set; }
    public decimal Deduction { get; set; }
    public string Department { get; set; } = "";
    public string Nrc { get; set; } = "";
    public decimal NetPay { get; set; }
    public decimal NegNetPay { get; set; }
}

public sealed class PayrollReportEmployeeDetail
{
    public string EmployeeNo { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string Nrc { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string PayType { get; set; } = "";
    public string PayMethod { get; set; } = "";
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public decimal BasicRate { get; set; }
    public string StartDate { get; set; } = "";
    public List<PayrollLineRow> Lines { get; set; } = [];
    public decimal GrossPay { get; set; }
    public decimal Taxable { get; set; }
    public decimal Tax { get; set; }
    public decimal NetPay { get; set; }
}

public sealed class PayrollLineRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal Payment { get; set; }
    public decimal Deduction { get; set; }
}

public sealed class PayrollReportTotals
{
    public int Count { get; set; }
    public decimal Payments { get; set; }
    public decimal Deductions { get; set; }
    public decimal NetPay { get; set; }
    public decimal NegNetPay { get; set; }
    public decimal Taxable { get; set; }
    public decimal Tax { get; set; }
}
