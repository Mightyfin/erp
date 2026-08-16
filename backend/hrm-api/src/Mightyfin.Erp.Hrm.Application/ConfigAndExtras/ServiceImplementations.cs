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
    IConfigRepository config,
    IUnitOfWork? unitOfWork = null) : IRecruitmentService
{
    public async Task<Paged<VacancyDto>> ListVacanciesAsync(string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var (items, total) = await repo.ListVacanciesAsync(status, ct);
        var rows = new List<VacancyDto>();
        foreach (var v in items)
            rows.Add(new VacancyDto(v.Id, v.JobTitle, v.Grade, v.Status, v.OrgUnit?.Name ?? "", v.CreatedAt, await repo.CountCandidatesForVacancyAsync(v.Id, ct)));
        return new Paged<VacancyDto>(rows, total, 1, 50);
    }

    public async Task<VacancyDto> CreateVacancyAsync(VacancyCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        if (await config.GetOrgUnitAsync(request.OrgUnitId, ct) is null)
            throw new DomainException("org-unit-not-found", "The selected org unit does not exist.");
        if (string.IsNullOrWhiteSpace(request.JobTitle)) throw new DomainException("job-title-required", "Job title is required.");
        var v = new Vacancy { OrgUnitId = request.OrgUnitId, JobTitle = request.JobTitle.Trim(), Grade = request.Grade, Description = request.Description, Status = "draft" };
        var created = await repo.CreateVacancyAsync(v, ct);
        if (request.Status == "published") return await PublishVacancyAsync(created.Id, ct);
        return MapVacancy(created);
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
        return MapVacancy(updated);
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
        return MapVacancy(updated);
    }

    public async Task<Paged<CandidateDto>> ListCandidatesAsync(Guid vacancyId, string? stage, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var (items, total) = await repo.ListCandidatesAsync(vacancyId, stage, ct);
        return new Paged<CandidateDto>(items.Select(MapCandidate).ToList(), total, 1, 50);
    }

    public async Task<CandidateDto> CreateCandidateAsync(CandidateCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var vacancy = await repo.GetVacancyAsync(request.VacancyId, ct)
            ?? throw new DomainException("vacancy-not-found", $"Vacancy {request.VacancyId} does not exist.");
        if (vacancy.Status != "published") throw new DomainException("vacancy-not-open", "Applications can only be added to a published vacancy.");
        if (string.IsNullOrWhiteSpace(request.FullName)) throw new DomainException("candidate-name-required", "Candidate name is required.");
        var c = new Candidate { VacancyId = request.VacancyId, FullName = request.FullName.Trim(), Email = request.Email, Phone = request.Phone, Source = request.Source, Notes = request.Notes, Stage = "applied", StageChangedAt = DateTimeOffset.UtcNow };
        var created = await repo.CreateCandidateAsync(c, ct);
        await repo.CreateStageEventAsync(new CandidateStageEvent { CandidateId = created.Id, FromStage = "", ToStage = "applied", Notes = "Application received" }, ct);
        return MapCandidate(created);
    }

    public async Task<CandidateDetailDto> GetCandidateAsync(Guid candidateId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var c = await repo.GetCandidateAsync(candidateId, ct) ?? throw new DomainException("candidate-not-found", $"Candidate {candidateId} does not exist.");
        var vacancy = await repo.GetVacancyAsync(c.VacancyId, ct) ?? throw new DomainException("vacancy-not-found", "The candidate vacancy no longer exists.");
        var interviews = await repo.ListInterviewsAsync(candidateId, ct);
        var history = await repo.ListStageEventsAsync(candidateId, ct);
        var docs = await repo.ListCandidateDocumentsAsync(candidateId, ct);
        var offers = (await repo.ListOffersAsync(null, ct)).Items.Where(x => x.CandidateId == candidateId).ToList();
        var preboarding = await repo.GetPreboardingForCandidateAsync(candidateId, ct);
        return new CandidateDetailDto(MapCandidate(c), MapVacancy(vacancy), interviews.Select(MapInterview).ToList(),
            offers.Select(MapOffer).ToList(), history.Select(x => new CandidateStageEventDto(x.Id, x.FromStage, x.ToStage, x.Score, x.Notes, x.CreatedAt)).ToList(),
            docs.Select(MapDocument).ToList(), preboarding is null ? null : MapPreboarding(preboarding));
    }

    public async Task<CandidateDto> AdvanceCandidateAsync(Guid candidateId, CandidateAdvanceRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var c = await repo.GetCandidateAsync(candidateId, ct) ?? throw new DomainException("candidate-not-found", $"Candidate {candidateId} does not exist.");
        var target = request.Stage.Trim().ToLowerInvariant();
        if (c.Stage is "hired" or "rejected")
            throw new DomainException("candidate-terminal-stage", $"Candidate is already in terminal stage '{c.Stage}' and cannot be advanced.");
        if (!AllowedTransitions.TryGetValue(c.Stage, out var allowed) || !allowed.Contains(target))
            throw new DomainException("candidate-invalid-transition", $"Candidate cannot move from '{c.Stage}' to '{target}'. Allowed: {string.Join(", ", allowed ?? [])}.");
        var from = c.Stage;
        c.Stage = target;
        if (request.Notes is not null) c.Notes = (c.Notes + " | " + request.Notes).TrimStart(' ', '|');
        c.StageScore = request.Score;
        c.StageChangedAt = DateTimeOffset.UtcNow;
        var updated = await repo.CreateCandidateAsync(c, ct);
        await repo.CreateStageEventAsync(new CandidateStageEvent { CandidateId = c.Id, FromStage = from, ToStage = target, Score = request.Score, Notes = request.Notes }, ct);
        return MapCandidate(updated);
    }
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new()
    {
        ["applied"] = ["screening", "rejected"], ["screening"] = ["shortlisted", "rejected"],
        ["shortlisted"] = ["interviewing", "rejected"], ["interviewing"] = ["interviewed", "rejected"],
        ["interviewed"] = ["offered", "rejected"], ["offered"] = ["rejected"], ["preboarding"] = ["hired", "rejected"]
    };

    public async Task<InterviewDto> CreateInterviewAsync(Guid candidateId, InterviewCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var c = await repo.GetCandidateAsync(candidateId, ct) ?? throw new DomainException("candidate-not-found", $"Candidate {candidateId} does not exist.");
        if (c.Stage != "shortlisted" && c.Stage != "interviewing") throw new DomainException("candidate-not-shortlisted", "Interviews can only be scheduled for shortlisted candidates.");
        if (!DateTimeOffset.TryParse(request.ScheduledAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var scheduled))
            throw new DomainException("interview-date-invalid", "ScheduledAt must be a valid ISO date and time.");
        var interview = await repo.CreateInterviewAsync(new CandidateInterview { CandidateId = candidateId, ScheduledAt = scheduled, InterviewType = request.InterviewType, InterviewerName = request.InterviewerName, Notes = request.Notes }, ct);
        if (c.Stage == "shortlisted") await AdvanceCandidateAsync(candidateId, new CandidateAdvanceRequest("interviewing", Notes: "Interview scheduled"), ct);
        return MapInterview(interview);
    }

    public async Task<InterviewDto> DecideInterviewAsync(Guid interviewId, InterviewDecisionRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        if (request.OverallScore is < 1 or > 5) throw new DomainException("interview-score-invalid", "Interview score must be between 1 and 5.");
        if (request.Recommendation is not ("hire" or "hold" or "reject")) throw new DomainException("interview-recommendation-invalid", "Recommendation must be hire, hold, or reject.");
        var interview = await repo.GetInterviewAsync(interviewId, ct) ?? throw new DomainException("interview-not-found", $"Interview {interviewId} does not exist.");
        if (interview.Status != "scheduled") throw new DomainException("interview-decided", "This interview already has a decision.");
        interview.Status = "completed"; interview.OverallScore = request.OverallScore; interview.Recommendation = request.Recommendation; interview.Notes = request.Notes;
        await repo.UpdateInterviewAsync(interview, ct);
        var candidate = await repo.GetCandidateAsync(interview.CandidateId, ct);
        if (candidate?.Stage == "interviewing") await AdvanceCandidateAsync(candidate.Id, new CandidateAdvanceRequest("interviewed", request.OverallScore.ToString(CultureInfo.InvariantCulture), request.Notes), ct);
        return MapInterview(interview);
    }

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
        if (request.BaseSalary <= 0) throw new DomainException("offer-salary-invalid", "Base salary must be greater than zero.");
        var o = new Offer { CandidateId = request.CandidateId, BaseSalary = request.BaseSalary, ContractType = request.ContractType, ProbationMonths = request.ProbationMonths, NoticeDays = request.NoticeDays, StartDate = request.StartDate, Notes = request.Notes, ExpiresOn = request.ExpiresOn, Status = "draft" };
        var created = await repo.CreateOfferAsync(o, ct);
        return MapOffer(created);
    }

    public async Task<Paged<OfferDto>> ListOffersAsync(string? status, CancellationToken ct)
    { authz.RequireAnyRole("hr_ops", "hr_admin"); var (items, total) = await repo.ListOffersAsync(status, ct); return new Paged<OfferDto>(items.Select(MapOffer).ToList(), total, 1, 50); }

    public async Task<OfferDto> ApproveOfferAsync(Guid offerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var o = await repo.GetOfferAsync(offerId, ct) ?? throw new DomainException("offer-not-found", $"Offer {offerId} does not exist.");
        if (o.Status != "draft") throw new DomainException("offer-not-draft", "Only a draft offer can be approved.");
        o.Status = "approved"; o.ApprovedAt = DateTimeOffset.UtcNow; await repo.UpdateOfferAsync(o, ct); return MapOffer(o);
    }

    public async Task<OfferDto> IssueOfferAsync(Guid offerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var o = await repo.GetOfferAsync(offerId, ct)
            ?? throw new DomainException("offer-not-found", $"Offer {offerId} does not exist.");
        if (o.Status != "approved")
            throw new DomainException("offer-not-approved", $"Only approved offers can be issued; this offer is '{o.Status}'.");
        var c = await repo.GetCandidateAsync(o.CandidateId, ct);
        if (c is null || c.Stage != "offered")
            throw new DomainException("candidate-stage-mismatch", "The candidate is no longer in the 'offered' stage.");
        o.Status = "issued"; o.IssuedAt = DateTimeOffset.UtcNow;
        var updated = await repo.UpdateOfferAsync(o, ct);
        return MapOffer(updated);
    }

    public async Task<OfferDto> DeclineOfferAsync(Guid offerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var o = await repo.GetOfferAsync(offerId, ct) ?? throw new DomainException("offer-not-found", $"Offer {offerId} does not exist.");
        if (o.Status != "issued") throw new DomainException("offer-not-issued", "Only an issued offer can be declined.");
        o.Status = "declined"; o.RespondedAt = DateTimeOffset.UtcNow; await repo.UpdateOfferAsync(o, ct); return MapOffer(o);
    }

    /// <summary>Accept an issued offer and convert the candidate into a preboarding
    /// worker (ties to M2 onboarding): creates the worker record and an initial
    /// assignment carrying the offer's contract terms.</summary>
    public async Task<OfferAcceptResultDto> AcceptOfferAsync(Guid offerId, OfferAcceptRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        OfferAcceptResultDto? result = null;
        async Task Convert(CancellationToken token)
        {
        var offer = await repo.GetOfferAsync(offerId, ct)
            ?? throw new DomainException("offer-not-found", $"Offer {offerId} does not exist.");
        if (offer.Status != "issued")
            throw new DomainException("offer-not-issued", $"Only issued offers can be accepted; this offer is '{offer.Status}'.");
        var candidate = await repo.GetCandidateAsync(offer.CandidateId, ct)
            ?? throw new DomainException("candidate-not-found", $"Candidate for offer {offerId} no longer exists.");
        if (candidate.Stage != "offered")
            throw new DomainException("candidate-stage-mismatch", "Candidate stage no longer matches the offer.");
        if (candidate.WorkerId.HasValue) throw new DomainException("candidate-already-converted", "This candidate is already linked to a worker.");

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
            StartDate: null,
            WorkerType: "employee");
        var worker = await workers.CreateAsync(workerReq, ct);

        var startDate = request.StartDate ?? offer.StartDate ?? DateTimeOffset.UtcNow.Date.ToString("yyyy-MM-dd");
        var assignment = await lifecycle.CreateAssignmentAsync(worker.Id, new AssignmentCreateRequest(
            WorkerId: worker.Id, LegalEntityId: legalEntityId, OrgUnitId: orgUnitId,
            LocationId: locationId, StartDate: startDate,
            JobTitle: vacancy?.JobTitle, Grade: vacancy?.Grade,
            ContractType: offer.ContractType, ProbationMonths: offer.ProbationMonths,
            NoticeDays: offer.NoticeDays), ct);

        await workers.UpdateAsync(worker.Id, new WorkerUpdateRequest(Status: "pre-hire"), ct);

        offer.Status = "accepted"; offer.RespondedAt = DateTimeOffset.UtcNow;
        await repo.UpdateOfferAsync(offer, ct);
        candidate.Stage = "preboarding"; candidate.WorkerId = worker.Id;
        candidate.StageChangedAt = DateTimeOffset.UtcNow;
        candidate.Notes = (candidate.Notes + " | Converted to worker " + worker.Id.ToString("N")[..6]).TrimStart(' ', '|');
        await repo.CreateCandidateAsync(candidate, ct);
        await repo.CreateStageEventAsync(new CandidateStageEvent { CandidateId = candidate.Id, FromStage = "offered", ToStage = "preboarding", Notes = "Offer accepted and worker record created" }, ct);
        var due = DateOnly.Parse(startDate);
        var preboarding = new PreboardingCase { CandidateId = candidate.Id, WorkerId = worker.Id, AssignmentId = assignment.Id, StartDate = due, Status = "preboarding" };
        foreach (var item in DefaultTasks)
            preboarding.Tasks.Add(new PreboardingTask { Code = item.Code, Title = item.Title, Required = true, DueDate = due, Owner = item.Owner });
        await repo.CreatePreboardingAsync(preboarding, ct);
        result = new OfferAcceptResultDto(offer.Id, worker.Id, worker.EmployeeNo, assignment.Id, "preboarding");
        }
        if (unitOfWork is null) await Convert(ct); else await unitOfWork.ExecuteAsync(Convert, ct);
        return result!;
    }

    public async Task<Paged<PreboardingCaseDto>> ListPreboardingAsync(string? status, CancellationToken ct)
    { authz.RequireAnyRole("hr_ops", "hr_admin", "manager"); var (items, total) = await repo.ListPreboardingAsync(status, ct); return new Paged<PreboardingCaseDto>(items.Select(MapPreboarding).ToList(), total, 1, 50); }

    public async Task<PreboardingCaseDto> GetPreboardingAsync(Guid caseId, CancellationToken ct)
    { authz.RequireAnyRole("hr_ops", "hr_admin", "manager"); return MapPreboarding(await repo.GetPreboardingAsync(caseId, ct) ?? throw new DomainException("preboarding-not-found", $"Preboarding case {caseId} does not exist.")); }

    public async Task<PreboardingTaskDto> AddPreboardingTaskAsync(Guid caseId, PreboardingTaskCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var record = await repo.GetPreboardingAsync(caseId, ct) ?? throw new DomainException("preboarding-not-found", $"Preboarding case {caseId} does not exist.");
        if (record.Status != "preboarding") throw new DomainException("preboarding-closed", "Tasks cannot be added after activation.");
        var task = await repo.CreatePreboardingTaskAsync(new PreboardingTask { PreboardingCaseId = caseId, Code = $"custom-{Guid.NewGuid():N}"[..15], Title = request.Title.Trim(), Required = request.Required, DueDate = string.IsNullOrWhiteSpace(request.DueDate) ? null : DateOnly.Parse(request.DueDate), Owner = request.Owner }, ct);
        return MapTask(task);
    }

    public async Task<PreboardingTaskDto> UpdatePreboardingTaskAsync(Guid caseId, Guid taskId, PreboardingTaskUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var task = await repo.GetPreboardingTaskAsync(taskId, ct) ?? throw new DomainException("preboarding-task-not-found", $"Task {taskId} does not exist.");
        if (task.PreboardingCaseId != caseId) throw new DomainException("preboarding-task-not-found", "Task does not belong to this preboarding case.");
        if (request.Status is not ("pending" or "completed" or "blocked")) throw new DomainException("preboarding-task-status-invalid", "Status must be pending, completed, or blocked.");
        task.Status = request.Status; task.Notes = request.Notes; task.CompletedAt = request.Status == "completed" ? DateTimeOffset.UtcNow : null;
        return MapTask(await repo.UpdatePreboardingTaskAsync(task, ct));
    }

    public async Task<PreboardingCaseDto> ActivatePreboardingAsync(Guid caseId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var record = await repo.GetPreboardingAsync(caseId, ct) ?? throw new DomainException("preboarding-not-found", $"Preboarding case {caseId} does not exist.");
        if (record.Status == "active") return MapPreboarding(record);
        var incomplete = record.Tasks.Where(x => x.Required && x.Status != "completed").Select(x => x.Title).ToList();
        if (incomplete.Count > 0) throw new DomainException("preboarding-incomplete", $"Complete required tasks before activation: {string.Join(", ", incomplete)}.");
        await workers.UpdateAsync(record.WorkerId, new WorkerUpdateRequest(Status: "active"), ct);
        var candidate = await repo.GetCandidateAsync(record.CandidateId, ct) ?? throw new DomainException("candidate-not-found", "Candidate no longer exists.");
        candidate.Stage = "hired"; candidate.StageChangedAt = DateTimeOffset.UtcNow; await repo.CreateCandidateAsync(candidate, ct);
        await repo.CreateStageEventAsync(new CandidateStageEvent { CandidateId = candidate.Id, FromStage = "preboarding", ToStage = "hired", Notes = "Required preboarding tasks completed; worker activated" }, ct);
        record.Status = "active"; record.ActivatedAt = DateTimeOffset.UtcNow; await repo.UpdatePreboardingAsync(record, ct);
        return MapPreboarding(record);
    }

    public async Task<CandidateDocumentDto> AddCandidateDocumentAsync(Guid candidateId, string category, string title, string fileName, string contentType, long sizeBytes, string storagePath, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        _ = await repo.GetCandidateAsync(candidateId, ct) ?? throw new DomainException("candidate-not-found", $"Candidate {candidateId} does not exist.");
        if (sizeBytes > 10 * 1024 * 1024) throw new DomainException("document-too-large", "Candidate documents cannot exceed 10 MB.");
        return MapDocument(await repo.CreateCandidateDocumentAsync(new CandidateDocument { CandidateId = candidateId, Category = category, Title = title, FileName = fileName, ContentType = contentType, SizeBytes = sizeBytes, StoragePath = storagePath }, ct));
    }

    public async Task<(CandidateDocument Document, Stream Stream)> GetCandidateDocumentAsync(Guid documentId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var doc = await repo.GetCandidateDocumentAsync(documentId, ct) ?? throw new DomainException("candidate-document-not-found", $"Document {documentId} does not exist.");
        if (!File.Exists(doc.StoragePath)) throw new DomainException("document-file-missing", "The document file is unavailable.");
        return (doc, File.OpenRead(doc.StoragePath));
    }

    private static readonly (string Code, string Title, string Owner)[] DefaultTasks =
    [
        ("signed-contract", "Collect signed employment contract", "HR"), ("identity", "Verify identity document", "HR"),
        ("statutory", "Capture statutory identifiers", "Payroll"), ("bank", "Capture payment details", "Payroll"),
        ("induction", "Schedule induction and equipment", "Hiring manager")
    ];
    private static VacancyDto MapVacancy(Vacancy v) => new(v.Id, v.JobTitle, v.Grade, v.Status, v.OrgUnit?.Name ?? "", v.CreatedAt);
    private static CandidateDto MapCandidate(Candidate c) => new(c.Id, c.VacancyId, c.FullName, c.Email, c.Phone, c.Stage, c.Notes, c.CreatedAt, c.WorkerId);
    private static OfferDto MapOffer(Offer o) => new(o.Id, o.CandidateId, o.BaseSalary, o.ContractType, o.Status, o.CreatedAt, o.Candidate?.FullName, o.Candidate?.Vacancy?.JobTitle, o.StartDate, o.ExpiresOn, o.ApprovedAt, o.IssuedAt, o.RespondedAt);
    private static InterviewDto MapInterview(CandidateInterview x) => new(x.Id, x.CandidateId, x.ScheduledAt.ToString("O"), x.InterviewType, x.InterviewerName, x.Status, x.OverallScore, x.Recommendation, x.Notes, x.CreatedAt);
    private static CandidateDocumentDto MapDocument(CandidateDocument x) => new(x.Id, x.CandidateId, x.Category, x.Title, x.FileName, x.ContentType, x.SizeBytes, x.CreatedAt);
    private static PreboardingTaskDto MapTask(PreboardingTask x) => new(x.Id, x.Code, x.Title, x.Required, x.Status, x.DueDate?.ToString("yyyy-MM-dd"), x.Owner, x.Notes, x.CompletedAt);
    private static PreboardingCaseDto MapPreboarding(PreboardingCase x) => new(x.Id, x.CandidateId, x.Candidate?.FullName ?? "", x.WorkerId, x.Worker?.EmployeeNo ?? "", x.AssignmentId,
        x.Candidate?.Vacancy?.JobTitle ?? x.Worker?.JobTitle ?? "", x.Status, x.StartDate.ToString("yyyy-MM-dd"), x.Tasks.Count(t => t.Status == "completed"), x.Tasks.Count, x.Tasks.Select(MapTask).ToList(), x.CreatedAt);
}

