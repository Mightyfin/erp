namespace Mightyfin.Erp.Hrm.Application;

/// <summary>M44: The organisational work scope of the current request. Populated
/// by the X-Shell-Location / X-Shell-Entity header middleware (ShellContextMiddleware).
/// LocationId set = the operator is scoped to one branch and must only
/// see/create operational data for that branch. LocationId null = the operator
/// works at legal-entity level and sees the whole organisation. EntityId is
/// informational (every request already belongs to one tenant via the principal).</summary>
public sealed class ShellContext
{
    public Guid? LocationId { get; set; }
    public Guid? EntityId { get; set; }

    /// <summary>True when the operator has deliberately narrowed work to one branch.</summary>
    public bool IsScopedToBranch => LocationId.HasValue;

    public override string ToString() =>
        IsScopedToBranch ? $"branch:{LocationId}" : (EntityId.HasValue ? $"entity:{EntityId}" : "global");
}
