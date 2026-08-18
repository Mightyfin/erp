using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Offboarding;

public interface IOffboardingRepository
{
    Task<List<OffboardingRequest>> ListRequestsAsync(string? status, CancellationToken ct);
    Task<OffboardingRequest?> GetRequestAsync(Guid id, CancellationToken ct);
    Task<OffboardingRequest?> GetActiveForWorkerAsync(Guid workerId, CancellationToken ct);
    Task<OffboardingRequest> CreateRequestAsync(OffboardingRequest request, CancellationToken ct);
    Task<OffboardingRequest> UpdateRequestAsync(OffboardingRequest request, CancellationToken ct);
    Task<OffboardingChecklistItem?> GetChecklistItemAsync(Guid id, CancellationToken ct);
    Task<OffboardingChecklistItem> CreateChecklistItemAsync(OffboardingChecklistItem item, CancellationToken ct);
    Task<OffboardingChecklistItem> UpdateChecklistItemAsync(OffboardingChecklistItem item, CancellationToken ct);
    Task<ExitInterview?> GetExitInterviewAsync(Guid requestId, CancellationToken ct);
    Task<ExitInterview?> GetExitInterviewByIdAsync(Guid id, CancellationToken ct);
    Task<ExitInterview> CreateExitInterviewAsync(ExitInterview interview, CancellationToken ct);
    Task<ExitInterview> UpdateExitInterviewAsync(ExitInterview interview, CancellationToken ct);
    Task<Worker?> FindWorkerAsync(Guid workerId, CancellationToken ct);
    Task<Worker?> FindWorkerBySubjectIdAsync(string subjectId, CancellationToken ct);
}

public interface IOffboardingService
{
    // HR admin: manage offboarding requests
    Task<List<OffboardingRequestDto>> ListRequestsAsync(string? status, CancellationToken ct);
    Task<OffboardingRequestDto> GetRequestAsync(Guid id, CancellationToken ct);
    Task<OffboardingRequestDto> CreateRequestAsync(OffboardingRequestCreate request, string subjectId, CancellationToken ct);
    Task<OffboardingRequestDto> ApproveRequestAsync(Guid id, string subjectId, CancellationToken ct);
    Task<OffboardingRequestDto> RejectRequestAsync(Guid id, string reason, string subjectId, CancellationToken ct);
    Task<OffboardingRequestDto> CancelRequestAsync(Guid id, string reason, CancellationToken ct);
    Task<OffboardingRequestDto> MarkFinalPayProcessedAsync(Guid id, CancellationToken ct);

    // Checklist items
    Task<ChecklistItemDto> AddChecklistItemAsync(Guid requestId, ChecklistItemCreate request, CancellationToken ct);
    Task<ChecklistItemDto> UpdateChecklistItemAsync(Guid requestId, Guid itemId, ChecklistItemUpdate request, CancellationToken ct);
    Task<ChecklistItemDto> CompleteChecklistItemAsync(Guid itemId, string subjectId, CancellationToken ct);

    // Exit interview
    Task<ExitInterviewDto> CreateExitInterviewAsync(Guid requestId, ExitInterviewCreate request, CancellationToken ct);
    Task<ExitInterviewDto> GetExitInterviewAsync(Guid requestId, CancellationToken ct);
    Task<ExitInterviewDto> UpdateExitInterviewAsync(Guid requestId, ExitInterviewUpdate request, CancellationToken ct);

    // Self-service: worker's own offboarding request
    Task<OffboardingRequestDto?> GetMyOffboardingAsync(string subjectId, CancellationToken ct);
    Task<OffboardingRequestDto> SubmitMyResignationAsync(OffboardingRequestCreate request, string subjectId, CancellationToken ct);
}

// DTOs
public sealed record OffboardingRequestDto(
    Guid Id, Guid WorkerId, string WorkerName, string EmployeeNo,
    string RequestType, string Reason, string? AdditionalNotes,
    string NoticeStartDate, string LastWorkingDay,
    string Status,
    string? ApprovedBy, string? ApproverName, DateTimeOffset? ApprovedAt,
    string? RejectionReason, string? CancelledReason,
    bool IsFinalPayProcessed,
    int ChecklistItemsCompleted, int ChecklistItemsTotal,
    List<ChecklistItemDto> ChecklistItems,
    ExitInterviewDto? ExitInterview,
    DateTimeOffset CreatedAt);

public sealed record ChecklistItemDto(
    Guid Id, string Title, string? Description, string Owner,
    bool IsCompleted, string? CompletedBy, DateTimeOffset? CompletedAt, int SortOrder);

public sealed record ExitInterviewDto(
    Guid Id, Guid WorkerId, string? WorkerName,
    string? ReasonForLeaving, string? ReasonDetails,
    string? WhatWentWell, string? WhatCouldImprove,
    string? WouldRecommend, string? ManagerFeedback, string? HrmNotes,
    string? InterviewedBy, DateTimeOffset? InterviewedAt, string Status);

// Request DTOs
public sealed record OffboardingRequestCreate(
    Guid WorkerId, string RequestType, string Reason, string? AdditionalNotes,
    string NoticeStartDate, string LastWorkingDay);

public sealed record ChecklistItemCreate(string Title, string? Description, string Owner, int SortOrder);
public sealed record ChecklistItemUpdate(string? Title, string? Description, string? Owner);

public sealed record ExitInterviewCreate(
    string? ReasonForLeaving, string? ReasonDetails, string? WhatWentWell,
    string? WhatCouldImprove, string? WouldRecommend);

public sealed record ExitInterviewUpdate(
    string? ReasonForLeaving, string? ReasonDetails, string? WhatWentWell,
    string? WhatCouldImprove, string? WouldRecommend,
    string? ManagerFeedback, string? HrmNotes, string? InterviewedBy, string? Status);
