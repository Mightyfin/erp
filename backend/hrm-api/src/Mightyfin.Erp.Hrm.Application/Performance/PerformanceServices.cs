using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Performance;

/// <summary>M36: performance cycle / goal / assessment administration plus
/// employee self-assessment (self-service). HR admins manage cycles and
/// finalise ratings; employees only touch their own self-assessment.</summary>
public interface IPerformanceService
{
    // Cycles (HR admin)
    Task<List<PerformanceCycleDto>> ListCyclesAsync(string? status, CancellationToken ct);
    Task<PerformanceCycleDto> CreateCycleAsync(PerformanceCycleCreate req, CancellationToken ct);
    Task<PerformanceCycleDto> GetCycleAsync(Guid id, CancellationToken ct);
    Task<PerformanceCycleDto> UpdateCycleAsync(Guid id, PerformanceCycleUpdate req, CancellationToken ct);
    Task<PerformanceCycleDto> CloseCycleAsync(Guid id, CancellationToken ct);

    // Goals (HR admin)
    Task<List<PerformanceGoalDto>> ListGoalsAsync(Guid cycleId, Guid? workerId, CancellationToken ct);
    Task<PerformanceGoalDto> CreateGoalAsync(Guid cycleId, PerformanceGoalCreate req, CancellationToken ct);
    Task<PerformanceGoalDto> UpdateGoalAsync(Guid id, PerformanceGoalUpdate req, CancellationToken ct);
    Task DeleteGoalAsync(Guid id, CancellationToken ct);

    // Assessments (HR admin)
    Task<List<PerformanceAssessmentDto>> ListAssessmentsAsync(Guid cycleId, CancellationToken ct);
    Task<PerformanceAssessmentDto> GetAssessmentAsync(Guid id, CancellationToken ct);
    /// Creates assessment rows for every active worker (bulk seeding at cycle
    /// activation) — idempotent: workers already present in the cycle are
    /// skipped rather than duplicated.
    Task<List<PerformanceAssessmentDto>> EnsureAssessmentsAsync(Guid cycleId, CancellationToken ct);
    Task<PerformanceAssessmentDto> SubmitManagerAssessmentAsync(Guid id, ManagerAssessmentSubmit req, CancellationToken ct);
    Task<PerformanceAssessmentDto> FinalizeAssessmentAsync(Guid id, FinalizeAssessment req, CancellationToken ct);
    Task<CycleReportDto> GetCycleReportAsync(Guid cycleId, CancellationToken ct);

    // Self-service
    Task<List<MyPerformanceDto>> GetMyPerformanceAsync(string subjectId, CancellationToken ct);
    Task<MyPerformanceDto?> GetMyAssessmentAsync(string subjectId, Guid cycleId, CancellationToken ct);
    Task<MyPerformanceDto> SubmitSelfAssessmentAsync(Guid assessmentId, string subjectId, SelfAssessmentSubmit req, CancellationToken ct);
}

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------
public sealed record PerformanceCycleDto(
    Guid Id, string Name, string PeriodType, DateOnly StartDate, DateOnly EndDate,
    string Status, string? Description, string? GoalTemplate,
    DateOnly? SelfAssessmentDeadline, DateOnly? ManagerAssessmentDeadline, DateOnly? ReviewMeetingDeadline,
    int GoalCount, int AssessmentCount, DateTimeOffset CreatedAt);

public sealed record PerformanceGoalDto(
    Guid Id, Guid CycleId, Guid? WorkerId, string? WorkerName, string Category,
    string Title, string? Description, decimal? Weight, string MeasurementType,
    string? TargetValue, string? ActualValue, int SortOrder);

public sealed record PerformanceAssessmentDto(
    Guid Id, Guid CycleId, string CycleName, Guid WorkerId, string? WorkerName,
    string? SelfRating, string? SelfComments, DateTimeOffset? SelfSubmittedAt,
    string? ManagerRating, string? ManagerComments, DateTimeOffset? ManagerSubmittedAt,
    string? ManagerName, string? FinalRating, string? FinalComments, DateTimeOffset? FinalizedAt,
    string? DevelopmentNotes, string? NextCycleGoals, string Status);

public sealed record CycleReportDto(
    Guid CycleId, string CycleName, int TotalWorkers, int SelfSubmitted, int ManagerSubmitted, int Finalized,
    List<RatingBucket> Ratings);

public sealed record RatingBucket(string Rating, int Count);

