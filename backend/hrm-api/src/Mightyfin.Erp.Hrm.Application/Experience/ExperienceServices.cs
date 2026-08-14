using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Experience;

/// <summary>UI-XPR-001/002 + HRM-053: employee-facing experience services —
/// service-desk requests, HR letters with template merge, and anonymous
/// protected disclosures.</summary>
public interface IExperienceService
{
    // HR requests
    Task<Paged<HrRequestDto>> ListRequestsAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<HrRequestDto> CreateRequestAsync(Guid workerId, HrRequestCreate request, CancellationToken ct);
    Task<HrRequestDto> AddMessageAsync(Guid requestId, Guid? actorWorkerId, string actorRole, HrRequestMessageCreate message, CancellationToken ct);
    Task<HrRequestDto> ResolveRequestAsync(Guid requestId, CancellationToken ct);

    // HR letters
    Task<Paged<HrLetterDto>> ListLettersAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<HrLetterDto> CreateLetterAsync(Guid workerId, HrLetterCreate request, CancellationToken ct);
    Task<HrLetterDto> ApproveLetterAsync(Guid id, CancellationToken ct);

    // Protected disclosure (speak up)
    Task<ProtectedDisclosureDto> SubmitDisclosureAsync(ProtectedDisclosureCreate request, CancellationToken ct);
    Task<ProtectedDisclosureStatusResponse?> GetDisclosureStatusAsync(string caseReference, string accessCode, CancellationToken ct);
}

public sealed record HrRequestDto(Guid Id, Guid? WorkerId, string WorkerName, string Category, string Subject, string Status, string Confidentiality, DateTimeOffset CreatedAt, List<HrRequestMessageDto> Messages);
public sealed record HrRequestMessageDto(Guid Id, string From, string Body, bool IsInternalNote, DateTimeOffset CreatedAt);
public sealed record HrLetterDto(Guid Id, Guid WorkerId, string WorkerName, string LetterType, string Status, string Addressee, string Purpose, string? VerificationCode, string? TemplateBody, DateTimeOffset CreatedAt);
public sealed record ProtectedDisclosureDto(string CaseReference, string AccessCode, string Status);

