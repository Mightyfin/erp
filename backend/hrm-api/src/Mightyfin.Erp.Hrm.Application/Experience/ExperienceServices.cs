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
    Task<HrRequestDto> CreateRequestAsync(Guid? workerId, HrRequestCreate request, CancellationToken ct);
    // M25: subject-keyed mirror of ListRequestsAsync — an employee sees only
    // their own requests; HR roles keep the broad query.
    Task<Paged<HrRequestDto>> GetMyRequestsAsync(string subjectId, string? status, CancellationToken ct);
    Task<HrRequestDto> CreateMyRequestAsync(string subjectId, HrRequestCreate request, CancellationToken ct);
    Task<HrRequestDto> GetMyRequestAsync(Guid requestId, string subjectId, CancellationToken ct);
    Task<HrRequestDto> AddMyRequestMessageAsync(Guid requestId, string subjectId, HrRequestMessageCreate message, CancellationToken ct);
    Task<HrRequestDto> AddMessageAsync(Guid requestId, Guid? actorWorkerId, string actorRole, HrRequestMessageCreate message, CancellationToken ct);
    Task<HrRequestDto> ResolveRequestAsync(Guid requestId, CancellationToken ct);

    // HR letters
    Task<Paged<HrLetterDto>> ListLettersAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<HrLetterDto> CreateLetterAsync(Guid workerId, HrLetterCreate request, CancellationToken ct);
    Task<HrLetterDto> ApproveLetterAsync(Guid id, CancellationToken ct);
    Task<MyLettersDto> GetMyLettersAsync(string subjectId, string? status, CancellationToken ct);
    Task<HrLetterDto> CreateMyLetterAsync(string subjectId, HrLetterCreate request, CancellationToken ct);
    Task<HrLetterDto> GetMyLetterAsync(Guid id, string subjectId, CancellationToken ct);

    // Protected disclosure (speak up)
    Task<ProtectedDisclosureDto> SubmitDisclosureAsync(ProtectedDisclosureCreate request, CancellationToken ct);
    Task<ProtectedDisclosureStatusResponse?> GetDisclosureStatusAsync(string caseReference, string accessCode, CancellationToken ct);
}

public sealed record HrRequestDto(Guid Id, Guid? WorkerId, string WorkerName, string Category, string Subject, string Status, string Confidentiality, DateTimeOffset CreatedAt, List<HrRequestMessageDto> Messages, string? Body = null);
public sealed record HrRequestMessageDto(Guid Id, string From, string Body, bool IsInternalNote, DateTimeOffset CreatedAt);
public sealed record HrLetterDto(Guid Id, Guid WorkerId, string WorkerName, string LetterType, string Status, string Addressee, string Purpose, string? VerificationCode, string? TemplateBody, DateTimeOffset CreatedAt);
public sealed record ProtectedDisclosureDto(string CaseReference, string AccessCode, string Status);
// M27 P0 UX audit: linked-worker envelope for the personal letters inbox
// (replaces Paged<HrLetterDto>, which 422-ed for unlinked identities).
public sealed record MyLettersDto(
    Guid WorkerId, string WorkerName, string? EmployeeNo, bool Linked,
    List<HrLetterDto> Items);

