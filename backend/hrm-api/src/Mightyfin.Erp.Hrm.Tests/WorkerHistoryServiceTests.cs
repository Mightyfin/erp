using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M33: education, external and internal work history child records.</summary>
public class WorkerHistoryServiceTests
{
    private const string Tenant = "t-m33-history";

    /// <summary>Strict double that mirrors production AuthzServiceImpl (RolePrincipals
    /// in Program.cs) — RequireAnyRole throws "forbidden" when no matching role.</summary>
    private sealed class StrictAuthz(string[] roles) : IAuthzService
    {
        public string CurrentSubjectId => "test-subject";
        public string[] Roles { get; } = roles;
        public void RequireAnyRole(params string[] roles)
        {
            if (!Roles.Any(r => roles.Contains(r)))
                throw new DomainException("forbidden", $"Requires one of roles: {string.Join(", ", roles)}");
        }
        public bool IsRole(params string[] roles) => Roles.Any(r => roles.Contains(r));
        public bool CanAccessSensitive(string category) => Roles.Contains("hr_admin") || Roles.Contains("payroll");
    }

    private static (WorkerLifecycleServiceImpl service, HrmDbContext ctx) Build(params string[] roles)
    {
        var ctx = TestDbContextFactory.Create(Tenant);
        var authz = roles.Length == 1 && roles[0] == "hr_ops" || roles.Contains("hr_admin") || roles.Contains("hr_ops")
            ? (IAuthzService)new PermissiveAuthz { Roles = roles } : new StrictAuthz(roles);
        var service = new WorkerLifecycleServiceImpl(new WorkerRepository(ctx), authz);
        return (service, ctx);
    }


    private static async Task<Worker> SeedWorkerAsync(HrmDbContext ctx, string suffix = "")
    {
        var worker = new Worker
        {
            EmployeeNo = "HIST-" + (string.IsNullOrEmpty(suffix) ? Guid.NewGuid().ToString("N")[..8] : suffix),
            FirstName = "History", LastName = "Test",
            Email = "history." + (string.IsNullOrEmpty(suffix) ? Guid.NewGuid().ToString("N")[..8] : suffix) + "@example.com",
            Phone = "0977000001",
            WorkerType = "employee", Status = "active",
        };
        ctx.Workers.Add(worker);
        await ctx.SaveChangesAsync();
        return worker;
    }

    // ---------- Education ----------

