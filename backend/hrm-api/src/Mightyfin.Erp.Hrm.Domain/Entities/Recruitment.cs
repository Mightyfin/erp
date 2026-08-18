// M38: Recruitment Pipeline — requisitions (authority to fill a post) and the
// audit trail of requisition lifecycle events. Vacancy gains a link to the
// requisition it was created from plus a closing date.
using System;
using System.Collections.Generic;

namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>M38: A requisition is the authority to fill a post. It records why
/// the post is needed (new position or replacement), the headcount, the grade,
/// the budget and the approval workflow. Only approved requisitions may be
/// advertised as vacancies.</summary>
public class Requisition : Entity
{
    public string RequisitionNo { get; set; } = null!;              // auto-generated REQ-0001 style
    public string JobTitle { get; set; } = null!;
    public string Reason { get; set; } = "new";                     // new | replacement
    public Guid? ReplacementWorkerId { get; set; }                  // worker being replaced (when reason=replacement)
    public int Headcount { get; set; } = 1;
    public string? Grade { get; set; }
    public Guid OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public Guid? LocationId { get; set; }
    public string? HiringManagerName { get; set; }
    public decimal? BudgetAnnual { get; set; }                      // annual cost estimate
    public string Currency { get; set; } = "ZMW";
    public string? BusinessCase { get; set; }
    public string Status { get; set; } = "draft";                   // draft | submitted | approved | returned | rejected
    public string? ApproversSubjectId { get; set; }                 // approver subject id
    public string? ApproverName { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ReturnedReason { get; set; }
    public string? RaisedBySubjectId { get; set; }
    public string? RaisedByName { get; set; }
    public ICollection<RequisitionEvent> Events { get; set; } = new List<RequisitionEvent>();
}

/// <summary>M38: Audit entry for requisition lifecycle transitions.</summary>
public class RequisitionEvent : Entity
{
    public Guid RequisitionId { get; set; }
    public Requisition? Requisition { get; set; }
    public string Action { get; set; } = null!;                     // created | submitted | approved | returned | rejected | cancelled
    public string ActorSubjectId { get; set; } = "system";
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Notes { get; set; }
}
