using System.Text.Json;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Workflow;

/// <summary>HRM-005: Sequential approval engine with maker-checker semantics.
/// Submitted items go to the subject's manager first (or their active delegate);
/// managers can approve, return (with note) or reject (with reason), or delegate.
/// Approved requests trigger effect application (e.g. leave approval finalizes
/// the balance reservation; letter approval renders the template). Overdue items
/// can be escalated up the reporting chain.</summary>
public sealed class WorkflowServiceImpl(IWorkflowRepository repo, IAuthzService authz, ILeaveEffectApplier effects) : IWorkflowService
{
    // draft -> submitted -> in-review -> approved | returned | rejected | cancelled
    private static readonly Dictionary<string, HashSet<string>> Transitions = new()
    {
        ["draft"] = new() { "submitted", "cancelled" },
        ["submitted"] = new() { "in-review", "approved", "rejected", "cancelled" },
        ["in-review"] = new() { "approved", "returned", "rejected", "cancelled" },
        ["returned"] = new() { "submitted", "cancelled" },
        ["approved"] = new() { "cancelled" },
        ["rejected"] = new() { },
        ["cancelled"] = new() { },
    };

    public async Task<WorkflowRequest> OpenAsync(string workflowType, Guid subjectId, Guid? subjectWorkerId, string payloadJson, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager");
        var approver = await ResolveApproverAsync(subjectWorkerId, workflowType, ct);
        var req = new WorkflowRequest
        {
            WorkflowType = workflowType,
            SubjectWorkerId = subjectWorkerId,
            PayloadJson = payloadJson,
            Status = approver.HasValue ? "in-review" : "submitted", // no manager => auto-routed to HR queue
            CurrentApproverId = approver,
            DueAt = DateTimeOffset.UtcNow.AddDays(3),
        };
        return await repo.CreateRequestAsync(req, ct);
    }

    public async Task<WorkflowRequest> DecideAsync(Guid requestId, Guid actorId, WorkflowDecisionRequest decision, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll");
        var req = await repo.GetRequestAsync(requestId, ct)
            ?? throw new DomainException("workflow-request-not-found", $"Request {requestId} does not exist.");

        // the assigned approver, their active delegate, or an admin may act
        var isDelegate = await repo.IsDelegateForAsync(decision.DelegatedToId.HasValue ? actorId : actorId, actorId,
            req.WorkflowType, DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date), ct);
        if (req.CurrentApproverId != actorId && !isDelegate && !authz.CanAccessSensitive("admin"))
            throw new DomainException("workflow-not-approver", "You are not the assigned approver for this item.");

        var allowed = Transitions.GetValueOrDefault(req.Status, []);
        if (!allowed.Contains(decision.Action switch { "approve" => "approved", "return" => "returned", "reject" => "rejected", "delegate" => "in-review", _ => "" }))
            throw new DomainException("workflow-invalid-transition", $"Cannot {decision.Action} a request in status {req.Status}.");
        if (decision.Action == "reject" && string.IsNullOrWhiteSpace(decision.Reason))
            throw new DomainException("workflow-reject-requires-reason", "A rejection reason is required.");

