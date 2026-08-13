using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.ConfigAndExtras;

/// <summary>Read-only configuration surface consumed by the admin screens: the
/// frontend's AdminConfigClient maps 1:1 onto AdminConfigDto.</summary>
public interface IConfigService
{
    Task<AdminConfigDto> GetConfigAsync(CancellationToken ct);
    Task<Paged<LeaveTypeDto>> ListLeaveTypesAsync(bool includeInactive, CancellationToken ct);
}

/// <summary>Recruitment (M7) surface.</summary>
public interface IRecruitmentService
{
    Task<Paged<VacancyDto>> ListVacanciesAsync(string? status, CancellationToken ct);
    Task<VacancyDto> CreateVacancyAsync(VacancyCreate request, CancellationToken ct);
    Task<Paged<CandidateDto>> ListCandidatesAsync(Guid vacancyId, string? stage, CancellationToken ct);
    Task<CandidateDto> CreateCandidateAsync(CandidateCreate request, CancellationToken ct);
    Task<CandidateDto> AdvanceCandidateAsync(Guid candidateId, CandidateAdvanceRequest request, CancellationToken ct);
    Task<OfferDto> CreateOfferAsync(OfferCreate request, CancellationToken ct);
}
public sealed record VacancyDto(Guid Id, string JobTitle, string? Grade, string Status, string OrgUnitName, DateTimeOffset CreatedAt);
public sealed record CandidateDto(Guid Id, Guid VacancyId, string FullName, string? Email, string? Phone, string Stage, string? Notes, DateTimeOffset CreatedAt);
public sealed record OfferDto(Guid Id, Guid CandidateId, decimal BaseSalary, string ContractType, string Status, DateTimeOffset CreatedAt);

/// <summary>Employee relations cases (M7): restricted-access case records.</summary>
public interface IRelationsService
{
    Task<Paged<RelationsCaseDto>> ListCasesAsync(string? category, CancellationToken ct);
    Task<RelationsCaseDto> CreateCaseAsync(RelationsCaseCreate request, CancellationToken ct);
}
public sealed record RelationsCaseDto(Guid Id, Guid? SubjectWorkerId, string CaseType, string Category, string Severity, string Summary, string Status, DateTimeOffset CreatedAt);

/// <summary>Documents & reports (M8).</summary>
public interface IDocumentsService
{
    Task<Paged<WorkerDocumentDto>> ListDocumentsAsync(Guid workerId, CancellationToken ct);
    Task<WorkerDocumentDto> RegisterDocumentAsync(WorkerDocumentCreate request, string storagePath, long sizeBytes, CancellationToken ct);
    Task<ReportDto> GetReportAsync(ReportQuery query, CancellationToken ct);
}
public sealed record WorkerDocumentDto(Guid Id, Guid WorkerId, string Category, string Title, string FileName, string ContentType, long SizeBytes, string Classification, string? ExpiryDate);

/// <summary>Persistence contracts implemented by EF Core in Infrastructure.</summary>
public interface IConfigRepository
{
    Task<List<LegalEntity>> ListLegalEntitiesAsync(CancellationToken ct);
    Task<List<WorkLocation>> ListLocationsAsync(CancellationToken ct);
    Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken ct);
    Task<List<WorkCalendar>> ListCalendarsAsync(CancellationToken ct);
    Task<List<LeaveType>> ListLeaveTypesAsync(bool includeInactive, CancellationToken ct);
    Task<List<CapabilityConfig>> ListCapabilitiesAsync(CancellationToken ct);
    Task<List<PayGroup>> ListPayGroupsAsync(CancellationToken ct);
    Task<List<Worker>> ListAllWorkersAsync(string? status, CancellationToken ct);
    Task<List<LeaveRequest>> ListLeaveRequestsAllAsync(string? status, CancellationToken ct);
    Task<List<PayrollRunLine>> ListRunLinesAllAsync(string periodFrom, string periodTo, CancellationToken ct);
}

public interface IRecruitmentRepository
{
    Task<(List<Vacancy> Items, int Total)> ListVacanciesAsync(string? status, CancellationToken ct);
    Task<Vacancy> CreateVacancyAsync(Vacancy vacancy, CancellationToken ct);
    Task<(List<Candidate> Items, int Total)> ListCandidatesAsync(Guid vacancyId, string? stage, CancellationToken ct);
    Task<Candidate> CreateCandidateAsync(Candidate candidate, CancellationToken ct);
    Task<Candidate?> GetCandidateAsync(Guid id, CancellationToken ct);
    Task<Offer> CreateOfferAsync(Offer offer, CancellationToken ct);
}

public interface IRelationsRepository
{
    Task<(List<RelationsCase> Items, int Total)> ListCasesAsync(string? category, CancellationToken ct);
    Task<RelationsCase> CreateCaseAsync(RelationsCase caseRecord, CancellationToken ct);
}

public interface IDocumentsRepository
{
    Task<(List<WorkerDocument> Items, int Total)> ListDocumentsAsync(Guid workerId, CancellationToken ct);
    Task<WorkerDocument> CreateDocumentAsync(WorkerDocument document, CancellationToken ct);
}

// Domain entities for recruitment/relations (kept small for M7)

public sealed class Vacancy : Entity
{
    public Guid OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public string JobTitle { get; set; } = null!;
    public string? Grade { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "draft"; // draft | published | closed | cancelled
    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}

public sealed class Candidate : Entity
{
    public Guid VacancyId { get; set; }
    public Vacancy? Vacancy { get; set; }
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public string Stage { get; set; } = "screening"; // screening | shortlisted | interviewed | offered | hired | rejected
    public ICollection<Offer> Offers { get; set; } = new List<Offer>();
}

public sealed class Offer : Entity
{
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public decimal BaseSalary { get; set; }
    public string ContractType { get; set; } = "permanent";
    public int ProbationMonths { get; set; } = 3;
    public int NoticeDays { get; set; } = 30;
    public string? StartDate { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "draft"; // draft | approved | issued | accepted | declined
}

public sealed class RelationsCase : Entity
{
    public Guid? SubjectWorkerId { get; set; }
    public Worker? SubjectWorker { get; set; }
    public string CaseType { get; set; } = null!;      // disciplinary | grievance | misconduct | investigation
    public string Category { get; set; } = null!;
    public string Severity { get; set; } = "medium";
    public string Summary { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Status { get; set; } = "open";        // open | in-progress | resolved | closed
    public string Classification { get; set; } = "restricted";
}
