namespace Mightyfin.Erp.Hrm.Application;

public sealed record SecurityControlDto(string Key, string Name, string Status, string Detail,
    DateTimeOffset? LastVerifiedAt, DateTimeOffset? ExpiresAt, string? EvidenceReference);
public sealed record RoleCapabilityDto(string Capability, string Description, string[] Roles,
    string DataScope, bool Sensitive, string Control);
public sealed record PrivilegedActionDto(Guid Id, string ActorSubjectId, string[] ActorRoles,
    string Method, string Path, string Outcome, int StatusCode, string RequestId, DateTimeOffset CreatedAt);
public sealed record EntityAuditDto(Guid Id, string EntityType, string EntityId, string Action,
    string ActorSubjectId, string? CorrelationId, string? BeforeJson, string? AfterJson,
    DateTimeOffset CreatedAt);
public sealed record RetentionRuleDto(string RecordType, int RetentionMonths, string LegalBasis,
    string Disposition, bool LegalHoldOverrides);
public sealed record ComplianceEvidenceDto(Guid Id, string ControlKey, string Status,
    string EvidenceReference, string? Notes, DateTimeOffset ExecutedAt, DateTimeOffset? ExpiresAt,
    string ExecutedBySubjectId);
public sealed record LegalHoldDto(Guid Id, string Reference, string Scope, string Reason, string Status,
    DateTimeOffset PlacedAt, string PlacedBySubjectId, DateTimeOffset? ReleasedAt,
    string? ReleasedBySubjectId, string? ReleaseReason);
public sealed record SecurityDashboardDto(string TenantId, List<SecurityControlDto> Controls,
    List<RoleCapabilityDto> RoleMatrix, List<PrivilegedActionDto> PrivilegedActions,
    List<EntityAuditDto> EntityAudit, List<RetentionRuleDto> RetentionRules,
    List<ComplianceEvidenceDto> Evidence, List<LegalHoldDto> LegalHolds,
    int OpenFindings, int ActiveLegalHolds);
public sealed record ComplianceEvidenceRequest(string ControlKey, string Status,
    string EvidenceReference, string? Notes, DateTimeOffset ExecutedAt, DateTimeOffset? ExpiresAt);
public sealed record LegalHoldRequest(string Reference, string Scope, string Reason);
public sealed record LegalHoldReleaseRequest(string Reason);

public interface ISecurityComplianceService
{
    Task<SecurityDashboardDto> GetDashboardAsync(string? actor, string? outcome, CancellationToken ct);
    Task<ComplianceEvidenceDto> RecordEvidenceAsync(ComplianceEvidenceRequest request, string actor, CancellationToken ct);
    Task<LegalHoldDto> PlaceLegalHoldAsync(LegalHoldRequest request, string actor, CancellationToken ct);
    Task<LegalHoldDto> ReleaseLegalHoldAsync(Guid id, LegalHoldReleaseRequest request, string actor, CancellationToken ct);
    Task<string> ExportAuditAsync(CancellationToken ct);
}
