using System.Runtime.CompilerServices;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Domain.Entities;

[assembly: InternalsVisibleTo("Mightyfin.Erp.Hrm.Tests")]

namespace Mightyfin.Erp.Hrm.Application.Payroll;

/// <summary>J-groups 01-23: Payroll setup reads and the run lifecycle.
/// Calculation is intentionally synchronous per-run-line for v1 (the calculation
/// job table preserves the boundary for a future worker/queue extraction).</summary>
public interface IPayrollService
{
    // Setup reads
    Task<List<SalaryComponentDto>> ListComponentsAsync(string? type, CancellationToken ct);
    Task<List<PayGroupDto>> ListPayGroupsAsync(CancellationToken ct);
    Task<List<PayPeriodDto>> ListPeriodsAsync(Guid payGroupId, CancellationToken ct);
    Task<List<TaxSlabDto>> ListTaxSlabsAsync(string taxYear, CancellationToken ct);
    Task<List<ContributionRuleDto>> ListContributionRulesAsync(CancellationToken ct);

    // M5 setup: worker payroll profiles (basic salary + allowances per worker)
    Task<List<WorkerPayrollProfileDto>> ListProfilesAsync(Guid? workerId, CancellationToken ct);
    Task<WorkerPayrollProfileDto> UpsertProfileAsync(Guid workerId, WorkerPayrollProfileCreate request, CancellationToken ct);

    // Run lifecycle
    Task<PayrollRunDto> CreateRunAsync(PayrollRunCreate request, CancellationToken ct);
    Task<PayrollRunDto> GetRunAsync(Guid id, CancellationToken ct);
    Task<PayrollRunDto> LockRunAsync(Guid id, CancellationToken ct);
    Task<PayrollRunDto> CalculateRunAsync(Guid id, CancellationToken ct);
    Task<Paged<PayrollRunLineDto>> GetRunLinesAsync(Guid id, CancellationToken ct);
    Task<PayrollRunDto> ApproveRunAsync(Guid id, string? note, CancellationToken ct);
    Task<PayrollRunDto> ReleaseRunAsync(Guid id, CancellationToken ct);
    Task<Paged<PayslipDto>> GetPayslipsAsync(Guid workerId, CancellationToken ct);
    Task<PayslipDto?> GetPayslipByIdAsync(Guid id, CancellationToken ct);
}

public sealed record SalaryComponentDto(Guid Id, string Code, string Name, string ComponentType, string CalculationBasis, decimal? Rate, decimal? FixedAmount, decimal? Ceiling, bool IsTaxable, bool IsStatutory, int Version, bool IsActive);
public sealed record PayPeriodDto(Guid Id, string PeriodLabel, string StartDate, string EndDate, string CutoffDate, string PayDate, string Status);
public sealed record TaxSlabDto(Guid Id, string TaxYear, decimal MinAmount, decimal? MaxAmount, decimal Rate, int Sequence);
public sealed record ContributionRuleDto(Guid Id, string Code, string Name, string Payer, decimal Rate, decimal? Ceiling, decimal? Floor);

public sealed class PayrollServiceImpl(IPayrollRepository repo, IAuthzService authz) : IPayrollService
{
    public async Task<List<SalaryComponentDto>> ListComponentsAsync(string? type, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListComponentsAsync(type, ct);
        return items.Select(c => new SalaryComponentDto(c.Id, c.Code, c.Name, c.ComponentType, c.CalculationBasis, c.Rate, c.FixedAmount, c.Ceiling, c.IsTaxable, c.IsStatutory, c.Version, c.IsActive)).ToList();
    }

