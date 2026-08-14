using Mightyfin.Erp.Hrm.Application.Workers;
using System.Globalization;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.ConfigAndExtras;

public sealed class ConfigServiceImpl(IConfigRepository repo, IAuthzService authz) : IConfigService
{
    public async Task<AdminConfigDto> GetConfigAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var entities = await repo.ListLegalEntitiesAsync(ct);
        var locations = await repo.ListLocationsAsync(ct);
        var units = await repo.ListOrgUnitsAsync(ct);
        var calendars = await repo.ListCalendarsAsync(ct);
        var leaveTypes = await repo.ListLeaveTypesAsync(true, ct);
        var capabilities = await repo.ListCapabilitiesAsync(ct);
        var payGroups = await repo.ListPayGroupsAsync(ct);
        return new AdminConfigDto(
            entities.Select(e => new LegalEntityDto(e.Id, e.Code, e.RegisteredName, e.TradingName, e.Currency)).ToList(),
            locations.Select(l => new WorkLocationDto(l.Id, l.Code, l.Name, l.LegalEntityId, l.Type)).ToList(),
            units.Select(u => new OrgUnitDto(u.Id, u.Code, u.Name, u.ParentId, u.UnitType, u.Status, u.ManagerId.HasValue ? "" : null)).ToList(),
            calendars.Select(c => new WorkCalendarDto(c.Id, c.Name, c.StandardWeeklyHours, c.WeekendDays, c.Holidays.Count)).ToList(),
            leaveTypes.Select(t => new LeaveTypeDto(t.Id, t.Code, t.Name, t.Category, t.DefaultDaysPerYear, t.IsActive)).ToList(),
            capabilities.Select(c => new CapabilityDto(c.FeatureKey, c.Tier, c.IsEnabled)).ToList(),
            payGroups.Select(g => new PayGroupDto(g.Id, g.Code, g.Name, g.Frequency, g.Currency, g.CalendarDayOfMonth)).ToList());
    }

    public async Task<Paged<LeaveTypeDto>> ListLeaveTypesAsync(bool includeInactive, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee", "manager", "payroll");
        var items = await repo.ListLeaveTypesAsync(includeInactive, ct);
        return new Paged<LeaveTypeDto>(items.Select(t => new LeaveTypeDto(t.Id, t.Code, t.Name, t.Category, t.DefaultDaysPerYear, t.IsActive)).ToList(), items.Count, 1, 100);
    }
}

