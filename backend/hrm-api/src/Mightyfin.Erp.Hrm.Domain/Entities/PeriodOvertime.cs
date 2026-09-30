namespace Mightyfin.Erp.Hrm.Domain.Entities;

public sealed class PeriodOvertime : Entity
{
    public Guid PayPeriodId { get; set; }
    public Guid WorkerId { get; set; }
    public decimal Amount { get; set; }
    public string Note { get; set; } = "";
}
