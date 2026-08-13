using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Workers;

/// <summary>M2: worker lifecycle surface — employment assignments, effective-dated
/// movements with impact previews, emergency contacts, bank details, and
/// onboarding/offboarding helpers.</summary>
public interface IWorkerLifecycleService
{
    // Assignments
    Task<List<AssignmentDto>> ListAssignmentsAsync(Guid workerId, CancellationToken ct);
    Task<AssignmentDto> CreateAssignmentAsync(Guid workerId, AssignmentCreateRequest request, CancellationToken ct);
    Task<AssignmentDto> UpdateAssignmentAsync(Guid workerId, Guid assignmentId, AssignmentUpdateRequest request, CancellationToken ct);
    Task EndAssignmentAsync(Guid workerId, Guid assignmentId, CancellationToken ct);

    // Movements (future-dated, pending approval until executed)
    Task<List<MovementDetailDto>> ListMovementsAsync(Guid workerId, CancellationToken ct);
    Task<MovementDetailDto> CreateMovementAsync(Guid workerId, MovementCreateRequest request, CancellationToken ct);
    Task<MovementDetailDto> GetMovementAsync(Guid workerId, Guid movementId, CancellationToken ct);
    Task<List<MovementImpactDto>> PreviewMovementAsync(Guid workerId, Guid movementId, CancellationToken ct);
    Task SubmitMovementAsync(Guid workerId, Guid movementId, CancellationToken ct);
    Task ApproveMovementAsync(Guid workerId, Guid movementId, CancellationToken ct);
    Task RejectMovementAsync(Guid workerId, Guid movementId, CancellationToken ct);
    Task CancelMovementAsync(Guid workerId, Guid movementId, CancellationToken ct);

    // Emergency contacts & bank details
    Task<List<EmergencyContactDto>> ListEmergencyContactsAsync(Guid workerId, CancellationToken ct);
    Task<EmergencyContactDto> AddEmergencyContactAsync(Guid workerId, EmergencyContactRequest request, CancellationToken ct);
    Task<EmergencyContactDto> UpdateEmergencyContactAsync(Guid workerId, Guid contactId, EmergencyContactRequest request, CancellationToken ct);
    Task DeleteEmergencyContactAsync(Guid workerId, Guid contactId, CancellationToken ct);
    Task<List<WorkerBankDetailDto>> ListBankDetailsAsync(Guid workerId, CancellationToken ct);
    Task<WorkerBankDetailDto> AddBankDetailAsync(Guid workerId, BankDetailRequest request, CancellationToken ct);
    Task<WorkerBankDetailDto> UpdateBankDetailAsync(Guid workerId, Guid bankId, BankDetailRequest request, CancellationToken ct);
    Task DeleteBankDetailAsync(Guid workerId, Guid bankId, CancellationToken ct);

    // Onboarding / offboarding
    Task<OnboardingPlanDto> GetOnboardingAsync(Guid workerId, CancellationToken ct);
    Task<OffboardingResultDto> OffboardAsync(Guid workerId, CancellationToken ct);
}
