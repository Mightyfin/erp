namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Base entity for all HRM records. Every table is tenant-scoped and
/// auditable, matching the ERP architecture position (tenant_id on every table
/// from the first migration; append-only audit trail).</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string TenantId { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsArchived { get; set; }
}

/// <summary>Records whose business truth is time-sensitive: the current record
/// at any date is found where effective_from &lt;= date and (effective_to is null or effective_to &gt;= date).</summary>
public interface IEffectiveDated
{
    DateOnly EffectiveFrom { get; set; }
    DateOnly? EffectiveTo { get; set; }
}

/// <summary>Soft-versioned configuration values: a new version supersedes the old
/// but the previous value remains queryable (e.g. salary components, tax slabs).</summary>
public interface IVersioned
{
    int Version { get; set; }
    bool IsActive { get; set; }
}