public sealed record MyPerformanceDto(
    Guid AssessmentId, Guid CycleId, string CycleName, DateOnly StartDate, DateOnly EndDate,
    string CycleStatus, string? SelfRating, DateTimeOffset? SelfSubmittedAt,
    string? ManagerRating, DateTimeOffset? ManagerSubmittedAt,
    string? FinalRating, DateTimeOffset? FinalizedAt, string Status);

// ---------------------------------------------------------------------------
// Request shapes
// ---------------------------------------------------------------------------
public sealed record PerformanceCycleCreate(
    string Name, string PeriodType, DateOnly StartDate, DateOnly EndDate,
    string? Description, string? GoalTemplate,
    DateOnly? SelfAssessmentDeadline, DateOnly? ManagerAssessmentDeadline, DateOnly? ReviewMeetingDeadline);

public sealed record PerformanceCycleUpdate(
    string? Name, string? PeriodType, DateOnly? StartDate, DateOnly? EndDate,
    string? Description, string? GoalTemplate,
    DateOnly? SelfAssessmentDeadline, DateOnly? ManagerAssessmentDeadline, DateOnly? ReviewMeetingDeadline);

public sealed record PerformanceGoalCreate(
    Guid? WorkerId, string Category, string Title, string? Description,
    decimal? Weight, string MeasurementType, string? TargetValue, int SortOrder);

public sealed record PerformanceGoalUpdate(
    string? Category, string? Title, string? Description, decimal? Weight,
    string? MeasurementType, string? TargetValue, string? ActualValue, int? SortOrder);

public sealed record SelfAssessmentSubmit(string Rating, string? Comments);
public sealed record ManagerAssessmentSubmit(string Rating, string? Comments);
public sealed record FinalizeAssessment(string FinalRating, string? FinalComments, string? DevelopmentNotes, string? NextCycleGoals);