public sealed class RecruitmentServiceImpl(
    IRecruitmentRepository repo,
    IAuthzService authz,
    IWorkerService workers,
    IWorkerLifecycleService lifecycle,
    IConfigRepository config) : IRecruitmentService
{
    public async Task<Paged<VacancyDto>> ListVacanciesAsync(string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var (items, total) = await repo.ListVacanciesAsync(status, ct);
        return new Paged<VacancyDto>(items.Select(v => new VacancyDto(v.Id, v.JobTitle, v.Grade, v.Status, v.OrgUnit?.Name ?? "", v.CreatedAt)).ToList(), total, 1, 50);
    }

    public async Task<VacancyDto> CreateVacancyAsync(VacancyCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var v = new Vacancy { OrgUnitId = request.OrgUnitId, JobTitle = request.JobTitle, Grade = request.Grade, Description = request.Description, Status = request.Status == "published" ? "published" : "draft" };
        var created = await repo.CreateVacancyAsync(v, ct);
        return new VacancyDto(created.Id, created.JobTitle, created.Grade, created.Status, created.OrgUnit?.Name ?? "", created.CreatedAt);
    }

    public async Task<VacancyDto> PublishVacancyAsync(Guid vacancyId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var v = await repo.GetVacancyAsync(vacancyId, ct)
            ?? throw new DomainException("vacancy-not-found", $"Vacancy {vacancyId} does not exist.");
        if (v.Status != "draft")
            throw new DomainException("vacancy-invalid-transition", $"Vacancy {v.Status} cannot be published; only draft vacancies can.");
        v.Status = "published";
        var updated = await repo.UpdateVacancyAsync(v, ct);
        return new VacancyDto(updated.Id, updated.JobTitle, updated.Grade, updated.Status, updated.OrgUnit?.Name ?? "", updated.CreatedAt);
    }

    public async Task<VacancyDto> CloseVacancyAsync(Guid vacancyId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var v = await repo.GetVacancyAsync(vacancyId, ct)
            ?? throw new DomainException("vacancy-not-found", $"Vacancy {vacancyId} does not exist.");
        if (v.Status != "published")
            throw new DomainException("vacancy-invalid-transition", $"Vacancy {v.Status} cannot be closed; only published vacancies can.");
        v.Status = "closed";
        var updated = await repo.UpdateVacancyAsync(v, ct);
        return new VacancyDto(updated.Id, updated.JobTitle, updated.Grade, updated.Status, updated.OrgUnit?.Name ?? "", updated.CreatedAt);
    }

    public async Task<Paged<CandidateDto>> ListCandidatesAsync(Guid vacancyId, string? stage, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var (items, total) = await repo.ListCandidatesAsync(vacancyId, stage, ct);
        return new Paged<CandidateDto>(items.Select(c => new CandidateDto(c.Id, c.VacancyId, c.FullName, c.Email, c.Phone, c.Stage, c.Notes, c.CreatedAt)).ToList(), total, 1, 50);
    }

    public async Task<CandidateDto> CreateCandidateAsync(CandidateCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var c = new Candidate { VacancyId = request.VacancyId, FullName = request.FullName, Email = request.Email, Phone = request.Phone, Source = request.Source, Notes = request.Notes, Stage = "screening" };
        var created = await repo.CreateCandidateAsync(c, ct);
        return new CandidateDto(created.Id, created.VacancyId, created.FullName, created.Email, created.Phone, created.Stage, created.Notes, created.CreatedAt);
    }

    public async Task<CandidateDto> AdvanceCandidateAsync(Guid candidateId, CandidateAdvanceRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var c = await repo.GetCandidateAsync(candidateId, ct) ?? throw new DomainException("candidate-not-found", $"Candidate {candidateId} does not exist.");
        if (!ValidStages.Contains(request.Stage))
            throw new DomainException("candidate-invalid-stage", $"Stage '{request.Stage}' is not valid. Valid stages: {string.Join(", ", ValidStages)}.");
        if (c.Stage == "hired" || c.Stage == "rejected")
            throw new DomainException("candidate-terminal-stage", $"Candidate is already in terminal stage '{c.Stage}' and cannot be advanced.");
        c.Stage = request.Stage;
        if (request.Notes is not null) c.Notes = (c.Notes + " | " + request.Notes).TrimStart(' ', '|');
        c.StageScore = request.Score;
        var updated = await repo.CreateCandidateAsync(c, ct);
        return new CandidateDto(updated.Id, updated.VacancyId, updated.FullName, updated.Email, updated.Phone, updated.Stage, updated.Notes, updated.CreatedAt);
    }
    private static readonly HashSet<string> ValidStages = ["screening", "shortlisted", "interviewed", "offered", "hired", "rejected"];

    /// <summary>Pick a work location for onboarding: prefer the first active
    /// location of the org unit, else the tenant's first active location.</summary>
    private async Task<Guid?> PickLocationAsync(Guid orgUnitId, CancellationToken ct)
    {
        var locations = await config.ListLocationsAsync(ct);
        return locations.FirstOrDefault()?.Id;
    }

    public async Task<OfferDto> CreateOfferAsync(OfferCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var c = await repo.GetCandidateAsync(request.CandidateId, ct)
            ?? throw new DomainException("candidate-not-found", $"Candidate {request.CandidateId} does not exist.");
        if (c.Stage != "offered")
            throw new DomainException("candidate-not-offered", "An offer can only be created for a candidate in the 'offered' stage.");
        var o = new Offer { CandidateId = request.CandidateId, BaseSalary = request.BaseSalary, ContractType = request.ContractType, ProbationMonths = request.ProbationMonths, NoticeDays = request.NoticeDays, StartDate = request.StartDate, Notes = request.Notes, Status = "draft" };
        var created = await repo.CreateOfferAsync(o, ct);
        return new OfferDto(created.Id, created.CandidateId, created.BaseSalary, created.ContractType, created.Status, created.CreatedAt);
    }

    public async Task<OfferDto> IssueOfferAsync(Guid offerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var o = await repo.GetOfferAsync(offerId, ct)
            ?? throw new DomainException("offer-not-found", $"Offer {offerId} does not exist.");
        if (o.Status != "draft")
            throw new DomainException("offer-not-draft", $"Only draft offers can be issued; this offer is '{o.Status}'.");
        var c = await repo.GetCandidateAsync(o.CandidateId, ct);
        if (c is null || c.Stage != "offered")
            throw new DomainException("candidate-stage-mismatch", "The candidate is no longer in the 'offered' stage.");
        o.Status = "issued";
        var updated = await repo.UpdateOfferAsync(o, ct);
        return new OfferDto(updated.Id, updated.CandidateId, updated.BaseSalary, updated.ContractType, updated.Status, updated.CreatedAt);
    }

    /// <summary>Accept an issued offer and convert the candidate into a preboarding
    /// worker (ties to M2 onboarding): creates the worker record and an initial
    /// assignment carrying the offer's contract terms.</summary>
    public async Task<OfferAcceptResultDto> AcceptOfferAsync(Guid offerId, OfferAcceptRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var offer = await repo.GetOfferAsync(offerId, ct)
            ?? throw new DomainException("offer-not-found", $"Offer {offerId} does not exist.");
        if (offer.Status != "issued")
            throw new DomainException("offer-not-issued", $"Only issued offers can be accepted; this offer is '{offer.Status}'.");
        var candidate = await repo.GetCandidateAsync(offer.CandidateId, ct)
            ?? throw new DomainException("candidate-not-found", $"Candidate for offer {offerId} no longer exists.");
        if (candidate.Stage != "offered")
            throw new DomainException("candidate-stage-mismatch", "Candidate stage no longer matches the offer.");

        var vacancy = await repo.GetVacancyAsync(candidate.VacancyId, ct);
        var orgUnitId = vacancy?.OrgUnitId
            ?? throw new DomainException("vacancy-missing-org-unit", "The candidate's vacancy has no org unit.");
        var locationId = request.LocationId ?? await PickLocationAsync(orgUnitId, ct)
            ?? throw new DomainException("no-location", "No location could be resolved for the onboarding assignment; provide a LocationId.");
        var legalEntities = await config.ListLegalEntitiesAsync(ct);
        var legalEntityId = request.LegalEntityId ?? legalEntities.FirstOrDefault()?.Id
            ?? throw new DomainException("no-legal-entity", "No active legal entity found for the onboarding assignment.");

        var nameParts = candidate.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var workerReq = new WorkerCreateRequest(
            EmployeeNo: request.EmployeeNo ?? $"CAND-{candidate.Id.ToString("N")[..6].ToUpperInvariant()}",
            FirstName: nameParts.FirstOrDefault() ?? "Candidate",
            LastName: nameParts.Length > 1 ? string.Join(' ', nameParts.Skip(1)) : "",
            Email: candidate.Email,
            Phone: candidate.Phone,
            OrgUnitId: orgUnitId,
            LocationId: locationId,
            JobTitle: vacancy?.JobTitle,
            Grade: vacancy?.Grade,
            StartDate: request.StartDate ?? offer.StartDate,
            WorkerType: "employee");
        var worker = await workers.CreateAsync(workerReq, ct);

        var startDate = request.StartDate ?? offer.StartDate ?? DateTimeOffset.UtcNow.Date.ToString("yyyy-MM-dd");
        var assignment = await lifecycle.CreateAssignmentAsync(worker.Id, new AssignmentCreateRequest(
            WorkerId: worker.Id, LegalEntityId: legalEntityId, OrgUnitId: orgUnitId,
            LocationId: locationId, StartDate: startDate,
            JobTitle: vacancy?.JobTitle, Grade: vacancy?.Grade,
            ContractType: offer.ContractType, ProbationMonths: offer.ProbationMonths,
            NoticeDays: offer.NoticeDays), ct);

        offer.Status = "accepted";
        await repo.UpdateOfferAsync(offer, ct);
        candidate.Stage = "hired";
        candidate.StageChangedAt = DateTimeOffset.UtcNow;
        candidate.Notes = (candidate.Notes + " | Converted to worker " + worker.Id.ToString("N")[..6]).TrimStart(' ', '|');
        await repo.CreateCandidateAsync(candidate, ct);
        return new OfferAcceptResultDto(offer.Id, worker.Id, worker.EmployeeNo, assignment.Id, "preboarding");
    }
}

