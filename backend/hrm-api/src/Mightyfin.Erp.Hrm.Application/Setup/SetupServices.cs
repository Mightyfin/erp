using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Setup;
// ---------- DTOs ----------
/// <summary>M49: the welcome/dashboard decision the frontend needs on every
/// render. Status=pending means the wizard overlay must be shown; complete
/// means go straight to the dashboard; missing-row means a legacy tenant
/// (setup ran before this feature existed) — never force-gated.</summary>
public record SetupStateDto(string Status, string? ResumeStepKey, IReadOnlyList<string> CompletedSteps,
    IReadOnlyList<string> OptionalSteps, double CompletionPercent);
/// <summary>M49: one entry per wizard step returned alongside SetupStateDto so
/// the wizard can render the step list with labels/descriptions in one call.</summary>
public record SetupStepDto(string Key, string Label, string Description, bool Mandatory, bool Completed, bool Open);

public interface ISetupService
{
    Task<SetupStateDto> GetStateAsync(CancellationToken ct);
    Task<IReadOnlyList<SetupStepDto>> ListStepsAsync(CancellationToken ct);
    Task CompleteStepAsync(string stepKey, string? dataJson, CancellationToken ct);
    Task FinishAsync(CancellationToken ct);
    /// <summary>DESTRUCTIVE: wipes all tenant data so the system behaves like
    /// a brand-new installation. Requires hr_admin + an explicit confirm body.</summary>
    Task ResetAsync(CancellationToken ct);
}
// ---------- Repository contract (infra implements) ----------
/// <summary>M49: setup persistence. Kept out of the application layer so the
/// service stays free of EF Core; all tenant filtering is automatic via the
/// global query filter on SetupState/SetupStepRecord.</summary>
public interface ISetupRepository
{
    Task<SetupState?> GetStateAsync(CancellationToken ct);
    Task<IReadOnlySet<string>> CompletedStepKeysAsync(CancellationToken ct);
    Task CompleteStepAsync(string stepKey, string? dataJson, CancellationToken ct);
    Task FinishAsync(SetupState state, CancellationToken ct);
    Task WipeAllDataAsync(CancellationToken ct);
    Task<SetupState> SeedPendingStateAsync(CancellationToken ct);
}

public sealed class SetupServiceImpl(ISetupRepository repo) : ISetupService
{
    private static readonly IReadOnlySet<string> ResetTableNames = new HashSet<string>
    {
        // people & lifecycle
        "emergency_contacts", "worker_bank_details", "education", "external_work_history",
        "internal_work_history", "worker_documents", "assignments", "movements", "workers",
        // policies & time
        "leave_balance_ledger", "leave_balance_adjustments", "leave_encashments", "leave_requests",
        "leave_types", "leave_accrual_runs", "attendance_records", "attendance_corrections",
        "shift_definitions", "worker_shift_assignments", "attendance_import_batches",
        // workflows & experience
        "workflow_requests", "workflow_decisions", "approval_delegations", "hr_requests",
        "hr_request_messages", "hr_letters", "protected_disclosures",
        // employment structure
        "jobs", "org_units",
        // payroll — runs/lines/payslips first, then configuration
        "payslip_access_logs", "payslips", "payroll_line_components", "payroll_run_lines",
        "payroll_run_events", "payroll_runs", "worker_component_values",
        "worker_payroll_profiles", "salary_structure_items", "salary_structures",
        "salary_components", "benefit_claims", "worker_benefit_allowances", "benefit_types",
        "pay_periods", "pay_groups", "tax_slabs", "contribution_rules",
        // structure (branches/locations go AFTER payroll because runs reference them)
        "work_locations", "work_calendars", "public_holidays",
        // config, compliance and extras
        "master_data_batches", "audit_entries", "retention_rules",
        "capability_configs", "vacancies", "requisition_events", "requisitions",
        "candidate_documents", "candidate_interviews", "candidate_stage_events", "candidates",
        "offers", "preboarding_tasks", "preboarding_cases",
        "relations_case_access", "relations_case_actions", "relations_case_events",
        "relations_evidence", "relations_cases",
        "performance_assessments", "performance_goals", "performance_cycles",
        "offboarding_checklist_items", "offboarding_requests", "exit_interviews",
        // privileges & signoffs
        "privileged_action_events", "compliance_evidence", "go_live_signoffs",
        "legal_holds", "outbox_messages", "integration_operations",
    };

