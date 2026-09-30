using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Payroll;

/// <summary>M41 Gap 2: pure proration logic — no EF, no DI, unit-testable.
///
/// Monthly payroll is prorated over scheduled workdays from the effective
/// work calendar. Public holidays are paid days. Approved unpaid leave removes
/// a full payment day; half-pay leave removes half a payment day.
///
/// paymentDays = scheduled workdays in
/// [max(periodStart, startDate)..min(periodEnd, endDate)] minus unpaid-leave
/// workdays overlapping that window. Proration factor = paymentDays /
/// scheduled workdays in the period and is applied by CalcContext to
/// salary-earned component amounts before statutory floors/ceilings re-apply.</summary>
public static class PaymentDaysCalculator
{
    public static (int WorkingDays, decimal PaymentDays, string? Note) For(
        PayrollProrationInputs inputs, Worker worker, List<ApprovedUnpaidLeave> unpaidLeaves)
    {
        var weekends = ParseWeekendDays(inputs.WeekendDays);
        int periodDays = CountWorkdays(inputs.PeriodStart, inputs.PeriodEnd, weekends);

        // Full-month employment window, clamped to the period.
        DateOnly start = worker.StartDate.HasValue
            ? MaxDate(worker.StartDate.Value, inputs.PeriodStart)
            : inputs.PeriodStart;
        DateOnly end = worker.EndDate.HasValue
            ? MinDate(worker.EndDate.Value, inputs.PeriodEnd)
            : inputs.PeriodEnd;
        if (end < start) return (periodDays, 0, "Not employed during this period");

        int days = CountWorkdays(start, end, weekends);
        var parts = new List<string>();

        // Approved unpaid/half-pay leave overlapping the clamped window.
        decimal leaveReduction = 0;
        foreach (var leave in unpaidLeaves)
        {
            DateOnly ls = MaxDate(leave.StartDate, start);
            DateOnly le = MinDate(leave.EndDate, end);
            if (le >= ls)
            {
                var overlapDays = CountWorkdays(ls, le, weekends);
                // Leave requested in partial days or capped by the worker's own
                // requested balance — honour the smaller of the two measures.
                var taken = Math.Min(leave.RequestedDays, overlapDays);
                var paidFraction = Math.Clamp(leave.PaidFraction, 0m, 1m);
                leaveReduction += taken * (1m - paidFraction);
                if (taken > 0)
                    parts.Add(paidFraction == 0m
                        ? $"minus {taken:0.##} unpaid leave day{(taken == 1m ? "" : "s")}"
                        : $"{taken:0.##} half-pay leave day{(taken == 1m ? "" : "s")} at 50% pay");
            }
        }
        decimal paymentDays = Math.Max(0m, days - leaveReduction);

        // Why is this month not full pay? Build the human note.
        bool lateStart = worker.StartDate.HasValue && worker.StartDate > inputs.PeriodStart;
        bool earlyEnd = worker.EndDate.HasValue && worker.EndDate < inputs.PeriodEnd;
        if (!lateStart && !earlyEnd && leaveReduction == 0)
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

    private static int CountWorkdays(DateOnly start, DateOnly end, HashSet<DayOfWeek> weekends)
    {
        var count = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
            if (!weekends.Contains(date.DayOfWeek)) count++;
        return count;
    }

    private static HashSet<DayOfWeek> ParseWeekendDays(string? values)
    {
        var result = new HashSet<DayOfWeek>();
        foreach (var value in (values ?? "sat,sun").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<DayOfWeek>(value, true, out var day)) result.Add(day);
            else if (value.Equals("sat", StringComparison.OrdinalIgnoreCase)) result.Add(DayOfWeek.Saturday);
            else if (value.Equals("sun", StringComparison.OrdinalIgnoreCase)) result.Add(DayOfWeek.Sunday);
            else if (value.Equals("mon", StringComparison.OrdinalIgnoreCase)) result.Add(DayOfWeek.Monday);
            else if (value.Equals("tue", StringComparison.OrdinalIgnoreCase)) result.Add(DayOfWeek.Tuesday);
            else if (value.Equals("wed", StringComparison.OrdinalIgnoreCase)) result.Add(DayOfWeek.Wednesday);
            else if (value.Equals("thu", StringComparison.OrdinalIgnoreCase)) result.Add(DayOfWeek.Thursday);
            else if (value.Equals("fri", StringComparison.OrdinalIgnoreCase)) result.Add(DayOfWeek.Friday);
        }
        return result;
    }
}
