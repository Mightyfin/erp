using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application;

/// <summary>Domain-level failure surfaced as a structured API error.</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Repository contract for workers and their related records. EF Core
/// implementation lives in Infrastructure.</summary>
public interface IWorkerRepository
{
    Task<(List<Worker> Items, int Total)> ListAsync(WorkerListFilters filters, CancellationToken ct);
    Task<Worker?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Worker> CreateAsync(Worker worker, CancellationToken ct);
    Task<Worker> UpdateAsync(Worker worker, CancellationToken ct);
    Task ArchiveAsync(Guid id, CancellationToken ct);
    Task<(List<Assignment> Items, int Total)> ListAssignmentsAsync(Guid workerId, CancellationToken ct);
    Task<Assignment> CreateAssignmentAsync(Assignment assignment, CancellationToken ct);
    Task<(List<Movement> Items, int Total)> ListMovementsAsync(Guid workerId, CancellationToken ct);
    Task<Movement> CreateMovementAsync(Movement movement, CancellationToken ct);
    Task<Movement?> GetMovementAsync(Guid id, CancellationToken ct);
    Task ExecuteMovementAsync(Movement movement, CancellationToken ct);
}

/// <summary>Authorization: role requirement checks against the current principal.</summary>
public interface IAuthzService
{
    void RequireAnyRole(params string[] roles);
    bool CanAccessSensitive(string category); // payroll, medical, restricted
}

/// <summary>Id/reference generation (employee numbers, case references, verification codes).</summary>
public interface IIdProvider
{
    string NewCorrelationId();
}
