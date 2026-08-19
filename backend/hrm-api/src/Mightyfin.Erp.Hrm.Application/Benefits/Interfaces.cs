using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Benefits;

/// <summary>M41 Gap 6b: flexible benefit claims — repository contract.</summary>
public interface IBenefitRepository
{
    // Benefit types
    Task<List<BenefitType>> ListBenefitTypesAsync(CancellationToken ct);
    Task<BenefitType?> GetBenefitTypeByCodeAsync(string code, CancellationToken ct);
    Task<BenefitType> CreateBenefitTypeAsync(BenefitType type, CancellationToken ct);
    Task<BenefitType> UpdateBenefitTypeAsync(BenefitType type, CancellationToken ct);
    Task DeleteBenefitTypeAsync(Guid id, CancellationToken ct);

    // Worker allowances
    Task<List<WorkerBenefitAllowance>> ListAllowancesAsync(Guid? workerId, CancellationToken ct);
    Task<WorkerBenefitAllowance?> GetAllowanceAsync(Guid workerId, Guid benefitTypeId, int year, CancellationToken ct);
    Task<WorkerBenefitAllowance> SetAllowanceAsync(WorkerBenefitAllowance allowance, CancellationToken ct);

    // Claims
    Task<(List<BenefitClaim> Items, int Total)> ListClaimsAsync(Guid? workerId, string? status, int page, int pageSize, CancellationToken ct);
    Task<BenefitClaim?> GetClaimAsync(Guid id, CancellationToken ct);
    Task<BenefitClaim> CreateClaimAsync(BenefitClaim claim, CancellationToken ct);
    Task UpdateClaimAsync(BenefitClaim claim, CancellationToken ct);
    Task<decimal> SumApprovedAsync(Guid workerId, Guid benefitTypeId, int year, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>M41 Gap 6b: flexible benefit claims — service contract.</summary>
public interface IBenefitService
{
    Task<List<BenefitTypeDto>> ListBenefitTypesAsync(CancellationToken ct);
    Task<BenefitTypeDto> CreateBenefitTypeAsync(BenefitTypeCreateRequest request, CancellationToken ct);
    Task<BenefitTypeDto> UpdateBenefitTypeAsync(Guid id, BenefitTypeUpdateRequest request, CancellationToken ct);
    Task DeleteBenefitTypeAsync(Guid id, CancellationToken ct);

    Task<List<BenefitAllowanceDto>> ListAllowancesAsync(Guid? workerId, CancellationToken ct);
    Task SetAllowanceAsync(AllowanceSetRequest request, CancellationToken ct);

    Task<(List<BenefitClaimDto> Items, int Total)> ListClaimsAsync(Guid? workerId, string? status, int page, int pageSize, CancellationToken ct);
    Task<BenefitClaimDto> CreateClaimAsync(BenefitClaimCreateRequest request, CancellationToken ct);
    Task<BenefitClaimDto> DecideClaimAsync(Guid id, ClaimDecideRequest request, CancellationToken ct);
    Task<BenefitClaimDto> PayClaimAsync(Guid id, CancellationToken ct);
}

public sealed record BenefitTypeCreateRequest(string Code, string Name, string? Description, decimal AnnualCap, bool RequiresEvidence);
public sealed record BenefitTypeUpdateRequest(string Code, string Name, string? Description, decimal AnnualCap, bool RequiresEvidence, bool IsActive);
public sealed record AllowanceSetRequest(Guid WorkerId, string BenefitTypeCode, decimal AnnualAmount, int Year);
public sealed record BenefitClaimCreateRequest(Guid WorkerId, string BenefitTypeCode, decimal AmountClaimed, string Currency, string? Note, bool EvidenceAttached);
public sealed record ClaimDecideRequest(string Action, string? Reason, decimal? ApprovedAmount);

public sealed record BenefitTypeDto(
    Guid Id, string Code, string Name, string? Description, decimal AnnualCap,
    bool RequiresEvidence, bool IsActive);
public sealed record BenefitAllowanceDto(
    Guid Id, Guid WorkerId, string WorkerName, string? EmployeeNo, string BenefitTypeCode,
    string BenefitTypeName, decimal AnnualAmount, int Year);
public sealed record BenefitClaimDto(
    Guid Id, Guid WorkerId, string WorkerName, string? EmployeeNo, Guid BenefitTypeId,
    string BenefitTypeCode, string BenefitTypeName, decimal AmountClaimed, string Currency,
    string? Note, bool EvidenceAttached, string Status, string? DecisionReason,
    decimal? ApprovedAmount,     string? CreatedBySubjectId, string? DecidedBySubjectId,
    DateTimeOffset? DecidedAt, string? PaidBySubjectId, DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt);