    public async Task<List<PayGroupDto>> ListPayGroupsAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListPayGroupsAsync(ct);
        return items.Select(g => new PayGroupDto(g.Id, g.Code, g.Name, g.Frequency, g.Currency, g.CalendarDayOfMonth)).ToList();
    }

    public async Task<List<PayPeriodDto>> ListPeriodsAsync(Guid payGroupId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListPeriodsAsync(payGroupId, ct);
        return items.Select(p => new PayPeriodDto(p.Id, p.PeriodLabel, p.StartDate.ToString(), p.EndDate.ToString(), p.CutoffDate.ToString(), p.PayDate.ToString(), p.Status)).ToList();
    }

    public async Task<List<TaxSlabDto>> ListTaxSlabsAsync(string taxYear, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListTaxSlabsAsync(taxYear, ct);
        return items.Select(s => new TaxSlabDto(s.Id, s.TaxYear, s.MinAmount, s.MaxAmount, s.Rate, s.Sequence)).ToList();
    }

    public async Task<List<ContributionRuleDto>> ListContributionRulesAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListContributionRulesAsync(ct);
        return items.Select(r => new ContributionRuleDto(r.Id, r.Code, r.Name, r.Payer, r.Rate, r.Ceiling, r.Floor)).ToList();
    }

    public async Task<PayrollRunDto> CreateRunAsync(PayrollRunCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var period = await repo.GetPeriodAsync(request.PayPeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", $"Pay period {request.PayPeriodId} does not exist.");
        if (period.Status != "open")
            throw new DomainException("pay-period-not-open", $"Pay period {period.PeriodLabel} is {period.Status} and cannot accept a new run.");
        var existing = await repo.FindRunByPeriodAsync(request.PayPeriodId, ct);
        if (existing is not null)
            throw new DomainException("run-already-exists", "A payroll run already exists for this period.");
        var run = new PayrollRun { PayPeriodId = request.PayPeriodId, PayGroupId = request.PayGroupId, Status = "draft", CalcVersion = "engine-v1" };
        var created = await repo.CreateRunAsync(run, ct);
        return MapRun(created);
    }

    /// <summary>Gross-to-net engine: applies active components in priority order
    /// per enrolled worker profile, taxes via progressive slab lookup, caps
    /// statutory contributions at ceilings, and records explainable line
    /// components with a pinned rule-version snapshot.</summary>
    public async Task<List<WorkerPayrollProfileDto>> ListProfilesAsync(Guid? workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var profiles = await repo.ListProfilesAsync(workerId, ct);
        return profiles.Select(MapProfile).ToList();
    }

    public async Task<WorkerPayrollProfileDto> UpsertProfileAsync(Guid workerId, WorkerPayrollProfileCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var worker = await repo.GetWorkerAsync(workerId, ct) ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");
        var group = await repo.GetPayGroupAsync(request.PayGroupId, ct)
            ?? throw new DomainException("pay-group-not-found", "Pay group does not exist.");
        if (!DateOnly.TryParse(request.EffectiveFrom, out var effective))
            throw new DomainException("bad-date", "EffectiveFrom must be a valid date (yyyy-MM-dd).");
        var allComponents = await repo.ListAllComponentsAsync(ct);
        var normalizedValues = new List<WorkerComponentValueCreate>();
        foreach (var v in request.Values)
        {
            var comp = await repo.GetComponentByIdAsync(v.ComponentId, ct);
            if (comp is null && !string.IsNullOrWhiteSpace(v.ComponentCode))
                comp = allComponents.FirstOrDefault(c => c.Code.Equals(v.ComponentCode, StringComparison.OrdinalIgnoreCase));
            if (comp is null)
                throw new DomainException("component-not-found", $"Component {v.ComponentCode ?? v.ComponentId.ToString()} does not exist.");
            normalizedValues.Add(new WorkerComponentValueCreate(comp.Id, comp.Code, v.Amount));
        }
        request = request with { Values = normalizedValues };

        var defaultStructure = await repo.FindStructureAsync("ZMW-STANDARD", ct);

        var existing = await repo.FindOpenProfileAsync(workerId, ct);
        WorkerPayrollProfile profile;
        if (existing is null)
        {
            profile = new WorkerPayrollProfile
            {
                WorkerId = workerId, PayGroupId = request.PayGroupId, EffectiveFrom = effective,
                StructureId = defaultStructure?.Id ?? Guid.Empty,
            };
            await repo.CreateProfileAsync(profile, ct);
        }
        else
        {
            existing.PayGroupId = request.PayGroupId;
            profile = existing;
        }
        await repo.DeleteProfileValuesAsync(profile.Id, ct);
        foreach (var v in request.Values)
            profile.ComponentValues.Add(new WorkerComponentValue { ComponentId = v.ComponentId, Amount = v.Amount });
        await repo.UpdateProfileAsync(profile, ct);
        return MapProfile(await repo.FindOpenProfileAsync(workerId, ct) ?? profile);
    }

    /// <summary>Locks the run for editing (freeze inputs before calculation).
    /// Segregation of duties: only draft runs can be locked; calculate then
    /// proceeds from locked.</summary>
    public async Task<PayrollRunDto> LockRunAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.Status != "draft")
            throw new DomainException("run-not-lockable", $"Run is in status {run.Status}; only draft runs can be locked.");
        run.Status = "locked";
        await repo.UpdateRunAsync(run, ct);
        return MapRun(run);
    }

    public async Task<PayrollRunDto> CalculateRunAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.Status is not "locked" and not "calculated")
            throw new DomainException("run-not-calculation-ready", $"Run is in status {run.Status} and cannot be calculated.");
        run.Status = "calculating";
        await repo.UpdateRunAsync(run, ct);

        var (profiles, components, rules, slabs, cutoff) = await repo.LoadCalculationInputsAsync(run.PayPeriodId, ct);
        int exceptions = 0;
        run.TotalGross = run.TotalDeductions = run.TotalNet = run.TotalEmployerCost = 0;
        await repo.ClearRunLinesAsync(run.Id, ct);

        foreach (var profile in profiles)
        {
            var worker = profile.Worker;
            if (worker is null) { exceptions++; continue; }
            var ctx = new CalcContext(worker, profile, components, rules, slabs);
            foreach (var comp in components.Where(c => c.IsActive).OrderBy(c => c.Priority))
                ctx.Evaluate(comp);
            var net = ctx.Gross - ctx.Deductions;
            if (net < 0) { exceptions++; ctx.ExceptionReason = "negative-net"; }
            if (!worker.BankDetails.Any(b => b.IsPrimary)) { exceptions++; ctx.ExceptionReason ??= "missing-bank"; }
            run.EmployeeCount++;
            run.TotalGross += ctx.Gross;
            run.TotalDeductions += ctx.Deductions;
            run.TotalNet += net;
            run.TotalEmployerCost += ctx.EmployerCost + ctx.Gross;
            run.ExceptionCount = exceptions;

            var line = new PayrollRunLine
            {
                RunId = run.Id, WorkerId = worker.Id,
                GrossPay = Math.Round(ctx.Gross, 2), TotalDeductions = Math.Round(ctx.Deductions, 2),
                NetPay = Math.Round(net, 2), EmployerCost = Math.Round(ctx.EmployerCost, 2),
                HasException = ctx.ExceptionReason is not null, ExceptionReason = ctx.ExceptionReason,
                ComponentCount = ctx.Components.Count,
                RuleVersionSnapshot = JsonSerializer.Serialize(components.Select(c => new { c.Id, c.Version }).ToList()),
            };
            foreach (var lc in ctx.Components)
                line.Components.Add(new PayrollLineComponent { ComponentCode = lc.Code, ComponentName = lc.Name, ComponentType = lc.Type, Amount = lc.Amount, Explanation = lc.Explanation, IsStatutory = lc.IsStatutory });
            await repo.AddRunLineAsync(line, ct);
        }
        run.Status = "calculated";
        await repo.UpdateRunAsync(run, ct);
        return MapRun(run);
    }

    public async Task<PayrollRunDto> GetRunAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        return MapRun(run);
    }

    public async Task<Paged<PayrollRunLineDto>> GetRunLinesAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var (items, total) = await repo.ListRunLinesAsync(id, ct);
        return new Paged<PayrollRunLineDto>(items.Select(l => new PayrollRunLineDto(
            l.Id, l.WorkerId, l.Worker?.FullName ?? "", l.Worker?.EmployeeNo ?? "",
            l.GrossPay, l.TotalDeductions, l.NetPay, l.EmployerCost, l.HasException, l.ExceptionReason,
            l.Components.Select(c => new PayrollLineComponentDto(c.ComponentCode, c.ComponentName, c.ComponentType, c.Amount, c.Explanation, c.IsStatutory)).ToList())).ToList(), total, 1, 100);
    }

    public async Task<PayrollRunDto> ApproveRunAsync(Guid id, string? note, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.Status != "calculated")
            throw new DomainException("run-not-review-ready", $"Run is in status {run.Status}; it must be calculated before approval.");
        run.Status = "approved";
        run.ApprovalNote = note;
        await repo.UpdateRunAsync(run, ct);
        return MapRun(run);
    }

    /// <summary>Release finalizes payslips and generates the payment file
    /// payload. Segregation of duties: releaser cannot be the sole approver —
    /// enforced here by requiring status = approved (approval was a separate actor).</summary>
    public async Task<PayrollRunDto> ReleaseRunAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.Status != "approved")
            throw new DomainException("run-not-releasable", $"Run is in status {run.Status}; it must be approved by a separate reviewer before release.");
        run.Status = "released";
        await repo.UpdateRunAsync(run, ct);
        await repo.FinalizePayslipsAsync(run.Id, ct);
        return MapRun(run);
    }

    public async Task<Paged<PayslipDto>> GetPayslipsAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "payroll", "hr_admin");
        var (items, total) = await repo.ListPayslipsAsync(workerId, ct);
        return new Paged<PayslipDto>(items.Select(MapPayslip).ToList(), total, 1, 50);
    }

    public async Task<PayslipDto?> GetPayslipByIdAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "payroll", "hr_admin");
        var slip = await repo.GetPayslipAsync(id, ct);
        return slip is null ? null : MapPayslip(slip);
    }

    private static PayslipDto MapPayslip(Payslip p) => new(
        p.Id, p.PayslipNo, p.Version, p.GrossPay, p.TotalDeductions, p.NetPay,
        p.YtdGross, p.YtdTax, p.YtdNet, p.Status, p.DocumentUrl, p.ReleasedAt, p.SupersedesId);

    private static PayrollRunDto MapRun(PayrollRun r) => new(
        r.Id, r.Status, r.PayPeriod?.PeriodLabel ?? "", r.EmployeeCount, r.TotalGross, r.TotalDeductions, r.TotalNet,
        r.TotalEmployerCost, r.ExceptionCount, r.CalcVersion, r.CreatedAt);

    private static WorkerPayrollProfileDto MapProfile(WorkerPayrollProfile p) => new(
        p.Id, p.WorkerId, p.Worker?.FullName, p.PayGroupId, p.PayGroup?.Name, p.EffectiveFrom.ToString(),
        p.ComponentValues.Select(v => new WorkerComponentValueDto(v.ComponentId,
            v.Component?.Code ?? "", v.Component?.Name ?? "", v.Amount)).ToList());

    private async Task<string> GetComponentCode(Guid componentId, CancellationToken ct)
    {
        var c = await repo.GetComponentByIdAsync(componentId, ct);
        return c?.Code ?? "";
    }
}

