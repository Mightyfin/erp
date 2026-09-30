namespace Mightyfin.Erp.Hrm.Application.Benefits;
public sealed record PeriodBenefitRow(Guid WorkerId, string EmployeeNo, string WorkerName, Guid? LocationId, string Branch, Guid? OrgUnitId, string Department, decimal? Amount, string Note, decimal RecurringMonthlyAmount, bool ClaimManaged);
public sealed record PeriodBenefitBatch(Guid PayPeriodId, string PeriodLabel, string Currency, Guid BenefitTypeId, string BenefitName, bool Editable, string? LockReason, List<PeriodBenefitRow> Rows);
public sealed record PeriodBenefitInput(Guid WorkerId, decimal? Amount, string? Note);
public sealed record PeriodBenefitSave(List<PeriodBenefitInput> Rows);
public interface IPeriodBenefitService
{
    Task<PeriodBenefitBatch> GetAsync(Guid periodId, Guid typeId, CancellationToken ct);
    Task<Mightyfin.Erp.Hrm.Domain.Entities.BenefitClaim> SubmitClaimAsync(Guid typeId, BenefitClaimCreateRequest request, CancellationToken ct);
    Task<Mightyfin.Erp.Hrm.Domain.Entities.BenefitClaim> UpdateClaimAsync(Guid id, BenefitClaimUpdateRequest request, CancellationToken ct);
    Task DeleteClaimAsync(Guid id, CancellationToken ct);
    Task SaveAsync(Guid periodId, Guid typeId, PeriodBenefitSave input, CancellationToken ct);
}
