namespace Mightyfin.Erp.Hrm.Application;

public sealed record GoLiveGateDto(string Key, string Category, string Name, string Status,
    string Detail, string? EvidenceReference, DateTimeOffset? VerifiedAt);
public sealed record GoLiveSignoffDto(Guid Id, string RoleKey, string RoleName, string Decision,
    string? Notes, string ActorSubjectId, DateTimeOffset SignedAt);
public sealed record GoLiveReadinessDto(string Decision, bool CanGoLive, DateTimeOffset EvaluatedAt,
    int PassedGates, int TotalGates, List<string> Blockers, List<GoLiveGateDto> Gates,
    List<GoLiveSignoffDto> Signoffs);
public sealed record GoLiveEvidenceRequest(string ControlKey, string Status,
    string EvidenceReference, string? Notes, DateTimeOffset ExecutedAt, DateTimeOffset? ExpiresAt);
public sealed record GoLiveSignoffRequest(string Decision, string? Notes);

public interface IGoLiveReadinessService
{
    Task<GoLiveReadinessDto> GetAsync(CancellationToken ct);
    Task<ComplianceEvidenceDto> RecordEvidenceAsync(GoLiveEvidenceRequest request, string actor, CancellationToken ct);
    Task<GoLiveSignoffDto> RecordSignoffAsync(string roleKey, GoLiveSignoffRequest request, string actor, CancellationToken ct);
}
