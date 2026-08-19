using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Benefits;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Benefits;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M41 Gap 6b: flexible benefit claims. HR defines claimable benefit
/// types, grants per-worker annual allowances, and claims against them are
/// validated against the allowance, approved by HR and marked paid.</summary>
public class M41Gap6bBenefitClaimsTests
{
    private sealed class NoOpAuthz(params string[] roles) : IAuthzService
    {
        private readonly HashSet<string> _roles = new(roles);
        public string CurrentSubjectId { get; set; } = "subject-hr";
        public void RequireAnyRole(params string[] roles)
        {
            if (!roles.Any(_roles.Contains))
                throw new DomainException("forbidden", $"Requires one of roles: {string.Join(", ", roles)}");
        }
        public bool IsRole(params string[] roles) => roles.Any(_roles.Contains);
        public bool CanAccessSensitive(string category) => true;
    }

    private static (BenefitServiceImpl svc, HrmDbContext ctx, Worker worker, BenefitType medical) Build(params string[] roles)
    {
        var ctx = TestDbContextFactory.Create("test-tenant");
        var authz = new NoOpAuthz(roles.Length == 0 ? ["hr_admin"] : roles);
        var workerRepo = new WorkerRepository(ctx);
        var benefitRepo = new BenefitRepository(ctx);
        var svc = new BenefitServiceImpl(benefitRepo, authz, workerRepo);

        var worker = new Worker
        {
            EmployeeNo = "EMP-BEN-001", FirstName = "Benefit", LastName = "Claimer",
            WorkerType = "employee", Status = "active", Nationality = "ZM",
            TenantId = "test-tenant", SubjectId = "subject-claimer",
        };
        ctx.Workers.Add(worker);

        var medical = new BenefitType
        {
            Code = "medical", Name = "Medical reimbursement", Description = "Receipt-backed medical",
            AnnualCap = 5000m, RequiresEvidence = true, IsActive = true, TenantId = "test-tenant",
        };
        ctx.BenefitTypes.Add(medical);

        var transport = new BenefitType
        {
            Code = "transport", Name = "Transport allowance", AnnualCap = 0,
            RequiresEvidence = false, IsActive = true, TenantId = "test-tenant",
        };
        ctx.BenefitTypes.Add(transport);
        ctx.SaveChanges();
        return (svc, ctx, worker, medical);
    }

    [Fact]
    public async Task CreateType_DuplicateCodeRejected()
    {
        var (svc, ctx, worker, medical) = Build();
        var hrAdmin = new BenefitServiceImpl(new BenefitRepository(ctx),
            new NoOpAuthz("hr_admin"), new WorkerRepository(ctx));
        await Assert.ThrowsAsync<DomainException>(() =>
            hrAdmin.CreateBenefitTypeAsync(
                new BenefitTypeCreateRequest("medical", "Dup Medical", null, 1000m, false), default));
    }

    [Fact]
    public async Task CreateType_NonAdminRejected()
    {
        var (svc, ctx, worker, medical) = Build();
        var hrOps = new BenefitServiceImpl(new BenefitRepository(ctx),
            new NoOpAuthz("hr_ops"), new WorkerRepository(ctx));
        await Assert.ThrowsAsync<DomainException>(() =>
            hrOps.CreateBenefitTypeAsync(
                new BenefitTypeCreateRequest("housing", "Housing", null, 2000m, false), default));
    }

