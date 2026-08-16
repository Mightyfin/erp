using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M7 tests: recruitment pipeline, offer-to-preboarding conversion,
/// employee-relations case management and anonymous speak-up intake.</summary>
public class RecruitmentRelationsTests
{
    private static HrmDbContext NewContext(string tenant = "test-tenant") => TestDbContextFactory.Create(tenant);

    private static (RecruitmentServiceImpl Recruitment, IExperienceService Experience, IRelationsService Relations, HrmDbContext Ctx) Build()
    {
        var ctx = NewContext();
        var authz = new PermissiveAuthz();
        var workers = new WorkerServiceImpl(new WorkerRepository(ctx), authz, new UlidIdProvider());
        var lifecycle = new WorkerLifecycleServiceImpl(new WorkerRepository(ctx), authz);
        var recruitment = new RecruitmentServiceImpl(
            new RecruitmentRepository(ctx), authz, workers, lifecycle, new ConfigRepository(ctx));
        var relations = new RelationsServiceImpl(new RelationsRepository(ctx), authz);
        var experience = new ExperienceServiceImpl(
            new ExperienceRepository(ctx), authz, new StubWorkflowService(), new StubLetterTemplates(), new StubMergeData());
        return (recruitment, experience, relations, ctx);
    }

    private static async Task SeedOrgAsync(HrmDbContext ctx)
    {
        var legalEntity = new LegalEntity { Code = "TE", RegisteredName = "Test Entity Ltd", TradingName = "TE" };
        var orgUnit = new OrgUnit { Code = "ENG", Name = "Engineering", LegalEntityId = legalEntity.Id };
        var location = new WorkLocation { Code = "LUS01", Name = "Lusaka HQ", LegalEntityId = legalEntity.Id };
        ctx.Set<LegalEntity>().Add(legalEntity);
        ctx.Set<OrgUnit>().Add(orgUnit);
        ctx.Set<WorkLocation>().Add(location);
        await ctx.SaveChangesAsync();
    }

    private static async Task<Vacancy> SeedVacancyAsync(HrmDbContext ctx, string status = "draft")
    {
        var orgUnit = await ctx.Set<OrgUnit>().FirstAsync();
        var v = new Vacancy { JobTitle = "Software Engineer", OrgUnitId = orgUnit.Id, Status = status };
        ctx.Set<Vacancy>().Add(v);
        await ctx.SaveChangesAsync();
        return v;
    }

    private static async Task<Candidate> SeedCandidateAsync(HrmDbContext ctx, Vacancy vacancy, string stage = "screening")
    {
        var c = new Candidate { VacancyId = vacancy.Id, FullName = "Jane Doe", Email = "jane@example.com", Stage = stage };
        ctx.Set<Candidate>().Add(c);
        await ctx.SaveChangesAsync();
        return c;
    }