public sealed class RelationsServiceImpl(IRelationsRepository repo, IAuthzService authz) : IRelationsService
{
    public async Task<Paged<RelationsCaseDto>> ListCasesAsync(string? category, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin"); // restricted: HR admin only
        var (items, total) = await repo.ListCasesAsync(category, ct);
        return new Paged<RelationsCaseDto>(items.Select(c => new RelationsCaseDto(c.Id, c.SubjectWorkerId, c.CaseType, c.Category, c.Severity, c.Summary, c.Status, c.CreatedAt)).ToList(), total, 1, 50);
    }

    public async Task<RelationsCaseDto> CreateCaseAsync(RelationsCaseCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var c = new RelationsCase { SubjectWorkerId = request.SubjectWorkerId, CaseType = request.CaseType, Category = string.IsNullOrWhiteSpace(request.Category) ? request.CaseType : request.Category, Severity = request.Severity, Summary = request.Summary, Description = request.Description, Status = "open" };
        var created = await repo.CreateCaseAsync(c, ct);
        return new RelationsCaseDto(created.Id, created.SubjectWorkerId, created.CaseType, created.Category, created.Severity, created.Summary, created.Status, created.CreatedAt);
    }

    public async Task<RelationsCaseDto> UpdateCaseAsync(Guid caseId, RelationsCaseUpdate request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var c = await repo.GetCaseAsync(caseId, ct)
            ?? throw new DomainException("case-not-found", $"Relations case {caseId} does not exist.");
        if (request.Status is not null)
        {
            if (!ValidCaseStatuses.Contains(request.Status))
                throw new DomainException("case-invalid-status", $"Status '{request.Status}' is not valid. Valid statuses: {string.Join(", ", ValidCaseStatuses)}.");
            c.Status = request.Status;
        }
        if (request.Severity is not null) c.Severity = request.Severity;
        if (request.Summary is not null) c.Summary = request.Summary;
        if (request.Description is not null) c.Description = request.Description;
        if (request.Outcome is not null) c.Outcome = request.Outcome;
        var updated = await repo.UpdateCaseAsync(c, ct);
        return new RelationsCaseDto(updated.Id, updated.SubjectWorkerId, updated.CaseType, updated.Category, updated.Severity, updated.Summary, updated.Status, updated.CreatedAt);
    }
    private static readonly HashSet<string> ValidCaseStatuses = ["open", "in-progress", "resolved", "closed"];
}

