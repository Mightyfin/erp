using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M41 Gap 6a: leave encashment. HR converts unused leave balance
/// into a cash payout at the worker's daily rate (basic monthly / 26
/// working days). Approval posts a permanent encashment ledger entry;
/// rejection and cancellation touch nothing else.</summary>
public class M41Gap6EncashmentTests
{
    /// <summary>Real effect applier wired to the same repositories as the
    /// production graph so the workflow-engine approve path is exercised
    /// exactly as deployed.</summary>
    private sealed class RealLeaveEffectApplier(HrmDbContext ctx) : ILeaveEffectApplier
    {
        public async Task ApplyAsync(WorkflowRequest request, string decisionAction, CancellationToken ct)
        {
            var timeRepo = new TimeRepository(ctx);
            var applier = new LeaveEffectApplierImpl(timeRepo, NoOpTemplates.Instance,
                NoOpExperience.Instance, NoOpMerge.Instance, outbox: null);
            await applier.ApplyAsync(request, decisionAction, ct);
        }
    }

    private sealed class NoOpTemplates : ILetterTemplates
    {
        public static readonly NoOpTemplates Instance = new();
        public string Render(string letterType, LetterMergeContext ctx) => "";
    }

    private sealed class NoOpExperience : IExperienceRepository
    {
        public static readonly NoOpExperience Instance = new();
        public Task<(List<HrRequest> Items, int Total)> ListRequestsAsync(Guid? workerId, string? status, CancellationToken ct) =>
            Task.FromResult<(List<HrRequest>, int)>((new List<HrRequest>(), 0));
        public Task<HrRequest?> GetRequestAsync(Guid id, CancellationToken ct) => Task.FromResult<HrRequest?>(null);
        public Task<HrRequest> CreateRequestAsync(HrRequest request, CancellationToken ct) => Task.FromResult(request);
        public Task<HrRequest> UpdateRequestAsync(HrRequest request, CancellationToken ct) => Task.FromResult(request);
        public Task<HrRequest> AddMessageAsync(HrRequest request, HrRequestMessage message, CancellationToken ct) => Task.FromResult(request);
        public Task<(List<HrLetter> Items, int Total)> ListLettersAsync(Guid? workerId, string? status, CancellationToken ct) =>
            Task.FromResult<(List<HrLetter>, int)>((new List<HrLetter>(), 0));
        public Task<HrLetter?> GetLetterAsync(Guid id, CancellationToken ct) => Task.FromResult<HrLetter?>(null);
        public Task<HrLetter> CreateLetterAsync(HrLetter letter, CancellationToken ct) => Task.FromResult(letter);
        public Task<HrLetter> UpdateLetterAsync(HrLetter letter, CancellationToken ct) => Task.FromResult(letter);
        public Task<int> CountDisclosuresThisYearAsync(CancellationToken ct) => Task.FromResult(0);
        public Task<ProtectedDisclosure> CreateDisclosureAsync(ProtectedDisclosure disclosure, CancellationToken ct) => Task.FromResult(disclosure);
        public Task<ProtectedDisclosure?> GetDisclosureByCaseReferenceAsync(string caseReference, CancellationToken ct) => Task.FromResult<ProtectedDisclosure?>(null);
    }

    private sealed class NoOpMerge : IMergeDataProvider
    {
        public static readonly NoOpMerge Instance = new();
        public Task<LetterMergeData> GetMergeDataAsync(Guid workerId, string letterType, CancellationToken ct) =>
            Task.FromResult(new LetterMergeData("", "", null, null, null, null, null, null));
    }

