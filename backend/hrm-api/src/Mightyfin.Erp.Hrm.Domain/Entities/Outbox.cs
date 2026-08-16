namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>
/// Transactional hand-off from the HRM domain to the shared platform event bus.
/// Rows are created in the same database transaction as the business mutation;
/// a separate publisher owns delivery and retry state.
/// </summary>
public sealed class OutboxMessage : Entity
{
    public string PublicId { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string EventVersion { get; set; } = "1";
    public string Environment { get; set; } = "production";
    public string SubjectId { get; set; } = null!;
    public string CorrelationId { get; set; } = null!;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "pending"; // pending | publishing | published | failed | fallback-delivered
    public int PublishAttempts { get; set; }
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public string? LastTransport { get; set; }
    public string? LastError { get; set; }
}