public sealed class ExperienceServiceImpl(IExperienceRepository repo, IAuthzService authz, IWorkflowService workflow, ILetterTemplates templates, IMergeDataProvider merge) : IExperienceService
{
    public async Task<Paged<HrRequestDto>> ListRequestsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee");
        var (items, total) = await repo.ListRequestsAsync(workerId, status, ct);
        return new Paged<HrRequestDto>(items.Select(Map).ToList(), total, 1, 50);
    }

    public async Task<HrRequestDto> CreateRequestAsync(Guid workerId, HrRequestCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        var req = new HrRequest
        {
            WorkerId = workerId,
            Category = request.Category,
            Subject = request.Subject,
            Body = request.Body,
            Confidentiality = request.Confidentiality,
            Status = "open",
        };
        var created = await repo.CreateRequestAsync(req, ct);
        await workflow.OpenAsync("hr-request", created.Id, created.WorkerId,
            JsonSerializer.Serialize(new { created.Category, created.Subject }), ct);
        return Map(created);
    }

    public async Task<HrRequestDto> AddMessageAsync(Guid requestId, Guid? actorWorkerId, string actorRole, HrRequestMessageCreate message, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee");
        var req = await repo.GetRequestAsync(requestId, ct) ?? throw new DomainException("hr-request-not-found", $"Request {requestId} does not exist.");
        // internal notes are always HR-side; conversational messages record who wrote them
        var from = message.IsInternalNote ? "hr" : actorRole == "hr_ops" || actorRole == "hr_admin" ? "hr" : "employee";
        if (from == "employee" && req.Status == "awaiting-employee") req.Status = "in-progress";
        if (from == "hr" && req.Status == "open") req.Status = "in-progress";
        // M22: insert the message top-level (immune to EF Core 10's Modified-parent
        // demotion of navigation-added children) rather than through req.Messages.
        var updated = await repo.AddMessageAsync(req, new HrRequestMessage
        {
            WorkerId = from == "employee" ? actorWorkerId : null,
            From = from,
            Body = message.Body,
            IsInternalNote = message.IsInternalNote,
        }, ct);
        return Map(updated);
    }

    public async Task<HrRequestDto> ResolveRequestAsync(Guid requestId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var req = await repo.GetRequestAsync(requestId, ct) ?? throw new DomainException("hr-request-not-found", $"Request {requestId} does not exist.");
        req.Status = req.Status == "resolved" ? "closed" : "resolved";
        var updated = await repo.UpdateRequestAsync(req, ct);
        return Map(updated);
    }

    public async Task<Paged<HrLetterDto>> ListLettersAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee");
        var (items, total) = await repo.ListLettersAsync(workerId, status, ct);
        return new Paged<HrLetterDto>(items.Select(MapLetter).ToList(), total, 1, 50);
    }

    public async Task<HrLetterDto> CreateLetterAsync(Guid workerId, HrLetterCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        var letter = new HrLetter
        {
            WorkerId = workerId,
            LetterType = request.LetterType,
            Addressee = request.Addressee,
            Purpose = request.Purpose,
            VerificationCode = GenerateVerificationCode(),
            Status = RequiresApproval(request.LetterType) ? "pending-approval" : "draft",
        };
        var created = await repo.CreateLetterAsync(letter, ct);
        await RenderAndStore(created, ct);
        if (RequiresApproval(request.LetterType))
            await workflow.OpenAsync("hr-letter", created.Id, created.WorkerId,
                JsonSerializer.Serialize(new { created.LetterType, created.Addressee, created.Purpose }), ct);
        return MapLetter(created);
    }

    public async Task<HrLetterDto> ApproveLetterAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var letter = await repo.GetLetterAsync(id, ct) ?? throw new DomainException("letter-not-found", $"Letter {id} does not exist.");
        letter.Status = "generated";
        RenderAndStore(letter, ct); // re-render so final version always reflects latest worker data
        var updated = await repo.UpdateLetterAsync(letter, ct);
        return MapLetter(updated);
    }

    /// <summary>Merges worker data into the matching built-in template and
    /// stores the result on the letter (UI-XPR-002).</summary>
    private async Task RenderAndStore(HrLetter letter, CancellationToken ct)
    {
        try
        {
            var data = await merge.GetMergeDataAsync(letter.WorkerId, letter.LetterType, ct);
            var rendered = templates.Render(letter.LetterType, new LetterMergeContext(
                data.WorkerFullName, data.EmployeeNo, data.JobTitle, data.Grade, data.StartDate,
                data.LegalEntityName, letter.Addressee, letter.Purpose,
                DateTimeOffset.UtcNow.ToString("d MMMM yyyy"), letter.VerificationCode!,
                data.BasicSalaryMonthly, data.ReferenceText));
            letter.TemplateBody = rendered;
            await repo.UpdateLetterAsync(letter, ct);
        }
        catch
        {
            // template rendering must never fail letter issuance; TemplateBody stays null
        }
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
        var bytes = RandomNumberGenerator.GetBytes(10);
        var chars = new StringBuilder();
        foreach (var b in bytes) chars.Append("ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[(int)b % 32]);
        return chars.ToString();
    }

    private static HrRequestDto Map(HrRequest r) => new(
        r.Id, r.WorkerId, r.Worker?.FullName ?? "", r.Category, r.Subject, r.Status, r.Confidentiality, r.CreatedAt,
        r.Messages.Select(m => new HrRequestMessageDto(m.Id, m.From, m.Body, m.IsInternalNote, m.CreatedAt)).ToList());

    private static HrLetterDto MapLetter(HrLetter l) => new(
        l.Id, l.WorkerId, l.Worker?.FullName ?? "", l.LetterType, l.Status, l.Addressee, l.Purpose,
        l.VerificationCode, l.TemplateBody, l.CreatedAt);
}

/// <summary>Worker/employment data merged into letter templates (UI-XPR-002).
/// Supplied by Infrastructure; keeps templates independent of the data source.</summary>
public interface IMergeDataProvider
{
    Task<LetterMergeData> GetMergeDataAsync(Guid workerId, string letterType, CancellationToken ct);
}

public sealed record LetterMergeData(string WorkerFullName, string EmployeeNo, string? JobTitle,
    string? Grade, DateOnly? StartDate, string? LegalEntityName, decimal? BasicSalaryMonthly,
    string? ReferenceText);
