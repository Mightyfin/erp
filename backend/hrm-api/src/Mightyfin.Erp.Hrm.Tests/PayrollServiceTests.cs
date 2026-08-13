using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M5: Zambian statutory deduction engine — ZRA PAYE slabs (2026),
/// NAPSA (5% capped) and NHIMA (1%) verified against published rates.</summary>
public class PayrollStatutoryTests
{
    /// <summary>2026 ZRA monthly PAYE bands (verified against the official ZRA
    /// PAYE calculator and PwC tax summaries): 0–5,100 @ 0%, 5,100.01–7,100 @
    /// 20%, 7,100.01–9,200 @ 30%, above 9,200 @ 37%. Annual boundaries
    /// 61,200 / 85,200 / 110,400 divide evenly by 12.</summary>
    private static List<TaxSlab> ZambiaPaye2026() => new()
    {
        new TaxSlab { TaxYear = "2026", MinAmount = 0m, MaxAmount = 5100m, Rate = 0m, Sequence = 10, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
        new TaxSlab { TaxYear = "2026", MinAmount = 5100m, MaxAmount = 7100m, Rate = 20m, Sequence = 20, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
        new TaxSlab { TaxYear = "2026", MinAmount = 7100m, MaxAmount = 9200m, Rate = 30m, Sequence = 30, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
        new TaxSlab { TaxYear = "2026", MinAmount = 9200m, MaxAmount = null, Rate = 37m, Sequence = 40, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
    };

    /// <summary>NAPSA 2026: 5% employee + 5% employer, earnings ceiling
    /// K37,236/month → max contribution K1,861.80 per party (PwC / NAPSA notice).</summary>
    private static List<ContributionRule> ZambiaNapsaNhima2026() => new()
    {
        new ContributionRule { Code = "napsa-ee", Name = "NAPSA Employee", Payer = "employee", Rate = 5m, Ceiling = 1861.80m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
        new ContributionRule { Code = "napsa-er", Name = "NAPSA Employer", Payer = "employer", Rate = 5m, Ceiling = 1861.80m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
        new ContributionRule { Code = "nhima-ee", Name = "NHIMA Employee", Payer = "employee", Rate = 1m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
        new ContributionRule { Code = "nhima-er", Name = "NHIMA Employer", Payer = "employer", Rate = 1m, TiedComponentCode = "basic", IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 },
    };

    private static SalaryComponent Comp(string code, string type, string basis, string? tied = null, decimal? rate = null, decimal? ceiling = null, int priority = 100, bool statutory = false) =>
        new() { Code = code, Name = code, ComponentType = type, CalculationBasis = basis, BasisComponentCode = tied ?? "basic", Rate = rate, Ceiling = ceiling, Priority = priority, IsStatutory = statutory, IsActive = true, EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 1, 1)), Version = 1 };

    private static Worker TestWorker(string empNo = "T001") => new()
    {
        Id = Guid.NewGuid(), EmployeeNo = empNo, FirstName = "Test", LastName = "Worker",
        Status = "active", WorkerType = "employee",
        BankDetails = { new WorkerBankDetail { BankName = "FNB", BranchCode = "250655", AccountNumber = "123456", AccountName = "Test", IsPrimary = true, PaymentMethod = "bank" } },
    };

    /// <summary>Evaluates a payroll context built from components, profile values, rules and slabs.
    /// Mirrors PayrollServiceImpl.CalculateRunAsync line-per-worker logic so the
    /// engine remains unit-testable without the repository.</summary>
    private static (decimal Gross, decimal Deductions, decimal Net, decimal EmployerCost, List<(string Code, string Name, string Type, decimal Amount, string Explanation, bool IsStatutory)> Components)
        Evaluate(List<SalaryComponent> components, List<(Guid ComponentId, string Code, decimal Amount)> values,
                 List<ContributionRule> rules, List<TaxSlab> slabs, string? bankDetailWorker = "x")
    {
        var worker = TestWorker();
        var profile = new WorkerPayrollProfile
        {
            WorkerId = worker.Id, Worker = worker,
            ComponentValues = values.Select(v => new WorkerComponentValue
            {
                ComponentId = v.ComponentId, Component = components.First(c => c.Code == v.Code), Amount = v.Amount,
            }).ToList(),
            EffectiveFrom = DateOnly.FromDateTime(new DateTime(2026, 8, 1)),
        };
        var ctx = new CalcContext(worker, profile, components, rules, slabs);
        foreach (var comp in components.Where(c => c.IsActive).OrderBy(c => c.Priority))
            ctx.Evaluate(comp);
        var net = ctx.Gross - ctx.Deductions;
        return (ctx.Gross, ctx.Deductions, net, ctx.EmployerCost, ctx.Components);
    }

    [Fact]
    public void Paye_TaxFreeBand_NoTaxBelow5100()
    {
        var basic = Comp("basic", "earning", "fixed", priority: 10);
        var paye = Comp("paye", "tax", "slab");
        var (gross, deductions, net, _, comps) = Evaluate(
            new() { basic, paye }, new() { (basic.Id, "basic", 5100m) },
            ZambiaNapsaNhima2026(), ZambiaPaye2026());
        Assert.Equal(5100m, gross);
        Assert.Equal(0m, comps.First(c => c.Code == "paye").Amount);
        Assert.Equal(5100m, net);
    }

    [Fact]
    public void Paye_BandBoundary_20PercentOnSecondBand()
    {
        var basic = Comp("basic", "earning", "fixed", priority: 10);
        var paye = Comp("paye", "tax", "slab");
        var (_, deductions, _, _, comps) = Evaluate(
            new() { basic, paye }, new() { (basic.Id, "basic", 7100m) },
            ZambiaNapsaNhima2026(), ZambiaPaye2026());
        // 20% on (7,100 − 5,100) = 2,000 → 400
        Assert.Equal(400m, comps.First(c => c.Code == "paye").Amount);
    }

    [Fact]
    public void Paye_MidBand_30PercentAppliesCorrectly()
    {
        var basic = Comp("basic", "earning", "fixed", priority: 10);
        var paye = Comp("paye", "tax", "slab");
        var (_, _, _, _, comps) = Evaluate(
            new() { basic, paye }, new() { (basic.Id, "basic", 8000m) },
            ZambiaNapsaNhima2026(), ZambiaPaye2026());
        // 20% × 2,000 + 30% × (8,000 − 7,100) = 400 + 270 = 670
        Assert.Equal(670m, comps.First(c => c.Code == "paye").Amount);
    }

    [Fact]
    public void Paye_TopBand_37PercentMarginal()
    {
        var basic = Comp("basic", "earning", "fixed", priority: 10);
        var paye = Comp("paye", "tax", "slab");
        var (gross, _, _, _, comps) = Evaluate(
            new() { basic, paye }, new() { (basic.Id, "basic", 12000m) },
            ZambiaNapsaNhima2026(), ZambiaPaye2026());
        // 400 + 630 (30% × 2,100) + 37% × (12,000 − 9,200) = 400 + 630 + 1,036 = 2,066
        Assert.Equal(2066m, comps.First(c => c.Code == "paye").Amount);
    }

    [Fact]
    public void Paye_ZeroIncome_NoException()
    {
        var basic = Comp("basic", "earning", "fixed", priority: 10);
        var paye = Comp("paye", "tax", "slab");
        var (_, _, _, _, comps) = Evaluate(
            new() { basic, paye }, new() { (basic.Id, "basic", 0m) },
            ZambiaNapsaNhima2026(), ZambiaPaye2026());
        Assert.Equal(0m, comps.First(c => c.Code == "paye").Amount);
    }

    [Fact]
    public void Napsa_5PercentCappedAtCeiling()
    {
        var basic = Comp("basic", "earning", "fixed", priority: 10);
        var napsaEe = Comp("napsa-ee", "deduction", "percent-of", tied: "basic", rate: 5m, statutory: true);
        var napsaEr = Comp("napsa-er", "employer-contribution", "percent-of", tied: "basic", rate: 5m, statutory: true);
        var (_, deductions, _, employerCost, comps) = Evaluate(
            new() { basic, napsaEe, napsaEr }, new() { (basic.Id, "basic", 40000m) },
            ZambiaNapsaNhima2026(), ZambiaPaye2026());
        // 40,000 × 5% = 2,000 > ceiling 1,861.80 → capped per party
        Assert.Equal(1861.80m, comps.First(c => c.Code == "napsa-ee").Amount);
        Assert.Equal(1861.80m, comps.First(c => c.Code == "napsa-er").Amount);
        Assert.Equal(1861.80m, deductions);
        Assert.Equal(1861.80m, employerCost);
    }

    [Fact]
    public void Napsa_BelowCeiling_Full5Percent()
    {
        var basic = Comp("basic", "earning", "fixed", priority: 10);
        var napsaEe = Comp("napsa-ee", "deduction", "percent-of", tied: "basic", rate: 5m, statutory: true);
        var (gross, _, _, _, comps) = Evaluate(
            new() { basic, napsaEe }, new() { (basic.Id, "basic", 15000m) },
            ZambiaNapsaNhima2026(), ZambiaPaye2026());
        // 15,000 × 5% = 750 (below ceiling)
        Assert.Equal(750m, comps.First(c => c.Code == "napsa-ee").Amount);
    }

    [Fact]
    public void Nhima_1PercentUncapped()
    {
        var basic = Comp("basic", "earning", "fixed", priority: 10);
        var nhimaEe = Comp("nhima-ee", "deduction", "percent-of", tied: "basic", rate: 1m, statutory: true);
        var nhimaEr = Comp("nhima-er", "employer-contribution", "percent-of", tied: "basic", rate: 1m, statutory: true);
        var (gross, deductions, _, employerCost, comps) = Evaluate(
            new() { basic, nhimaEe, nhimaEr }, new() { (basic.Id, "basic", 50000m) },
            ZambiaNapsaNhima2026(), ZambiaPaye2026());
        Assert.Equal(500m, comps.First(c => c.Code == "nhima-ee").Amount);
        Assert.Equal(500m, comps.First(c => c.Code == "nhima-er").Amount);
        Assert.Equal(500m, deductions);
        Assert.Equal(500m, employerCost);
    }

    [Fact]
    public void FullPayslip_IntegratesEarningsStatutoryAndTax()
    {
        var basic = Comp("basic", "earning", "fixed", priority: 10);
        var housing = Comp("housing-allowance", "earning", "fixed", priority: 20);
        var napsaEe = Comp("napsa-ee", "deduction", "percent-of", tied: "basic", rate: 5m, statutory: true);
        var nhimaEe = Comp("nhima-ee", "deduction", "percent-of", tied: "basic", rate: 1m, statutory: true);
        var paye = Comp("paye", "tax", "slab", tied: "gross");
        var napsaEr = Comp("napsa-er", "employer-contribution", "percent-of", tied: "basic", rate: 5m, statutory: true);
        var nhimaEr = Comp("nhima-er", "employer-contribution", "percent-of", tied: "basic", rate: 1m, statutory: true);

        var (gross, deductions, net, employerCost, comps) = Evaluate(
            new() { basic, housing, napsaEe, nhimaEe, paye, napsaEr, nhimaEr },
            new() { (basic.Id, "basic", 25000m), (housing.Id, "housing-allowance", 5000m) },
            ZambiaNapsaNhima2026(), ZambiaPaye2026());

        // Gross = 25,000 + 5,000 = 30,000
        Assert.Equal(30000m, gross);
        // PAYE on 30,000: 400 + 630 + 37% × 20,800 = 400 + 630 + 7,696 = 8,726
        var payeAmt = comps.First(c => c.Code == "paye").Amount;
        Assert.Equal(8726m, payeAmt);
        // NAPSA-EE on basic 25,000 × 5% = 1,250 (below ceiling); NHIMA-EE 1% × 25,000 = 250
        Assert.Equal(1250m, comps.First(c => c.Code == "napsa-ee").Amount);
        Assert.Equal(250m, comps.First(c => c.Code == "nhima-ee").Amount);
        // Deductions = 8,726 + 1,250 + 250 = 10,226
        Assert.Equal(10226m, deductions);
        // Net = 30,000 − 10,226 = 19,774
        Assert.Equal(19774m, net);
        // Employer cost = 1,250 (NAPSA-ER 5% × 25,000) + 250 (NHIMA-ER 1% × 25,000) = 1,500
        Assert.Equal(1500m, employerCost);
    }
}
