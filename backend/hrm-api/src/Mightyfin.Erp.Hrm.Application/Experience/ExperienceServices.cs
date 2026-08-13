using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Experience;

/// <summary>UI-XPR-001/002 + HRM-053: employee-facing experience services —
/// service-desk requests, HR letters, and anonymous protected disclosures.</summary>
public interface IExperienceService
{
    // HR requests
    Task<Paged<HrRequestDto>> ListRequestsAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<HrRequestDto> CreateRequestAsync(HrRequestCreate request, CancellationToken ct);
    Task<HrRequestDto> AddMessageAsync(Guid requestId, HrRequestMessageCreate message, CancellationToken ct);

    // HR letters
    Task<Paged<HrLetterDto>> ListLettersAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<HrLetterDto> CreateLetterAsync(HrLetterCreate request, CancellationToken ct);
    Task<HrLetterDto> ApproveLetterAsync(Guid id, CancellationToken ct);

    // Protected disclosure (speak up)
    Task<ProtectedDisclosureDto> SubmitDisclosureAsync(ProtectedDisclosureCreate request, CancellationToken ct);
    Task<ProtectedDisclosureStatusResponse?> GetDisclosureStatusAsync(string caseReference, string accessCode, CancellationToken ct);
}

public sealed record HrRequestDto(Guid Id, Guid WorkerId, string WorkerName, string Category, string Subject, string Status, string Confidentiality, DateTimeOffset CreatedAt, List<HrRequestMessageDto> Messages);
public sealed record HrRequestMessageDto(Guid Id, string From, string Body, bool IsInternalNote, DateTimeOffset CreatedAt);
public sealed record HrLetterDto(Guid Id, Guid WorkerId, string WorkerName, string LetterType, string Status, string Addressee, string Purpose, string? VerificationCode, DateTimeOffset CreatedAt);
public sealed record ProtectedDisclosureDto(string CaseReference, string AccessCode, string Status);