public sealed class DocumentsServiceImpl(IDocumentsRepository repo, IConfigRepository configRepo, IAuthzService authz) : IDocumentsService
{
    public async Task<Paged<WorkerDocumentDto>> ListDocumentsAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee");
        var (items, total) = await repo.ListDocumentsAsync(workerId, ct);
        return new Paged<WorkerDocumentDto>(items.Select(d => new WorkerDocumentDto(d.Id, d.WorkerId, d.Category, d.Title, d.FileName, d.ContentType, d.SizeBytes, d.Classification, d.ExpiryDate?.ToString())).ToList(), total, 1, 50);
    }

    public async Task<WorkerDocumentDto> UploadDocumentAsync(Guid workerId, string category, string title, string fileName, string contentType, long sizeBytes, string storagePath, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        if (!ValidDocumentCategories.Contains(category))
            throw new DomainException("document-invalid-category", $"Category '{category}' is not valid. Valid categories: {string.Join(", ", ValidDocumentCategories)}.");
        if (!AllowedContentTypes.Any(a => contentType.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("document-invalid-content-type", $"Content type '{contentType}' is not allowed. Allowed types: {string.Join(", ", AllowedContentTypes)}.");
        const long MaxSizeBytes = 25 * 1024 * 1024;
        if (sizeBytes > MaxSizeBytes)
            throw new DomainException("document-too-large", $"Document size {sizeBytes} bytes exceeds the {MaxSizeBytes} byte limit.");
        var d = new WorkerDocument { WorkerId = workerId, Category = category, Title = title, FileName = fileName, ContentType = contentType, Classification = "internal", StoragePath = storagePath, SizeBytes = sizeBytes, IsLatest = true };
        var created = await repo.CreateDocumentAsync(d, ct);
        return new WorkerDocumentDto(created.Id, created.WorkerId, created.Category, created.Title, created.FileName, created.ContentType, created.SizeBytes, created.Classification, created.ExpiryDate?.ToString());
    }

    public async Task<(WorkerDocument Document, Stream Stream)> GetDocumentStreamAsync(Guid documentId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee");
        var doc = await repo.GetDocumentAsync(documentId, ct)
            ?? throw new DomainException("document-not-found", $"Document {documentId} does not exist.");
        if (!File.Exists(doc.StoragePath))
            throw new DomainException("document-missing", $"The stored file for document {documentId} is missing on disk at {doc.StoragePath}.");
        return (doc, new FileStream(doc.StoragePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan));
    }

    private static readonly HashSet<string> ValidDocumentCategories = new(StringComparer.OrdinalIgnoreCase) { "contract", "id", "qualification", "medical", "certificate", "letter", "evidence" };
    private static readonly string[] AllowedContentTypes = { "application/pdf", "image/png", "image/jpeg", "image/webp", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" };

    /// <summary>Report engine (M8): headcount, leave, and payroll register built
    /// from ledger-consistent queries; rows returned as key-value dicts so the
    /// frontend table columns drive rendering.</summary>
    public async Task<ReportDto> GetReportAsync(ReportQuery query, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var now = DateTimeOffset.UtcNow;
        var rows = new List<Dictionary<string, object?>>();
        var summary = new Dictionary<string, object?>();

        if (query.ReportType == "headcount")
        {
            var workers = await configRepo.ListAllWorkersAsync("active", ct);
            var byUnit = workers.GroupBy(w => w.OrgUnit?.Name ?? "Unassigned")
                .Select(g => new Dictionary<string, object?> { ["org_unit"] = g.Key, ["count"] = g.Count() });
            rows.AddRange(byUnit);
            summary["total_active"] = workers.Count;
            summary["as_of"] = now.ToString("o");
        }
        else if (query.ReportType == "leave")
        {
            var requests = await configRepo.ListLeaveRequestsAllAsync(query.ToDate is null ? null : "all", ct);
            var byType = requests.GroupBy(r => r.LeaveTypeCode)
                .Select(g => new Dictionary<string, object?> { ["leave_type"] = g.Key, ["requests"] = g.Count(), ["days"] = Math.Round(g.Sum(r => r.RequestedDays), 1) });
            rows.AddRange(byType);
            summary["total_requests"] = requests.Count;
        }
        else if (query.ReportType == "payroll-register")
        {
            var lines = await configRepo.ListRunLinesAllAsync(query.FromDate ?? "", query.ToDate ?? "", ct);
            var byPeriod = lines.GroupBy(l => l.Run?.PayPeriod?.PeriodLabel ?? "unknown")
                .Select(g => new Dictionary<string, object?> { ["period"] = g.Key, ["employees"] = g.Count(), ["gross"] = Math.Round(g.Sum(l => l.GrossPay), 2), ["deductions"] = Math.Round(g.Sum(l => l.TotalDeductions), 2), ["net"] = Math.Round(g.Sum(l => l.NetPay), 2) });
            rows.AddRange(byPeriod);
            summary["total_net"] = Math.Round(lines.Sum(l => l.NetPay), 2);
        }
        else
        {
            throw new DomainException("report-not-found", $"Report type {query.ReportType} is not available. Use headcount, leave, or payroll-register.");
        }
        return new ReportDto(query.ReportType, now.ToString("o"), summary, rows);
    }
}

/// <summary>Data-quality engine (M8): completeness of the statutory identity
/// pack, duplicate identity detection, and expiring documents.</summary>
public sealed class DqServiceImpl(IConfigRepository configRepo, IDocumentsRepository docRepo, IAuthzService authz) : IDqService
{
    public async Task<List<DqResult>> RunChecksAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "hr_ops");
        var results = new List<DqResult>();

        // Rule 1 — statutory completeness: every active worker should carry the
        // Zambian identity pack needed for payroll and leave reporting.
        var workers = await configRepo.ListAllWorkersAsync("active", ct);
        foreach (var w in workers)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(w.Email)) missing.Add("email");
            if (string.IsNullOrWhiteSpace(w.Phone)) missing.Add("phone");
            if (string.IsNullOrWhiteSpace(w.Nrc)) missing.Add("nrc");
            if (string.IsNullOrWhiteSpace(w.Tpin)) missing.Add("tpin");
            if (string.IsNullOrWhiteSpace(w.NapsaNumber)) missing.Add("napsa_number");
            if (string.IsNullOrWhiteSpace(w.NhimaNumber)) missing.Add("nhima_number");
            if (w.OrgUnitId is null) missing.Add("org_unit");
            if (w.StartDate is null) missing.Add("start_date");
            if (missing.Count > 0)
                results.Add(new DqResult("completeness", "medium", w.Id, $"Missing: {string.Join(", ", missing)}"));
        }

        // Rule 2 — identity duplicates across the tenant.
        var all = await configRepo.ListAllWorkersAsync(null, ct);
        var byEmail = all.Where(w => !string.IsNullOrWhiteSpace(w.Email)).GroupBy(w => w.Email!.Trim().ToLowerInvariant()).Where(g => g.Count() > 1);
        foreach (var g in byEmail)
            foreach (var w in g)
                results.Add(new DqResult("duplicate-email", "high", w.Id, $"Email '{w.Email}' shared by {g.Count()} workers."));
        var byNrc = all.Where(w => !string.IsNullOrWhiteSpace(w.Nrc)).GroupBy(w => w.Nrc!.Trim()).Where(g => g.Count() > 1);
        foreach (var g in byNrc)
            foreach (var w in g)
                results.Add(new DqResult("duplicate-nrc", "high", w.Id, $"NRC '{w.Nrc}' shared by {g.Count()} workers."));
        var byPhone = all.Where(w => !string.IsNullOrWhiteSpace(w.Phone)).GroupBy(w => w.Phone!.Trim()).Where(g => g.Count() > 1);
        foreach (var g in byPhone)
            foreach (var w in g)
                results.Add(new DqResult("duplicate-phone", "high", w.Id, $"Phone '{w.Phone}' shared by {g.Count()} workers."));

        // Rule 3 — documents expiring within 90 days (medical certificates, etc.).
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90));
        var docs = await docRepo.ListAllDocumentsAsync(ct);
        foreach (var d in docs.Where(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value <= cutoff))
            results.Add(new DqResult("document-expiring", "low", d.WorkerId, $"Document '{d.Title}' (category {d.Category}) expires {d.ExpiryDate.Value:yyyy-MM-dd}."));

        return results;
    }
}

