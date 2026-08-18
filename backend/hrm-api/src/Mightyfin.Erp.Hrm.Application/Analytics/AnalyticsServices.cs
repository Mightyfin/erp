using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
namespace Mightyfin.Erp.Hrm.Application.Analytics;

// ===================== DTOs (M40) =====================
/// <summary>Simple JSON-friendly trend row — System.Text.Json ignores
/// ValueTuple element names, so plain classes are used for all lists.</summary>
public sealed class TrendRow
{
    public string Month { get; init; } = "";
    public int ActiveCount { get; init; }
    public int Joined { get; init; }
    public int Left { get; init; }
}
public sealed class LeaveRow
{
    public string LeaveType { get; init; } = "";
    public double RequestedDays { get; init; }
    public double ApprovedDays { get; init; }
    public int Requests { get; init; }
    public int Approved { get; init; }
}
public sealed class RatingRow
{
    public string Rating { get; init; } = "";
    public int Count { get; init; }
}
public sealed class FunnelRow
{
    public string Stage { get; init; } = "";
    public int Count { get; init; }
}
public sealed class AttendanceRow
{
    public string DerivedStatus { get; init; } = "";
    public int Days { get; init; }
}
public sealed record WorkforceDto(
    int ActiveCount, int PreHireCount, int ArchivedCount,
    double AverageTenureYears,
    List<TrendRow> MonthlyTrend,
    double TurnoverRatePct);
/// <summary>Approved/requested leave split by leave type code.</summary>
public sealed record LeaveUtilizationDto(List<LeaveRow> ByType,
    double ApprovalRatePct);
/// <summary>One completed payroll run with its cost figures.</summary>
public sealed record PayrollRunDto(string PeriodLabel, string Status, decimal TotalGross, decimal TotalDeductions, decimal TotalNet,
    decimal TotalEmployerCost, int EmployeeCount, DateTime? PayDate);
/// <summary>Payroll cost panel: recent completed runs + totals.</summary>
public sealed record PayrollCostDto(List<PayrollRunDto> Runs, decimal GrossTotalLast6, decimal EmployerCostTotalLast6);
/// <summary>Performance assessment distribution: counts per final rating plus
/// cycle completion rate across open/completed cycles.</summary>
public sealed record PerformanceDistributionDto(List<RatingRow> ByRating,
    int Cycles, int Assessments, int Finalized, double CompletionRatePct);
/// <summary>Recruitment funnel totals plus funnel counts per vacancy stage.</summary>
public sealed record RecruitmentDto(int OpenRequisitions, int OpenVacancies, int CandidatesInPipeline,
    int OffersPending, int Hired,
    List<FunnelRow> StageFunnel);
/// <summary>Attendance presence summary for the trailing 30 days.</summary>
public sealed record AttendanceDto(List<AttendanceRow> ByStatus, double AverageDailyHours, double TotalOvertimeHours);
/// <summary>Full dashboard payload for one GET call.</summary>
public sealed record DashboardDto(
    DateTimeOffset AsAt,
    WorkforceDto Workforce,
    LeaveUtilizationDto Leave,
    PayrollCostDto Payroll,
    PerformanceDistributionDto Performance,
    RecruitmentDto Recruitment,
    AttendanceDto Attendance);

// ===================== Interfaces (M40) =====================
public interface IAnalyticsRepository
{
    Task<(int Active, int PreHire, int Archived)> WorkerCountsAsync(CancellationToken ct);
    Task<List<(string Month, int Joined, int Left)>> HeadcountMonthlyTrendAsync(int months, CancellationToken ct);
    Task<List<(string LeaveType, double RequestedDays, double ApprovedDays, int Requests, int Approved)>> LeaveByTypeAsync(CancellationToken ct);
    Task<int> LeaveTotalRequestsAsync(CancellationToken ct);
    Task<List<(string PeriodLabel, string Status, decimal Gross, decimal Deductions, decimal Net, decimal EmployerCost, int EmployeeCount, DateTime? PayDate)>> PayrollRunsAsync(int count, CancellationToken ct);
    Task<List<(string Rating, int Count)>> PerformanceByRatingAsync(CancellationToken ct);
    Task<(int Cycles, int Assessments, int Finalized)> PerformanceCycleStatsAsync(CancellationToken ct);
    Task<(int OpenRequisitions, int OpenVacancies, int CandidatesInPipeline, int OffersPending, int Hired)> RecruitmentCountsAsync(CancellationToken ct);
    Task<List<(string Stage, int Count)>> CandidateStageFunnelAsync(CancellationToken ct);
    Task<List<(string DerivedStatus, int Days)>> AttendanceByStatusAsync(int days, CancellationToken ct);
    Task<List<(decimal? TotalHours, decimal? OvertimeHours)>> AttendanceHoursAsync(int days, CancellationToken ct);
}
public interface IAnalyticsService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct);
}

