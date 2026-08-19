namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>HRM-004: Leave type with entitlement rules. Effective-dated so a
/// policy change never silently rewrites past entitlements.</summary>
public class LeaveType : Entity, IEffectiveDated
{
    public string Code { get; set; } = null!;      // annual | sick | parental | unpaid | study | compassionate
    public string Name { get; set; } = null!;
    public string Category { get; set; } = "paid"; // paid | unpaid | half-pay
    public int DefaultDaysPerYear { get; set; } = 24;
    public decimal MaxConsecutiveDays { get; set; } = 999;
    public bool RequiresEvidence { get; set; }
    public int MinNoticeDays { get; set; }
    public bool AllowsPartialDays { get; set; }
    public int CarryForwardDays { get; set; }
    public int CarryForwardExpiryMonths { get; set; }
    public bool AllowNegative { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Accrual ledger entry. Balances are computed by summing this table,
/// which also carries the full history for explainability.</summary>
public class LeaveBalanceLedger : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string LeaveTypeCode { get; set; } = null!;
    public decimal Days { get; set; }                  // + accrual, - taken, - expired
    public string Reason { get; set; } = null!;        // annual-accrual | request | carry-forward | forfeiture | manual-adjustment
    public Guid? ReferenceId { get; set; }             // linked leave request if applicable
    public string ReferenceType { get; set; } = "";    // leave-request | adjustment | encashment
    public string Note { get; set; } = "";             // M41 Gap 6a: human-readable note (e.g. encashment gross amount)
    public DateOnly ForDate { get; set; }
}

/// <summary>HRM-027: Leave request with the standard request state machine.</summary>
public class LeaveRequest : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public Guid? ApproverId { get; set; }
    // M44 branch scoping: location the request was raised under; null = global.
    public Guid? LocationId { get; set; }
    public string LeaveTypeCode { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsPartialDay { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public decimal RequestedDays { get; set; }
    public string Status { get; set; } = "draft"; // draft | submitted | in-review | approved | returned | rejected | cancelled
    public string? RejectionReason { get; set; }
    public string? ReturnNote { get; set; }
    public bool EvidenceAttached { get; set; }
    public bool BalanceReserved { get; set; }
    public DateOnly CreatedForPeriod { get; set; }     // payroll period the absence belongs to
    public bool CrossesCutoff { get; set; }
}

/// <summary>HRM-025/029: Raw attendance event plus correction lifecycle.</summary>
public class AttendanceRecord : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    // M44 branch scoping: branch the attendance event belongs to.
    public Guid? LocationId { get; set; }
    public DateOnly WorkDate { get; set; }
    public TimeOnly? ClockIn { get; set; }
    public TimeOnly? ClockOut { get; set; }
    public string Source { get; set; } = "self-service"; // self-service | device | roster | corrected
    public string DerivedStatus { get; set; } = "unknown"; // present | absent | late | early-departure | half-day | unknown
    public decimal TotalHours { get; set; }
    public decimal ScheduledHours { get; set; }
    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal OvertimeMultiplier { get; set; }
    public Guid? ShiftId { get; set; }
    public Guid? ImportBatchId { get; set; }
}

/// <summary>HRM-029: Employee-submitted attendance correction with manager review.
/// Original values are preserved for compare.</summary>
public class AttendanceCorrection : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public Guid? OriginalRecordId { get; set; }
    // M44 branch scoping: branch the correction belongs to.
    public Guid? LocationId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string IssueType { get; set; } = null!;   // missing-clock-in | missing-clock-out | wrong-time | wrong-shift | duty | leave | other
    public TimeOnly? ProposedClockIn { get; set; }
    public TimeOnly? ProposedClockOut { get; set; }
    public string? ProposedStatus { get; set; }
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = "submitted"; // submitted | in-review | approved | returned | rejected
    public string? RejectionReason { get; set; }
}