        req.Decisions.Add(new WorkflowDecision { ActorId = actorId, Action = decision.Action, Reason = decision.Reason, DelegatedToId = decision.DelegatedToId });
        if (decision.Action == "delegate")
        {
            req.CurrentApproverId = decision.DelegatedToId;
        }
        else
        {
            req.Status = decision.Action switch { "approve" => "approved", "return" => "returned", "reject" => "rejected", _ => req.Status };
            req.RejectionReason = decision.Action == "reject" ? decision.Reason : req.RejectionReason;
            req.ReturnNote = decision.Action == "return" ? decision.Reason : req.ReturnNote;
            req.CurrentApproverId = decision.Action == "return" ? req.SubjectWorkerId : null;
        }
        var updated = await repo.UpdateRequestAsync(req, ct);
        await effects.ApplyAsync(updated, decision.Action, ct);
        return updated;
    }

    /// <summary>HRM-005 escalation: an overdue or stuck item is reassigned to the
    /// current approver's manager (or to HR if the chain ends). The actor must be
    /// an admin or HR staff, or the assigned approver themselves.</summary>
    public async Task<WorkflowRequest> EscalateAsync(Guid requestId, Guid actorId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var req = await repo.GetRequestAsync(requestId, ct)
            ?? throw new DomainException("workflow-request-not-found", $"Request {requestId} does not exist.");
        if (req.Status is "approved" or "rejected" or "cancelled")
            throw new DomainException("workflow-already-final", "Cannot escalate a request that is already in a final state.");
        if (req.CurrentApproverId != actorId && !authz.CanAccessSensitive("admin"))
            throw new DomainException("workflow-not-approver", "Only the assigned approver or HR staff can escalate this item.");
        var current = req.CurrentApproverId;
        var next = current.HasValue ? await repo.FindManagerOfAsync(current.Value, ct) : null;
        // if the chain ends at the same level or above, fall back to the HR queue
        req.CurrentApproverId = next != current ? next : null;
        req.Status = req.CurrentApproverId.HasValue ? "in-review" : "submitted";
        req.EscalatedAt = DateTimeOffset.UtcNow;
        req.DueAt = DateTimeOffset.UtcNow.AddDays(3);
        return await repo.UpdateRequestAsync(req, ct);
    }

    public async Task<Paged<WorkQueueItemDto>> GetWorkQueueAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll");
        var (items, total) = await repo.ListOpenRequestsAsync(ct);
        var ids = items.Where(r => r.SubjectWorkerId.HasValue || r.CurrentApproverId.HasValue)
            .SelectMany(r => new[] { r.SubjectWorkerId, r.CurrentApproverId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var names = ids.Count == 0 ? new Dictionary<Guid, string>() : await repo.GetWorkerNamesAsync(ids, ct);
        return new Paged<WorkQueueItemDto>(items.Select(r => new WorkQueueItemDto(
            r.Id, r.WorkflowType, r.Status, r.SubjectWorkerId,
            r.SubjectWorkerId is null ? null : names.GetValueOrDefault(r.SubjectWorkerId.Value),
            r.CurrentApproverId is null ? null : names.GetValueOrDefault(r.CurrentApproverId.Value),
            r.DueAt, r.DueAt < DateTimeOffset.UtcNow, r.CreatedAt)).ToList(), total, 1, 50);
    }

    public async Task<WorkflowRequestDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll", "employee");
        var req = await repo.GetRequestAsync(id, ct);
        if (req is null) return null;
        var ids = new List<Guid>();
        if (req.SubjectWorkerId.HasValue) ids.Add(req.SubjectWorkerId.Value);
        if (req.CurrentApproverId.HasValue) ids.Add(req.CurrentApproverId.Value);
        ids.AddRange(req.Decisions.Where(d => d.ActorId != Guid.Empty).Select(d => d.ActorId));
        ids.AddRange(req.Decisions.Where(d => d.DelegatedToId.HasValue).Select(d => d.DelegatedToId!.Value));
        var distinct = ids.Distinct().ToList();
        var names = distinct.Count == 0 ? new Dictionary<Guid, string>() : await repo.GetWorkerNamesAsync(distinct, ct);
        return new WorkflowRequestDto(req.Id, req.WorkflowType, req.SubjectWorkerId,
            req.SubjectWorkerId is null ? null : names.GetValueOrDefault(req.SubjectWorkerId.Value),
            req.Status, req.PayloadJson, req.RejectionReason, req.ReturnNote, req.CurrentApproverId,
            req.CurrentApproverId is null ? null : names.GetValueOrDefault(req.CurrentApproverId.Value),
            req.DueAt, req.EscalatedAt, req.CreatedAt,
            req.Decisions.Select(d => new WorkflowDecisionDto(d.Id, d.RequestId, d.ActorId,
                names.GetValueOrDefault(d.ActorId), d.Action, d.Reason, d.DelegatedToId,
                d.DelegatedToId is null ? null : names.GetValueOrDefault(d.DelegatedToId.Value),
                d.CreatedAt)).ToList());
    }

    public Task ApplyDecisionEffectsAsync(WorkflowRequest request, CancellationToken ct) => effects.ApplyAsync(request, "approve", ct);

    /// <summary>Resolves the first approver for a new request: the subject's
    /// active delegate's delegator is skipped — if a delegate currently covers
    /// the manager, the delegate receives the item.</summary>
    private async Task<Guid?> ResolveApproverAsync(Guid? subjectWorkerId, string workflowType, CancellationToken ct)
    {
        var manager = await repo.FindManagerOfAsync(subjectWorkerId ?? Guid.Empty, ct);
        if (!manager.HasValue || manager.Value == Guid.Empty) return null;
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
        var delegation = await repo.GetActiveDelegationForAsync(manager.Value, workflowType, today, ct);
        return delegation ?? manager;
    }
}

/// <summary>Post-approval side effects per workflow type (convert or release
/// balance reservations, execute movements, render approved letters).</summary>
public interface ILeaveEffectApplier
{
    Task ApplyAsync(WorkflowRequest request, string decisionAction, CancellationToken ct);
}

public interface IWorkflowRepository
{
    Task<WorkflowRequest> CreateRequestAsync(WorkflowRequest request, CancellationToken ct);
    Task<WorkflowRequest?> GetRequestAsync(Guid id, CancellationToken ct);
    Task<WorkflowRequest> UpdateRequestAsync(WorkflowRequest request, CancellationToken ct);
    Task<(List<WorkflowRequest> Items, int Total)> ListOpenRequestsAsync(CancellationToken ct);
    Task<Guid?> FindManagerOfAsync(Guid workerId, CancellationToken ct);
    /// <summary>True when <c>actorId</c> is an active delegate of <c>delegatorId</c>
    /// for the given workflow type on <c>date</c> (scope null = covers all).</summary>
    Task<bool> IsDelegateForAsync(Guid delegatorId, Guid actorId, string workflowType, DateOnly date, CancellationToken ct);
    /// <summary>Active delegation covering <c>delegatorId</c> on <c>date</c> for the
    /// given type, or null. Returned delegate worker id.</summary>
    Task<Guid?> GetActiveDelegationForAsync(Guid delegatorId, string workflowType, DateOnly date, CancellationToken ct);
    /// <summary>Display names for a set of workers (work-queue enrichment).</summary>
    Task<Dictionary<Guid, string>> GetWorkerNamesAsync(IEnumerable<Guid> ids, CancellationToken ct);
}