    private static (TimeServiceImpl time, PayrollRepository payroll, HrmDbContext ctx, Worker worker) Build()
    {
        var ctx = TestDbContextFactory.Create("test-tenant");
        var authz = new PermissiveAuthz();
        var timeRepo = new TimeRepository(ctx);
        var wfRepo = new WorkflowRepository(ctx);
        var wf = new WorkflowServiceImpl(wfRepo, authz, new RealLeaveEffectApplier(ctx));
        var workerRepo = new WorkerRepository(ctx);
        var payrollRepo = new PayrollRepository(ctx);
        var time = new TimeServiceImpl(timeRepo, authz, wf, workerRepo, null, payrollRepo);

        var worker = new Worker
        {
            EmployeeNo = "EMP-ENC-001", FirstName = "Encash", LastName = "Me",
            WorkerType = "employee", Status = "active", Nationality = "ZM",
            TenantId = "test-tenant", SubjectId = "hr-subject",
        };
        ctx.Workers.Add(worker);

        var group = new PayGroup
        {
            Code = "TEST-GROUP", Name = "Test Group", Currency = "ZMW",
            Frequency = "monthly", CalendarDayOfMonth = 25,
            TenantId = "test-tenant",
        };
        ctx.PayGroups.Add(group);
        ctx.SaveChanges();

        var structure = new SalaryStructure
        {
            Code = "ENC-STD", Name = "Encash Standard", IsActive = true,
            TenantId = "test-tenant",
        };
        ctx.SalaryStructures.Add(structure);

        var basic = new SalaryComponent
        {
            Code = "basic", Name = "basic", ComponentType = "earning",
            CalculationBasis = "fixed", Priority = 10, IsActive = true, Version = 1,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            TenantId = "test-tenant",
        };
        ctx.SalaryComponents.Add(basic);
        ctx.SaveChanges();

        var profile = new WorkerPayrollProfile
        {
            WorkerId = worker.Id, PayGroupId = group.Id, StructureId = structure.Id,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            PayBasis = "salary", TenantId = "test-tenant",
        };
        profile.ComponentValues.Add(new WorkerComponentValue
        {
            ComponentId = basic.Id, Component = basic, Amount = 2600m,
            TenantId = "test-tenant",
        });
        ctx.WorkerPayrollProfiles.Add(profile);

        ctx.LeaveTypes.Add(new LeaveType
        {
            Code = "annual", Name = "Annual Leave", Category = "annual",
            DefaultDaysPerYear = 12, IsActive = true, AllowNegative = false,
            MaxConsecutiveDays = 999, RequiresEvidence = false, MinNoticeDays = 0,
            AllowsPartialDays = false, CarryForwardDays = 0, CarryForwardExpiryMonths = 0,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            TenantId = "test-tenant",
        });
        ctx.SaveChanges();
        return (time, payrollRepo, ctx, worker);
    }

    private static async Task SeedBalance(HrmDbContext ctx, Worker worker, decimal days = 10m)
    {
        var ledger = new LeaveBalanceLedger
        {
            WorkerId = worker.Id, LeaveTypeCode = "annual", Days = days,
            Reason = "annual-accrual", ForDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = "test-tenant",
        };
        ctx.LeaveBalanceLedgers.Add(ledger);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task RateQuote_DerivesDailyRateFromBasicOver26Days()
    {
        var (time, _, _, worker) = Build();
        var quote = await time.GetEncashmentRateAsync(worker.Id, "annual", 5m, CancellationToken.None);
        Assert.Equal(2600m, quote.MonthlyBasic);
        Assert.Equal(100m, quote.DailyRate);
        // 5/26 * 2600 = 500
        Assert.Equal(500m, quote.EstimatedGross);
        Assert.Equal("ZMW", quote.Currency);
    }

    [Fact]
    public async Task RateQuote_ZeroBasicYieldsZeroQuote()
    {
        // A worker with no pay profile (or only non-basic components) quotes zero.
        var ctx = TestDbContextFactory.Create("test-tenant");
        var authz = new PermissiveAuthz();
        var time = new TimeServiceImpl(new TimeRepository(ctx), authz,
            new WorkflowServiceImpl(new WorkflowRepository(ctx), authz, new RealLeaveEffectApplier(ctx)),
            new WorkerRepository(ctx), null, new PayrollRepository(ctx));
        var worker = new Worker
        {
            EmployeeNo = "EMP-NO-PAY", FirstName = "No", LastName = "Payroll",
            WorkerType = "employee", Status = "active", Nationality = "ZM",
            TenantId = "test-tenant",
        };
        ctx.Workers.Add(worker);
        await ctx.SaveChangesAsync();

        var quote = await time.GetEncashmentRateAsync(worker.Id, "annual", 5m, CancellationToken.None);
        Assert.Equal(0m, quote.MonthlyBasic);
        Assert.Equal(0m, quote.DailyRate);
        Assert.Equal(0m, quote.EstimatedGross);
    }

    [Fact]
    public async Task CreateEncashment_ValidatesZeroDays()
    {
        var (time, _, ctx, worker) = Build();
        await SeedBalance(ctx, worker);
        await Assert.ThrowsAsync<DomainException>(() =>
            time.CreateEncashmentAsync(new LeaveEncashmentCreateRequest(worker.Id, "annual", 0m, "x"),
                "hr-subject", CancellationToken.None));
    }

    [Fact]
    public async Task CreateEncashment_RejectsUnknownLeaveType()
    {
        var (time, _, ctx, worker) = Build();
        await SeedBalance(ctx, worker);
        await Assert.ThrowsAsync<DomainException>(() =>
            time.CreateEncashmentAsync(new LeaveEncashmentCreateRequest(worker.Id, "does-not-exist", 1m, "x"),
                "hr-subject", CancellationToken.None));
    }

    [Fact]
    public async Task CreateEncashment_InsufficientBalanceRejected()
    {
        var (time, _, ctx, worker) = Build();
        await SeedBalance(ctx, worker, 2m);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            time.CreateEncashmentAsync(new LeaveEncashmentCreateRequest(worker.Id, "annual", 5m, "vacation payout"),
                "hr-subject", CancellationToken.None));
        Assert.Equal("encashment-insufficient-balance", ex.Code);
    }