    [Fact]
    public async Task VacancyLifecycle_OnlyDraftCanBePublished()
    {
        var ctx = NewContext();
        await SeedOrgAsync(ctx);
        var vacancy = await SeedVacancyAsync(ctx, "draft");
        var svc = new RecruitmentServiceImpl(new RecruitmentRepository(ctx), new PermissiveAuthz(),
            new WorkerServiceImpl(new WorkerRepository(ctx), new PermissiveAuthz(), new UlidIdProvider()),
            new WorkerLifecycleServiceImpl(new WorkerRepository(ctx), new PermissiveAuthz()),
            new ConfigRepository(ctx));

        var published = await svc.PublishVacancyAsync(vacancy.Id, CancellationToken.None);
        Assert.Equal("published", published.Status);

        // Published vacancies cannot be re-published.
        await Assert.ThrowsAsync<DomainException>(() => svc.PublishVacancyAsync(vacancy.Id, CancellationToken.None));

        var closed = await svc.CloseVacancyAsync(vacancy.Id, CancellationToken.None);
        Assert.Equal("closed", closed.Status);

        // Closed vacancies cannot be re-published either.
        await Assert.ThrowsAsync<DomainException>(() => svc.PublishVacancyAsync(vacancy.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CandidateAdvance_RejectsInvalidAndTerminalStages()
    {
        var ctx = NewContext();
        await SeedOrgAsync(ctx);
        var vacancy = await SeedVacancyAsync(ctx);
        var candidate = await SeedCandidateAsync(ctx, vacancy, "interviewed");
        var svc = new RecruitmentServiceImpl(new RecruitmentRepository(ctx), new PermissiveAuthz(),
            new WorkerServiceImpl(new WorkerRepository(ctx), new PermissiveAuthz(), new UlidIdProvider()),
            new WorkerLifecycleServiceImpl(new WorkerRepository(ctx), new PermissiveAuthz()),
            new ConfigRepository(ctx));

        var advanced = await svc.AdvanceCandidateAsync(candidate.Id,
            new CandidateAdvanceRequest(Stage: "offered", Score: "8/10"), CancellationToken.None);
        Assert.Equal("offered", advanced.Stage);

        // Rejected is terminal: no further advances allowed.
        await svc.AdvanceCandidateAsync(candidate.Id,
            new CandidateAdvanceRequest(Stage: "rejected"), CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(() => svc.AdvanceCandidateAsync(candidate.Id,
            new CandidateAdvanceRequest(Stage: "screening"), CancellationToken.None));

        // Nonsense stages are rejected.
        var c2 = await SeedCandidateAsync(ctx, vacancy);
        await Assert.ThrowsAsync<DomainException>(() => svc.AdvanceCandidateAsync(c2.Id,
            new CandidateAdvanceRequest(Stage: "dragon-tested"), CancellationToken.None));
    }

    [Fact]
    public async Task OfferCreate_RequiresCandidateInOfferedStage()
    {
        var ctx = NewContext();
        await SeedOrgAsync(ctx);
        var vacancy = await SeedVacancyAsync(ctx);
        var candidate = await SeedCandidateAsync(ctx, vacancy, "screening");
        var svc = new RecruitmentServiceImpl(new RecruitmentRepository(ctx), new PermissiveAuthz(),
            new WorkerServiceImpl(new WorkerRepository(ctx), new PermissiveAuthz(), new UlidIdProvider()),
            new WorkerLifecycleServiceImpl(new WorkerRepository(ctx), new PermissiveAuthz()),
            new ConfigRepository(ctx));

        await Assert.ThrowsAsync<DomainException>(() => svc.CreateOfferAsync(
            new OfferCreate(CandidateId: candidate.Id, BaseSalary: 50000, ContractType: "permanent"),
            CancellationToken.None));

        await svc.AdvanceCandidateAsync(candidate.Id, new CandidateAdvanceRequest(Stage: "shortlisted"), CancellationToken.None);
        await svc.AdvanceCandidateAsync(candidate.Id, new CandidateAdvanceRequest(Stage: "interviewing"), CancellationToken.None);
        await svc.AdvanceCandidateAsync(candidate.Id, new CandidateAdvanceRequest(Stage: "interviewed"), CancellationToken.None);
        var offered = await svc.AdvanceCandidateAsync(candidate.Id, new CandidateAdvanceRequest(Stage: "offered"), CancellationToken.None);
        var offer = await svc.CreateOfferAsync(
            new OfferCreate(CandidateId: candidate.Id, BaseSalary: 50000, ContractType: "permanent"),
            CancellationToken.None);
        Assert.Equal("draft", offer.Status);
    }

    [Fact]
    public async Task OfferAccept_ConvertsCandidateToPreboardingWorkerWithAssignment()
    {
        var ctx = NewContext();
        await SeedOrgAsync(ctx);
        var vacancy = await SeedVacancyAsync(ctx);
        var candidate = await SeedCandidateAsync(ctx, vacancy, "offered");
        var svc = new RecruitmentServiceImpl(new RecruitmentRepository(ctx), new PermissiveAuthz(),
            new WorkerServiceImpl(new WorkerRepository(ctx), new PermissiveAuthz(), new UlidIdProvider()),
            new WorkerLifecycleServiceImpl(new WorkerRepository(ctx), new PermissiveAuthz()),
            new ConfigRepository(ctx));

        var offer = await svc.CreateOfferAsync(
            new OfferCreate(CandidateId: candidate.Id, BaseSalary: 60000, ContractType: "permanent",
                ProbationMonths: 3, NoticeDays: 30, StartDate: "2026-09-01"), CancellationToken.None);
        var offerRepo = new RecruitmentRepository(ctx);
        var offerEntity = await offerRepo.GetOfferAsync(offer.Id, CancellationToken.None) ?? throw new InvalidOperationException();
        offerEntity.Status = "issued";
        await offerRepo.UpdateOfferAsync(offerEntity, CancellationToken.None);

        var result = await svc.AcceptOfferAsync(offer.Id,
            new OfferAcceptRequest(EmployeeNo: "PD-2026-001", StartDate: "2026-09-01"), CancellationToken.None);

        Assert.Equal("preboarding", result.Status);
        Assert.Equal("PD-2026-001", result.EmployeeNo);

        var worker = await ctx.Workers.FirstAsync(w => w.Id == result.WorkerId);
        Assert.Equal("pre-hire", worker.Status);
        Assert.Equal("Software Engineer", worker.JobTitle);

        var assignment = await ctx.Set<Assignment>().FirstAsync(a => a.Id == result.AssignmentId);
        Assert.Equal("permanent", assignment.ContractType);
        Assert.Equal(3, assignment.ProbationMonths);

        // Acceptance creates a preboarding case and keeps the worker inactive.
        var candidateAfter = await ctx.Set<Candidate>().FirstAsync(c => c.Id == candidate.Id);
        Assert.Equal("preboarding", candidateAfter.Stage);
        var preboarding = await ctx.PreboardingCases.Include(x => x.Tasks).SingleAsync(x => x.CandidateId == candidate.Id);
        Assert.Equal(5, preboarding.Tasks.Count);
        await Assert.ThrowsAsync<DomainException>(() => svc.AcceptOfferAsync(offer.Id,
            new OfferAcceptRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task M29_FullJourney_InterviewOfferPreboardingActivatesWorker()
    {
        var (svc, _, _, ctx) = Build();
        await SeedOrgAsync(ctx);
        var vacancy = await SeedVacancyAsync(ctx, "published");
        var candidate = await svc.CreateCandidateAsync(new CandidateCreate(vacancy.Id, "Mary Phiri", "mary@example.com"), CancellationToken.None);
        Assert.Equal("applied", candidate.Stage);
        await svc.AdvanceCandidateAsync(candidate.Id, new CandidateAdvanceRequest("screening"), CancellationToken.None);
        await svc.AdvanceCandidateAsync(candidate.Id, new CandidateAdvanceRequest("shortlisted"), CancellationToken.None);
        var interview = await svc.CreateInterviewAsync(candidate.Id, new InterviewCreateRequest("2026-08-20T09:00:00Z", "panel", "Hiring panel"), CancellationToken.None);
        await svc.DecideInterviewAsync(interview.Id, new InterviewDecisionRequest(4, "hire", "Strong evidence"), CancellationToken.None);
        await svc.AdvanceCandidateAsync(candidate.Id, new CandidateAdvanceRequest("offered"), CancellationToken.None);
        var offer = await svc.CreateOfferAsync(new OfferCreate(candidate.Id, 120000, "2026-09-01", ExpiresOn: "2026-08-25"), CancellationToken.None);
        offer = await svc.ApproveOfferAsync(offer.Id, CancellationToken.None);
        offer = await svc.IssueOfferAsync(offer.Id, CancellationToken.None);
        var accepted = await svc.AcceptOfferAsync(offer.Id, new OfferAcceptRequest(StartDate: "2026-09-01"), CancellationToken.None);
        var preboarding = (await svc.ListPreboardingAsync("preboarding", CancellationToken.None)).Items.Single(x => x.WorkerId == accepted.WorkerId);
        foreach (var task in preboarding.Tasks)
            await svc.UpdatePreboardingTaskAsync(preboarding.Id, task.Id, new PreboardingTaskUpdateRequest("completed"), CancellationToken.None);
        var activated = await svc.ActivatePreboardingAsync(preboarding.Id, CancellationToken.None);
        Assert.Equal("active", activated.Status);
        Assert.Equal("active", (await ctx.Workers.SingleAsync(x => x.Id == accepted.WorkerId)).Status);
        Assert.Equal("hired", (await ctx.Candidates.SingleAsync(x => x.Id == candidate.Id)).Stage);
        Assert.Equal(8, await ctx.CandidateStageEvents.CountAsync(x => x.CandidateId == candidate.Id));
    }

    [Fact]
    public async Task RelationsCase_RequiresHrAdminAndValidStatusTransitions()
    {
        var ctx = NewContext();
        await SeedOrgAsync(ctx);
        var svc = new RelationsServiceImpl(new RelationsRepository(ctx), new PermissiveAuthz());

        var created = await svc.CreateCaseAsync(new RelationsCaseCreate(
            SubjectWorkerId: null, CaseType: "disciplinary", Category: "attendance", Severity: "high",
            Summary: "Repeated lateness", Description: "Three occasions in July"), CancellationToken.None);
        Assert.Equal("open", created.Status);

        var updated = await svc.UpdateCaseAsync(created.Id, new RelationsCaseUpdate(Status: "in-progress", Severity: null, Summary: null, Description: null), CancellationToken.None);
        Assert.Equal("in-progress", updated.Status);

        var resolved = await svc.UpdateCaseAsync(created.Id,
            new RelationsCaseUpdate(Status: "resolved", Severity: null, Summary: null, Description: null, Outcome: "Written warning issued"), CancellationToken.None);
        Assert.Equal("resolved", resolved.Status);
        var persisted = await new RelationsRepository(ctx).GetCaseAsync(created.Id, CancellationToken.None);
        Assert.Equal("Written warning issued", persisted?.Outcome);

        await Assert.ThrowsAsync<DomainException>(() => svc.UpdateCaseAsync(created.Id,
            new RelationsCaseUpdate(Status: "dragon-slayed", Severity: null, Summary: null, Description: null), CancellationToken.None));

        var missing = await Assert.ThrowsAsync<DomainException>(() => svc.UpdateCaseAsync(Guid.NewGuid(),
            new RelationsCaseUpdate(Status: "closed", Severity: null, Summary: null, Description: null), CancellationToken.None));
        Assert.Equal("case-not-found", missing.Code);
    }

    [Fact]
    public async Task SpeakUp_IsAnonymousAndStatusLookupRequiresSecretAccessCode()
    {
        var ctx = NewContext();
        var svc = new ExperienceServiceImpl(new ExperienceRepository(ctx), new PermissiveAuthz(),
            new StubWorkflowService(), new StubLetterTemplates(), new StubMergeData());

        var submitted = await svc.SubmitDisclosureAsync(
            new ProtectedDisclosureCreate(Category: "financial-misconduct", Severity: "high",
                Description: "Suspicious vendor payments"), CancellationToken.None);
        Assert.StartsWith("SD-", submitted.CaseReference);
        Assert.Equal(10, submitted.AccessCode.Length);
        Assert.Equal("new", submitted.Status);

        // Anyone with the access code can check status; identity was never requested.
        var status = await svc.GetDisclosureStatusAsync(submitted.CaseReference, submitted.AccessCode, CancellationToken.None);
        Assert.NotNull(status);
        Assert.Equal(submitted.CaseReference, status!.CaseReference);
        Assert.Equal("new", status.Status);

        var forged = await svc.GetDisclosureStatusAsync(submitted.CaseReference, "WRONGCODEX", CancellationToken.None);
        Assert.Null(forged);

        // No role check was performed anywhere in the anonymous path.
    }

    [Fact]
    public async Task TenantFilter_ScopesCandidatesAndCasesToCurrentTenant()
    {
        var ctxA = TestDbContextFactory.Create("tenant-a");
        var ctxB = TestDbContextFactory.Create("tenant-b");
        await SeedOrgAsync(ctxA);
        await SeedOrgAsync(ctxB);
        var orgA = await ctxA.Set<OrgUnit>().FirstAsync();
        var orgB = await ctxB.Set<OrgUnit>().FirstAsync();
        var vA = new Vacancy { JobTitle = "A role", OrgUnitId = orgA.Id, Status = "draft" };
        var vB = new Vacancy { JobTitle = "B role", OrgUnitId = orgB.Id, Status = "draft" };
        ctxA.Set<Vacancy>().Add(vA); await ctxA.SaveChangesAsync();
        ctxB.Set<Vacancy>().Add(vB); await ctxB.SaveChangesAsync();
        var cA = new Candidate { VacancyId = vA.Id, FullName = "A", Email = "a@example.com", Stage = "screening" };
        var cB = new Candidate { VacancyId = vB.Id, FullName = "B", Email = "b@example.com", Stage = "screening" };
        ctxA.Set<Candidate>().Add(cA); await ctxA.SaveChangesAsync();
        ctxB.Set<Candidate>().Add(cB); await ctxB.SaveChangesAsync();

        Assert.Equal(1, await ctxA.Set<Candidate>().CountAsync());
        Assert.Equal(1, await ctxB.Set<Candidate>().CountAsync());
    }
}

/// <summary>Stub merge-data provider for the experience service tests.</summary>
internal sealed class StubMergeData : IMergeDataProvider
{
    public Task<LetterMergeData> GetMergeDataAsync(Guid workerId, string letterType, CancellationToken ct)
        => Task.FromResult(new LetterMergeData("X", "X", null, null, null, null, null, null));
}

internal sealed class StubWorkflowService : IWorkflowService
{
    public Task<WorkflowRequest> OpenAsync(string workflowType, Guid subjectId, Guid? subjectWorkerId, string payloadJson, CancellationToken ct)
        => Task.FromResult((WorkflowRequest)null!);
    public Task<WorkflowRequest> DecideAsync(Guid requestId, Guid actorId, WorkflowDecisionRequest decision, CancellationToken ct)
        => Task.FromResult((WorkflowRequest)null!);
    public Task<WorkflowRequest?> GetOpenBySubjectAsync(string workflowType, Guid subjectWorkerId, CancellationToken ct)
        => Task.FromResult<WorkflowRequest?>(null);
    public Task<WorkflowRequest> CancelAsync(Guid requestId, CancellationToken ct)
        => Task.FromResult((WorkflowRequest)null!);
    public Task<WorkflowRequest> EscalateAsync(Guid requestId, Guid actorId, CancellationToken ct)
        => Task.FromResult((WorkflowRequest)null!);
    public Task<Paged<WorkQueueItemDto>> GetWorkQueueAsync(CancellationToken ct)
        => Task.FromResult(new Paged<WorkQueueItemDto>([], 0, 1, 50));
    public Task<WorkflowRequestDto?> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult<WorkflowRequestDto?>(null);
    public Task ApplyDecisionEffectsAsync(WorkflowRequest request, CancellationToken ct)
        => Task.CompletedTask;
    public Task<EscalationRunDto> EscalateOverdueAsync(CancellationToken ct)
        => Task.FromResult(new EscalationRunDto(0, 0, DateTimeOffset.UtcNow));
}

internal sealed class StubLetterTemplates : ILetterTemplates
{
    public string Render(string letterType, LetterMergeContext ctx) => "";
}