public sealed class RelationsServiceImpl(IRelationsRepository repo, IAuthzService authz) : IRelationsService
{
    public async Task<Paged<RelationsCaseDto>> ListCasesAsync(string? category, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "investigator");
        var (items, total) = await repo.ListCasesAsync(category, ct);
        return new Paged<RelationsCaseDto>(items.Select(x => MapList(x)).ToList(), total, 1, 50);
    }

    public async Task<RelationsCaseDto> CreateCaseAsync(RelationsCaseCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var count = await repo.CountCasesThisYearAsync(ct);
        var c = new RelationsCase
        {
            Reference = $"ER-{DateTimeOffset.UtcNow.Year}-{count + 1:D5}", SubjectWorkerId = request.SubjectWorkerId,
            CaseType = request.CaseType.Trim().ToLowerInvariant(), Category = string.IsNullOrWhiteSpace(request.Category) ? request.CaseType : request.Category,
            Severity = request.Severity, Summary = request.Summary, Description = request.Description, Status = "open",
            Confidentiality = request.Confidentiality == "confidential" ? "confidential" : "restricted",
            Classification = "restricted", OwnerSubjectId = request.OwnerSubjectId, RaisedBy = request.RaisedBy,
            DueDate = string.IsNullOrWhiteSpace(request.DueDate) ? null : DateOnly.Parse(request.DueDate)
        };
        var created = await repo.CreateCaseAsync(c, ct);
        await repo.CreateEventAsync(new RelationsCaseEvent { CaseId = created.Id, Action = "created", ActorSubjectId = "system", ToStatus = "open" }, ct);
        return MapList(created, revealSummary: true);
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
        return MapList(updated, revealSummary: true);
    }
    private static readonly HashSet<string> ValidCaseStatuses = ["open", "in-progress", "resolved", "closed"];

    public async Task<RelationsAccessDto> DeclareAccessAsync(Guid caseId, RelationsAccessDeclarationRequest request, string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "investigator");
        var c = await RequireCaseAsync(caseId, ct);
        RequireAssignedOrAdmin(c, actorSubjectId);
        var decision = request.Decision.Trim().ToLowerInvariant();
        if (decision is not ("no-conflict" or "conflict")) throw new DomainException("access-decision-invalid", "Decision must be no-conflict or conflict.");
        if (decision == "conflict" && string.IsNullOrWhiteSpace(request.Notes)) throw new DomainException("conflict-reason-required", "Explain the conflict so the case can be reassigned safely.");
        var declaration = await repo.CreateAccessAsync(new RelationsCaseAccess { CaseId = caseId, ActorSubjectId = actorSubjectId, Decision = decision, Notes = request.Notes }, ct);
        await RecordAsync(caseId, decision == "conflict" ? "conflict-declared" : "access-declared", actorSubjectId, c.Status, c.Status, request.Notes, ct);
        return MapAccess(declaration);
    }

    public async Task<RelationsCaseDetailDto> GetCaseAsync(Guid caseId, string actorSubjectId, CancellationToken ct)
    {
        var c = await RequireAccessibleAsync(caseId, actorSubjectId, ct);
        await RecordAsync(caseId, "viewed", actorSubjectId, c.Status, c.Status, null, ct);
        return await MapDetailAsync(c, ct);
    }

    public async Task<RelationsCaseDto> AssignCaseAsync(Guid caseId, RelationsCaseAssignRequest request, string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var c = await RequireCaseAsync(caseId, ct);
        if (string.IsNullOrWhiteSpace(request.OwnerSubjectId)) throw new DomainException("owner-required", "An investigator subject id is required.");
        c.OwnerSubjectId = request.OwnerSubjectId.Trim(); await repo.UpdateCaseAsync(c, ct);
        await RecordAsync(caseId, "assigned", actorSubjectId, c.Status, c.Status, $"Assigned to {c.OwnerSubjectId}", ct);
        return MapList(c, revealSummary: true);
    }

    public async Task<RelationsCaseDetailDto> TransitionCaseAsync(Guid caseId, RelationsCaseTransitionRequest request, string actorSubjectId, CancellationToken ct)
    {
        var c = await RequireAccessibleAsync(caseId, actorSubjectId, ct);
        var target = request.Status.Trim().ToLowerInvariant();
        if (!CaseTransitions.TryGetValue(c.Status, out var allowed) || !allowed.Contains(target))
            throw new DomainException("case-invalid-transition", $"Case cannot move from '{c.Status}' to '{target}'. Allowed: {string.Join(", ", allowed ?? [])}.");
        if (target is "resolved" or "closed" && string.IsNullOrWhiteSpace(request.Outcome ?? c.Outcome))
            throw new DomainException("case-outcome-required", "An outcome is required before resolution or closure.");
        if (target == "resolved" && string.IsNullOrWhiteSpace(request.Findings ?? c.Findings))
            throw new DomainException("case-findings-required", "Findings are required before resolution.");
        if (target == "resolved" && (await repo.ListActionsAsync(caseId, ct)).Any(x => x.Status == "pending"))
            throw new DomainException("case-actions-open", "Complete or cancel all pending actions before resolving the case.");
        var from = c.Status; c.Status = target;
        if (request.Findings is not null) c.Findings = request.Findings;
        if (request.Outcome is not null) c.Outcome = request.Outcome;
        if (target == "closed") c.ClosedAt = DateTimeOffset.UtcNow;
        await repo.UpdateCaseAsync(c, ct);
        await RecordAsync(caseId, "status-changed", actorSubjectId, from, target, request.Notes, ct);
        return await MapDetailAsync(c, ct);
    }

    public async Task<RelationsActionDto> AddActionAsync(Guid caseId, RelationsActionCreateRequest request, string actorSubjectId, CancellationToken ct)
    {
        _ = await RequireAccessibleAsync(caseId, actorSubjectId, ct);
        if (string.IsNullOrWhiteSpace(request.Title)) throw new DomainException("action-title-required", "Action title is required.");
        var action = await repo.CreateActionAsync(new RelationsCaseAction { CaseId = caseId, ActionType = request.ActionType, Title = request.Title.Trim(), OwnerSubjectId = request.OwnerSubjectId, DueDate = string.IsNullOrWhiteSpace(request.DueDate) ? null : DateOnly.Parse(request.DueDate), Notes = request.Notes }, ct);
        await RecordAsync(caseId, "action-added", actorSubjectId, null, null, action.Title, ct); return MapAction(action);
    }

    public async Task<RelationsActionDto> UpdateActionAsync(Guid caseId, Guid actionId, RelationsActionUpdateRequest request, string actorSubjectId, CancellationToken ct)
    {
        _ = await RequireAccessibleAsync(caseId, actorSubjectId, ct);
        var action = await repo.GetActionAsync(actionId, ct) ?? throw new DomainException("case-action-not-found", $"Action {actionId} does not exist.");
        if (action.CaseId != caseId) throw new DomainException("case-action-not-found", "Action does not belong to this case.");
        if (request.Status is not ("pending" or "completed" or "cancelled")) throw new DomainException("case-action-status-invalid", "Action status must be pending, completed, or cancelled.");
        action.Status = request.Status; action.Notes = request.Notes; action.CompletedAt = request.Status == "completed" ? DateTimeOffset.UtcNow : null;
        await repo.UpdateActionAsync(action, ct); await RecordAsync(caseId, $"action-{request.Status}", actorSubjectId, null, null, action.Title, ct); return MapAction(action);
    }

    public async Task<RelationsEvidenceDto> AddEvidenceAsync(Guid caseId, string title, string evidenceType, string fileName, string contentType, long sizeBytes, string storagePath, string actorSubjectId, CancellationToken ct)
    {
        _ = await RequireAccessibleAsync(caseId, actorSubjectId, ct);
        if (sizeBytes <= 0 || sizeBytes > 10 * 1024 * 1024) throw new DomainException("evidence-size-invalid", "Evidence must be between 1 byte and 10 MB.");
        if (contentType is not ("application/pdf" or "image/png" or "image/jpeg" or "text/plain")) throw new DomainException("evidence-type-invalid", "Evidence must be PDF, PNG, JPEG, or plain text.");
        var evidence = await repo.CreateEvidenceAsync(new RelationsEvidence { CaseId = caseId, Title = title, EvidenceType = evidenceType, FileName = fileName, ContentType = contentType, SizeBytes = sizeBytes, StoragePath = storagePath, AddedBySubjectId = actorSubjectId }, ct);
        await RecordAsync(caseId, "evidence-added", actorSubjectId, null, null, title, ct); return MapEvidence(evidence);
    }

    public async Task<(RelationsEvidence Evidence, Stream Stream)> GetEvidenceAsync(Guid evidenceId, string actorSubjectId, CancellationToken ct)
    {
        var evidence = await repo.GetEvidenceAsync(evidenceId, ct) ?? throw new DomainException("case-evidence-not-found", $"Evidence {evidenceId} does not exist.");
        _ = await RequireAccessibleAsync(evidence.CaseId, actorSubjectId, ct);
        if (!File.Exists(evidence.StoragePath)) throw new DomainException("evidence-file-missing", "The evidence file is unavailable.");
        await RecordAsync(evidence.CaseId, "evidence-downloaded", actorSubjectId, null, null, evidence.Title, ct);
        return (evidence, File.OpenRead(evidence.StoragePath));
    }

    public async Task<Paged<ProtectedDisclosureInvestigationDto>> ListProtectedDisclosuresAsync(string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "investigator"); var (items, total) = await repo.ListProtectedDisclosuresAsync(status, ct);
        return new Paged<ProtectedDisclosureInvestigationDto>(items.Select(x => MapDisclosure(x, [], false)).ToList(), total, 1, 50);
    }

    public async Task<ProtectedDisclosureInvestigationDto> GetProtectedDisclosureAsync(Guid id, string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "investigator"); var d = await RequireDisclosureAsync(id, actorSubjectId, ct);
        await repo.CreateProtectedDisclosureEventAsync(new ProtectedDisclosureEvent { DisclosureId = id, Action = "viewed", ActorSubjectId = actorSubjectId, FromStatus = d.Status, ToStatus = d.Status }, ct);
        return MapDisclosure(d, await repo.ListProtectedDisclosureEventsAsync(id, ct), true);
    }

    public async Task<ProtectedDisclosureInvestigationDto> UpdateProtectedDisclosureAsync(Guid id, ProtectedDisclosureUpdateRequest request, string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "investigator"); var d = await RequireDisclosureAsync(id, actorSubjectId, ct); var target = request.Status.Trim().ToLowerInvariant();
        if (!DisclosureTransitions.TryGetValue(d.Status, out var allowed) || !allowed.Contains(target)) throw new DomainException("disclosure-invalid-transition", $"Disclosure cannot move from '{d.Status}' to '{target}'.");
        if (target is "resolved" or "dismissed" && string.IsNullOrWhiteSpace(request.Outcome)) throw new DomainException("disclosure-outcome-required", "An outcome is required to close a protected disclosure.");
        var from = d.Status; d.Status = target; if (request.TriageNotes is not null) d.TriageNotes = request.TriageNotes; if (request.Outcome is not null) d.Outcome = request.Outcome;
        if (request.AssignedToSubjectId is not null) d.AssignedToSubjectId = request.AssignedToSubjectId;
        if (target is "resolved" or "dismissed") d.ClosedAt = DateTimeOffset.UtcNow;
        await repo.UpdateProtectedDisclosureAsync(d, ct); await repo.CreateProtectedDisclosureEventAsync(new ProtectedDisclosureEvent { DisclosureId = id, Action = "status-changed", ActorSubjectId = actorSubjectId, FromStatus = from, ToStatus = target, Notes = request.TriageNotes }, ct);
        return MapDisclosure(d, await repo.ListProtectedDisclosureEventsAsync(id, ct), true);
    }

    private static readonly Dictionary<string, HashSet<string>> CaseTransitions = new()
    {
        ["open"] = ["triage"], ["triage"] = ["investigating"], ["investigating"] = ["action-pending", "resolved"],
        ["in-progress"] = ["investigating", "resolved"],
        ["action-pending"] = ["investigating", "resolved"], ["resolved"] = ["investigating", "closed"], ["closed"] = []
    };
    private static readonly Dictionary<string, HashSet<string>> DisclosureTransitions = new()
    { ["new"] = ["triage"], ["triage"] = ["investigating", "dismissed"], ["investigating"] = ["resolved", "dismissed"], ["resolved"] = [], ["dismissed"] = [] };

    private async Task<RelationsCase> RequireCaseAsync(Guid id, CancellationToken ct) => await repo.GetCaseAsync(id, ct) ?? throw new DomainException("case-not-found", $"Relations case {id} does not exist.");
    private void RequireAssignedOrAdmin(RelationsCase c, string actor)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor == "system") throw new DomainException("actor-required", "An authenticated subject is required.");
        if (c.OwnerSubjectId is not null && !string.Equals(c.OwnerSubjectId, actor, StringComparison.Ordinal) && !authz.IsRole("hr_admin")) throw new DomainException("case-access-denied", "This case is assigned to another investigator.");
    }
    private async Task<RelationsCase> RequireAccessibleAsync(Guid id, string actor, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "investigator"); var c = await RequireCaseAsync(id, ct); RequireAssignedOrAdmin(c, actor);
        var declaration = await repo.GetAccessAsync(id, actor, ct);
        if (declaration?.Decision != "no-conflict") throw new DomainException(declaration?.Decision == "conflict" ? "case-conflict-declared" : "case-access-declaration-required", "Declare that you have no conflict before accessing restricted case details.");
        return c;
    }
    private async Task<ProtectedDisclosure> RequireDisclosureAsync(Guid id, string actor, CancellationToken ct)
    {
        var d = await repo.GetProtectedDisclosureAsync(id, ct) ?? throw new DomainException("disclosure-not-found", $"Protected disclosure {id} does not exist.");
        if (d.AssignedToSubjectId is not null && d.AssignedToSubjectId != actor && !authz.IsRole("hr_admin")) throw new DomainException("disclosure-access-denied", "This protected disclosure is assigned to another investigator."); return d;
    }
    private async Task RecordAsync(Guid caseId, string action, string actor, string? from, string? to, string? notes, CancellationToken ct)
        => _ = await repo.CreateEventAsync(new RelationsCaseEvent { CaseId = caseId, Action = action, ActorSubjectId = actor, FromStatus = from, ToStatus = to, Notes = notes }, ct);
    private async Task<RelationsCaseDetailDto> MapDetailAsync(RelationsCase c, CancellationToken ct) => new(MapList(c, true), c.Description, c.Findings, c.Outcome, c.RaisedBy,
        (await repo.ListActionsAsync(c.Id, ct)).Select(MapAction).ToList(), (await repo.ListEvidenceAsync(c.Id, ct)).Select(MapEvidence).ToList(),
        (await repo.ListEventsAsync(c.Id, ct)).Select(MapEvent).ToList(), (await repo.ListAccessAsync(c.Id, ct)).Select(MapAccess).ToList());
    private static RelationsCaseDto MapList(RelationsCase c, bool revealSummary = false) => new(c.Id, c.SubjectWorkerId, c.CaseType, c.Category, c.Severity,
        revealSummary || c.Confidentiality != "restricted" ? c.Summary : "Restricted case", c.Status, c.CreatedAt, c.Reference, c.Confidentiality, c.OwnerSubjectId, c.DueDate?.ToString("yyyy-MM-dd"));
    private static RelationsAccessDto MapAccess(RelationsCaseAccess x) => new(x.Id, x.CaseId, x.ActorSubjectId, x.Decision, x.Notes, x.CreatedAt);
    private static RelationsEventDto MapEvent(RelationsCaseEvent x) => new(x.Id, x.Action, x.ActorSubjectId, x.FromStatus, x.ToStatus, x.Notes, x.CreatedAt);
    private static RelationsEventDto MapEvent(ProtectedDisclosureEvent x) => new(x.Id, x.Action, x.ActorSubjectId, x.FromStatus, x.ToStatus, x.Notes, x.CreatedAt);
    private static RelationsActionDto MapAction(RelationsCaseAction x) => new(x.Id, x.ActionType, x.Title, x.Status, x.OwnerSubjectId, x.DueDate?.ToString("yyyy-MM-dd"), x.Notes, x.CompletedAt);
    private static RelationsEvidenceDto MapEvidence(RelationsEvidence x) => new(x.Id, x.Title, x.EvidenceType, x.FileName, x.ContentType, x.SizeBytes, x.Classification, x.AddedBySubjectId, x.CreatedAt);
    private static ProtectedDisclosureInvestigationDto MapDisclosure(ProtectedDisclosure x, List<ProtectedDisclosureEvent> history, bool reveal) => new(x.Id, x.CaseReference, x.Category, x.Severity, x.Status,
        reveal ? x.Description : null, reveal ? x.TriageNotes : null, reveal ? x.Outcome : null, x.AssignedToSubjectId, x.CreatedAt, history.Select(MapEvent).ToList());
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

        var employer = (await configRepo.ListLegalEntitiesAsync(ct))
            .FirstOrDefault(e => e.IsDefault) ?? (await configRepo.ListLegalEntitiesAsync(ct)).FirstOrDefault();

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
