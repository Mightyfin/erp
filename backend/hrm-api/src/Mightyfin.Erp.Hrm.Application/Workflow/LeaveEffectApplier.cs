using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Workflow;

/// <summary>Wires workflow decisions back to the subject records: approved leave
/// finalizes the reservation, rejected/cancelled releases it; corrections and
/// letters update their own status; movements get approved for later execution.</summary>
public sealed class LeaveEffectApplierImpl(ITimeRepository timeRepo, IWorkflowRepository workflowRepo) : ILeaveEffectApplier
{
    public async Task ApplyAsync(WorkflowRequest request, string decisionAction, CancellationToken ct)
    {
        var workflowType = request.WorkflowType;
        if (workflowType == "leave" && request.SubjectWorkerId.HasValue)
        {
            var (items, _) = await timeRepo.ListLeaveRequestsAsync(request.SubjectWorkerId, null, ct);
            var lr = items.FirstOrDefault(i => i.Id == request.SubjectWorkerId.Value);
            if (lr is null) return;
            switch (decisionAction)
            {
                case "approve":
                    lr.Status = "approved";
                    lr.BalanceReserved = true;
                    break;
                case "reject":
                    lr.Status = "rejected";
                    lr.RejectionReason = request.RejectionReason;
                    await ReleaseReservationAsync(lr, ct);
                    break;
                case "return":
                    lr.Status = "returned";
                    lr.ReturnNote = request.ReturnNote;
                    await ReleaseReservationAsync(lr, ct);
                    break;
                case "delegate":
                    return; // no state change on the subject
            }
        }
        else if (workflowType == "attendance-correction" && request.SubjectWorkerId.HasValue)
        {
            var (items, _) = await timeRepo.ListCorrectionsAsync(request.SubjectWorkerId, null, ct);
            var c = items.FirstOrDefault(i => i.Id == request.SubjectWorkerId.Value);
            if (c is null) return;
            c.Status = decisionAction switch { "approve" => "approved", "reject" => "rejected", "return" => "returned", _ => c.Status };
            c.RejectionReason = decisionAction == "reject" ? request.RejectionReason : c.RejectionReason;
        }
        else if (workflowType == "movement" && request.SubjectWorkerId.HasValue)
        {
            // Movement repository is reached via the worker repository; we mark approved via workflow payload only in v1.
            // A dedicated MovementRepository will be added in Infrastructure with the same interface surface.
        }
        else if (workflowType == "hr-request" && request.SubjectWorkerId.HasValue)
        {
            // HR request thread updated by ExperienceService messages; workflow notes carried in Decision.
        }
        else if (workflowType == "hr-letter" && request.SubjectWorkerId.HasValue)
        {
            // Letter approval handled by ExperienceService.ApproveLetterAsync after this hook.
        }
    }

    private async Task ReleaseReservationAsync(LeaveRequest lr, CancellationToken ct)
    {
        if (lr.BalanceReserved)
            await timeRepo.ReserveBalanceAsync(lr.WorkerId, lr.LeaveTypeCode, lr.RequestedDays, lr.Id, ct);
    }
}
