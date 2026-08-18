// M37: Offboarding & Exit Management
using System;
using System.Collections.Generic;

namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>M37: An offboarding (exit) request for a departing worker. Tracks the
/// resignation/termination, approval workflow, last working day, and reason.
/// Each request carries a set of <see cref="OffboardingChecklistItem"/> tasks and
/// optionally an <see cref="ExitInterview"/>.</summary>
public class OffboardingRequest : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string RequestType { get; set; } = "resignation";       // resignation | termination | retirement | redundancy
    public string Reason { get; set; } = null!;                     // free-text reason from worker or manager
    public string? AdditionalNotes { get; set; }
    public DateOnly NoticeStartDate { get; set; }                   // when notice period begins
    public DateOnly LastWorkingDay { get; set; }                    // effective separation date
    public string Status { get; set; } = "requested";               // requested | approved | in_progress | completed | cancelled
    public string? ApprovedBy { get; set; }                         // approver subject id
    public string? ApproverName { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? CancelledReason { get; set; }
    public bool IsFinalPayProcessed { get; set; }
    public int ChecklistItemsCompleted { get; set; }
    public int ChecklistItemsTotal { get; set; }
    public ICollection<OffboardingChecklistItem> ChecklistItems { get; set; } = new List<OffboardingChecklistItem>();
    public ExitInterview? ExitInterview { get; set; }
}

/// <summary>M37: A task that must be completed as part of the exit process
/// (equipment return, IT handover, access revocation, exit interview, etc.).</summary>
public class OffboardingChecklistItem : Entity
{
    public Guid OffboardingRequestId { get; set; }
    public OffboardingRequest? Request { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Owner { get; set; } = "hr";                       // hr | it | manager | finance | worker
    public bool IsCompleted { get; set; }
    public string? CompletedBy { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>M37: Structured exit interview recorded by HR at the end of the
/// offboarding process. Captures the departing worker's feedback.</summary>
public class ExitInterview : Entity
{
    public Guid OffboardingRequestId { get; set; }
    public OffboardingRequest? Request { get; set; }
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string? ReasonForLeaving { get; set; }                   // structured: better opportunity, compensation, management, relocation, health, other
    public string? ReasonDetails { get; set; }
    public string? WhatWentWell { get; set; }
    public string? WhatCouldImprove { get; set; }
    public string? WouldRecommend { get; set; }                     // yes | no | unsure
    public string? ManagerFeedback { get; set; }
    public string? HrmNotes { get; set; }
    public string? InterviewedBy { get; set; }
    public DateTimeOffset? InterviewedAt { get; set; }
    public string Status { get; set; } = "pending";                 // pending | conducted | saved
}
