namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>A controlled worker-master mutation. The submitted rows, validation
/// result, and recovery snapshot remain together so imports and bulk updates
/// can be explained and reversed as one operation.</summary>
public sealed class MasterDataBatch : Entity
{
    public string BatchType { get; set; } = null!; // worker-import | bulk-update | reactivation
    public string? FileName { get; set; }
    public string Status { get; set; } = "previewed"; // previewed | applied | rolled-back | rejected
    public DateOnly EffectiveDate { get; set; }
    public int RowCount { get; set; }
    public int ReadyCount { get; set; }
    public int UnchangedCount { get; set; }
    public int ErrorCount { get; set; }
    public string PayloadJson { get; set; } = "[]";
    public string SummaryJson { get; set; } = "[]";
    public string SnapshotJson { get; set; } = "[]";
    public string ErrorsJson { get; set; } = "[]";
    public string RequestedBySubjectId { get; set; } = null!;
    public string? AppliedBySubjectId { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public DateTimeOffset? RolledBackAt { get; set; }
}
