using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Payroll;

/// <summary>Builds JV and payroll-by-department reports for a released or
/// closed run. Transaction codes come from SalaryComponent.Code (the same
/// convention the reference reports use: payment codes P0001, deduction codes
/// D9990/Net Pay etc.). The JV summary is the booking report: its Payment
/// column totals are debits and its Deduction column totals are credits.</summary>
public sealed class PayrollReportServiceImpl(
    IPayrollRepository repo,
    IAuthzService authz) : IPayrollReportService
{
    private static readonly string UnmappedMark = "*";

    public async Task<PayrollReportPayload> GetAsync(PayrollReportKind kind, Guid runId,
        string format, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin", "accounts");
        if (format is not ("csv" or "pdf"))
            throw new DomainException("report-format-not-supported", "format must be csv or pdf");

        var run = await repo.GetRunAsync(runId, ct)
            ?? throw new DomainException("payroll-run-not-found", $"Run {runId} does not exist.");
        if (run.Status is not ("released" or "closed"))
            throw new DomainException("report-run-not-released",
                "Payroll reports are only available for released or closed runs.");

        var org = (await repo.ListLegalEntitiesAsync(ct)).FirstOrDefault();
        var (lines, _) = await repo.ListRunLinesAsync(runId, ct);
        var allComponents = await repo.ListAllComponentsAsync(ct);
        var componentByName = allComponents.GroupBy(c => c.Code).ToDictionary(g => g.Key,
            g => g.OrderByDescending(x => x.Version).First(), System.StringComparer.OrdinalIgnoreCase);

        var payload = new PayrollReportPayload
        {
            CompanyName = org?.TradingName ?? org?.RegisteredName ?? "Mightyfin",
            PeriodLabel = run.PayPeriod?.PeriodLabel ?? runId.ToString("D"),
            PrintDate = System.DateTimeOffset.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        };

        switch (kind)
        {
            case PayrollReportKind.JvDetailed:
                BuildJvDetailed(payload, lines, componentByName);
                payload.ReportTitle = $"Detailed JV Report — {payload.PeriodLabel}";
                break;
            case PayrollReportKind.JvSummary:
                BuildJvSummary(payload, lines, componentByName);
                payload.ReportTitle = $"JV Report By Transaction — {payload.PeriodLabel}";
                break;
            case PayrollReportKind.DeptSummary:
                await BuildDeptSummary(payload, lines, runId, ct);
                payload.ReportTitle = $"Payroll Summary By Department — {payload.PeriodLabel}";
                break;
            case PayrollReportKind.DeptDetailed:
                await BuildDeptDetailed(payload, lines, componentByName, runId, ct);
                payload.ReportTitle = $"Payroll Report By Department — {payload.PeriodLabel}";
                break;
        }
        return payload;
    }

    // ---------- JV detailed: Employee | Transaction | Payment | Deduction | Dept | Currency ----------
    private void BuildJvDetailed(PayrollReportPayload payload,
        List<PayrollRunLine> lines,
        System.Collections.Generic.Dictionary<string, SalaryComponent> componentByName)
    {
        var company = new PayrollReportTotals();
        var grouped = lines
            .Where(l => !l.IsExcluded)
            .OrderBy(l => l.Worker?.EmployeeNo)
            .ToList();

        foreach (var line in grouped)
        {
            var group = new PayrollReportGroup
            {
                GroupLabel = $"{line.Worker?.EmployeeNo ?? ""} — {line.Worker?.FullName ?? ""}",
            };
            var dept = line.Worker?.OrgUnit?.Name ?? "";
            var empPayments = 0m;
            var empDeductions = 0m;

            foreach (var lc in line.Components.OrderBy(c => c.ComponentType))
            {
                componentByName.TryGetValue(lc.ComponentCode, out var comp);
                var name = lc.ComponentName;
                if (comp is not null && string.IsNullOrWhiteSpace(comp.GlAccountRef))
                {
                    name += " " + UnmappedMark;
                    payload.HasUnmappedAccounts = true;
                }
                var row = new PayrollReportRow
                {
                    EmployeeNo = line.Worker?.EmployeeNo ?? "",
                    EmployeeName = line.Worker?.FullName ?? "",
                    TransactionCode = lc.ComponentCode,
                    TransactionName = name,
                    Department = dept,
                };
                // Employer-paid side (earnings + employer contributions) is the
                // Payment column; employee deductions (incl. tax) sit in the
                // Deduction column.
                if (lc.ComponentType is "earning" or "employer-contribution")
                {
                    row.Payment = lc.Amount;
                    empPayments += lc.Amount;
                }
                else
                {
                    row.Deduction = lc.Amount;
                    empDeductions += lc.Amount;
                }
                group.Rows.Add(row);
            }
            // Net pay: its own deduction-coded row (the D9990 convention).
            group.Rows.Add(new PayrollReportRow
            {
                EmployeeNo = line.Worker?.EmployeeNo ?? "",
                EmployeeName = line.Worker?.FullName ?? "",
                TransactionCode = "net-pay",
                TransactionName = "Net Pay",
                Department = dept,
                Deduction = line.NetPay,
            });
            empDeductions += line.NetPay;

            group.GroupTotals = new PayrollReportTotals
            {
                Payments = empPayments,
                Deductions = empDeductions,
            };
            company.Payments += empPayments;
            company.Deductions += empDeductions;
            payload.Groups.Add(group);
        }
        company.Count = grouped.Count;
        payload.CompanyTotals = company;
    }

    // ---------- JV summary: Transaction Code | Transaction Name | Payment | Deduction ----------
    private void BuildJvSummary(PayrollReportPayload payload,
        List<PayrollRunLine> lines,
        System.Collections.Generic.Dictionary<string, SalaryComponent> componentByName)
    {
        var totals = new PayrollReportTotals();
        var byCode = new System.Collections.Generic.Dictionary<string, PayrollReportRow>(
            System.StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines.Where(l => !l.IsExcluded))
        {
            foreach (var lc in line.Components)
            {
                if (!byCode.TryGetValue(lc.ComponentCode, out var row))
                {
                    componentByName.TryGetValue(lc.ComponentCode, out var comp);
                    var name = lc.ComponentName;
                    if (comp is not null && string.IsNullOrWhiteSpace(comp.GlAccountRef))
                    {
                        name += " " + UnmappedMark;
                        payload.HasUnmappedAccounts = true;
                    }
                    row = new PayrollReportRow
                    {
                        TransactionCode = lc.ComponentCode,
                        TransactionName = name,
                    };
                    byCode[lc.ComponentCode] = row;
                }
                if (lc.ComponentType is "earning" or "employer-contribution")
                    row.Payment += lc.Amount;
                else
                    row.Deduction += lc.Amount;
            }
            if (!byCode.TryGetValue("net-pay", out var netRow))
            {
                netRow = new PayrollReportRow { TransactionCode = "net-pay", TransactionName = "Net Pay" };
                byCode["net-pay"] = netRow;
            }
            netRow.Deduction += line.NetPay;
        }

        payload.Groups.Add(new PayrollReportGroup
        {
            GroupLabel = "Transactions",
            Rows = byCode.Values.OrderBy(r => r.TransactionCode).ToList(),
        });

        totals.Payments = payload.Groups[0].Rows.Sum(r => r.Payment);
        totals.Deductions = payload.Groups[0].Rows.Sum(r => r.Deduction);
        payload.CompanyTotals = totals;
    }

    // ---------- Department summary: Employee | NRC | Payments | Deductions | Net Pay | Neg Net Pay ----------
    private async Task BuildDeptSummary(PayrollReportPayload payload, List<PayrollRunLine> lines,
        Guid runId, CancellationToken ct)
    {
        var company = new PayrollReportTotals();
        var ordered = lines.Where(l => !l.IsExcluded)
            .OrderBy(l => l.Worker?.OrgUnit?.Name)
            .ThenBy(l => l.Worker?.EmployeeNo)
            .ToList();

        foreach (var deptGroup in ordered.GroupBy(l => l.Worker?.OrgUnit?.Name ?? "Unassigned"))
        {
            var group = new PayrollReportGroup { GroupLabel = deptGroup.Key };
            foreach (var line in deptGroup)
            {
                var row = new PayrollReportRow
                {
                    EmployeeNo = line.Worker?.EmployeeNo ?? "",
                    EmployeeName = line.Worker?.FullName ?? "",
                    Nrc = line.Worker?.Nrc ?? "",
                    Payment = line.GrossPay + line.EmployerCost - line.EmployerCost, // payments = earnings side
                    Deduction = line.TotalDeductions,
                    NetPay = line.NetPay,
                };
                // Payment column in the reference summary means gross payments
                // to the employee (earnings only, employer contributions excluded).
                row.Payment = line.GrossPay;
                if (row.NetPay < 0)
                {
                    row.NegNetPay = -row.NetPay;
                    row.NetPay = 0;
                }
                group.Rows.Add(row);
                group.GroupTotals.Count++;
                group.GroupTotals.Payments += row.Payment;
                group.GroupTotals.Deductions += row.Deduction;
                group.GroupTotals.NetPay += row.NetPay;
                group.GroupTotals.NegNetPay += row.NegNetPay;
            }
            company.Count += group.GroupTotals.Count;
            company.Payments += group.GroupTotals.Payments;
            company.Deductions += group.GroupTotals.Deductions;
            company.NetPay += group.GroupTotals.NetPay;
            company.NegNetPay += group.GroupTotals.NegNetPay;
            payload.Groups.Add(group);
        }
        payload.CompanyTotals = company;
    }

    // ---------- Department detailed: per-employee blocks with component lines + tax block ----------
    private async Task BuildDeptDetailed(PayrollReportPayload payload, List<PayrollRunLine> lines,
        System.Collections.Generic.Dictionary<string, SalaryComponent> componentByName,
        Guid runId, CancellationToken ct)
    {
        var company = new PayrollReportTotals();
        var ordered = lines.Where(l => !l.IsExcluded)
            .OrderBy(l => l.Worker?.OrgUnit?.Name)
            .ThenBy(l => l.Worker?.EmployeeNo)
            .ToList();

        foreach (var deptGroup in ordered.GroupBy(l => l.Worker?.OrgUnit?.Name ?? "Unassigned"))
        {
            var group = new PayrollReportGroup { GroupLabel = deptGroup.Key };
            foreach (var line in deptGroup)
            {
                var bank = line.Worker?.BankDetails?.OrderByDescending(b => b.IsPrimary).FirstOrDefault();
                var detail = new PayrollReportEmployeeDetail
                {
                    EmployeeNo = line.Worker?.EmployeeNo ?? "",
                    EmployeeName = line.Worker?.FullName ?? "",
                    Nrc = line.Worker?.Nrc ?? "",
                    JobTitle = line.Worker?.JobTitle ?? "",
                    PayType = "SALARY",
                    PayMethod = bank?.PaymentMethod switch
                    {
                        "mobile-money" => "MOBILE MONEY",
                        "cash" => "CASH",
                        _ => "BANK",
                    },
                    BankName = bank?.BankName ?? "",
                    AccountNumber = bank?.AccountNumber ?? "",
                    StartDate = line.Worker?.StartDate?.ToString("O") ?? "",
                    NetPay = line.NetPay,
                };
                decimal payments = 0, deductions = 0, taxable = 0, tax = 0;
                foreach (var lc in line.Components.OrderBy(c => c.ComponentType).ThenBy(c => c.ComponentCode))
                {
                    componentByName.TryGetValue(lc.ComponentCode, out var comp);
                    var isPayment = lc.ComponentType is "earning" or "employer-contribution";
                    detail.Lines.Add(new PayrollLineRow
                    {
                        Code = lc.ComponentCode,
                        Name = lc.ComponentName,
                        Qty = 1,
                        Payment = isPayment ? lc.Amount : 0,
                        Deduction = isPayment ? 0 : lc.Amount,
                    });
                    if (isPayment) payments += lc.Amount;
                    else
                    {
                        deductions += lc.Amount;
                        if (lc.ComponentType == "tax") tax += lc.Amount;
                    }
                    if (comp?.IsTaxable == true && lc.ComponentType == "earning") taxable += lc.Amount;
                }
                // Net-pay final row inside the employee block.
                detail.Lines.Add(new PayrollLineRow { Code = "net-pay", Name = "Net Pay", Qty = 1, Payment = 0, Deduction = line.NetPay });
                detail.GrossPay = payments;
                detail.Taxable = taxable;
                detail.Tax = tax;

                group.EmployeeDetails.Add(detail);
                group.GroupTotals.Count++;
                group.GroupTotals.Payments += payments;
                group.GroupTotals.Deductions += deductions;
                group.GroupTotals.NetPay += Math.Max(0m, line.NetPay);
                group.GroupTotals.NegNetPay += Math.Max(0m, -line.NetPay);
                group.GroupTotals.Taxable += taxable;
                group.GroupTotals.Tax += tax;
            }
            company.Count += group.GroupTotals.Count;
            company.Payments += group.GroupTotals.Payments;
            company.Deductions += group.GroupTotals.Deductions;
            company.NetPay += group.GroupTotals.NetPay;
            company.NegNetPay += group.GroupTotals.NegNetPay;
            company.Taxable += group.GroupTotals.Taxable;
            company.Tax += group.GroupTotals.Tax;
            payload.Groups.Add(group);
        }
        payload.CompanyTotals = company;
    }
}