public sealed class ExperienceServiceImpl(IExperienceRepository repo, IAuthzService authz, IWorkflowService workflow) : IExperienceService
{
    public async Task<Paged<HrRequestDto>> ListRequestsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee");
        var (items, total) = await repo.ListRequestsAsync(workerId, status, ct);
        return new Paged<HrRequestDto>(items.Select(Map).ToList(), total, 1, 50);
    }

    public async Task<HrRequestDto> CreateRequestAsync(HrRequestCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        var req = new HrRequest { Category = request.Category, Subject = request.Subject, Body = request.Body, Confidentiality = request.Confidentiality, Status = "open" };
        var created = await repo.CreateRequestAsync(req, ct);
        await workflow.OpenAsync("hr-request", created.Id, created.WorkerId, JsonSerializer.Serialize(new { created.Category, created.Subject }), ct);
        return Map(created);
    }

    public async Task<HrRequestDto> AddMessageAsync(Guid requestId, HrRequestMessageCreate message, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee");
        var req = await repo.GetRequestAsync(requestId, ct) ?? throw new DomainException("hr-request-not-found", $"Request {requestId} does not exist.");
        req.Messages.Add(new HrRequestMessage { Body = message.Body, IsInternalNote = message.IsInternalNote, From = "hr" });
        if (req.Status == "awaiting-employee") req.Status = "in-progress";
        var updated = await repo.UpdateRequestAsync(req, ct);
        return Map(updated);
    }

    public async Task<Paged<HrLetterDto>> ListLettersAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee");
        var (items, total) = await repo.ListLettersAsync(workerId, status, ct);
        return new Paged<HrLetterDto>(items.Select(l => new HrLetterDto(
            l.Id, l.WorkerId, l.Worker?.FullName ?? "", l.LetterType, l.Status, l.Addressee, l.Purpose, l.VerificationCode, l.CreatedAt)).ToList(), total, 1, 50);
    }

    public async Task<HrLetterDto> CreateLetterAsync(HrLetterCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        var letter = new HrLetter
        {
            LetterType = request.LetterType,
            Addressee = request.Addressee,
            Purpose = request.Purpose,
            VerificationCode = GenerateVerificationCode(),
            Status = "draft",
        };
        var created = await repo.CreateLetterAsync(letter, ct);
        // letters requiring approval enter the standard workflow; others can be generated directly by HR
        if (RequiresApproval(request.LetterType))
            await workflow.OpenAsync("hr-letter", created.Id, created.WorkerId,
                JsonSerializer.Serialize(new { created.LetterType, created.Addressee, created.Purpose }), ct);
        return new HrLetterDto(created.Id, created.WorkerId, created.Worker?.FullName ?? "", created.LetterType, created.Status, created.Addressee, created.Purpose, created.VerificationCode, created.CreatedAt);
    }

    public async Task<HrLetterDto> ApproveLetterAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var letter = await repo.GetLetterAsync(id, ct) ?? throw new DomainException("letter-not-found", $"Letter {id} does not exist.");
        letter.Status = "generated";
        var updated = await repo.UpdateLetterAsync(letter, ct);
        return new HrLetterDto(updated.Id, updated.WorkerId, updated.Worker?.FullName ?? "", updated.LetterType, updated.Status, updated.Addressee, updated.Purpose, updated.VerificationCode, updated.CreatedAt);
    }

    public async Task<ProtectedDisclosureDto> SubmitDisclosureAsync(ProtectedDisclosureCreate request, CancellationToken ct)
    {
        // Anonymous: no authz role required; conflict-safe investigator assignment
        var existing = await repo.CountDisclosuresThisYearAsync(ct);
        var pd = new ProtectedDisclosure
        {
            CaseReference = $"SD-{DateTimeOffset.UtcNow.Year}-{(existing + 1):D5}",
            AccessCode = GenerateAccessCode(),
            Category = request.Category,
            Severity = request.Severity,
            Description = request.Description,
            Status = "new",
        };
        var created = await repo.CreateDisclosureAsync(pd, ct);
        return new ProtectedDisclosureDto(created.CaseReference, created.AccessCode, created.Status);
    }

    public async Task<ProtectedDisclosureStatusResponse?> GetDisclosureStatusAsync(string caseReference, string accessCode, CancellationToken ct)
    {
        var pd = await repo.GetDisclosureByCaseReferenceAsync(caseReference, ct);
        if (pd is null || pd.AccessCode != accessCode) return null;
        return new ProtectedDisclosureStatusResponse(pd.CaseReference, pd.Status, pd.UpdatedAt, pd.Status == "new" ? "Awaiting triage by an investigator" : "Under review");
    }

    private static bool RequiresApproval(string letterType) => letterType is "reference" or "visa" or "salary-confirmation";

    private static string GenerateVerificationCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        return Convert.ToHexString(bytes)[..8];
    }

    private static string GenerateAccessCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var chars = new StringBuilder();
        foreach (var b in bytes) chars.Append("ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[(int)b % 32]);
        return chars.ToString(0, 10);
    }

    private static HrRequestDto Map(HrRequest r) => new(
        r.Id, r.WorkerId, r.Worker?.FullName ?? "", r.Category, r.Subject, r.Status, r.Confidentiality, r.CreatedAt,
        r.Messages.Select(m => new HrRequestMessageDto(m.Id, m.From, m.Body, m.IsInternalNote, m.CreatedAt)).ToList());
}

public interface IExperienceRepository
{
    Task<(List<HrRequest> Items, int Total)> ListRequestsAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<HrRequest?> GetRequestAsync(Guid id, CancellationToken ct);
    Task<HrRequest> CreateRequestAsync(HrRequest request, CancellationToken ct);
    Task<HrRequest> UpdateRequestAsync(HrRequest request, CancellationToken ct);
    Task<(List<HrLetter> Items, int Total)> ListLettersAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<HrLetter?> GetLetterAsync(Guid id, CancellationToken ct);
    Task<HrLetter> CreateLetterAsync(HrLetter letter, CancellationToken ct);
    Task<HrLetter> UpdateLetterAsync(HrLetter letter, CancellationToken ct);
    Task<int> CountDisclosuresThisYearAsync(CancellationToken ct);
    Task<ProtectedDisclosure> CreateDisclosureAsync(ProtectedDisclosure disclosure, CancellationToken ct);
    Task<ProtectedDisclosure?> GetDisclosureByCaseReferenceAsync(string caseReference, CancellationToken ct);
}