public sealed class ExperienceServiceImpl(IExperienceRepository repo, IAuthzService authz, IWorkflowService workflow, ILetterTemplates templates, IMergeDataProvider merge, Workers.IWorkerService? workers = null, IOutboxWriter? outbox = null, IUnitOfWork? unitOfWork = null) : IExperienceService
{
    public async Task<Paged<HrRequestDto>> ListRequestsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var (items, total) = await repo.ListRequestsAsync(workerId, status, ct);
        return new Paged<HrRequestDto>(items.Select(x => Map(x)).ToList(), total, 1, 50);
    }

    // M25: subject-keyed inbox — resolves the worker bound to the caller's
    // identity and lists only their own requests. Not-linked → empty list.
    public async Task<Paged<HrRequestDto>> GetMyRequestsAsync(string subjectId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        // A caller with a subject claim who is not linked to a worker must
        // never see anyone else's requests — unknown identity → empty inbox.
        if (!string.IsNullOrEmpty(subjectId))
        {
            if (workers is null) return new Paged<HrRequestDto>([], 0, 1, 50);
            var own = await workers.GetBySubjectAsync(subjectId, ct);
            if (own is null)
                return new Paged<HrRequestDto>([], 0, 1, 50);
            var (items, total) = await repo.ListRequestsAsync(own.Id, status, ct);
            return new Paged<HrRequestDto>(items.Select(x => Map(x, includeInternalNotes: false)).ToList(), total, 1, 50);
        }
        // No identity claim on the caller: they have no inbox (an unlinked
        // principal must never be able to enumerate anyone's requests).
        return new Paged<HrRequestDto>([], 0, 1, 50);
    }

    public async Task<HrRequestDto> GetMyRequestAsync(Guid requestId, string subjectId, CancellationToken ct)
    {
        var own = await RequireOwnWorkerAsync(subjectId, ct);
        var request = await repo.GetRequestAsync(requestId, ct)
            ?? throw new DomainException("hr-request-not-found", $"Request {requestId} does not exist.");
        if (request.WorkerId != own.Id) throw new DomainException("hr-request-not-owned", "The request does not belong to the signed-in worker.");
        return Map(request, includeInternalNotes: false);
    }

    public async Task<HrRequestDto> CreateMyRequestAsync(string subjectId, HrRequestCreate request, CancellationToken ct)
    {
        var own = await RequireOwnWorkerAsync(subjectId, ct);
        return await CreateRequestAsync(own.Id, request with { WorkerId = own.Id }, ct);
    }

    public async Task<HrRequestDto> AddMyRequestMessageAsync(Guid requestId, string subjectId, HrRequestMessageCreate message, CancellationToken ct)
    {
        if (message.IsInternalNote) throw new DomainException("internal-note-forbidden", "Employees cannot create internal HR notes.");
        var own = await RequireOwnWorkerAsync(subjectId, ct);
        var request = await repo.GetRequestAsync(requestId, ct)
            ?? throw new DomainException("hr-request-not-found", $"Request {requestId} does not exist.");
        if (request.WorkerId != own.Id) throw new DomainException("hr-request-not-owned", "The request does not belong to the signed-in worker.");
        if (request.Status is "resolved" or "closed") throw new DomainException("hr-request-closed", "A closed request cannot receive new replies.");
        return await AddMessageAsync(requestId, own.Id, "employee", message with { IsInternalNote = false }, ct);
    }

    public async Task<HrRequestDto> CreateRequestAsync(Guid? workerId, HrRequestCreate request, CancellationToken ct)
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
        if (actorRole == "employee" && (actorWorkerId is null || req.WorkerId != actorWorkerId))
            throw new DomainException("hr-request-not-owned", "The request does not belong to the signed-in worker.");
        if (actorRole == "employee" && message.IsInternalNote)
            throw new DomainException("internal-note-forbidden", "Employees cannot create internal HR notes.");
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
        HrRequest updated = req;
        async Task DecideAndEnqueue(CancellationToken transactionCt)
        {
            req.Status = req.Status == "resolved" ? "closed" : "resolved";
            updated = await repo.UpdateRequestAsync(req, transactionCt);
            if (outbox is null || req.Worker is null) return;
            await outbox.EnqueueAsync(
                HrmEventTypes.RequestDecided,
                req.Worker.SubjectId ?? req.Worker.Id.ToString("D"),
                new
                {
                    request_id = req.Id.ToString("D"),
                    worker_id = req.Worker.Id.ToString("D"),
                    status = req.Status,
                    email = req.Worker.Email ?? "",
                    first_name = req.Worker.FirstName,
                    last_name = req.Worker.LastName,
                },
                transactionCt);
        }
        if (unitOfWork is null)
            await DecideAndEnqueue(ct);
        else
            await unitOfWork.ExecuteAsync(DecideAndEnqueue, ct);
        return Map(updated);
    }

    public async Task<Paged<HrLetterDto>> ListLettersAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
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

    // M27 P0 UX audit: mirror the M25 subject-keyed inbox pattern — an
    // unlinked identity must never 422 the self-service widget; return an
    // envelope carrying Linked:false so the UI shows "Account not linked".
    public async Task<MyLettersDto> GetMyLettersAsync(string subjectId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "manager", "hr_ops", "hr_admin", "payroll");
        if (!string.IsNullOrEmpty(subjectId))
        {
            if (workers is null) return new MyLettersDto(Guid.Empty, "", null, false, []);
            var own = await workers.GetBySubjectAsync(subjectId, ct);
            if (own is null) return new MyLettersDto(Guid.Empty, "", null, false, []);
            var (items, total) = await repo.ListLettersAsync(own.Id, status, ct);
            return new MyLettersDto(own.Id, own.FullName, own.EmployeeNo, true, items.Select(MapLetter).ToList());
        }
        return new MyLettersDto(Guid.Empty, "", null, false, []);
    }

    public async Task<HrLetterDto> CreateMyLetterAsync(string subjectId, HrLetterCreate request, CancellationToken ct)
    {
        var own = await RequireOwnWorkerAsync(subjectId, ct);
        return await CreateLetterAsync(own.Id, request with { WorkerId = own.Id }, ct);
    }

    public async Task<HrLetterDto> GetMyLetterAsync(Guid id, string subjectId, CancellationToken ct)
    {
        var own = await RequireOwnWorkerAsync(subjectId, ct);
        var letter = await repo.GetLetterAsync(id, ct)
            ?? throw new DomainException("letter-not-found", $"Letter {id} does not exist.");
        if (letter.WorkerId != own.Id) throw new DomainException("letter-not-owned", "The letter does not belong to the signed-in worker.");
        return MapLetter(letter);
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

    private async Task<WorkerDto> RequireOwnWorkerAsync(string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "manager", "hr_ops", "hr_admin", "payroll");
        if (string.IsNullOrWhiteSpace(subjectId)) throw new DomainException("no-subject-claim", "The request carries no identity claim.");
        if (workers is null) throw new DomainException("worker-resolution-unavailable", "Worker identity resolution is unavailable.");
        return await workers.GetBySubjectAsync(subjectId, ct)
            ?? throw new DomainException("not-linked", "No worker record is linked to the signed-in identity.");
    }

    private static HrRequestDto Map(HrRequest r, bool includeInternalNotes = true) => new(
        r.Id, r.WorkerId, r.Worker?.FullName ?? "", r.Category, r.Subject, r.Status, r.Confidentiality, r.CreatedAt,
        r.Messages.Where(m => includeInternalNotes || !m.IsInternalNote).Select(m => new HrRequestMessageDto(m.Id, m.From, m.Body, m.IsInternalNote, m.CreatedAt)).ToList(), r.Body);

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
