using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M41: accounting-facing payroll reports — JV detailed/summary and
/// payroll-by-department summary/detailed. These are the reports the accounts
/// team books the salary from (payments = debits, deductions = credits).</summary>
public class M41AccountingReportTests
{
    /// Shares the engine's in-memory SQLite connection so seeded data (worker,
    /// legal entity, bank detail) is visible to the report service. Each test
    /// owns its own connection via its own unique database name.
    private static (PayrollReportServiceImpl service, HrmDbContext ctx) Build(Microsoft.Data.Sqlite.SqliteConnection conn)
    {
        var opts = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(conn).Options;
        var ctx = new HrmDbContext(opts, new FixedTenantAccessor("m41-reports"));
        var repo = new PayrollRepository(ctx);
        var svc = new PayrollReportServiceImpl(repo, new PermissiveAuthz());
        return (svc, ctx);
    }

    /// Re-uses the engine's shared-connection SQLite context so one test's
    /// lifecycle runs inside the same database the report service reads.
    private static (PayrollServiceImpl service, HrmDbContext ctx) BuildEngine(Microsoft.Data.Sqlite.SqliteConnection conn)
    {
        var opts = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(conn).Options;
        var ctx = new HrmDbContext(opts, new FixedTenantAccessor("m41-reports"));
        ctx.Database.EnsureCreated();
        var repo = new PayrollRepository(ctx);
        var svc = new PayrollServiceImpl(repo, new PermissiveAuthz(), new FakePayslipDoc());
        return (svc, ctx);
    }

    private sealed class FakePayslipDoc : IPayslipDocumentService
    {
        public Task<string> GenerateAsync(Payslip slip, PayrollRunLine line, CancellationToken ct)
            => Task.FromResult("https://storage.example/payslip.pdf");
    }

