using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M2: worker lifecycle tests (assignments, movements, contacts,
/// bank details, onboarding/offboarding) over EF InMemory.</summary>
public class WorkerLifecycleServiceTests
{
    private static (WorkerLifecycleServiceImpl service, HrmDbContext ctx, Worker worker) BuildWithWorker(
        string tenant = "test-tenant")
    {
        var ctx = TestDbContextFactory.Create(tenant);
        var repo = new WorkerRepository(ctx);
        var service = new WorkerLifecycleServiceImpl(repo, new PermissiveAuthz());

        // Seed org structure the lifecycle service validates references against.
        var entity = new LegalEntity { RegisteredName = "TestCo", Code = "TST", PacraNumber = "123" };
        var unit = new OrgUnit { Name = "Engineering", Code = "ENG", Status = "active", LegalEntityId = entity.Id };
        var location = new WorkLocation { Name = "Lusaka HQ", Code = "LUN", LegalEntityId = entity.Id };
        ctx.LegalEntities.Add(entity);
        ctx.OrgUnits.Add(unit);
        ctx.WorkLocations.Add(location);

        var worker = new Worker
        {
            EmployeeNo = "EMP-LC-001", FirstName = "Lifecycle", LastName = "Worker",
            WorkerType = "employee", Status = "active", Nationality = "ZM", OrgUnitId = unit.Id, LocationId = location.Id,
        };
        repo.CreateAsync(worker, CancellationToken.None).GetAwaiter().GetResult();
        return (service, ctx, worker);
    }

    [Fact]
    public async Task CreateAssignment_FutureStart_IsProposed()
    {
        var (service, ctx, worker) = BuildWithWorker();
        var entity = ctx.LegalEntities.First();
        var unit = ctx.OrgUnits.First();
        var location = ctx.WorkLocations.First();

        var dto = await service.CreateAssignmentAsync(worker.Id, new AssignmentCreateRequest(
            WorkerId: worker.Id, LegalEntityId: entity.Id, OrgUnitId: unit.Id, LocationId: location.Id,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd"),
            ContractType: "permanent"), CancellationToken.None);

        Assert.Equal("proposed", dto.Status);
    }

    [Fact]
    public async Task CreateAssignment_PastStart_AppliesToWorker()
    {
        var (service, ctx, worker) = BuildWithWorker();
        var entity = ctx.LegalEntities.First();
        var unit = ctx.OrgUnits.First();
        var location = ctx.WorkLocations.First();

        var dto = await service.CreateAssignmentAsync(worker.Id, new AssignmentCreateRequest(
            WorkerId: worker.Id, LegalEntityId: entity.Id, OrgUnitId: unit.Id, LocationId: location.Id,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)).ToString("yyyy-MM-dd"),
            ContractType: "fixed-term"), CancellationToken.None);

        Assert.Equal("current", dto.Status);
        // ApplyAssignmentToWorker should have updated the worker's org unit.
        var refreshed = await ctx.Workers.FirstAsync(w => w.Id == worker.Id);
        Assert.Equal(unit.Id, refreshed.OrgUnitId);
    }

    [Fact]
    public async Task CreateMovement_RejectsBackdating()
    {
        var (service, ctx, worker) = BuildWithWorker();
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateMovementAsync(
            worker.Id,
            new MovementCreateRequest(worker.Id, "transfer", "team move",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)).ToString("yyyy-MM-dd"),
                ToOrgUnitId: null, ToJobTitle: null, ToGrade: null, ToLocationId: null,
                ToManagerId: null, SalaryChange: null),
            CancellationToken.None));
        Assert.Equal("movement-backdated", ex.Code);
    }

    [Fact]
    public async Task Movement_Lifecycle_GoesDraftToPendingToApproved()
    {
        var (service, ctx, worker) = BuildWithWorker();

        var target = ctx.OrgUnits.First();
        var created = await service.CreateMovementAsync(worker.Id,
            new MovementCreateRequest(worker.Id, "transfer", "team move",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)).ToString("yyyy-MM-dd"),
                ToOrgUnitId: target.Id, ToJobTitle: "Senior Engineer", ToGrade: null, ToLocationId: null,
                ToManagerId: null, SalaryChange: null),
            CancellationToken.None);
        Assert.Equal("draft", created.Status);

        await service.SubmitMovementAsync(worker.Id, created.Id, CancellationToken.None);
        var pending = await service.GetMovementAsync(worker.Id, created.Id, CancellationToken.None);
        Assert.Equal("pending", pending!.Status);

        await service.ApproveMovementAsync(worker.Id, created.Id, CancellationToken.None);
        var approved = await service.GetMovementAsync(worker.Id, created.Id, CancellationToken.None);
        // Approved movements execute at (or after) their effective date; future-dated approvals stay "approved".
        Assert.Contains(approved!.Status, new[] { "approved", "executed" });
    }

    [Fact]
    public async Task EmergencyContact_CrudWorks()
    {
        var (service, ctx, worker) = BuildWithWorker();
        var created = await service.AddEmergencyContactAsync(worker.Id,
            new EmergencyContactRequest("spouse", "Jane Doe", "0971234567", IsPrimary: true), CancellationToken.None);
        Assert.Equal("spouse", created.Relationship);

        var updated = await service.UpdateEmergencyContactAsync(worker.Id, created.Id,
            new EmergencyContactRequest("spouse", "Jane Doe-Smith"), CancellationToken.None);
        Assert.Equal("Jane Doe-Smith", updated.FullName);

        await service.DeleteEmergencyContactAsync(worker.Id, created.Id, CancellationToken.None);
        var count = await ctx.EmergencyContacts.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task BankDetail_CrudWorks()
    {
        var (service, ctx, worker) = BuildWithWorker();
        var created = await service.AddBankDetailAsync(worker.Id,
            new BankDetailRequest("Stanbic", "010", "0001234567890", "Lifecycle Worker"),
            CancellationToken.None);
        Assert.Equal("Stanbic", created.BankName);

        await service.UpdateBankDetailAsync(worker.Id, created.Id,
            new BankDetailRequest("Zanaco", "020", "0009876543210", "Lifecycle Worker"), CancellationToken.None);
        var row = await ctx.WorkerBankDetails.FirstAsync(b => b.Id == created.Id);
        Assert.Equal("Zanaco", row.BankName);

        await service.DeleteBankDetailAsync(worker.Id, created.Id, CancellationToken.None);
        Assert.Equal(0, await ctx.WorkerBankDetails.CountAsync());
    }

    [Fact]
    public async Task BankDetail_AcceptsMobileMoneyAndCashPaymentMethods()
    {
        var (service, ctx, worker) = BuildWithWorker();

        var mobile = await service.AddBankDetailAsync(worker.Id,
            new BankDetailRequest("", "", "", "Lifecycle Worker", "mobile-money", "+260977000001", true),
            CancellationToken.None);

        Assert.Equal("mobile-money", mobile.PaymentMethod);
        Assert.Equal("+260977000001", mobile.MobileMoneyNumber);
        Assert.Equal("Mobile money", mobile.BankName);
        Assert.Equal("+260977000001", mobile.AccountNumber);

        var cash = await service.UpdateBankDetailAsync(worker.Id, mobile.Id,
            new BankDetailRequest("", "", "", "Lifecycle Worker", "cash", null, true),
            CancellationToken.None);

        Assert.Equal("cash", cash.PaymentMethod);
        Assert.Equal("Cash", cash.BankName);
        Assert.Equal("N/A", cash.AccountNumber);
        Assert.Single(await ctx.WorkerBankDetails.ToListAsync());
    }

    [Fact]
    public async Task Offboard_EndsCurrentAssignmentAndTerminatesWorker()
    {
        var (service, ctx, worker) = BuildWithWorker();
        // Give the worker a current assignment so offboarding has something to end.
        var entity = ctx.LegalEntities.First();
        var unit = ctx.OrgUnits.First();
        var location = ctx.WorkLocations.First();
        await service.CreateAssignmentAsync(worker.Id, new AssignmentCreateRequest(
            WorkerId: worker.Id, LegalEntityId: entity.Id, OrgUnitId: unit.Id, LocationId: location.Id,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)).ToString("yyyy-MM-dd"),
            ContractType: "permanent"), CancellationToken.None);

        // Without bank details the offboarding report flags a clearance item.
        var result = await service.OffboardAsync(worker.Id, CancellationToken.None);
        Assert.False(result.Cleared);
        Assert.Contains("bank_detail", result.OpenItems);
        var closed = await ctx.Workers.FirstAsync(w => w.Id == worker.Id);
        Assert.Equal("terminated", closed.Status);
        // The worker's current assignment was ended as part of offboarding.
        // ListAllAssignmentsAsync queries the store directly, bypassing per-worker tracking quirks.
        var repoCheck = new WorkerRepository(ctx);
        var all = await repoCheck.ListAllAssignmentsAsync(CancellationToken.None);
        Assert.Contains(all, a => a.WorkerId == worker.Id && a.Status == "ended");
    }

    [Fact]
    public async Task Onboarding_ShowsProgress()
    {
        var (service, ctx, worker) = BuildWithWorker();
        var plan = await service.GetOnboardingAsync(worker.Id, CancellationToken.None);
        Assert.Equal(worker.Id, plan.WorkerId);
        Assert.True(plan.TasksTotal > 0);
    }
}