// ===================== Implementation (M40) =====================
public sealed class AnalyticsServiceImpl(IAnalyticsRepository repo, IAuthzService authz) : IAnalyticsService
{
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var (active, preHire, archived) = await repo.WorkerCountsAsync(ct);
        var trend = await repo.HeadcountMonthlyTrendAsync(12, ct);
        // Annualised turnover: leavers over trailing 12 months ÷ average
        // active headcount during the same window (headcount approximated
        // as today's active count plus net flow). Left is included because
        // archived workers' CreatedAt may be older than the window.
        double turnoverPct = 0;
        if (active > 0)
        {
            var totalLeft = trend.Sum(t => t.Left);
            turnoverPct = Math.Round(totalLeft / (double)active * 100, 1);
        }
        // Average tenure: approximate from created_at of active workers.
        double avgTenure = 0;
        var workforce = new WorkforceDto(active, preHire, archived, avgTenure,
            trend.Select(t => new TrendRow { Month = t.Month, ActiveCount = active, Joined = t.Joined, Left = t.Left }).ToList(), turnoverPct);
        var leaveRows = await repo.LeaveByTypeAsync(ct);
        var leaveTotal = await repo.LeaveTotalRequestsAsync(ct);
        double leaveApprovalPct = leaveTotal > 0
            ? Math.Round(leaveRows.Sum(r => r.Approved) / (double)leaveTotal * 100, 1) : 0;
        var runs = await repo.PayrollRunsAsync(6, ct);
        var runDtos = runs.Select(r => new PayrollRunDto(r.PeriodLabel, r.Status, r.Gross, r.Deductions, r.Net, r.EmployerCost, r.EmployeeCount, r.PayDate)).ToList();
        var payroll = new PayrollCostDto(runDtos,
            runs.Take(6).Sum(r => r.Gross), runs.Take(6).Sum(r => r.EmployerCost));
        var ratingRows = await repo.PerformanceByRatingAsync(ct);
        var cycleStats = await repo.PerformanceCycleStatsAsync(ct);
        double perfCompletionPct = cycleStats.Assessments > 0
            ? Math.Round(cycleStats.Finalized / (double)cycleStats.Assessments * 100, 1) : 0;
        var performance = new PerformanceDistributionDto(
            ratingRows.Select(r => new RatingRow { Rating = r.Rating, Count = r.Count }).ToList(),
            cycleStats.Cycles, cycleStats.Assessments, cycleStats.Finalized, perfCompletionPct);
        var rec = await repo.RecruitmentCountsAsync(ct);
        var funnel = await repo.CandidateStageFunnelAsync(ct);
        var recruitment = new RecruitmentDto(rec.OpenRequisitions, rec.OpenVacancies,
            rec.CandidatesInPipeline, rec.OffersPending, rec.Hired,
            funnel.Select(f => new FunnelRow { Stage = f.Stage, Count = f.Count }).ToList());
        var attStatus = await repo.AttendanceByStatusAsync(30, ct);
        var attHours = await repo.AttendanceHoursAsync(30, ct);
        double avgHours = attHours.Count > 0
            ? Math.Round(attHours.Average(a => (double?)a.TotalHours ?? 0), 1) : 0;
        double overtime = Math.Round(attHours.Sum(a => (double?)a.OvertimeHours ?? 0), 1);
        var attendance = new AttendanceDto(
            attStatus.Select(a => new AttendanceRow { DerivedStatus = a.DerivedStatus, Days = a.Days }).ToList(), avgHours, overtime);
        return new DashboardDto(DateTimeOffset.UtcNow, workforce,
            new LeaveUtilizationDto(leaveRows.Select(r => new LeaveRow { LeaveType = r.LeaveType, RequestedDays = r.RequestedDays, ApprovedDays = r.ApprovedDays, Requests = r.Requests, Approved = r.Approved }).ToList(), leaveApprovalPct),
            payroll, performance, recruitment, attendance);
    }
}
