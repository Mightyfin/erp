namespace Mightyfin.Erp.Hrm.Domain.Entities;
/// <summary>M49: per-tenant first-time setup state. One row per tenant. An absent
/// row means "legacy tenant — setup was completed before this feature existed"
/// so existing organisations are never force-gated into the wizard. A
/// status=complete row means the wizard finished; status=pending means the
/// operator should see the setup wizard overlay.</summary>
public class SetupState : Entity
{
    /// <summary>pending | complete | resetting.</summary>
    public string Status { get; set; } = "pending";
    public DateTimeOffset? CompletedAt { get; set; }
}
/// <summary>M49: per-tenant completion record for each wizard step. A step is
/// considered complete when a row exists with Completed=true; rows are never
/// deleted so the audit trail shows when each part of the organisation was
/// configured (reset wipes the tenant's rows and re-seeds an empty state).</summary>
public class SetupStepRecord : Entity
{
    public string StepKey { get; set; } = null!;
    public bool Completed { get; set; }
    public string? DataJson { get; set; }
}
