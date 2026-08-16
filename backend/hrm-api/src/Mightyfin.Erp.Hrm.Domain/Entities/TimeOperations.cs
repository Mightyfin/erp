namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>M28 reusable shift definition, including the overtime controls
/// applied when attendance is imported or corrected.</summary>
public class ShiftDefinition : Entity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int UnpaidBreakMinutes { get; set; }
    public decimal StandardHours { get; set; } = 8;
    public decimal DailyOvertimeThresholdHours { get; set; } = 8;
    public decimal WeekdayOvertimeMultiplier { get; set; } = 1.5m;
    public decimal RestDayOvertimeMultiplier { get; set; } = 2m;
    public decimal HolidayOvertimeMultiplier { get; set; } = 2m;
    public bool IsActive { get; set; } = true;
}

public class WorkerShiftAssignment : Entity, IEffectiveDated
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public Guid ShiftId { get; set; }
    public ShiftDefinition? Shift { get; set; }
    public Guid? CalendarId { get; set; }
    public WorkCalendar? Calendar { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>One traceable attendance import submission. Individual attendance
/// records retain the batch id, while reconciliation counts stay here.</summary>
public class AttendanceImportBatch : Entity
{
    public string FileName { get; set; } = null!;
    public string Status { get; set; } = "completed"; // completed | completed-with-errors | failed
    public int RowCount { get; set; }
    public int ImportedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int RejectedCount { get; set; }
    public string? ErrorsJson { get; set; }
    public string ImportedBySubjectId { get; set; } = null!;
}

public class LeaveAccrualRun : Entity
{
    public string Period { get; set; } = null!; // yyyy-MM
    public string Status { get; set; } = "completed";
    public int WorkerCount { get; set; }
    public int LedgerEntryCount { get; set; }
    public decimal TotalDaysAccrued { get; set; }
    public string RunBySubjectId { get; set; } = null!;
}

public class LeaveBalanceAdjustment : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string LeaveTypeCode { get; set; } = null!;
    public decimal Days { get; set; }
    public string Reason { get; set; } = null!;
    public string AdjustedBySubjectId { get; set; } = null!;
    public Guid LedgerEntryId { get; set; }
}
