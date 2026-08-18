using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Performance;

/// <summary>M36: repository contract for performance cycles, goals and assessments.</summary>
public interface IPerformanceRepository
{
    Task<(List<PerformanceCycle> Items, int Total)> ListCyclesAsync(string? status, CancellationToken ct);
    Task<PerformanceCycle?> GetCycleAsync(Guid id, CancellationToken ct);
    Task<PerformanceCycle> CreateCycleAsync(PerformanceCycle cycle, CancellationToken ct);
    Task<PerformanceCycle> UpdateCycleAsync(PerformanceCycle cycle, CancellationToken ct);
    Task<List<PerformanceGoal>> ListGoalsAsync(Guid cycleId, Guid? workerId, CancellationToken ct);
    Task<PerformanceGoal> CreateGoalAsync(PerformanceGoal goal, CancellationToken ct);
    Task<PerformanceGoal> UpdateGoalAsync(PerformanceGoal goal, CancellationToken ct);
    Task DeleteGoalAsync(Guid id, CancellationToken ct);
    Task<PerformanceGoal?> GetGoalAsync(Guid id, CancellationToken ct);
    Task<List<PerformanceAssessment>> ListAssessmentsAsync(Guid cycleId, CancellationToken ct);
    Task<PerformanceAssessment?> GetAssessmentAsync(Guid id, CancellationToken ct);
    Task<PerformanceAssessment> CreateAssessmentAsync(PerformanceAssessment assessment, CancellationToken ct);
    /// M36: explicit top-level insert for the assessment (EF Core 10
    /// Modified-parent demotion immunity) and persists the cycle status
    /// transition in the same unit of work.
    Task AddRangeAssessmentsAsync(List<PerformanceAssessment> assessments, CancellationToken ct);
    Task<PerformanceAssessment> UpdateAssessmentAsync(PerformanceAssessment assessment, CancellationToken ct);
    Task<List<Worker>> ListActiveWorkersAsync(CancellationToken ct);
    Task<List<PerformanceCycle>> ListMyCyclesAsync(string subjectId, CancellationToken ct);
    Task<PerformanceAssessment?> GetMyAssessmentAsync(Guid cycleId, Guid workerId, CancellationToken ct);
}
