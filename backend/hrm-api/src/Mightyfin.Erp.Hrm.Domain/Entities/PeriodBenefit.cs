namespace Mightyfin.Erp.Hrm.Domain.Entities;
public sealed class PeriodBenefit : Entity
{
    public Guid PayPeriodId { get; set; }
    public Guid WorkerId { get; set; }
    public Guid BenefitTypeId { get; set; }
    public BenefitType? BenefitType { get; set; }
    public decimal Amount { get; set; }
    public string Note { get; set; } = "";
}
