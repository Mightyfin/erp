namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Append-only request-level evidence for privileged API mutations.
/// This complements entity audit rows by retaining denied and failed attempts.</summary>
public sealed class PrivilegedActionEvent : Entity
{
    public string ActorSubjectId { get; set; } = null!;
    public string ActorRoles { get; set; } = "";
    public string Method { get; set; } = null!;
    public string Path { get; set; } = null!;
    public string Outcome { get; set; } = null!;
    public int StatusCode { get; set; }
    public string RequestId { get; set; } = null!;
    public string? SourceAddressHash { get; set; }
}

/// <summary>Evidence that an operational compliance control was actually run.</summary>
public sealed class ComplianceEvidence : Entity
{
    public string ControlKey { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string EvidenceReference { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string ExecutedBySubjectId { get; set; } = null!;
}

/// <summary>Append-only, role-specific acceptance of the current go-live
/// decision. A later decision supersedes an earlier one without destroying
/// the audit history.</summary>
public sealed class GoLiveSignoff : Entity
{
    public string RoleKey { get; set; } = null!;
    public string Decision { get; set; } = null!; // approved | rejected | withdrawn
    public string? Notes { get; set; }
    public string ActorSubjectId { get; set; } = null!;
    public DateTimeOffset SignedAt { get; set; }
}

/// <summary>A legal hold prevents retention disposal for its declared scope.</summary>
public sealed class LegalHold : Entity
{
    public string Reference { get; set; } = null!;
    public string Scope { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = "active";
    public DateTimeOffset PlacedAt { get; set; }
    public string PlacedBySubjectId { get; set; } = null!;
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? ReleasedBySubjectId { get; set; }
    public string? ReleaseReason { get; set; }
}
