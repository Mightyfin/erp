using System.Text.Json;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Workflow;

/// <summary>HRM-005: Sequential approval engine with maker-checker semantics.
/// Submitted items go to the subject's manager first; managers can approve,
/// return (with note) or reject (with reason), or delegate. Approvals of
/// approved requests trigger effect application (e.g. leave approved finalizes
/// the balance reservation).</summary>
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
        var approver = await repo.FindManagerOfAsync(subjectWorkerId ?? Guid.Empty, ct);
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
        if (req.CurrentApproverId != actorId && !authz.CanAccessSensitive("admin"))
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

    public async Task<Paged<WorkQueueItemDto>> GetWorkQueueAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll");
        var (items, total) = await repo.ListOpenRequestsAsync(ct);
        return new Paged<WorkQueueItemDto>(items.Select(r => new WorkQueueItemDto(
            r.Id, r.WorkflowType, r.Status, r.SubjectWorkerId, null, null,
            r.DueAt, r.DueAt < DateTimeOffset.UtcNow, r.CreatedAt)).ToList(), total, 1, 50);
    }

    public Task<WorkflowRequest?> GetByIdAsync(Guid id, CancellationToken ct) => repo.GetRequestAsync(id, ct);

    public Task ApplyDecisionEffectsAsync(WorkflowRequest request, CancellationToken ct) => effects.ApplyAsync(request, "approve", ct);
}

/// <summary>Post-approval side effects per workflow type (reserve/release balances,
/// execute movements, release letters). Kept in Application so the engine stays
/// generic; infra registers implementations per type.</summary>
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
}