    public async Task<SetupStateDto> GetStateAsync(CancellationToken ct)
    {
        var state = await repo.GetStateAsync(ct);
        if (state is null)
            // Legacy tenant: setup completed before M49 existed. Never gate.
            return new SetupStateDto("complete", null,
                SetupDefinitions.Steps.Select(s => s.Key).ToList(),
                SetupDefinitions.Steps.Where(s => !s.Mandatory).Select(s => s.Key).ToList(), 100);

        var completed = await repo.CompletedStepKeysAsync(ct);
        var open = SetupDefinitions.Steps.FirstOrDefault(s => !completed.Contains(s.Key))?.Key;
        return new SetupStateDto(state.Status,
            state.Status == SetupDefinitions.StatusComplete ? null : open,
            completed.ToList(),
            SetupDefinitions.Steps.Where(s => !s.Mandatory).Select(s => s.Key).ToList(),
            SetupDefinitions.CompletionPercent(completed));
    }

    public async Task<IReadOnlyList<SetupStepDto>> ListStepsAsync(CancellationToken ct)
    {
        var state = await repo.GetStateAsync(ct);
        var completed = state is null
            ? new HashSet<string>(SetupDefinitions.Steps.Select(s => s.Key))
            : await repo.CompletedStepKeysAsync(ct);
        var result = new List<SetupStepDto>(SetupDefinitions.Steps.Count);
        var done = new HashSet<string>();
        foreach (var s in SetupDefinitions.Steps)
        {
            var isComplete = completed.Contains(s.Key);
            if (isComplete) done.Add(s.Key);
            result.Add(new SetupStepDto(s.Key, s.Label, s.Description, s.Mandatory,
                isComplete, !isComplete && SetupDefinitions.MayComplete(s.Key, done)));
            if (isComplete) done.Add(s.Key);
        }
        return result;
    }

    public async Task CompleteStepAsync(string stepKey, string? dataJson, CancellationToken ct)
    {
        var state = await repo.GetStateAsync(ct)
            ?? await repo.SeedPendingStateAsync(ct);
        if (state.Status != SetupDefinitions.StatusPending)
            throw new DomainException("setup-already-finished", "Setup is already complete. Reset the organisation to run the wizard again.");

        var records = await repo.CompletedStepKeysAsync(ct);
        if (!SetupDefinitions.MayComplete(stepKey, records))
            throw new DomainException("setup-step-gated",
                $"Step '{stepKey}' cannot be completed yet — complete the required steps before it first.");
        await repo.CompleteStepAsync(stepKey, dataJson, ct);
    }

    public async Task FinishAsync(CancellationToken ct)
    {
        var state = await repo.GetStateAsync(ct);
        if (state is null) return;
        if (state.Status != SetupDefinitions.StatusPending)
            throw new DomainException("setup-already-finished", "Setup is already complete.");
        // Only the mandatory prefix (incl. payroll confirmation) gates finish;
        // optional steps surface afterwards as the after-onboarding checklist.
        var completed = await repo.CompletedStepKeysAsync(ct);
        var missing = SetupDefinitions.Steps.Where(s => s.Mandatory && !completed.Contains(s.Key)).ToList();
        if (missing.Count != 0)
            throw new DomainException("setup-incomplete",
                $"Cannot finish: mandatory steps are still open ({string.Join(", ", missing.Select(s => s.Key))}).");
        await repo.FinishAsync(state, ct);
    }

    public async Task ResetAsync(CancellationToken ct)
    {
        // Destructive by design: only top-level HR may trigger a wipe and they
        // must POST an explicit confirmation body (the route handler enforces
        // the exact string — a plain POST without confirm is rejected).
        await repo.WipeAllDataAsync(ct);
        await repo.SeedPendingStateAsync(ct);
    }
}
