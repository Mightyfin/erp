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
    // M14 identity link: resolve the worker record bound to a Keycloak subject id.
    Task<Worker?> FindBySubjectIdAsync(string subjectId, CancellationToken ct);
    Task<Worker> CreateAsync(Worker worker, CancellationToken ct);
    Task<Worker> UpdateAsync(Worker worker, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task AddEmergencyContactsAsync(IEnumerable<EmergencyContact> contacts, CancellationToken ct);
    Task AddBankDetailsAsync(IEnumerable<WorkerBankDetail> details, CancellationToken ct);
    Task<bool> ExistsAsync(string employeeNo, CancellationToken ct);
    // M31 import/export: natural-key lookup used by Update-mode matching
    // (employee number first, then NRC, then NAPSA number).
    Task<Worker?> FindByNaturalKeyAsync(string employeeNo, string? nrc, string? napsaNumber, CancellationToken ct);
    // M54 import re-locate: rows without any natural keys fall back to their
    // (required) work email to find the just-created record.
    Task<Worker?> FindByEmailAsync(string email, CancellationToken ct);
    Task ArchiveAsync(Guid id, CancellationToken ct);
    Task<(List<Assignment> Items, int Total)> ListAssignmentsAsync(Guid workerId, CancellationToken ct);
    Task<Assignment> CreateAssignmentAsync(Assignment assignment, CancellationToken ct);
    Task<(List<Movement> Items, int Total)> ListMovementsAsync(Guid workerId, CancellationToken ct);
    Task<Movement> CreateMovementAsync(Movement movement, CancellationToken ct);
    Task<Movement?> GetMovementAsync(Guid id, CancellationToken ct);
    Task ExecuteMovementAsync(Movement movement, CancellationToken ct);

    // M2 lifecycle extras
    Task<List<Assignment>> ListAllAssignmentsAsync(CancellationToken ct);
    Task<Assignment> UpdateAssignmentAsync(Assignment assignment, CancellationToken ct);
    Task<List<LegalEntity>> ListAllLegalEntitiesAsync(CancellationToken ct);
    Task<List<OrgUnit>> ListAllOrgUnitsAsync(CancellationToken ct);
    Task<List<WorkLocation>> ListAllLocationsAsync(CancellationToken ct);
    Task<List<Worker>> ListAllWorkersAsync(Guid? orgUnitId, CancellationToken ct);
    Task<List<Worker>> ListAllWorkersWithDetailsAsync(string? status, CancellationToken ct);

    // Emergency contacts
    Task<EmergencyContact?> GetEmergencyContactAsync(Guid id, CancellationToken ct);
    Task<EmergencyContact> AddEmergencyContactAsync(EmergencyContact contact, CancellationToken ct);
    Task UpdateEmergencyContactAsync(EmergencyContact contact, CancellationToken ct);
    Task DeleteEmergencyContactAsync(Guid id, CancellationToken ct);

    // Bank details
    Task<WorkerBankDetail?> GetBankDetailAsync(Guid id, CancellationToken ct);
    Task<WorkerBankDetail> AddBankDetailAsync(WorkerBankDetail detail, CancellationToken ct);
    Task UpdateBankDetailAsync(WorkerBankDetail detail, CancellationToken ct);
    Task DeleteBankDetailAsync(Guid id, CancellationToken ct);

    // M33 history child records (education, external & internal work history)
    Task<List<WorkerEducation>> ListEducationAsync(Guid workerId, CancellationToken ct);
    Task<WorkerEducation> AddEducationAsync(WorkerEducation education, CancellationToken ct);
    Task UpdateEducationAsync(WorkerEducation education, CancellationToken ct);
    Task DeleteEducationAsync(Guid id, CancellationToken ct);
    Task<WorkerEducation?> GetByIdEducationAsync(Guid id, CancellationToken ct);
    Task<List<ExternalWorkHistory>> ListExternalWorkHistoryAsync(Guid workerId, CancellationToken ct);
    Task<ExternalWorkHistory> AddExternalWorkHistoryAsync(ExternalWorkHistory record, CancellationToken ct);
    Task UpdateExternalWorkHistoryAsync(ExternalWorkHistory record, CancellationToken ct);
    Task DeleteExternalWorkHistoryAsync(Guid id, CancellationToken ct);
    Task<ExternalWorkHistory?> GetByIdExternalWorkHistoryAsync(Guid id, CancellationToken ct);
    Task<List<InternalWorkHistory>> ListInternalWorkHistoryAsync(Guid workerId, CancellationToken ct);
    Task<InternalWorkHistory> AddInternalWorkHistoryAsync(InternalWorkHistory record, CancellationToken ct);
    Task UpdateInternalWorkHistoryAsync(InternalWorkHistory record, CancellationToken ct);
    Task DeleteInternalWorkHistoryAsync(Guid id, CancellationToken ct);
    Task<InternalWorkHistory?> GetByIdInternalWorkHistoryAsync(Guid id, CancellationToken ct);
}

/// <summary>Authorization: role requirement checks against the current principal.</summary>
public interface IAuthzService
{
    string CurrentSubjectId { get; }
    void RequireAnyRole(params string[] roles);
    // M25: true when the current principal holds any of the given roles — used
    // by self-service ownership guards (an HR role can read broadly; an
    // employee can only ever read their own records).
    bool IsRole(params string[] roles);
    bool CanAccessSensitive(string category); // payroll, medical, restricted
}

/// <summary>Id/reference generation (employee numbers, case references, verification codes).</summary>
public interface IIdProvider
{
    string NewCorrelationId();
}
