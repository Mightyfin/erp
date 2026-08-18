using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Offboarding;

public class OffboardingServiceImpl(
    IOffboardingRepository repo,
    IAuthzService authz,
    IUnitOfWork? unitOfWork = null) : IOffboardingService
{
    private static readonly string[] AdminRoles = ["hr_ops", "hr_admin", "manager"];

    public async Task<List<OffboardingRequestDto>> ListRequestsAsync(string? status, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var items = await repo.ListRequestsAsync(status, ct);
        return items.Select(MapRequest).ToList();
    }

    public async Task<OffboardingRequestDto> GetRequestAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        return MapRequest(await repo.GetRequestAsync(id, ct)
            ?? throw new DomainException("offboarding-not-found", $"Offboarding request {id} does not exist."));
    }

    public async Task<OffboardingRequestDto> CreateRequestAsync(OffboardingRequestCreate request, string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var existing = await repo.GetActiveForWorkerAsync(request.WorkerId, ct);
        if (existing is not null)
            throw new DomainException("offboarding-active", "This worker already has an active offboarding request.");

        var worker = await repo.FindWorkerAsync(request.WorkerId, ct)
            ?? throw new DomainException("worker-not-found", $"Worker {request.WorkerId} does not exist.");

        var noticeStart = DateOnly.Parse(request.NoticeStartDate);
        var lastDay = DateOnly.Parse(request.LastWorkingDay);
        if (lastDay <= noticeStart)
            throw new DomainException("offboarding-invalid-dates", "Last working day must be after the notice start date.");

        var record = new OffboardingRequest
        {
            WorkerId = request.WorkerId,
            RequestType = request.RequestType,
            Reason = request.Reason.Trim(),
            AdditionalNotes = request.AdditionalNotes,
            NoticeStartDate = noticeStart,
            LastWorkingDay = lastDay,
            Status = "requested",
        };
        record.ChecklistItems = DefaultChecklistItems(record);
        record.ChecklistItemsTotal = record.ChecklistItems.Count;
        record.ChecklistItemsCompleted = 0;

        var created = await repo.CreateRequestAsync(record, ct);
        return MapRequest(created);
    }

    public async Task<OffboardingRequestDto> ApproveRequestAsync(Guid id, string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var record = await repo.GetRequestAsync(id, ct)
            ?? throw new DomainException("offboarding-not-found", $"Offboarding request {id} does not exist.");
        if (record.Status != "requested")
            throw new DomainException("offboarding-invalid-transition", "Only pending requests can be approved.");

        record.Status = "approved";
        record.ApprovedBy = subjectId;
        record.ApprovedAt = DateTimeOffset.UtcNow;
        record.ApproverName = ResolveActorName(subjectId);
        return MapRequest(await repo.UpdateRequestAsync(record, ct));
    }

    public async Task<OffboardingRequestDto> RejectRequestAsync(Guid id, string reason, string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var record = await repo.GetRequestAsync(id, ct)
            ?? throw new DomainException("offboarding-not-found", $"Offboarding request {id} does not exist.");
        if (record.Status != "requested")
            throw new DomainException("offboarding-invalid-transition", "Only pending requests can be rejected.");

        record.Status = "cancelled";
        record.RejectionReason = reason.Trim();
        record.ApprovedBy = subjectId;
        record.ApprovedAt = DateTimeOffset.UtcNow;
        record.ApproverName = ResolveActorName(subjectId);
        return MapRequest(await repo.UpdateRequestAsync(record, ct));
    }

    public async Task<OffboardingRequestDto> CancelRequestAsync(Guid id, string reason, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var record = await repo.GetRequestAsync(id, ct)
            ?? throw new DomainException("offboarding-not-found", $"Offboarding request {id} does not exist.");
        if (record.Status == "completed")
            throw new DomainException("offboarding-already-completed", "Cannot cancel a completed offboarding.");

        record.Status = "cancelled";
        record.CancelledReason = reason.Trim();
        return MapRequest(await repo.UpdateRequestAsync(record, ct));
    }

    public async Task<OffboardingRequestDto> MarkFinalPayProcessedAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var record = await repo.GetRequestAsync(id, ct)
            ?? throw new DomainException("offboarding-not-found", $"Offboarding request {id} does not exist.");
        if (record.Status != "completed")
            throw new DomainException("offboarding-not-complete", "Only completed offboardings can be marked as final-pay-processed.");

        record.IsFinalPayProcessed = true;
        return MapRequest(await repo.UpdateRequestAsync(record, ct));
    }

    public async Task<ChecklistItemDto> AddChecklistItemAsync(Guid requestId, ChecklistItemCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var record = await repo.GetRequestAsync(requestId, ct)
            ?? throw new DomainException("offboarding-not-found", $"Offboarding request {requestId} does not exist.");

        var item = await repo.CreateChecklistItemAsync(new OffboardingChecklistItem
        {
            OffboardingRequestId = requestId,
            Title = request.Title.Trim(),
            Description = request.Description,
            Owner = request.Owner,
            SortOrder = request.SortOrder,
        }, ct);
        record.ChecklistItemsTotal = record.ChecklistItems.Count;
        await repo.UpdateRequestAsync(record, ct);
        return MapChecklistItem(item);
    }

    public async Task<ChecklistItemDto> UpdateChecklistItemAsync(Guid requestId, Guid itemId, ChecklistItemUpdate request, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var item = await repo.GetChecklistItemAsync(itemId, ct)
            ?? throw new DomainException("offboarding-checklist-not-found", $"Checklist item {itemId} does not exist.");
        if (item.OffboardingRequestId != requestId)
            throw new DomainException("offboarding-checklist-not-found", "Checklist item does not belong to this request.");

        if (request.Title is not null) item.Title = request.Title.Trim();
        if (request.Description is not null) item.Description = request.Description;
        if (request.Owner is not null) item.Owner = request.Owner;
        return MapChecklistItem(await repo.UpdateChecklistItemAsync(item, ct));
    }

    public async Task<ChecklistItemDto> CompleteChecklistItemAsync(Guid itemId, string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var item = await repo.GetChecklistItemAsync(itemId, ct)
            ?? throw new DomainException("offboarding-checklist-not-found", $"Checklist item {itemId} does not exist.");

        if (item.IsCompleted)
            throw new DomainException("offboarding-checklist-already-done", "This checklist item is already completed.");

        item.IsCompleted = true;
        item.CompletedBy = subjectId;
        item.CompletedAt = DateTimeOffset.UtcNow;
        var updated = await repo.UpdateChecklistItemAsync(item, ct);

        // Check if all items are done and auto-advance the request status
        var record = await repo.GetRequestAsync(item.OffboardingRequestId, ct);
        if (record is not null)
        {
            record.ChecklistItemsCompleted = record.ChecklistItems.Count(x => x.IsCompleted);
            record.ChecklistItemsTotal = record.ChecklistItems.Count;
            if (record.ChecklistItemsCompleted == record.ChecklistItemsTotal && record.Status == "approved")
            {
                record.Status = "completed";
            }
            await repo.UpdateRequestAsync(record, ct);
        }
        return MapChecklistItem(updated);
    }

    public async Task<ExitInterviewDto> CreateExitInterviewAsync(Guid requestId, ExitInterviewCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var record = await repo.GetRequestAsync(requestId, ct)
            ?? throw new DomainException("offboarding-not-found", $"Offboarding request {requestId} does not exist.");
        var existing = await repo.GetExitInterviewAsync(requestId, ct);
        if (existing is not null)
            throw new DomainException("exit-interview-exists", "An exit interview already exists for this offboarding request.");

        var interview = await repo.CreateExitInterviewAsync(new ExitInterview
        {
            OffboardingRequestId = requestId,
            WorkerId = record.WorkerId,
            ReasonForLeaving = request.ReasonForLeaving,
            ReasonDetails = request.ReasonDetails,
            WhatWentWell = request.WhatWentWell,
            WhatCouldImprove = request.WhatCouldImprove,
            WouldRecommend = request.WouldRecommend,
            Status = "pending",
        }, ct);
        return MapExitInterview(interview);
    }

    public async Task<ExitInterviewDto> GetExitInterviewAsync(Guid requestId, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var interview = await repo.GetExitInterviewAsync(requestId, ct)
            ?? throw new DomainException("exit-interview-not-found", $"No exit interview for offboarding request {requestId}.");
        return MapExitInterview(interview);
    }

    public async Task<ExitInterviewDto> UpdateExitInterviewAsync(Guid requestId, ExitInterviewUpdate request, CancellationToken ct)
    {
        authz.RequireAnyRole([.. AdminRoles]);
        var interview = await repo.GetExitInterviewAsync(requestId, ct)
            ?? throw new DomainException("exit-interview-not-found", $"No exit interview for offboarding request {requestId}.");

        if (request.ReasonForLeaving is not null) interview.ReasonForLeaving = request.ReasonForLeaving;
        if (request.ReasonDetails is not null) interview.ReasonDetails = request.ReasonDetails;
        if (request.WhatWentWell is not null) interview.WhatWentWell = request.WhatWentWell;
        if (request.WhatCouldImprove is not null) interview.WhatCouldImprove = request.WhatCouldImprove;
        if (request.WouldRecommend is not null) interview.WouldRecommend = request.WouldRecommend;
        if (request.ManagerFeedback is not null) interview.ManagerFeedback = request.ManagerFeedback;
        if (request.HrmNotes is not null) interview.HrmNotes = request.HrmNotes;
        if (request.InterviewedBy is not null) interview.InterviewedBy = request.InterviewedBy;
        if (request.Status is not null)
        {
            interview.Status = request.Status;
            if (request.Status == "conducted")
                interview.InterviewedAt = DateTimeOffset.UtcNow;
        }
        return MapExitInterview(await repo.UpdateExitInterviewAsync(interview, ct));
    }

    public async Task<OffboardingRequestDto?> GetMyOffboardingAsync(string subjectId, CancellationToken ct)
    {
        // Workers can see their own offboarding status
        var request = await FindForSubjectAsync(subjectId, ct);
        return request is null ? null : MapRequest(request);
    }

    public async Task<OffboardingRequestDto> SubmitMyResignationAsync(OffboardingRequestCreate request, string subjectId, CancellationToken ct)
    {
        // Worker self-service: submit own resignation
        var worker = await FindWorkerForSubjectAsync(subjectId, ct)
            ?? throw new DomainException("worker-not-linked", "Your account is not linked to a worker record.");

        // Verify the subject is acting on their own record
        if (worker.SubjectId != subjectId)
            throw new DomainException("worker-not-authorized", "You can only submit offboarding for yourself.");

        var existing = await repo.GetActiveForWorkerAsync(worker.Id, ct);
        if (existing is not null)
            throw new DomainException("offboarding-active", "You already have an active offboarding request.");

        var noticeStart = DateOnly.Parse(request.NoticeStartDate);
        var lastDay = DateOnly.Parse(request.LastWorkingDay);
        if (lastDay <= noticeStart)
            throw new DomainException("offboarding-invalid-dates", "Last working day must be after the notice start date.");

        var record = new OffboardingRequest
        {
            WorkerId = worker.Id,
            RequestType = "resignation",
            Reason = request.Reason.Trim(),
            AdditionalNotes = request.AdditionalNotes,
            NoticeStartDate = noticeStart,
            LastWorkingDay = lastDay,
            Status = "requested",
        };
        record.ChecklistItems = DefaultChecklistItems(record);
        record.ChecklistItemsTotal = record.ChecklistItems.Count;

        return MapRequest(await repo.CreateRequestAsync(record, ct));
    }

    private async Task<OffboardingRequest?> FindForSubjectAsync(string subjectId, CancellationToken ct)
    {
        var worker = await repo.FindWorkerBySubjectIdAsync(subjectId, ct);
        if (worker is null) return null;
        return await repo.GetActiveForWorkerAsync(worker.Id, ct);
    }

    private async Task<Worker?> FindWorkerForSubjectAsync(string subjectId, CancellationToken ct)
    {
        return await repo.FindWorkerBySubjectIdAsync(subjectId, ct);
    }

    private string ResolveActorName(string subjectId) => subjectId;

    private static List<OffboardingChecklistItem> DefaultChecklistItems(OffboardingRequest record) =>
    [
        new() { Title = "Revoke system access and accounts", Description = "Disable email, VPN, ERP, and all system accounts", Owner = "it", SortOrder = 0 },
        new() { Title = "Return equipment and assets", Description = "Laptop, phone, keys, ID badge, etc.", Owner = "hr", SortOrder = 1 },
        new() { Title = "Complete knowledge transfer", Description = "Document processes and hand over to successor", Owner = "manager", SortOrder = 2 },
        new() { Title = "Process final payroll", Description = "Calculate and process final pay including leave encashment", Owner = "finance", SortOrder = 3 },
        new() { Title = "Conduct exit interview", Description = "Record departing worker feedback", Owner = "hr", SortOrder = 4 },
        new() { Title = "Collect outstanding obligations", Description = "Return advances, clear debts, settle allowances", Owner = "finance", SortOrder = 5 },
    ];

    // Mappers
    private static OffboardingRequestDto MapRequest(OffboardingRequest r) => new(
        Id: r.Id,
        WorkerId: r.WorkerId,
        WorkerName: r.Worker?.FullName ?? "",
        EmployeeNo: r.Worker?.EmployeeNo ?? "",
        RequestType: r.RequestType,
        Reason: r.Reason,
        AdditionalNotes: r.AdditionalNotes,
        NoticeStartDate: r.NoticeStartDate.ToString("yyyy-MM-dd"),
        LastWorkingDay: r.LastWorkingDay.ToString("yyyy-MM-dd"),
        Status: r.Status,
        ApprovedBy: r.ApprovedBy,
        ApproverName: r.ApproverName,
        ApprovedAt: r.ApprovedAt,
        RejectionReason: r.RejectionReason,
        CancelledReason: r.CancelledReason,
        IsFinalPayProcessed: r.IsFinalPayProcessed,
        ChecklistItemsCompleted: r.ChecklistItemsCompleted,
        ChecklistItemsTotal: r.ChecklistItemsTotal,
        ChecklistItems: r.ChecklistItems.Select(MapChecklistItem).ToList(),
        ExitInterview: r.ExitInterview is null ? null : MapExitInterview(r.ExitInterview),
        CreatedAt: r.CreatedAt);

    private static ChecklistItemDto MapChecklistItem(OffboardingChecklistItem c) => new(
        Id: c.Id, Title: c.Title, Description: c.Description, Owner: c.Owner,
        IsCompleted: c.IsCompleted, CompletedBy: c.CompletedBy, CompletedAt: c.CompletedAt, SortOrder: c.SortOrder);

    private static ExitInterviewDto MapExitInterview(ExitInterview e) => new(
        Id: e.Id, WorkerId: e.WorkerId, WorkerName: e.Worker?.FullName,
        ReasonForLeaving: e.ReasonForLeaving, ReasonDetails: e.ReasonDetails,
        WhatWentWell: e.WhatWentWell, WhatCouldImprove: e.WhatCouldImprove,
        WouldRecommend: e.WouldRecommend, ManagerFeedback: e.ManagerFeedback, HrmNotes: e.HrmNotes,
        InterviewedBy: e.InterviewedBy, InterviewedAt: e.InterviewedAt, Status: e.Status);
}
