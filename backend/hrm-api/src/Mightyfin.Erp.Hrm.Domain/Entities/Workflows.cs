namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>HRM-005: Generic approval workflow request. Every approvable action
/// (leave, correction, movement, letter, compensation change) creates one of
/// these with a workflow type and a JSON payload.</summary>
public class WorkflowRequest : Entity
{
    public string WorkflowType { get; set; } = null!; // leave | attendance-correction | movement | hr-request | hr-letter | compensation-change | onboarding-task
    public Guid? SubjectWorkerId { get; set; }
    public string Status { get; set; } = "draft"; // draft | submitted | in-review | approved | returned | rejected | cancelled
    public string PayloadJson { get; set; } = "{}";
    public string? RejectionReason { get; set; }
    public string? ReturnNote { get; set; }
    public Guid? CurrentApproverId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? EscalatedAt { get; set; }
    public ICollection<WorkflowDecision> Decisions { get; set; } = new List<WorkflowDecision>();
}

public class WorkflowDecision : Entity
{
    public Guid RequestId { get; set; }
    public WorkflowRequest? Request { get; set; }
    public Guid ActorId { get; set; }        // worker id of the decider
    public string Action { get; set; } = null!; // approve | return | reject | delegate
    public string? Reason { get; set; }
    public Guid? DelegatedToId { get; set; }
}

/// <summary>Active delegation: while effective, requests routed to DelegatorId
/// go to DelegateWorkerId.</summary>
public class ApprovalDelegation : Entity
{
    public Guid DelegatorId { get; set; }
    public Guid DelegateWorkerId { get; set; }
    public string? Scope { get; set; }         // null = all, else e.g. "leave"
    public DateOnly FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>UI-XPR-001: HR service-desk request with threaded conversation.</summary>
public class HrRequest : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string Category { get; set; } = null!; // payroll | benefits | contract | data-change | employment-letter | other
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string Status { get; set; } = "open"; // open | in-progress | awaiting-employee | resolved | closed
    public string Confidentiality { get; set; } = "normal"; // normal | confidential
    public string? ServiceTargetDays { get; set; }
    public ICollection<HrRequestMessage> Messages { get; set; } = new List<HrRequestMessage>();
}

public class HrRequestMessage : Entity
{
    public Guid RequestId { get; set; }
    public HrRequest? Request { get; set; }
    public Guid? WorkerId { get; set; }            // null = HR staff/system
    public string From { get; set; } = null!;      // employee | hr | system
    public string Body { get; set; } = null!;
    public bool IsInternalNote { get; set; }
}

/// <summary>UI-XPR-002: HR letter generation with template merge and verification code.</summary>
public class HrLetter : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public Guid? RequestId { get; set; }
    public string LetterType { get; set; } = null!; // employment-confirmation | salary-confirmation | bank | visa | reference | service-certificate | leave-confirmation | custom
    public string Status { get; set; } = "draft"; // draft | pending-approval | approved | generated | delivered
    public string Addressee { get; set; } = null!;
    public string Purpose { get; set; } = null!;
    public string? VerificationCode { get; set; }   // 8-char code printed on the document
    public string? DocumentUrl { get; set; }
    public string? TemplateBody { get; set; }       // rendered markdown/pdf source
}

/// <summary>HRM-053: Anonymous protected-disclosure intake. The reporter gets a
/// case reference and a secret access code; identity is never stored.</summary>
public class ProtectedDisclosure : Entity
{
    public string CaseReference { get; set; } = null!; // e.g. SD-2026-00001
    public string AccessCode { get; set; } = null!;    // only the reporter knows this
    public string Category { get; set; } = null!;      // financial-misconduct | harassment | safety | discrimination | other
    public string Severity { get; set; } = "medium";
    public string Description { get; set; } = null!;
    public string Status { get; set; } = "new";        // new | triage | investigating | resolved | dismissed
    public string? TriageNotes { get; set; }           // restricted: investigator workspace only
    public Guid? AssignedToId { get; set; }            // investigator worker id; conflict-safe assignment
    public string? Outcome { get; set; }
}

/// <summary>HRM-006: Employee document in the digital file.</summary>
public class WorkerDocument : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string Category { get; set; } = null!;      // contract | id | qualification | medical | certificate | letter | evidence
    public string Title { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = "application/pdf";
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = null!;
    public string Classification { get; set; } = "internal"; // internal | confidential | restricted
    public DateOnly? ExpiryDate { get; set; }
    public bool IsLatest { get; set; } = true;
}