/// <summary>Zambian statutory export engine (M8). Produces CSV files for NAPSA
/// remittance, NHIMA remittance, the ZRA PAYE register, and a NAPSA bank
/// payment file, all derived from released (non-reversed) payroll run lines.</summary>
public sealed class StatutoryExportServiceImpl(IPayrollRepository payrollRepo, IConfigRepository configRepo, IAuthzService authz) : IStatutoryExportService
{
    public async Task<string> GenerateAsync(string exportType, Guid payPeriodId, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var lines = await payrollRepo.ListReleasedRunLinesForPeriodAsync(payPeriodId, ct);
        var label = lines.FirstOrDefault()?.Run?.PayPeriod?.PeriodLabel ?? payPeriodId.ToString();
        if (lines.Count == 0)
            throw new DomainException("export-no-data", $"No released payroll data found for period {payPeriodId}.");

        var fileName = exportType switch
        {
            "napsa" => $"napsa-remittance-{label}.csv",
            "nhima" => $"nhima-remittance-{label}.csv",
            "zra" => $"zra-paye-register-{label}.csv",
            "paye-return" => $"zra-paye-return-{label}.csv",
            "napsa-bankfile" => $"napsa-bankfile-{label}.txt",
            _ => throw new DomainException("export-not-found", $"Export type '{exportType}' is not supported. Use napsa, nhima, zra, paye-return, or napsa-bankfile.")
        };

        var employer = (await configRepo.ListLegalEntitiesAsync(ct)).FirstOrDefault(e => e.IsDefault);

        var withComponents = new List<(PayrollRunLine Line, Dictionary<string, decimal> Amounts)>(lines.Count);
        foreach (var l in lines)
        {
            var amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var comp in l.Components)
                amounts[comp.ComponentCode] = amounts.TryGetValue(comp.ComponentCode, out var prev) ? prev + comp.Amount : comp.Amount;
            withComponents.Add((l, amounts));
        }