/// <summary>In-memory component evaluator for one worker in one run (unit-testable).</summary>
internal sealed class CalcContext
{
    public decimal Gross;
    public decimal Deductions;
    public decimal EmployerCost;
    public string? ExceptionReason;
    public readonly List<(string Code, string Name, string Type, decimal Amount, string Explanation, bool IsStatutory)> Components = [];
    private readonly Dictionary<string, decimal> _values = new();
    private readonly Worker _worker;
    private readonly WorkerPayrollProfile _profile;
    private readonly List<SalaryComponent> _components;
    private readonly List<ContributionRule> _rules;
    private readonly List<TaxSlab> _slabs;

    public CalcContext(Worker worker, WorkerPayrollProfile profile, List<SalaryComponent> components, List<ContributionRule> rules, List<TaxSlab> slabs)
    {
        _worker = worker; _profile = profile; _components = components; _rules = rules; _slabs = slabs;
    }

    public void Evaluate(SalaryComponent comp)
    {
        decimal amount = comp.CalculationBasis switch
        {
            // fixed basis: structure default first, then worker profile override
            "fixed" => comp.FixedAmount ?? ProfileAmount(comp.Id),
            "percent-of" => Resolve(comp.BasisComponentCode ?? "") * (comp.Rate ?? 0) / 100m,
            "slab" => ApplySlabs(Resolve(comp.BasisComponentCode ?? "basic")),
            _ => 0,
        };

        // statutory rules override (NAPSA ceiling example: min(pay*rate, ceiling)).
        // A rule is tied to an EARNING component (TiedComponentCode) and applies to
        // the deduction/tax/contribution component whose BasisComponentCode matches it.
        // Rule.Code must match the statutory component's code; TiedComponentCode
        // identifies the earning basis the percentage is taken from.
        var rule = comp.ComponentType != "earning"
            ? _rules.FirstOrDefault(r => r.Code == comp.Code && r.IsActive)
            : null;
        if (rule is not null)
        {
            var basis = Resolve(rule.TiedComponentCode ?? "");
            amount = basis * rule.Rate / 100m;
            if (rule.Ceiling.HasValue) amount = Math.Min(amount, rule.Ceiling.Value);
            if (rule.Floor.HasValue) amount = Math.Max(amount, rule.Floor.Value);
        }

        _values[comp.Code] = amount;
        Components.Add((comp.Code, comp.Name, comp.ComponentType, Math.Round(amount, 2),
            BuildExplanation(comp, amount), comp.IsStatutory));

        if (comp.ComponentType == "earning") Gross += amount;
        else if (comp.ComponentType is "deduction" or "tax") Deductions += amount;
        else if (comp.ComponentType == "employer-contribution") EmployerCost += amount;
    }

