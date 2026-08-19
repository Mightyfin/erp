using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Payroll;

/// <summary>M41 Gap 2: pure proration logic — no EF, no DI, unit-testable.
///
/// Zambian pay convention: a monthly salary buys the whole calendar month,
/// public holidays are PAID days and never reduce pay. Payment days are
/// therefore reduced only by (a) the worker starting or ending mid-month and
/// (b) approved unpaid (or half-pay) leave overlapping the period.
///
/// paymentDays = days in [max(periodStart, startDate)..min(periodEnd, endDate)]
/// minus unpaid-leave days overlapping that window. Proration factor =
/// paymentDays / periodDays and is applied by CalcContext to salary-earned
/// component amounts before statutory floors/ceilings re-apply.</summary>
public static class PaymentDaysCalculator
{
    public static (int WorkingDays, int PaymentDays, string? Note) For(
        PayrollProrationInputs inputs, Worker worker, List<ApprovedUnpaidLeave> unpaidLeaves)
    {
        int periodDays = inputs.PeriodEnd.DayNumber - inputs.PeriodStart.DayNumber + 1;

        // Full-month employment window, clamped to the period.
        DateOnly start = worker.StartDate.HasValue
            ? MaxDate(worker.StartDate.Value, inputs.PeriodStart)
            : inputs.PeriodStart;
        DateOnly end = worker.EndDate.HasValue
            ? MinDate(worker.EndDate.Value, inputs.PeriodEnd)
            : inputs.PeriodEnd;
        if (end < start) return (periodDays, 0, "Not employed during this period");

        int days = end.DayNumber - start.DayNumber + 1;
        var parts = new List<string>();

        // Approved unpaid/half-pay leave overlapping the clamped window.
        int leaveDays = 0;
        foreach (var leave in unpaidLeaves)
        {
            DateOnly ls = MaxDate(leave.StartDate, start);
            DateOnly le = MinDate(leave.EndDate, end);
            if (le >= ls)
            {
                var overlapDays = le.DayNumber - ls.DayNumber + 1;
                // Leave requested in partial days or capped by the worker's own
                // requested balance — honour the smaller of the two measures.
                var taken = Math.Min(leave.RequestedDays, overlapDays);
                leaveDays += (int)Math.Round(taken, MidpointRounding.AwayFromZero);
            }
        }
        if (leaveDays > 0) parts.Add($"minus {leaveDays} unpaid leave day{(leaveDays == 1 ? "" : "s")}");

        int paymentDays = Math.Max(0, days - leaveDays);

        // Why is this month not full pay? Build the human note.
        bool lateStart = worker.StartDate.HasValue && worker.StartDate > inputs.PeriodStart;
        bool earlyEnd = worker.EndDate.HasValue && worker.EndDate < inputs.PeriodEnd;
        if (!lateStart && !earlyEnd && leaveDays == 0)
            return (periodDays, periodDays, null);

        var reasons = new List<string>();
        if (lateStart) reasons.Add($"started {worker.StartDate:dd MMM yyyy}");
        if (earlyEnd) reasons.Add($"ended {worker.EndDate:dd MMM yyyy}");
        reasons.AddRange(parts);
        string note = string.Join("; ", reasons);
        return (periodDays, paymentDays, note);
    }

    private static DateOnly MaxDate(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly MinDate(DateOnly a, DateOnly b) => a < b ? a : b;
}