    [Fact]
    public async Task CreateEncashment_StoresQuotedRateAndOpensWorkflow()
    {
        var (time, _, ctx, worker) = Build();
        await SeedBalance(ctx, worker);
        var created = await time.CreateEncashmentAsync(
            new LeaveEncashmentCreateRequest(worker.Id, "annual", 5m, "vacation payout"),
            "hr-subject", CancellationToken.None);
        Assert.Equal("submitted", created.Status);
        Assert.Equal(2600m, created.MonthlyRate);
        Assert.Equal(500m, created.GrossAmount);
        var wfRequest = await ctx.WorkflowRequests.FirstAsync(w => w.WorkflowType == "leave-encashment", CancellationToken.None);
        Assert.Equal(worker.Id, wfRequest.SubjectWorkerId);
    }

    [Fact]
    public async Task ApproveViaWorkflow_PostsLedgerDeductionAndReducesBalance()
    {
        var (time, payroll, ctx, worker) = Build();
        await SeedBalance(ctx, worker);
        var created = await time.CreateEncashmentAsync(
            new LeaveEncashmentCreateRequest(worker.Id, "annual", 4m, "cash payout"),
            "hr-subject", CancellationToken.None);
        var wfRequest = await ctx.WorkflowRequests.FirstAsync(w => w.WorkflowType == "leave-encashment", CancellationToken.None);

        var decided = await time.DecideEncashmentAsync(created.Id,
            new LeaveEncashmentDecideRequest("approve", "approved for payout"),
            "hr-subject", CancellationToken.None);
        Assert.Equal("approved", decided.Status);

        var ledger = await ctx.LeaveBalanceLedgers
            .Where(l => l.ReferenceType == "encashment" && l.WorkerId == worker.Id).ToListAsync(CancellationToken.None);
        Assert.Single(ledger);
        Assert.Equal(-4m, ledger[0].Days);
        Assert.Equal("encashment", ledger[0].Reason);
        Assert.Equal(created.Id, ledger[0].ReferenceId);
        var notes = ctx.LeaveBalanceLedgers
            .Where(l => l.WorkerId == worker.Id).ToList()
            .Select(l => l.Note);
        Assert.Contains(notes, n => n is not null && n.Contains(created.GrossAmount.ToString()));

        // balance recomputation
        var totalDays = await ctx.LeaveBalanceLedgers
            .Where(l => l.WorkerId == worker.Id && l.LeaveTypeCode == "annual")
            .SumAsync(l => l.Days, CancellationToken.None);
        Assert.Equal(6m, totalDays);
    }

    [Fact]
    public async Task RejectViaWorkflow_LeavesBalanceUntouched()
    {
        var (time, _, ctx, worker) = Build();
        await SeedBalance(ctx, worker);
        var created = await time.CreateEncashmentAsync(
            new LeaveEncashmentCreateRequest(worker.Id, "annual", 4m, "nope"),
            "hr-subject", CancellationToken.None);
        var decided = await time.DecideEncashmentAsync(created.Id,
            new LeaveEncashmentDecideRequest("reject", "budget constraints"),
            "hr-subject", CancellationToken.None);
        Assert.Equal("rejected", decided.Status);
        var ledger = await ctx.LeaveBalanceLedgers
            .Where(l => l.WorkerId == worker.Id).ToListAsync(CancellationToken.None);
        Assert.Single(ledger); // only the original accrual
        Assert.Equal(10m, ledger[0].Days);
    }

    [Fact]
    public async Task DoubleApprovalBlocked_OnlyOneLedgerDeductionPosted()
    {
        var (time, _, ctx, worker) = Build();
        await SeedBalance(ctx, worker, 12m);
        var created = await time.CreateEncashmentAsync(
            new LeaveEncashmentCreateRequest(worker.Id, "annual", 2m, "d1"),
            "hr-subject", CancellationToken.None);
        await time.DecideEncashmentAsync(created.Id,
            new LeaveEncashmentDecideRequest("approve", "ok"),
            "hr-subject", CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() =>
            time.DecideEncashmentAsync(created.Id,
                new LeaveEncashmentDecideRequest("approve", "again?"),
                "hr-subject", CancellationToken.None));

        var ledger = await ctx.LeaveBalanceLedgers
            .Where(l => l.ReferenceType == "encashment").ToListAsync(CancellationToken.None);
        Assert.Single(ledger);
        Assert.Equal(-2m, ledger[0].Days);
    }
}