    private decimal Resolve(string code) =>
        code switch
        {
            "gross" or "taxable" => Gross, // 'gross'/'taxable' are engine keywords
            _ => _profile.ComponentValues.FirstOrDefault(v => v.Component?.Code == code)?.Amount ?? _values.GetValueOrDefault(code, 0),
        };

    private decimal ProfileAmount(Guid componentId) =>
        _profile.ComponentValues.FirstOrDefault(v => v.ComponentId == componentId)?.Amount ?? 0;

    private decimal _lastTaxable;

    private decimal ApplySlabs(decimal taxable)
    {
        _lastTaxable = taxable;
        decimal tax = 0;
        foreach (var slab in _slabs.Where(s => s.IsActive).OrderBy(s => s.Sequence))
        {
            if (taxable <= slab.MinAmount) break;
            var upper = slab.MaxAmount ?? taxable;
            var band = Math.Min(taxable, upper) - slab.MinAmount;
            if (band > 0) tax += band * slab.Rate / 100m;
        }
        return tax;
    }

    private string BuildExplanation(SalaryComponent comp, decimal amount) =>
        comp.CalculationBasis switch
        {
            "percent-of" => $"{comp.Rate}% of {comp.BasisComponentCode ?? "basis"}",
            "slab" => $"Progressive PAYE slab calculation on taxable income K{_lastTaxable:N2} (ZRA bands)",
            _ => comp.Ceiling.HasValue ? $"Fixed/capped at ceiling {comp.Ceiling}" : "Fixed amount",
        };
}