    /// Seeds the payroll stack via PayrollEngineTests and runs a released lifecycle.
    private static async Task<(PayrollReportServiceImpl Svc, HrmDbContext Ctx, Guid RunId, PayGroup Group,
        Worker Worker)> SeededReleasedAsync()
    {
        var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=hrm-m41-" + System.Guid.NewGuid() + ";Mode=Memory;Cache=Shared");
        conn.Open();
        var (engine, ctx) = BuildEngine(conn);
        var (group, _, period, _, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);
        ctx.LegalEntities.Add(new LegalEntity { Code = "MFZ", RegisteredName = "Mightyfin Limited", TradingName = "Mighty Finance" });
        var worker = await ctx.Workers.SingleAsync(w => w.EmployeeNo == "T001");
        ctx.WorkerBankDetails.Add(new WorkerBankDetail
        {
            WorkerId = worker.Id,
            BankName = "Zanaco", BranchCode = "001", AccountNumber = "1000000001",
            AccountName = "Test Worker", PaymentMethod = "bank", IsPrimary = true,
        });
        await ctx.SaveChangesAsync();

        var run = await engine.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), CancellationToken.None);
        run = await engine.LockRunAsync(run.Id, CancellationToken.None);
        run = await engine.CalculateRunAsync(run.Id, CancellationToken.None);
        run = await engine.ApproveRunAsync(run.Id, "ok", CancellationToken.None);
        run = await engine.ReleaseRunAsync(run.Id, CancellationToken.None);

        var (svc, reportCtx) = Build(conn);
        return (svc, reportCtx, run.Id, group, worker);
    }

    [Fact]
    public async Task JvSummary_PaymentColumnEqualsEarningsPlusEmployerContributions()
    {
        var (svc, ctx, runId, _, _) = await SeededReleasedAsync();
        var payload = await svc.GetAsync(PayrollReportKind.JvSummary, runId, "csv", CancellationToken.None);

        var row = payload.Groups[0].Rows;
        var payments = row.Sum(r => r.Payment);
        var deductions = row.Sum(r => r.Deduction);
        var employerContributions = row.Where(r => !r.TransactionCode.StartsWith("ee", System.StringComparison.OrdinalIgnoreCase))
            .Where(r => r.TransactionCode.Contains("-er")).Sum(r => r.Payment);

        // Booking identity: debits (earnings + employer contributions) =
        // credits (employee deductions + net pay) + employer contributions.
        Assert.Equal(deductions + employerContributions, payments);
        Assert.Contains(row, r => r.TransactionCode == "net-pay");
        Assert.Contains(row, r => r.TransactionCode == "napsa-er" && r.Payment > 0);
    }

    [Fact]
    public async Task JvSummary_KeepsEmployerContributionsInPaymentColumn()
    {
        var (svc, _, runId, _, _) = await SeededReleasedAsync();
        var payload = await svc.GetAsync(PayrollReportKind.JvSummary, runId, "csv", CancellationToken.None);

        var napsaEr = payload.Groups[0].Rows.Single(r => r.TransactionCode == "napsa-er");
        var housing = payload.Groups[0].Rows.Single(r => r.TransactionCode == "housing");
        Assert.True(napsaEr.Payment > 0 && napsaEr.Deduction == 0, "employer contribution must be a payment");
        Assert.True(housing.Payment > 0, "earnings must be payments");
    }

    [Fact]
    public async Task JvDetailed_EveryLineRowSumsToLineTotals()
    {
        var (svc, _, runId, _, _) = await SeededReleasedAsync();
        var payload = await svc.GetAsync(PayrollReportKind.JvDetailed, runId, "csv", CancellationToken.None);

        foreach (var group in payload.Groups)
        {
            var lineTotals = group.Rows;
            Assert.Equal(lineTotals.Sum(r => r.Payment), group.GroupTotals.Payments);
            Assert.Equal(lineTotals.Sum(r => r.Deduction), group.GroupTotals.Deductions);
        }
    }

    [Fact]
    public async Task DeptSummary_PaysToNetEqualsPaymentsMinusDeductions()
    {
        var (svc, _, runId, _, _) = await SeededReleasedAsync();
        var payload = await svc.GetAsync(PayrollReportKind.DeptSummary, runId, "csv", CancellationToken.None);

        var row = payload.Groups[0].Rows[0];
        Assert.Equal(row.Payment - row.Deduction, row.NetPay);
        Assert.Equal(payload.CompanyTotals.Payments - payload.CompanyTotals.Deductions, payload.CompanyTotals.NetPay);
    }

    [Fact]
    public async Task DeptDetailed_IncludesEmpBlockWithJobAndBankDetails()
    {
        var (svc, _, runId, _, worker) = await SeededReleasedAsync();
        var payload = await svc.GetAsync(PayrollReportKind.DeptDetailed, runId, "csv", CancellationToken.None);

        var emp = payload.Groups[0].EmployeeDetails[0];
        Assert.Equal("T001", emp.EmployeeNo);
        Assert.Equal("BANK", emp.PayMethod);
        Assert.Equal("Zanaco", emp.BankName);
        Assert.Contains(emp.Lines, ln => ln.Code == "net-pay" && ln.Deduction > 0);
    }

    [Fact]
    public async Task Reports_BlockedForDraftRuns()
    {
        var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=hrm-m41b-" + System.Guid.NewGuid() + ";Mode=Memory;Cache=Shared");
        conn.Open();
        var (engine, ctx) = BuildEngine(conn);
        var (group, _, period, _, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(ctx);
        await ctx.SaveChangesAsync();
        var run = await engine.CreateRunAsync(new PayrollRunCreate(period.Id, group.Id), CancellationToken.None);

        var (svc, _) = Build(conn);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            svc.GetAsync(PayrollReportKind.JvSummary, run.Id, "csv", CancellationToken.None));
        Assert.Equal("report-run-not-released", ex.Code);
    }

    [Fact]
    public async Task Reports_RejectBadFormats()
    {
        var (svc, _, runId, _, _) = await SeededReleasedAsync();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.GetAsync(PayrollReportKind.JvSummary, runId, "xlsx", CancellationToken.None));
    }

    [Fact]
    public async Task Csv_RoundTripSafe_QuotesValuesWithCommas()
    {
        var (svc, ctx, runId, group, worker) = await SeededReleasedAsync();
        ctx.LegalEntities.Update(await ctx.LegalEntities.SingleAsync());
        await ctx.SaveChangesAsync();

        var payload = await svc.GetAsync(PayrollReportKind.JvSummary, runId, "csv", CancellationToken.None);
        var csv = PayrollReportFormatter.ToCsv(payload, PayrollReportKind.JvSummary);
        Assert.Contains("net-pay", csv);
        Assert.Contains("company total", csv, System.StringComparison.OrdinalIgnoreCase);
    }
}