    [Fact]
    public async Task AddEducation_RequiredFields_Validates()
    {
        var (service, ctx) = Build("hr_admin");
        var worker = await SeedWorkerAsync(ctx);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.AddEducationAsync(worker.Id, new EducationRequest("", ""), CancellationToken.None));
    }

    [Fact]
    public async Task AddEducation_ReturnsRecord_AndListIncludesIt()
    {
        var (service, ctx) = Build("hr_ops");
        var worker = await SeedWorkerAsync(ctx);

        var created = await service.AddEducationAsync(worker.Id,
            new EducationRequest("UNZA", "Bachelor of Science", "Computer Science", "Second Class", 2014, 2018),
            CancellationToken.None);
        Assert.Equal("UNZA", created.Institution);
        Assert.Equal(2018, created.EndYear);

        var list = await service.ListEducationAsync(worker.Id, CancellationToken.None);
        Assert.Single(list);
        Assert.Equal("UNZA", list[0].Institution);
    }

    [Fact]
    public async Task UpdateEducation_CanOnlyOwnRecords()
    {
        var (service, ctx) = Build("hr_admin");
        var worker = await SeedWorkerAsync(ctx);
        var otherWorker = await SeedWorkerAsync(ctx);

        var record = await service.AddEducationAsync(worker.Id,
            new EducationRequest("CBU", "Diploma", "Accounting", null, 2012, 2014),
            CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateEducationAsync(otherWorker.Id, record.Id,
                new EducationRequest("CBU", "Diploma in Accounting"), CancellationToken.None));
    }

    [Fact]
    public async Task AddEducation_InvalidRange_Rejected()
    {
        var (service, ctx) = Build("hr_admin");
        var worker = await SeedWorkerAsync(ctx);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.AddEducationAsync(worker.Id,
                new EducationRequest("UNZA", "BSc", null, null, 2020, 2015),
                CancellationToken.None));
    }

    [Fact]
    public async Task DeleteEducation_OwnershipChecked()
    {
        var (service, ctx) = Build("hr_admin");
        var worker = await SeedWorkerAsync(ctx);
        var otherWorker = await SeedWorkerAsync(ctx);

        var record = await service.AddEducationAsync(worker.Id,
            new EducationRequest("UNZA", "BSc", null, null, 2014, 2018), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.DeleteEducationAsync(otherWorker.Id, record.Id, CancellationToken.None));

        await service.DeleteEducationAsync(worker.Id, record.Id, CancellationToken.None);
        Assert.Empty(await service.ListEducationAsync(worker.Id, CancellationToken.None));
    }

    // ---------- External work history ----------

    [Fact]
    public async Task AddExternalWorkHistory_NormalizesDates()
    {
        var (service, ctx) = Build("hr_ops");
        var worker = await SeedWorkerAsync(ctx);

        var created = await service.AddExternalWorkHistoryAsync(worker.Id,
            new ExternalWorkHistoryRequest("Bank Zed", "Teller", "2021-03-01", "2023-12-31", "Branch operations"),
            CancellationToken.None);
        Assert.Equal("2021-03-01", created.StartDate);

        var yearOnly = await service.AddExternalWorkHistoryAsync(worker.Id,
            new ExternalWorkHistoryRequest("Retail Co", "Cashier", "2019", "2021"),
            CancellationToken.None);
        Assert.Equal("2019", yearOnly.StartDate);
    }

    [Fact]
    public async Task AddExternalWorkHistory_ReverseRange_Rejected()
    {
        var (service, ctx) = Build("hr_admin");
        var worker = await SeedWorkerAsync(ctx);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.AddExternalWorkHistoryAsync(worker.Id,
                new ExternalWorkHistoryRequest("Bank Zed", "Teller", "2023-01-01", "2021-01-01"),
                CancellationToken.None));
    }

    // ---------- Internal work history ----------

    [Fact]
    public async Task AddInternalWorkHistory_StoresOrgMove()
    {
        var (service, ctx) = Build("hr_admin");
        var worker = await SeedWorkerAsync(ctx);

        var created = await service.AddInternalWorkHistoryAsync(worker.Id,
            new InternalWorkHistoryRequest("Finance", "Accounts Officer", "G4", "2020-01-01", "2024-06-30", "Promoted to HR"),
            CancellationToken.None);
        Assert.Equal("Finance", created.OrgUnitName);

        var list = await service.ListInternalWorkHistoryAsync(worker.Id, CancellationToken.None);
        Assert.Single(list);
        Assert.Equal("Accounts Officer", list[0].Role);
    }

    // ---------- Authorization ----------

    [Fact]
    public async Task ListEducation_RequiresAuthorizedRole()
    {
        var (service, ctx) = Build("unrelated_role");
        var worker = await SeedWorkerAsync(ctx);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.ListEducationAsync(worker.Id, CancellationToken.None));

        // Same context, different role impersonation — the worker already exists here.
        var payroll = new WorkerLifecycleServiceImpl(new WorkerRepository(ctx), new StrictAuthz(new[] { "payroll" }));
        await payroll.ListEducationAsync(worker.Id, CancellationToken.None);
    }

    [Fact]
    public async Task AddEducation_RequiresWriterRole()
    {
        var (service, ctx) = Build("payroll");
        var worker = await SeedWorkerAsync(ctx);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.AddEducationAsync(worker.Id,
                new EducationRequest("UNZA", "BSc"), CancellationToken.None));

        var employee = new WorkerLifecycleServiceImpl(new WorkerRepository(ctx), new StrictAuthz(new[] { "employee" }));
        await employee.AddEducationAsync(worker.Id,
            new EducationRequest("UNZA", "BSc"), CancellationToken.None);
    }

    // ---------- WorkerDto projection ----------

    [Fact]
    public async Task WorkerDto_IncludesHistoryLists()
    {
        var (workerService, ctx) = BuildWorkerService();
        var worker = await SeedWorkerAsync(ctx);
        var edu = new WorkerEducation { WorkerId = worker.Id, Institution = "UNZA", Qualification = "BSc" };
        var ext = new ExternalWorkHistory { WorkerId = worker.Id, Company = "Bank Zed" };
        var intr = new InternalWorkHistory { WorkerId = worker.Id, OrgUnitName = "Finance" };
        // EF Core 10 SQLite Guid-V7 bug: AddRange children explicitly before SaveChanges.
        ctx.WorkerEducations.Add(edu);
        ctx.ExternalWorkHistory.Add(ext);
        ctx.InternalWorkHistory.Add(intr);
        await ctx.SaveChangesAsync();

        var dto = await workerService.GetByIdAsync(worker.Id, CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Single(dto.Education);
        Assert.Single(dto.ExternalWorkHistory);
        Assert.Single(dto.InternalWorkHistory);
    }

    private static (WorkerServiceImpl service, HrmDbContext ctx) BuildWorkerService()
    {
        var ctx = TestDbContextFactory.Create(Tenant);
        var service = new WorkerServiceImpl(
            new WorkerRepository(ctx), new PermissiveAuthz(), new UlidIdProvider());
        return (service, ctx);
    }
}