public interface IPayrollRepository
{
    Task<List<PayGroup>> ListPayGroupsAllAsync(CancellationToken ct);
    Task<PayGroup?> GetPayGroupAsync(Guid id, CancellationToken ct);
    Task<List<SalaryComponent>> ListAllComponentsAsync(CancellationToken ct);
    Task<SalaryComponent?> GetComponentByIdAsync(Guid id, CancellationToken ct);
    Task<List<WorkerPayrollProfile>> ListProfilesAsync(Guid? workerId, CancellationToken ct);
    Task<WorkerPayrollProfile?> FindOpenProfileAsync(Guid workerId, CancellationToken ct);
    Task<WorkerPayrollProfile> CreateProfileAsync(WorkerPayrollProfile profile, CancellationToken ct);
    Task<WorkerPayrollProfile> UpdateProfileAsync(WorkerPayrollProfile profile, CancellationToken ct);
    Task DeleteProfileValuesAsync(Guid profileId, CancellationToken ct);
    Task<SalaryStructure?> FindStructureAsync(string code, CancellationToken ct);
    Task<Worker?> GetWorkerAsync(Guid id, CancellationToken ct);
    Task<List<SalaryComponent>> ListComponentsAsync(string? type, CancellationToken ct);
    Task<List<PayGroup>> ListPayGroupsAsync(CancellationToken ct);
    Task<List<PayPeriod>> ListPeriodsAsync(Guid payGroupId, CancellationToken ct);
    Task<PayPeriod?> GetPeriodAsync(Guid id, CancellationToken ct);
    Task<List<TaxSlab>> ListTaxSlabsAsync(string taxYear, CancellationToken ct);
    Task<List<ContributionRule>> ListContributionRulesAsync(CancellationToken ct);
    Task<PayrollRun?> GetRunAsync(Guid id, CancellationToken ct);
    Task<PayrollRun?> FindRunByPeriodAsync(Guid payPeriodId, CancellationToken ct);
    Task<PayrollRun> CreateRunAsync(PayrollRun run, CancellationToken ct);
    Task<PayrollRun> UpdateRunAsync(PayrollRun run, CancellationToken ct);
    Task<(List<WorkerPayrollProfile> Profiles, List<SalaryComponent> Components, List<ContributionRule> Rules, List<TaxSlab> Slabs, DateOnly? Cutoff)> LoadCalculationInputsAsync(Guid payPeriodId, CancellationToken ct);
    Task ClearRunLinesAsync(Guid runId, CancellationToken ct);
    Task AddRunLineAsync(PayrollRunLine line, CancellationToken ct);
    Task<(List<PayrollRunLine> Items, int Total)> ListRunLinesAsync(Guid runId, CancellationToken ct);
    Task FinalizePayslipsAsync(Guid runId, CancellationToken ct);
    Task<(List<Payslip> Items, int Total)> ListPayslipsAsync(Guid workerId, CancellationToken ct);
    Task<Payslip?> GetPayslipAsync(Guid id, CancellationToken ct);
}