// ---------------------------------------------------------------------------
// Implementation
// ---------------------------------------------------------------------------
public sealed class PerformanceServiceImpl(
    IPerformanceRepository repo, IAuthzService authz,
    IWorkerRepository? workerRepo = null) : IPerformanceService
{
    private static readonly HashSet<string> AdminRoles = new(StringComparer.Ordinal) { "hr_ops", "hr_admin" };

    public async Task<List<PerformanceCycleDto>> ListCyclesAsync(string? status, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var (items, _) = await repo.ListCyclesAsync(status, ct);
        return items.Select(MapCycle).ToList();
    }

    public async Task<PerformanceCycleDto> CreateCycleAsync(PerformanceCycleCreate req, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        ValidatePeriod(req.StartDate, req.EndDate);
        var cycle = new PerformanceCycle
        {
            Name = req.Name,
            PeriodType = req.PeriodType,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            Description = req.Description,
            GoalTemplate = req.GoalTemplate,
            SelfAssessmentDeadline = req.SelfAssessmentDeadline,
            ManagerAssessmentDeadline = req.ManagerAssessmentDeadline,
            ReviewMeetingDeadline = req.ReviewMeetingDeadline,
            Status = "draft",
        };
        return MapCycle(await repo.CreateCycleAsync(cycle, ct));
    }

    public async Task<PerformanceCycleDto> GetCycleAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var cycle = await repo.GetCycleAsync(id, ct)
            ?? throw new DomainException("cycle-not-found", $"Performance cycle {id} does not exist.");
        return MapCycle(cycle);
    }

    public async Task<PerformanceCycleDto> UpdateCycleAsync(Guid id, PerformanceCycleUpdate req, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var cycle = await repo.GetCycleAsync(id, ct)
            ?? throw new DomainException("cycle-not-found", $"Performance cycle {id} does not exist.");
        if (req.Name is not null) cycle.Name = req.Name;
        if (req.PeriodType is not null) cycle.PeriodType = req.PeriodType;
        if (req.StartDate.HasValue) cycle.StartDate = req.StartDate.Value;
        if (req.EndDate.HasValue) cycle.EndDate = req.EndDate.Value;
        if (req.Description is not null) cycle.Description = req.Description;
        if (req.GoalTemplate is not null) cycle.GoalTemplate = req.GoalTemplate;
        if (req.SelfAssessmentDeadline.HasValue) cycle.SelfAssessmentDeadline = req.SelfAssessmentDeadline.Value;
        if (req.ManagerAssessmentDeadline.HasValue) cycle.ManagerAssessmentDeadline = req.ManagerAssessmentDeadline.Value;
        if (req.ReviewMeetingDeadline.HasValue) cycle.ReviewMeetingDeadline = req.ReviewMeetingDeadline.Value;
        if (cycle.Status == "draft") ValidatePeriod(cycle.StartDate, cycle.EndDate);
        return MapCycle(await repo.UpdateCycleAsync(cycle, ct));
    }

    public async Task<PerformanceCycleDto> CloseCycleAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var cycle = await repo.GetCycleAsync(id, ct)
            ?? throw new DomainException("cycle-not-found", $"Performance cycle {id} does not exist.");
        cycle.Status = "closed";
        return MapCycle(await repo.UpdateCycleAsync(cycle, ct));
    }

    public async Task<List<PerformanceGoalDto>> ListGoalsAsync(Guid cycleId, Guid? workerId, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        await RequireCycleAsync(cycleId, ct);
        var goals = await repo.ListGoalsAsync(cycleId, workerId, ct);
        return goals.Select(MapGoal).ToList();
    }

    public async Task<PerformanceGoalDto> CreateGoalAsync(Guid cycleId, PerformanceGoalCreate req, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var cycle = await RequireCycleAsync(cycleId, ct);
        var goal = new PerformanceGoal
        {
            CycleId = cycleId,
            WorkerId = req.WorkerId,
            Category = req.Category,
            Title = req.Title,
            Description = req.Description,
            Weight = req.Weight,
            MeasurementType = req.MeasurementType,
            TargetValue = req.TargetValue,
            SortOrder = req.SortOrder,
        };
        return MapGoal(await repo.CreateGoalAsync(goal, ct));
    }

    public async Task<PerformanceGoalDto> UpdateGoalAsync(Guid id, PerformanceGoalUpdate req, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var goal = await repo.GetGoalAsync(id, ct)
            ?? throw new DomainException("goal-not-found", $"Performance goal {id} does not exist.");
        if (req.Category is not null) goal.Category = req.Category;
        if (req.Title is not null) goal.Title = req.Title;
        if (req.Description is not null) goal.Description = req.Description;
        if (req.Weight.HasValue) goal.Weight = req.Weight.Value;
        if (req.MeasurementType is not null) goal.MeasurementType = req.MeasurementType;
        if (req.TargetValue is not null) goal.TargetValue = req.TargetValue;
        if (req.ActualValue is not null) goal.ActualValue = req.ActualValue;
        if (req.SortOrder.HasValue) goal.SortOrder = req.SortOrder.Value;
        return MapGoal(await repo.UpdateGoalAsync(goal, ct));
    }

    public async Task DeleteGoalAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        await repo.DeleteGoalAsync(id, ct);
    }

    public async Task<List<PerformanceAssessmentDto>> ListAssessmentsAsync(Guid cycleId, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var cycle = await RequireCycleAsync(cycleId, ct);
        var items = await repo.ListAssessmentsAsync(cycleId, ct);
        return items.Select(x => MapAssessment(x, cycle)).ToList();
    }

    public async Task<PerformanceAssessmentDto> GetAssessmentAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var assessment = await repo.GetAssessmentAsync(id, ct)
            ?? throw new DomainException("assessment-not-found", $"Assessment {id} does not exist.");
        var cycle = await RequireCycleAsync(assessment.CycleId, ct);
        return MapAssessment(assessment, cycle);
    }

    public async Task<List<PerformanceAssessmentDto>> EnsureAssessmentsAsync(Guid cycleId, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var cycle = await RequireCycleAsync(cycleId, ct);
        var existing = await repo.ListAssessmentsAsync(cycleId, ct);
        var existingWorkerIds = new HashSet<Guid>(existing.Select(x => x.WorkerId));
        var allWorkers = await repo.ListActiveWorkersAsync(ct);
        var toCreate = allWorkers.Where(w => !existingWorkerIds.Contains(w.Id)).Select(w => new PerformanceAssessment
        {
            CycleId = cycleId,
            WorkerId = w.Id,
            Status = "not_started",
        }).ToList();
        if (toCreate.Count == 0) return existing.Select(x => MapAssessment(x, cycle)).ToList();
        await repo.AddRangeAssessmentsAsync(toCreate, ct);
        // cycle goes live the moment assessments exist
        if (cycle.Status == "draft")
        {
            cycle.Status = "assessments_open";
            await repo.UpdateCycleAsync(cycle, ct);
        }
        var final = await repo.ListAssessmentsAsync(cycleId, ct);
        return final.Select(x => MapAssessment(x, cycle)).ToList();
    }

    public async Task<PerformanceAssessmentDto> SubmitManagerAssessmentAsync(Guid id, ManagerAssessmentSubmit req, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var assessment = await repo.GetAssessmentAsync(id, ct)
            ?? throw new DomainException("assessment-not-found", $"Assessment {id} does not exist.");
        if (assessment.Status is "finalized")
            throw new DomainException("assessment-finalized", "A finalized assessment cannot be changed.");
        ValidateRating(req.Rating);
        assessment.ManagerRating = req.Rating;
        assessment.ManagerComments = req.Comments;
        assessment.ManagerSubmittedAt = DateTimeOffset.UtcNow;
        assessment.ManagerName = authz.CurrentSubjectId;
        assessment.Status = "manager_assessment";
        return MapAssessment(await repo.UpdateAssessmentAsync(assessment, ct), await RequireCycleAsync(assessment.CycleId, ct));
    }

    public async Task<PerformanceAssessmentDto> FinalizeAssessmentAsync(Guid id, FinalizeAssessment req, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var assessment = await repo.GetAssessmentAsync(id, ct)
            ?? throw new DomainException("assessment-not-found", $"Assessment {id} does not exist.");
        if (assessment.Status is "finalized")
            throw new DomainException("assessment-finalized", "A finalized assessment cannot be changed.");
        ValidateRating(req.FinalRating);
        assessment.FinalRating = req.FinalRating;
        assessment.FinalComments = req.FinalComments;
        assessment.FinalizedAt = DateTimeOffset.UtcNow;
        assessment.DevelopmentNotes = req.DevelopmentNotes;
        assessment.NextCycleGoals = req.NextCycleGoals;
        assessment.Status = "finalized";
        return MapAssessment(await repo.UpdateAssessmentAsync(assessment, ct), await RequireCycleAsync(assessment.CycleId, ct));
    }

    public async Task<CycleReportDto> GetCycleReportAsync(Guid cycleId, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var cycle = await RequireCycleAsync(cycleId, ct);
        var items = await repo.ListAssessmentsAsync(cycleId, ct);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var a in items)
        {
            if (a.FinalRating is not null)
                counts.TryGetValue(a.FinalRating, out var c);
        }
        foreach (var a in items.Where(x => x.FinalRating is not null))
        {
            var r = a.FinalRating!;
            counts.TryGetValue(r, out var c);
            counts[r] = c + 1;
        }
        return new CycleReportDto(
            CycleId: cycle.Id,
            CycleName: cycle.Name,
            TotalWorkers: items.Count,
            SelfSubmitted: items.Count(x => x.SelfSubmittedAt is not null),
            ManagerSubmitted: items.Count(x => x.ManagerSubmittedAt is not null),
            Finalized: items.Count(x => x.Status == "finalized"),
            Ratings: counts.Select(kv => new RatingBucket(kv.Key, kv.Value)).OrderByDescending(x => x.Count).ToList());
    }

    // -----------------------------------------------------------------------
    // Self-service
    // -----------------------------------------------------------------------
    public async Task<List<MyPerformanceDto>> GetMyPerformanceAsync(string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        var worker = await ResolveWorkerAsync(subjectId, ct);
        if (worker is null) return [];
        var cycles = await repo.ListMyCyclesAsync(subjectId, ct);
        var result = new List<MyPerformanceDto>();
        foreach (var c in cycles)
        {
            var a = await repo.GetMyAssessmentAsync(c.Id, worker.Id, ct);
            if (a is not null)
                result.Add(MapMy(c, a));
        }
        return result;
    }

    public async Task<MyPerformanceDto?> GetMyAssessmentAsync(string subjectId, Guid cycleId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        var worker = await ResolveWorkerAsync(subjectId, ct);
        if (worker is null) return null;
        var cycles = await repo.ListMyCyclesAsync(subjectId, ct);
        var cycle = cycles.FirstOrDefault(c => c.Id == cycleId);
        if (cycle is null) return null;
        var a = await repo.GetMyAssessmentAsync(cycleId, worker.Id, ct);
        return a is null ? null : MapMy(cycle, a);
    }

    public async Task<MyPerformanceDto> SubmitSelfAssessmentAsync(Guid assessmentId, string subjectId, SelfAssessmentSubmit req, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        var worker = await ResolveWorkerAsync(subjectId, ct)
            ?? throw new DomainException("not-linked-worker", "The signed-in identity is not linked to a worker.");
        var assessment = await repo.GetAssessmentAsync(assessmentId, ct)
            ?? throw new DomainException("assessment-not-found", $"Assessment {assessmentId} does not exist.");
        if (assessment.WorkerId != worker.Id)
            throw new DomainException("assessment-not-owned", "This assessment belongs to another worker.");
        if (assessment.Status is "finalized")
            throw new DomainException("assessment-finalized", "A finalized assessment cannot be changed.");
        ValidateRating(req.Rating);
        assessment.SelfRating = req.Rating;
        assessment.SelfComments = req.Comments;
        assessment.SelfSubmittedAt = DateTimeOffset.UtcNow;
        assessment.Status = assessment.ManagerSubmittedAt is not null ? "manager_assessment" : "self_assessment";
        var updated = await repo.UpdateAssessmentAsync(assessment, ct);
        var cycles = await repo.ListMyCyclesAsync(subjectId, ct);
        var cycle = cycles.FirstOrDefault(c => c.Id == updated.CycleId)
            ?? throw new DomainException("cycle-not-found", "The assessment's cycle is no longer accessible.");
        return MapMy(cycle, updated);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private static void ValidatePeriod(DateOnly start, DateOnly end)
    {
        if (end < start)
            throw new DomainException("invalid-cycle-period", "The cycle end date must be on or after the start date.");
    }

    private static void ValidateRating(string rating)
    {
        const string allowed = "exceptional,exceeds,meets,developing,unsatisfactory";
        if (!allowed.Split(',').Contains(rating, StringComparer.Ordinal))
            throw new DomainException("invalid-rating", $"Rating must be one of: {allowed}.");
    }

    private async Task<PerformanceCycle> RequireCycleAsync(Guid id, CancellationToken ct)
        => await repo.GetCycleAsync(id, ct)
            ?? throw new DomainException("cycle-not-found", $"Performance cycle {id} does not exist.");

    private async Task<Worker?> ResolveWorkerAsync(string subjectId, CancellationToken ct)
    {
        if (workerRepo is null) return null;
        if (string.IsNullOrEmpty(subjectId)) return null;
        return await workerRepo.FindBySubjectIdAsync(subjectId, ct);
    }

    private static PerformanceCycleDto MapCycle(PerformanceCycle c) => new(
        Id: c.Id, Name: c.Name, PeriodType: c.PeriodType,
        StartDate: c.StartDate, EndDate: c.EndDate, Status: c.Status,
        Description: c.Description, GoalTemplate: c.GoalTemplate,
        SelfAssessmentDeadline: c.SelfAssessmentDeadline,
        ManagerAssessmentDeadline: c.ManagerAssessmentDeadline,
        ReviewMeetingDeadline: c.ReviewMeetingDeadline,
        GoalCount: c.Goals.Count, AssessmentCount: c.Assessments.Count,
        CreatedAt: c.CreatedAt);

    private static PerformanceGoalDto MapGoal(PerformanceGoal g) => new(
        Id: g.Id, CycleId: g.CycleId, WorkerId: g.WorkerId,
        WorkerName: g.Worker?.FullName, Category: g.Category,
        Title: g.Title, Description: g.Description, Weight: g.Weight,
        MeasurementType: g.MeasurementType, TargetValue: g.TargetValue,
        ActualValue: g.ActualValue, SortOrder: g.SortOrder);

    private static PerformanceAssessmentDto MapAssessment(PerformanceAssessment a, PerformanceCycle cycle) => new(
        Id: a.Id, CycleId: a.CycleId, CycleName: cycle.Name,
        WorkerId: a.WorkerId, WorkerName: a.Worker?.FullName,
        SelfRating: a.SelfRating, SelfComments: a.SelfComments, SelfSubmittedAt: a.SelfSubmittedAt,
        ManagerRating: a.ManagerRating, ManagerComments: a.ManagerComments,
        ManagerSubmittedAt: a.ManagerSubmittedAt, ManagerName: a.ManagerName,
        FinalRating: a.FinalRating, FinalComments: a.FinalComments, FinalizedAt: a.FinalizedAt,
        DevelopmentNotes: a.DevelopmentNotes, NextCycleGoals: a.NextCycleGoals, Status: a.Status);

    private static MyPerformanceDto MapMy(PerformanceCycle c, PerformanceAssessment a) => new(
        AssessmentId: a.Id, CycleId: c.Id, CycleName: c.Name,
        StartDate: c.StartDate, EndDate: c.EndDate, CycleStatus: c.Status,
        SelfRating: a.SelfRating, SelfSubmittedAt: a.SelfSubmittedAt,
        ManagerRating: a.ManagerRating, ManagerSubmittedAt: a.ManagerSubmittedAt,
        FinalRating: a.FinalRating, FinalizedAt: a.FinalizedAt, Status: a.Status);
}
