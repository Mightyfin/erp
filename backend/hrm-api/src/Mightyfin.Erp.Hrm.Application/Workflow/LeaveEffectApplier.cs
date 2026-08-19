using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Workflow;

/// <summary>Wires workflow decisions back to the subject records: approved leave
/// converts the open balance reservation to a permanent deduction; rejected or
/// returned leave releases the reservation; approved letters get rendered from
/// their templates; movements and HR requests are marked approved at this stage
/// (execution of movements is handled by the movement service).</summary>
public sealed class LeaveEffectApplierImpl(
    ITimeRepository timeRepo,
    ILetterTemplates templates,
    IExperienceRepository experienceRepo,
    IMergeDataProvider merge,
    IOutboxWriter? outbox = null) : ILeaveEffectApplier
{
    public async Task ApplyAsync(WorkflowRequest request, string decisionAction, CancellationToken ct)
    {
        var workflowType = request.WorkflowType;
        if (workflowType == "leave" && request.SubjectWorkerId.HasValue)
        {
            var (items, _) = await timeRepo.ListLeaveRequestsAsync(request.SubjectWorkerId, null, ct);
            var lr = items.FirstOrDefault(i => i.WorkerId == request.SubjectWorkerId.Value && i.Status == "submitted");
            if (lr is null) return;
            switch (decisionAction)
            {
                case "approve":
                    lr.Status = "approved";
                    lr.BalanceReserved = true;
                    await timeRepo.ConvertReservationAsync(lr.Id, ct);
                    break;
                case "reject":
                    lr.Status = "rejected";
                    lr.RejectionReason = request.RejectionReason;
                    await timeRepo.ReleaseReservationAsync(lr.Id, ct);
                    break;
                case "return":
                    lr.Status = "returned";
                    lr.ReturnNote = request.ReturnNote;
                    await timeRepo.ReleaseReservationAsync(lr.Id, ct);
                    break;
                case "delegate":
                    return; // no state change on the subject
            }
            if (outbox is not null && lr.Worker is not null)
            {
                await outbox.EnqueueAsync(
                    HrmEventTypes.LeaveDecided,
                    lr.Worker.SubjectId ?? lr.Worker.Id.ToString("D"),
                    new
                    {
                        leave_id = lr.Id.ToString("D"),
                        worker_id = lr.Worker.Id.ToString("D"),
                        leave_type_code = lr.LeaveTypeCode,
                        start_date = lr.StartDate.ToString("yyyy-MM-dd"),
                        end_date = lr.EndDate.ToString("yyyy-MM-dd"),
                        status = lr.Status,
                        email = lr.Worker.Email ?? "",
                        first_name = lr.Worker.FirstName,
                        last_name = lr.Worker.LastName,
                    },
                    ct);
            }
        }
        else if (workflowType == "leave-encashment" && request.SubjectWorkerId.HasValue)
        {
            // M41 Gap 6a: approving an encashment posts a permanent ledger
            // deduction so the converted days leave the available balance; the
            // request record (rate, gross amount) was already quoted at
            // submission time so rejection/cancellation touch nothing else.
            var (items, _) = await timeRepo.ListEncashmentsAsync(request.SubjectWorkerId, null, ct);
            var enc = items.FirstOrDefault(i => i.Status == "submitted");
            if (enc is null) return;
            switch (decisionAction)
            {
                case "approve":
                    enc.Status = "approved";
                    enc.DecisionReason = request.RejectionReason ?? enc.DecisionReason;
                    var ledger = await timeRepo.AddLedgerEntryAsync(new LeaveBalanceLedger
                    {
                        WorkerId = enc.WorkerId,
                        LeaveTypeCode = enc.LeaveTypeCode,
                        Days = -enc.Days,
                        Reason = "encashment",
                        ReferenceId = enc.Id,
                        ReferenceType = "encashment",
                        Note = $"Leave encashment approved — {enc.Days} day(s) at {enc.GrossAmount} (quoted rate {enc.MonthlyRate}/month)",
                    }, ct);
                    enc.LedgerEntryId = ledger.Id;
                    break;
                case "reject":
                    enc.Status = "rejected";
                    enc.DecisionReason = request.RejectionReason;
                    break;
                case "return":
                    enc.Status = "returned";
                    enc.DecisionReason = request.ReturnNote;
                    break;
                case "cancel":
                    enc.Status = "cancelled";
                    break;
                case "delegate":
                    return; // no state change on the subject
            }
            // attribute the decision to the most recent decider's actor id;
            // the decider's linked worker (if any) carries the subject id used
            // for attribution and downstream notifications.
            var lastDecision = request.Decisions
                .OrderByDescending(d => d.CreatedAt).FirstOrDefault();
            if (lastDecision?.ActorId is not null && lastDecision.ActorId != default)
                enc.DecidedBySubjectId = lastDecision.ActorId.ToString("D");
            enc.DecidedAt = enc.DecidedAt ?? DateTimeOffset.UtcNow;
            await timeRepo.UpdateEncashmentAsync(enc, ct);
        }
        else if (workflowType == "attendance-correction" && request.SubjectWorkerId.HasValue)
        {
            var (items, _) = await timeRepo.ListCorrectionsAsync(request.SubjectWorkerId, null, ct);
            var c = items.FirstOrDefault(i => i.WorkerId == request.SubjectWorkerId.Value && i.Status == "submitted");
            if (c is null) return;
            c.Status = decisionAction switch { "approve" => "approved", "reject" => "rejected", "return" => "returned", _ => c.Status };
            c.RejectionReason = decisionAction == "reject" ? request.RejectionReason : c.RejectionReason;
            if (decisionAction == "approve")
            {
                // apply the proposed values to the underlying attendance record (or create one)
                var existing = await timeRepo.GetAttendanceAsync(c.WorkerId, c.WorkDate, ct);
                if (existing is null)
                {
                    await timeRepo.CreateAttendanceAsync(new AttendanceRecord
                    {
                        WorkerId = c.WorkerId,
                        WorkDate = c.WorkDate,
                        ClockIn = c.ProposedClockIn,
                        ClockOut = c.ProposedClockOut,
                        Source = "corrected",
                        DerivedStatus = DeriveStatus(c.ProposedClockIn, c.ProposedClockOut, c.ProposedStatus),
                        TotalHours = c.ProposedClockIn.HasValue && c.ProposedClockOut.HasValue
                            ? (decimal)(c.ProposedClockOut.Value - c.ProposedClockIn.Value).TotalHours : 0,
                    }, ct);
                }
                else
                {
                    if (c.ProposedClockIn.HasValue) existing.ClockIn = c.ProposedClockIn;
                    if (c.ProposedClockOut.HasValue) existing.ClockOut = c.ProposedClockOut;
                    if (c.ProposedStatus is not null) existing.DerivedStatus = c.ProposedStatus;
                    existing.Source = "corrected";
                    if (existing.ClockIn.HasValue && existing.ClockOut.HasValue)
                        existing.TotalHours = (decimal)(existing.ClockOut.Value - existing.ClockIn.Value).TotalHours;
                    await timeRepo.UpdateAttendanceAsync(existing, ct);
                }
            }
        }
        else if (workflowType == "movement" && request.SubjectWorkerId.HasValue)
        {
            // Movement execution (applying To fields as future/current assignments)
            // lands in M4 as part of the approval rails; the movement service owns it.
        }
        else if (workflowType == "hr-letter" && request.SubjectWorkerId.HasValue)
        {
            // Advance the letter that opened this workflow request (UI-XPR-003):
            // approval renders the final template and marks the letter generated;
            // rejection or return drops it back to draft for correction.
            var subjectId = request.SubjectWorkerId.Value;
            var (letters, _) = await experienceRepo.ListLettersAsync(subjectId, null, ct);
            var letter = letters.FirstOrDefault(l => l.WorkerId == subjectId && l.Status == "pending-approval");
            if (letter is null) return;
            switch (decisionAction)
            {
                case "approve":
                    var data = await merge.GetMergeDataAsync(letter.WorkerId, letter.LetterType, ct);
                    letter.TemplateBody = templates.Render(letter.LetterType, new LetterMergeContext(
                        data.WorkerFullName, data.EmployeeNo, data.JobTitle, data.Grade, data.StartDate,
                        data.LegalEntityName, letter.Addressee, letter.Purpose,
                        DateTimeOffset.UtcNow.ToString("d MMMM yyyy"), letter.VerificationCode!,
                        data.BasicSalaryMonthly, data.ReferenceText));
                    letter.Status = "generated";
                    await experienceRepo.UpdateLetterAsync(letter, ct);
                    break;
                case "reject":
                    letter.Status = "draft";
                    await experienceRepo.UpdateLetterAsync(letter, ct);
                    break;
                case "return":
                    letter.Status = "draft";
                    await experienceRepo.UpdateLetterAsync(letter, ct);
                    break;
                case "delegate":
                    return; // no state change on the subject
            }
        }
        else if (workflowType == "hr-request" && request.SubjectWorkerId.HasValue)
        {
            // Advance the HR request that opened this workflow request: approval
            // resolves the request; rejection or return reopens it for revision.
            var subjectId = request.SubjectWorkerId.Value;
            var (items, _) = await experienceRepo.ListRequestsAsync(subjectId, null, ct);
            var hr = items.FirstOrDefault(i => i.WorkerId == subjectId && i.Status == "open");
            if (hr is null) return;
            switch (decisionAction)
            {
                case "approve":
                    hr.Status = "resolved";
                    await experienceRepo.UpdateRequestAsync(hr, ct);
                    break;
                case "reject":
                    hr.Status = "closed";
                    await experienceRepo.UpdateRequestAsync(hr, ct);
                    break;
                case "return":
                    hr.Status = "open";
                    await experienceRepo.UpdateRequestAsync(hr, ct);
                    break;
                case "delegate":
                    return; // no state change on the subject
            }
        }
    }

    /// <summary>Duplicate of the status derivation used by the time service —
    /// kept small so the effect applier can create corrected attendance records.</summary>
    private static string DeriveStatus(TimeOnly? clockIn, TimeOnly? clockOut, string? proposedStatus)
    {
        if (proposedStatus is not null) return proposedStatus;
        if (clockIn.HasValue && clockOut.HasValue) return "done";
        if (clockIn.HasValue) return "in";
        return "absent";
    }
}

/// <summary>Built-in letter templates with placeholder merge. Templates are
/// markdown snippets with {placeholder} tokens filled from worker data at
/// render time.</summary>
public interface ILetterTemplates
{
    string Render(string letterType, LetterMergeContext ctx);
}

/// <summary>Worker and employment data merged into a letter template.</summary>
public sealed record LetterMergeContext(
    string WorkerFullName, string EmployeeNo, string? JobTitle, string? Grade,
    DateOnly? StartDate, string? LegalEntityName, string Addressee, string Purpose,
    string DateText, string VerificationCode, decimal? BasicSalaryMonthly, string? ReferenceText);