        var rows = exportType switch
        {
            "napsa" => withComponents.Select(t => NapsaRow(t.Line, t.Amounts)),
            "nhima" => withComponents.Select(t => NhimaRow(t.Line, t.Amounts)),
            "zra" => withComponents.Select(t => ZraRow(t.Line, t.Amounts)),
            "napsa-bankfile" => withComponents.Select(t => NapsaBankRow(t.Line, t.Amounts)),
            "paye-return" => PayeReturnRows(employer, lines, withComponents),
            _ => throw new DomainException("export-not-found", $"Export type '{exportType}' is not supported.")
        };

        var joined = string.Join("\r\n", rows);
        var file = Path.Combine(Path.GetTempPath(), fileName);
        await File.WriteAllTextAsync(file, joined + "\r\n", ct);
        return file;
    }

    /// <summary>Aggregate statutory summary for one period — used by the
    /// reports UI so totals are visible without downloading a file.</summary>
    public async Task<StatutorySummaryDto> SummaryAsync(Guid payPeriodId, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var lines = await payrollRepo.ListReleasedRunLinesForPeriodAsync(payPeriodId, ct);
        var label = lines.FirstOrDefault()?.Run?.PayPeriod?.PeriodLabel ?? "";
        if (label == "")
        {
            var period = await payrollRepo.GetPeriodAsync(payPeriodId, ct);
            label = period?.PeriodLabel ?? "";
        }
        decimal paye = 0, napsaEe = 0, napsaEr = 0, nhimaEe = 0, nhimaEr = 0;
        decimal gross = 0, net = 0;
        foreach (var l in lines)
        {
            gross += l.GrossPay;
            net += l.NetPay;
            foreach (var comp in l.Components)
            {
                if (comp.ComponentCode.Equals("paye", StringComparison.OrdinalIgnoreCase)) paye += comp.Amount;
                else if (comp.ComponentCode.Equals("napsa-ee", StringComparison.OrdinalIgnoreCase)) napsaEe += comp.Amount;
                else if (comp.ComponentCode.Equals("napsa-er", StringComparison.OrdinalIgnoreCase)) napsaEr += comp.Amount;
                else if (comp.ComponentCode.Equals("nhima-ee", StringComparison.OrdinalIgnoreCase)) nhimaEe += comp.Amount;
                else if (comp.ComponentCode.Equals("nhima-er", StringComparison.OrdinalIgnoreCase)) nhimaEr += comp.Amount;
            }
        }
        var employer = (await configRepo.ListLegalEntitiesAsync(ct))
            .FirstOrDefault(e => e.IsDefault) ?? (await configRepo.ListLegalEntitiesAsync(ct)).FirstOrDefault();
        // NOTE: positional args only — this SDK rejects named arguments on
        // record constructors (see M23-KICKOFF-NOTES.md repro).
        return new StatutorySummaryDto(
            label,
            lines.Select(l => l.WorkerId).Distinct().Count(),
            Math.Round(gross, 2), Math.Round(paye, 2),
            Math.Round(napsaEe, 2), Math.Round(napsaEr, 2),
            Math.Round(nhimaEe, 2), Math.Round(nhimaEr, 2),
            Math.Round(net, 2),
            employer?.TradingName ?? employer?.RegisteredName ?? "",
            employer?.Tpin ?? "", employer?.NapsaEmployerRef ?? "",
            employer?.NhimaEmployerRef ?? "", employer?.Currency ?? "ZMW");
    }

    /// <summary>Monthly PAYE return: employer header block, one worker line
    /// each, and a totals row — the exact figures an employer files with ZRA.</summary>
    private List<string> PayeReturnRows(LegalEntity? employer, List<PayrollRunLine> lines, List<(PayrollRunLine Line, Dictionary<string, decimal> Amounts)> withComponents)
    {
        var period = lines.First().Run?.PayPeriod;
        var rows = new List<string>();
        rows.Add(Csv("ZRA PAYE MONTHLY RETURN"));
        rows.Add(Csv("Employer", employer?.TradingName ?? employer?.RegisteredName ?? ""));
        rows.Add(Csv("Employer TPIN", employer?.Tpin ?? ""));
        rows.Add(Csv("NAPSA Employer Ref", employer?.NapsaEmployerRef ?? ""));
        rows.Add(Csv("NHIMA Employer Ref", employer?.NhimaEmployerRef ?? ""));
        rows.Add(Csv("Period", period?.PeriodLabel ?? ""));
        rows.Add(Csv("Currency", employer?.Currency ?? "ZMW"));
        rows.Add("");
        rows.Add(Csv("Employee No", "Employee Name", "TPIN", "NAPSA No", "Gross Pay", "PAYE", "Net Pay"));
        decimal tGross = 0, tPaye = 0, tNet = 0;
        foreach (var t in withComponents)
        {
            rows.Add(Csv(WorkerNo(t.Line), FullNameOf(t.Line), t.Line.Worker?.Tpin ?? "",
                t.Line.Worker?.NapsaNumber ?? "", Math.Round(t.Line.GrossPay, 2),
                Math.Round(t.Amounts.TryGetValue("paye", out var p) ? p : 0, 2),
                Math.Round(t.Line.NetPay, 2)));
            tGross += t.Line.GrossPay;
            tPaye += t.Amounts.TryGetValue("paye", out var p2) ? p2 : 0;
            tNet += t.Line.NetPay;
        }
        rows.Add("");
        rows.Add(Csv("TOTALS", "", "", "", Math.Round(tGross, 2), Math.Round(tPaye, 2), Math.Round(tNet, 2)));
        return rows;
    }

    private static string Csv(params object?[] values) =>
        string.Join(",", values.Select(v => CsvField(v?.ToString() ?? "")));

    private static string CsvField(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static string NapsaRow(PayrollRunLine l, Dictionary<string, decimal> a) =>
        Csv("EE", WorkerNo(l), FullNameOf(l), l.Worker?.NapsaNumber ?? "", Math.Round(a.TryGetValue("napsa-ee", out var v) ? v : 0, 2),
            Math.Round(a.TryGetValue("napsa-er", out var e) ? e : 0, 2), Math.Round((a.TryGetValue("napsa-ee", out var e1) ? e1 : 0) + (a.TryGetValue("napsa-er", out var e2) ? e2 : 0), 2));

    private static string NhimaRow(PayrollRunLine l, Dictionary<string, decimal> a) =>
        Csv("EE", WorkerNo(l), FullNameOf(l), l.Worker?.NhimaNumber ?? "", Math.Round(a.TryGetValue("nhima-ee", out var v) ? v : 0, 2),
            Math.Round(a.TryGetValue("nhima-er", out var e) ? e : 0, 2), Math.Round((a.TryGetValue("nhima-ee", out var e1) ? e1 : 0) + (a.TryGetValue("nhima-er", out var e2) ? e2 : 0), 2));

    private static string ZraRow(PayrollRunLine l, Dictionary<string, decimal> a) =>
        Csv(WorkerNo(l), FullNameOf(l), l.Worker?.Tpin ?? "", Math.Round(l.GrossPay, 2), Math.Round(a.TryGetValue("paye", out var v) ? v : 0, 2), Math.Round(l.NetPay, 2));

    private static string NapsaBankRow(PayrollRunLine l, Dictionary<string, decimal> a) =>
        // Fixed-width NAPSA bank payment file: name left-padded, member number, amount in ngwee (cents) right-padded.
        $"{FullNameOf(l),-30}{(l.Worker?.NapsaNumber ?? "").PadRight(9)}{((long)Math.Round(a.TryGetValue("napsa-ee", out var v) ? v : 0, 2) * 100).ToString().PadLeft(12, '0')}";

    private static string WorkerNo(PayrollRunLine l) => l.Worker?.EmployeeNo ?? "";
    private static string FullNameOf(PayrollRunLine l) => $"{l.Worker?.FirstName ?? ""} {l.Worker?.LastName ?? ""}".Trim();
}
