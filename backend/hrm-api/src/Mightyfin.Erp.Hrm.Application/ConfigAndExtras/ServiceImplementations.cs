using System.Globalization;
using Mightyfin.Erp.Hrm.Application.Time;
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

public sealed class RecruitmentServiceImpl(IRecruitmentRepository repo, IAuthzService authz) : IRecruitmentService
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
        var v = new Vacancy { OrgUnitId = request.OrgUnitId, JobTitle = request.JobTitle, Grade = request.Grade, Description = request.Description, Status = request.Status };
        var created = await repo.CreateVacancyAsync(v, ct);
        return new VacancyDto(created.Id, created.JobTitle, created.Grade, created.Status, created.OrgUnit?.Name ?? "", created.CreatedAt);
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
        c.Stage = request.Stage;
        if (request.Notes is not null) c.Notes = (c.Notes + " | " + request.Notes).TrimStart(' ', '|');
        var updated = await repo.CreateCandidateAsync(c, ct);
        return new CandidateDto(updated.Id, updated.VacancyId, updated.FullName, updated.Email, updated.Phone, updated.Stage, updated.Notes, updated.CreatedAt);
    }

    public async Task<OfferDto> CreateOfferAsync(OfferCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var o = new Offer { CandidateId = request.CandidateId, BaseSalary = request.BaseSalary, ContractType = request.ContractType, ProbationMonths = request.ProbationMonths, NoticeDays = request.NoticeDays, StartDate = request.StartDate, Notes = request.Notes, Status = "draft" };
        var created = await repo.CreateOfferAsync(o, ct);
        return new OfferDto(created.Id, created.CandidateId, created.BaseSalary, created.ContractType, created.Status, created.CreatedAt);
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
        var c = new RelationsCase { SubjectWorkerId = request.SubjectWorkerId, CaseType = request.CaseType, Category = request.Category, Severity = request.Severity, Summary = request.Summary, Description = request.Description, Status = "open" };
        var created = await repo.CreateCaseAsync(c, ct);
        return new RelationsCaseDto(created.Id, created.SubjectWorkerId, created.CaseType, created.Category, created.Severity, created.Summary, created.Status, created.CreatedAt);
    }
}

public sealed class DocumentsServiceImpl(IDocumentsRepository repo, IConfigRepository configRepo, IAuthzService authz) : IDocumentsService
{
    public async Task<Paged<WorkerDocumentDto>> ListDocumentsAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "employee");
        var (items, total) = await repo.ListDocumentsAsync(workerId, ct);
        return new Paged<WorkerDocumentDto>(items.Select(d => new WorkerDocumentDto(d.Id, d.WorkerId, d.Category, d.Title, d.FileName, d.ContentType, d.SizeBytes, d.Classification, d.ExpiryDate?.ToString())).ToList(), total, 1, 50);
    }

    public async Task<WorkerDocumentDto> RegisterDocumentAsync(WorkerDocumentCreate request, string storagePath, long sizeBytes, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var d = new WorkerDocument { WorkerId = request.WorkerId, Category = request.Category, Title = request.Title, FileName = request.FileName, ContentType = request.ContentType, Classification = request.Classification, StoragePath = storagePath, SizeBytes = sizeBytes, IsLatest = true };
        var created = await repo.CreateDocumentAsync(d, ct);
        return new WorkerDocumentDto(created.Id, created.WorkerId, created.Category, created.Title, created.FileName, created.ContentType, created.SizeBytes, created.Classification, created.ExpiryDate?.ToString());
    }

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