    [Fact]
    public async Task Allowance_WithoutAllowance_OrgCapApplies()
    {
        var (svc, ctx, worker, medical) = Build();
        var claim = await svc.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 100m, "ZMW", "clinic", true), default);
        Assert.Equal("submitted", claim.Status);
        var approved = await svc.DecideClaimAsync(claim.Id,
            new ClaimDecideRequest("approve", "ok", null), default);
        Assert.Equal("approved", approved.Status);
        Assert.Equal(100m, approved.ApprovedAmount);
        // Only approved/paid claims consume the allowance, so approve the
        // 4900 claim first (100 + 4900 = 5000 fits the org cap); the next 1
        // does not.
        var claim2 = await svc.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 4900m, "ZMW", null, true), default);
        await svc.DecideClaimAsync(claim2.Id, new ClaimDecideRequest("approve", null, null), default);
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 1m, "ZMW", null, true), default));
    }

    [Fact]
    public async Task Allowance_WorkerAllowanceIsTheEffectiveCap()
    {
        var (svc, ctx, worker, medical) = Build();
        await svc.SetAllowanceAsync(new AllowanceSetRequest(worker.Id, "medical", 200m, DateTime.UtcNow.Year), default);
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 300m, "ZMW", null, true), default));
        var claim = await svc.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 200m, "ZMW", null, true), default);
        Assert.Equal(200m, claim.AmountClaimed);
    }

    [Fact]
    public async Task CreateClaim_NoAllowanceConfiguredRejected()
    {
        var (svc, ctx, worker, medical) = Build();
        medical.AnnualCap = 0;
        ctx.SaveChanges();
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 100m, "ZMW", null, true), default));
    }

    [Fact]
    public async Task CreateClaim_EvidenceRequired_WithoutEvidenceRejected()
    {
        var (svc, ctx, worker, medical) = Build();
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 100m, "ZMW", null, false), default));
    }

    [Fact]
    public async Task CreateClaim_InactiveTypeRejected()
    {
        var (svc, ctx, worker, medical) = Build();
        medical.IsActive = false;
        ctx.SaveChanges();
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 100m, "ZMW", null, true), default));
    }

    [Fact]
    public async Task Decide_RejectAndReturnTransitionsCorrectly()
    {
        var (svc, ctx, worker, medical) = Build();
        var claim = await svc.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 500m, "ZMW", "rx", true), default);
        var returned = await svc.DecideClaimAsync(claim.Id,
            new ClaimDecideRequest("return", "missing receipt", null), default);
        Assert.Equal("returned", returned.Status);
        var rejected = await svc.DecideClaimAsync(claim.Id,
            new ClaimDecideRequest("reject", "final decision", null), default);
        Assert.Equal("rejected", rejected.Status);
        await Assert.ThrowsAsync<DomainException>(() => svc.DecideClaimAsync(claim.Id,
            new ClaimDecideRequest("approve", null, null), default));
    }

    [Fact]
    public async Task Pay_OnlyApprovedClaimsCanBePaid()
    {
        var (svc, ctx, worker, medical) = Build();
        var claim = await svc.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 500m, "ZMW", "rx", true), default);
        var paidSubject = new NoOpAuthz("payroll") { CurrentSubjectId = "subject-payroll" };
        var payrollSvc = new BenefitServiceImpl(new BenefitRepository(ctx), paidSubject, new WorkerRepository(ctx));
        await Assert.ThrowsAsync<DomainException>(() => payrollSvc.PayClaimAsync(claim.Id, default));
        await svc.DecideClaimAsync(claim.Id, new ClaimDecideRequest("approve", null, null), default);
        var paid = await payrollSvc.PayClaimAsync(claim.Id, default);
        Assert.Equal("paid", paid.Status);
        Assert.Equal("subject-payroll", paid.PaidBySubjectId);
        Assert.NotNull(paid.PaidAt);
    }

    [Fact]
    public async Task ListClaims_FiltersByWorkerAndStatus()
    {
        var (svc, ctx, worker, medical) = Build();
        await svc.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 100m, "ZMW", null, true), default);
        var (items, total) = await svc.ListClaimsAsync(worker.Id, null, 1, 50, default);
        Assert.Single(items);
        Assert.Equal(1, total);
        var (empty, emptyTotal) = await svc.ListClaimsAsync(worker.Id, "paid", 1, 50, default);
        Assert.Empty(empty);
    }

    [Fact]
    public async Task EmployeeSelfScope_OnlyOwnWorkerAccessible()
    {
        var (svc, ctx, worker, medical) = Build();
        var employee = new BenefitServiceImpl(new BenefitRepository(ctx),
            new NoOpAuthz("employee") { CurrentSubjectId = "subject-claimer" }, new WorkerRepository(ctx));
        await employee.CreateClaimAsync(
            new BenefitClaimCreateRequest(worker.Id, "medical", 100m, "ZMW", null, true), default);
        var foreignWorker = new Worker
        {
            EmployeeNo = "EMP-BEN-002", FirstName = "Other", LastName = "Person",
            WorkerType = "employee", Status = "active", Nationality = "ZM",
            TenantId = "test-tenant", SubjectId = "subject-other",
        };
        ctx.Workers.Add(foreignWorker);
        ctx.SaveChanges();
        await Assert.ThrowsAsync<DomainException>(() => employee.CreateClaimAsync(
            new BenefitClaimCreateRequest(foreignWorker.Id, "medical", 100m, "ZMW", null, true), default));
    }
}
