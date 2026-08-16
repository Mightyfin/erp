namespace Mightyfin.Erp.Hrm.Application;

public sealed record IntegrationContractDto(
    string Key, string Name, string Direction, string ContractVersion,
    string Transport, string Owner, string RetryStrategy,
    string ReconciliationProcess, string Status, string? Detail);

public sealed record IntegrationOperationDto(
    Guid Id, string PublicId, string IntegrationKey, string OperationType,
    string ContractVersion, string IdempotencyKey, string Status,
    Guid? SourceId, string? SourceReference, string ContentType, int AttemptCount,
    DateTimeOffset? LastAttemptAt, DateTimeOffset? NextAttemptAt,
    string? LastError, string? ExternalReference,
    string? ReconciliationOutcome, string? ReconciliationNote,
    DateTimeOffset? ReconciledAt, string CreatedBySubjectId,
    string? ReconciledBySubjectId, DateTimeOffset CreatedAt);

public sealed record IntegrationDashboardDto(
    List<IntegrationContractDto> Contracts,
    List<IntegrationOperationDto> Operations,
    int Ready, int Delivered, int Failed, int Reconciled,
    int ActiveWorkers, int LinkedWorkers, int UnlinkedWorkers,
    string DocumentStorageMode);

public sealed record IntegrationSourceRequest(Guid SourceId);
public sealed record StatutoryHandoffRequest(string ExportType, Guid PeriodId);
public sealed record IdentitySyncRequest(string Mode = "delta");
public sealed record IntegrationReconciliationRequest(
    string Outcome, string ExternalReference, string? Note = null);

public interface IIntegrationOperationsService
{
    Task<IntegrationDashboardDto> GetDashboardAsync(string? integrationKey, string? status, CancellationToken ct);
    Task<IntegrationOperationDto> CreateFinancePostingAsync(Guid runId, string actorSubjectId, CancellationToken ct);
    Task<IntegrationOperationDto> CreatePaymentHandoffAsync(Guid runId, string actorSubjectId, CancellationToken ct);
    Task<IntegrationOperationDto> CreateStatutoryHandoffAsync(StatutoryHandoffRequest request, string actorSubjectId, CancellationToken ct);
    Task<IntegrationOperationDto> CreateIdentitySyncAsync(IdentitySyncRequest request, string actorSubjectId, CancellationToken ct);
    Task<IntegrationOperationDto> RetryAsync(Guid operationId, string actorSubjectId, CancellationToken ct);
    Task<IntegrationOperationDto> ReconcileAsync(Guid operationId, IntegrationReconciliationRequest request, string actorSubjectId, CancellationToken ct);
    Task<(string Payload, string ContentType, string FileName)> DownloadAsync(Guid operationId, CancellationToken ct);
}
