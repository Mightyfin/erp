namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>M36: A performance review cycle defines a period (e.g. Q1 2026, Annual 2026)
/// during which employees are assessed against goals and competencies.</summary>
public class PerformanceCycle : Entity
{
    public string Name { get; set; } = null!;             // e.g. "Annual Review 2026"
    public string PeriodType { get; set; } = "annual";    // annual | semi-annual | quarterly | custom
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = "draft";         // draft | active | assessments_open | assessments_due | completed | closed
    public string? Description { get; set; }
    public string? GoalTemplate { get; set; }             // JSON: suggested goal categories
    public DateOnly? SelfAssessmentDeadline { get; set; }
    public DateOnly? ManagerAssessmentDeadline { get; set; }
    public DateOnly? ReviewMeetingDeadline { get; set; }
    public ICollection<PerformanceGoal> Goals { get; set; } = new List<PerformanceGoal>();
    public ICollection<PerformanceAssessment> Assessments { get; set; } = new List<PerformanceAssessment>();
}

/// <summary>M36: A goal/KPI set for a worker within a performance cycle. Goals are
/// defined by the manager at cycle creation and can be updated during the cycle.</summary>
public class PerformanceGoal : Entity
{
    public Guid CycleId { get; set; }
    public PerformanceCycle? Cycle { get; set; }
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string Category { get; set; } = "business";    // business | development | behavioural
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal? Weight { get; set; }                  // relative importance 0-100
    public string MeasurementType { get; set; } = "qualitative"; // qualitative | quantitative
    public string? TargetValue { get; set; }              // e.g. "95%", "K3.5M revenue"
    public string? ActualValue { get; set; }              // filled during assessment
    public int SortOrder { get; set; }
}

/// <summary>M36: Assessment record for a worker in a cycle. Contains self-assessment
/// (employee), manager assessment, and final agreed rating.</summary>
public class PerformanceAssessment : Entity
{
    public Guid CycleId { get; set; }
    public PerformanceCycle? Cycle { get; set; }
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    // Self-assessment (employee submits)
    public string? SelfRating { get; set; }               // exceptional | exceeds | meets | developing | unsatisfactory
    public string? SelfComments { get; set; }
    public DateTimeOffset? SelfSubmittedAt { get; set; }
    // Manager assessment
    public string? ManagerRating { get; set; }
    public string? ManagerComments { get; set; }
    public DateTimeOffset? ManagerSubmittedAt { get; set; }
    public string? ManagerName { get; set; }
    // Final / agreed outcome
    public string? FinalRating { get; set; }
    public string? FinalComments { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public string? DevelopmentNotes { get; set; }         // agreed development actions
    public string? NextCycleGoals { get; set; }           // JSON: goals suggested for next cycle
    public string Status { get; set; } = "not_started";   // not_started | self_assessment | manager_assessment | finalized
}
