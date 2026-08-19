using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Mightyfin.Erp.Hrm.Application.Payroll;

/// <summary>Formats a PayrollReportPayload as CSV or HTML (for PDF rendering).
/// CSV is round-trip safe (values containing commas/quotes are quoted). The
/// HTML mirrors the reference JV / payroll-by-department layouts: compact
/// monospace table report, per-group headings, employee totals and company
/// total footer rows.</summary>
public static class PayrollReportFormatter
{
    private static readonly CultureInfo CsvCulture = CultureInfo.InvariantCulture;

    public static string ToCsv(PayrollReportPayload payload, PayrollReportKind kind)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CsvLine("Company", "Period", "Report", "Printed"));
        sb.AppendLine(CsvLine(payload.CompanyName, payload.PeriodLabel, payload.ReportTitle, payload.PrintDate));
        sb.AppendLine();

        switch (kind)
        {
            case PayrollReportKind.JvDetailed:
                sb.AppendLine(CsvLine("Employee", "Transaction Code", "Transaction Name",
                    "Payment", "Deduction", "Department", "Currency"));
                foreach (var group in payload.Groups)
                {
                    foreach (var row in group.Rows)
                        sb.AppendLine(CsvLine(row.EmployeeName, row.TransactionCode, row.TransactionName,
                            Fmt(row.Payment), Fmt(row.Deduction), row.Department, "ZMW"));
                    if (group.Rows.Count > 0)
                        sb.AppendLine(CsvLine($"{group.Rows[^1].EmployeeName} Total", "", "",
                            Fmt(group.GroupTotals.Payments), Fmt(group.GroupTotals.Deductions), "", ""));
                }
                sb.AppendLine(CsvLine("Company Total", payload.CompanyTotals.Count.ToString(CsvCulture), "",
                    Fmt(payload.CompanyTotals.Payments), Fmt(payload.CompanyTotals.Deductions), "", "ZMW"));
                break;
            case PayrollReportKind.JvSummary:
                sb.AppendLine(CsvLine("Transaction Code", "Transaction Name", "Payment", "Deduction"));
                if (payload.Groups.Count > 0)
                    foreach (var row in payload.Groups[0].Rows)
                        sb.AppendLine(CsvLine(row.TransactionCode, row.TransactionName,
                            Fmt(row.Payment), Fmt(row.Deduction)));
                sb.AppendLine(CsvLine("Company Total", "", Fmt(payload.CompanyTotals.Payments),
                    Fmt(payload.CompanyTotals.Deductions)));
                break;
            case PayrollReportKind.DeptSummary:
                sb.AppendLine(CsvLine("Department", "Employee", "NRC", "Total Payments",
                    "Total Deductions", "Net Pay", "Neg Net Pay", "Currency"));
                foreach (var group in payload.Groups)
                {
                    foreach (var row in group.Rows)
                        sb.AppendLine(CsvLine(group.GroupLabel, row.EmployeeName, row.Nrc,
                            Fmt(row.Payment), Fmt(row.Deduction), Fmt(row.NetPay), Fmt(row.NegNetPay), "ZMW"));
                    sb.AppendLine(CsvLine($"Dept Total: {group.GroupLabel}", group.GroupTotals.Count.ToString(CsvCulture), "",
                        Fmt(group.GroupTotals.Payments), Fmt(group.GroupTotals.Deductions),
                        Fmt(group.GroupTotals.NetPay), Fmt(group.GroupTotals.NegNetPay), "ZMW"));
                }
                sb.AppendLine(CsvLine("Company Total", payload.CompanyTotals.Count.ToString(CsvCulture), "",
                    Fmt(payload.CompanyTotals.Payments), Fmt(payload.CompanyTotals.Deductions),
                    Fmt(payload.CompanyTotals.NetPay), Fmt(payload.CompanyTotals.NegNetPay), "ZMW"));
                break;
            case PayrollReportKind.DeptDetailed:
                sb.AppendLine(CsvLine("Department", "Employee No", "Employee Name", "NRC", "Job Title",
                    "Pay Type", "Pay Method", "Basic Rate", "Component Code", "Component Name",
                    "Qty", "Payment", "Deduction", "Currency"));
                foreach (var group in payload.Groups)
                {
                    foreach (var emp in group.EmployeeDetails)
                    {
                        foreach (var ln in emp.Lines)
                            sb.AppendLine(CsvLine(group.GroupLabel, emp.EmployeeNo, emp.EmployeeName, emp.Nrc,
                                emp.JobTitle, emp.PayType, emp.PayMethod, Fmt(emp.BasicRate), ln.Code, ln.Name,
                                ln.Qty.ToString("0.##", CsvCulture), Fmt(ln.Payment), Fmt(ln.Deduction), "ZMW"));
                    }
                }
                sb.AppendLine(CsvLine("Company Totals", payload.CompanyTotals.Count.ToString(CsvCulture), "", "", "", "", "",
                    "", "", "Payments", "", Fmt(payload.CompanyTotals.Payments), "", "ZMW"));
                sb.AppendLine(CsvLine("Company Totals", "", "", "", "", "", "", "", "", "Deductions", "",
                    Fmt(payload.CompanyTotals.Deductions), "", "ZMW"));
                sb.AppendLine(CsvLine("Company Totals", "", "", "", "", "", "", "", "", "Net Pay", "",
                    Fmt(payload.CompanyTotals.NetPay), "", "ZMW"));
                break;
        }
        return sb.ToString();
    }

    public static string ToHtml(PayrollReportPayload payload, PayrollReportKind kind)
    {
        var sb = new StringBuilder();
        sb.Append("<html><head><meta charset=\"utf-8\"><style>" + Styles + "</style></head><body>");
        sb.Append($"<div class=\"report\"><h1 class=\"company\">{Esc(payload.CompanyName)}</h1>");
        sb.Append($"<h2 class=\"title\">{Esc(payload.ReportTitle)}</h2>");
        sb.Append($"<div class=\"meta\">Month Ending: {Esc(payload.PeriodLabel)}</div>");
        sb.Append("<div class=\"meta\">CLASS: General Payroll</div>");
        sb.Append($"<div class=\"printline\"><span>Print Date: {Esc(payload.PrintDate)}</span>" +
                  $"<span>Page: 1</span></div>");

        switch (kind)
        {
            case PayrollReportKind.JvDetailed:
                sb.Append("<table><tr><th>Employee</th><th>Transaction</th><th class=\"num\">Payment</th>" +
                          "<th class=\"num\">Deduction</th><th>Dept</th><th>Currency</th></tr>");
                foreach (var group in payload.Groups)
                {
                    foreach (var row in group.Rows)
                        sb.Append($"<tr><td>{Esc(row.EmployeeNo)} {Esc(row.EmployeeName)}</td>" +
                                  $"<td>{Esc(row.TransactionCode)} {Esc(row.TransactionName)}</td>" +
                                  $"<td class=\"num\">{Fmt(row.Payment)}</td><td class=\"num\">{Fmt(row.Deduction)}</td>" +
                                  $"<td>{Esc(row.Department)}</td><td>ZMW</td></tr>");
                    sb.Append($"<tr class=\"subtotal\"><td colspan=\"2\">Employee Total</td>" +
                              $"<td class=\"num\">{Fmt(group.GroupTotals.Payments)}</td>" +
                              $"<td class=\"num\">{Fmt(group.GroupTotals.Deductions)}</td><td></td><td></td></tr>");
                    sb.Append("<tr><td colspan=\"6\">&nbsp;</td></tr>");
                }
                sb.Append($"<tr class=\"total\"><td>Company Total</td><td>{payload.CompanyTotals.Count}</td>" +
                          $"<td class=\"num\">{Fmt(payload.CompanyTotals.Payments)}</td>" +
                          $"<td class=\"num\">{Fmt(payload.CompanyTotals.Deductions)}</td><td></td><td></td></tr></table>");
                break;
            case PayrollReportKind.JvSummary:
                sb.Append("<table><tr><th>Transaction Code</th><th>Transaction Name</th>" +
                          "<th class=\"num\">Payment</th><th class=\"num\">Deduction</th></tr>");
                if (payload.Groups.Count > 0)
                    foreach (var row in payload.Groups[0].Rows)
                        sb.Append($"<tr><td>{Esc(row.TransactionCode)}</td><td>{Esc(row.TransactionName)}</td>" +
                                  $"<td class=\"num\">{Fmt(row.Payment)}</td><td class=\"num\">{Fmt(row.Deduction)}</td></tr>");
                sb.Append($"<tr class=\"total\"><td colspan=\"2\">Company Total</td>" +
                          $"<td class=\"num\">{Fmt(payload.CompanyTotals.Payments)}</td>" +
                          $"<td class=\"num\">{Fmt(payload.CompanyTotals.Deductions)}</td></tr></table>");
                break;
            case PayrollReportKind.DeptSummary:
                foreach (var group in payload.Groups)
                {
                    sb.Append($"<h3 class=\"dept\">{Esc(group.GroupLabel)}</h3>");
                    sb.Append("<table><tr><th>Employee</th><th>NRC</th><th class=\"num\">Total Payments</th>" +
                              "<th class=\"num\">Total Deductions</th><th class=\"num\">Net Pay</th>" +
                              "<th class=\"num\">Neg. Net Pay</th></tr>");
                    foreach (var row in group.Rows)
                        sb.Append($"<tr><td>{Esc(row.EmployeeNo)} {Esc(row.EmployeeName)}</td><td>{Esc(row.Nrc)}</td>" +
                                  $"<td class=\"num\">{Fmt(row.Payment)}</td><td class=\"num\">{Fmt(row.Deduction)}</td>" +
                                  $"<td class=\"num\">{Fmt(row.NetPay)}</td><td class=\"num\">{Fmt(row.NegNetPay)}</td></tr>");
                    sb.Append($"<tr class=\"subtotal\"><td colspan=\"2\">Dept Totals ({group.GroupTotals.Count})</td>" +
                              $"<td class=\"num\">{Fmt(group.GroupTotals.Payments)}</td>" +
                              $"<td class=\"num\">{Fmt(group.GroupTotals.Deductions)}</td>" +
                              $"<td class=\"num\">{Fmt(group.GroupTotals.NetPay)}</td>" +
                              $"<td class=\"num\">{Fmt(group.GroupTotals.NegNetPay)}</td></tr></table>");
                }
                sb.Append("<table><tr class=\"total\"><td>Company Total</td>" +
                          $"<td>{payload.CompanyTotals.Count}</td>" +
                          $"<td class=\"num\">{Fmt(payload.CompanyTotals.Payments)}</td>" +
                          $"<td class=\"num\">{Fmt(payload.CompanyTotals.Deductions)}</td>" +
                          $"<td class=\"num\">{Fmt(payload.CompanyTotals.NetPay)}</td>" +
                          $"<td class=\"num\">{Fmt(payload.CompanyTotals.NegNetPay)}</td></tr></table>");
                break;
            case PayrollReportKind.DeptDetailed:
                foreach (var group in payload.Groups)
                {
                    sb.Append($"<h3 class=\"dept\">Payroll By Dept — {Esc(group.GroupLabel)}</h3>");
                    foreach (var emp in group.EmployeeDetails)
                    {
                        sb.Append($"<table class=\"emp\"><tr><td class=\"k\">Employee</td>" +
                                  $"<td>{Esc(emp.EmployeeNo)} {Esc(emp.EmployeeName)}</td>" +
                                  $"<td class=\"k\">NRC</td><td>{Esc(emp.Nrc)}</td></tr>");
                        sb.Append($"<tr><td class=\"k\">Job</td><td>{Esc(emp.JobTitle)}</td>" +
                                  $"<td class=\"k\">Pay Type</td><td>{Esc(emp.PayType)}</td></tr>");
                        sb.Append($"<tr><td class=\"k\">Pay Method</td><td>{Esc(emp.PayMethod)}</td>" +
                                  $"<td class=\"k\">Bank</td><td>{Esc(emp.BankName)} {Esc(emp.AccountNumber)}</td></tr></table>");
                        sb.Append("<table><tr><th>Details</th><th class=\"num\">Qty</th><th class=\"num\">Payments</th>" +
                                  "<th class=\"num\">Deductions</th></tr>");
                        foreach (var ln in emp.Lines)
                        {
                            var css = ln.Code == "net-pay" ? " class=\"netpay\"" : "";
                            sb.Append($"<tr{css}><td>{Esc(ln.Code)} {Esc(ln.Name)}</td><td class=\"num\">{ln.Qty:0.##}</td>" +
                                      $"<td class=\"num\">{Fmt(ln.Payment)}</td><td class=\"num\">{Fmt(ln.Deduction)}</td></tr>");
                        }
                        sb.Append($"<tr class=\"subtotal\"><td>Totals</td><td></td>" +
                                  $"<td class=\"num\">{Fmt(emp.GrossPay)}</td>" +
                                  $"<td class=\"num\">{Fmt(emp.Tax + (emp.NetPay - emp.GrossPay + emp.Taxable - emp.Taxable))}</td></tr>");
                        sb.Append($"<tr class=\"netrow\"><td>NET PAY</td><td></td><td></td>" +
                                  $"<td class=\"num\">{Fmt(emp.NetPay)}</td></tr></table>");
                        sb.Append($"<div class=\"taxblock\">Tax Basis: NORMAL &nbsp; Gross Pay: {Fmt(emp.GrossPay)} " +
                                  $"&nbsp; Taxable: {Fmt(emp.Taxable)} &nbsp; Tax: {Fmt(emp.Tax)}</div>");
                    }
                    sb.Append($"<div class=\"deptbox\">Totals For Dept: {Esc(group.GroupLabel)} — Payments: {Fmt(group.GroupTotals.Payments)}" +
                              $" Deductions: {Fmt(group.GroupTotals.Deductions)} Net Pay: {Fmt(group.GroupTotals.NetPay)}" +
                              $" Employees: {group.GroupTotals.Count}</div>");
                }
                sb.Append("<table><tr class=\"total\"><td>Company Totals</td>" +
                          $"<td>Employees: {payload.CompanyTotals.Count}</td>" +
                          $"<td class=\"num\">Payments {Fmt(payload.CompanyTotals.Payments)}</td>" +
                          $"<td class=\"num\">Deductions {Fmt(payload.CompanyTotals.Deductions)}</td>" +
                          $"<td class=\"num\">Net Pay {Fmt(payload.CompanyTotals.NetPay)}</td>" +
                          $"<td class=\"num\">Tax {Fmt(payload.CompanyTotals.Tax)}</td></tr></table>");
                break;
        }
        if (payload.HasUnmappedAccounts)
            sb.Append("<div class=\"note\">* component not yet mapped to a GL account — accounts must assign one before booking</div>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static string Fmt(decimal v) => v.ToString("#,##0.00", CsvCulture);
    private static string Esc(string? s) => HttpUtility.HtmlEncode(s ?? "");

    private static string CsvLine(params string?[] values) =>
        string.Join(",", values.Select(v => v is null ? "" : CsvQuote(v)));

    private static string CsvQuote(string v) =>
        v.Contains(',') || v.Contains('"') || v.Contains('\n')
            ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;

    private const string Styles = @"
body{font-family:'Courier New',monospace;font-size:9pt;color:#000;margin:18px}
.report{max-width:760px}
.company{text-align:center;font-size:10pt;margin:0}
.title{text-align:center;font-size:9pt;margin:2px 0}
.meta{text-align:center;font-size:8pt;margin:0}
.printline{display:flex;justify-content:space-between;font-size:8pt;margin-top:10px;border-top:1px solid #000;padding-top:4px}
table{width:100%;border-collapse:collapse;margin:6px 0}
th,td{padding:2px 4px;text-align:left;font-size:8.5pt;border-bottom:1px dotted #888}
th{border-bottom:1px solid #000}
.num{text-align:right}
.subtotal td{border-top:1px solid #000;font-weight:bold}
.total td{border-top:2px solid #000;border-bottom:2px solid #000;font-weight:bold}
.dept{margin:14px 0 2px;font-size:9pt}
.emp td.k{font-weight:bold;width:18%}
.netpay td{font-weight:bold}
.netrow td{border-top:1px solid #000;font-weight:bold}
.taxblock{font-size:8pt;margin:4px 0 10px}
.deptbox{border:1px solid #000;padding:6px;font-size:8.5pt;margin:8px 0}
.note{font-size:8pt;margin-top:12px;border:1px dashed #666;padding:6px}";
}

/// <summary>Renders the HTML report to PDF and returns the bytes.</summary>
public interface IPayrollReportPdfRenderer
{
    Task<byte[]> RenderPdfAsync(string html, CancellationToken ct);
}
