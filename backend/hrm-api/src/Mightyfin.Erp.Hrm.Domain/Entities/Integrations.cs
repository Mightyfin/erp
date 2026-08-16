namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>
/// Durable hand-off record for an HRM boundary. The payload is the immutable
/// contract snapshot sent to finance, banking, statutory, storage, or identity
/// services; status and reconciliation fields form the operational audit trail.
/// </summary>
public sealed class IntegrationOperation : Entity
{
    public string PublicId { get; set; } = null!;
    public string IntegrationKey { get; set; } = null!;
    public string OperationType { get; set; } = null!;
    public string ContractVersion { get; set; } = "1.0";
    public string IdempotencyKey { get; set; } = null!;
    public string Status { get; set; } = "ready"; // ready | delivered | failed | reconciled | rejected
    public Guid? SourceId { get; set; }
    public string? SourceReference { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string ContentType { get; set; } = "application/json";
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public string? ExternalReference { get; set; }
    public string? ReconciliationOutcome { get; set; }
    public string? ReconciliationNote { get; set; }
    public DateTimeOffset? ReconciledAt { get; set; }
    public string CreatedBySubjectId { get; set; } = null!;
    public string? ReconciledBySubjectId { get; set; }
}
