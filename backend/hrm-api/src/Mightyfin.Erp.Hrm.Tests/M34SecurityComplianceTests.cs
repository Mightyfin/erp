using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class M34SecurityComplianceTests
{
    [Fact]
    public async Task TenantBoundary_FiltersReads_OverridesCreates_AndRejectsCrossTenantWrites()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(connection).Options;
        await using var tenantA = new HrmDbContext(options, new FixedTenantAccessor("tenant-a"));
        await tenantA.Database.EnsureCreatedAsync();
        var worker = new Worker
        {
            TenantId = "untrusted-client-tenant", EmployeeNo = "A-001", FirstName = "Alice",
            LastName = "Tenant A", WorkerType = "employee", Status = "active",
        };
        tenantA.Workers.Add(worker);
        await tenantA.SaveChangesAsync();
        Assert.Equal("tenant-a", worker.TenantId);

        await using var tenantB = new HrmDbContext(options, new FixedTenantAccessor("tenant-b"));
        Assert.Empty(await tenantB.Workers.ToListAsync());
        worker.FirstName = "Cross tenant edit";
        tenantB.Attach(worker).State = EntityState.Modified;
        var error = await Assert.ThrowsAsync<DomainException>(() => tenantB.SaveChangesAsync());
        Assert.Equal("cross-tenant-write", error.Code);
    }

    [Fact]
    public async Task Audit_IsAppendOnly_CapturesCreatesAndUpdates_AndRedactsSensitiveFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var accessor = new FixedTenantAccessor("audit-tenant");
        var http = new HttpContextAccessor { HttpContext = Context("audit-admin", "hr_admin") };
        var options = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(connection)
            .AddInterceptors(new AuditInterceptor(http, accessor)).Options;
        await using var db = new HrmDbContext(options, accessor);
        await db.Database.EnsureCreatedAsync();
        var worker = new Worker
        {
            EmployeeNo = "SEC-001", FirstName = "Secure", LastName = "Worker", WorkerType = "employee",
            Status = "active", Nrc = "123456/78/9", Tpin = "1002003000",
        };
        db.Workers.Add(worker);
        await db.SaveChangesAsync();
        worker.Nrc = "999999/99/9";
        await db.SaveChangesAsync();

        var audit = await db.AuditEntries.Where(x => x.EntityId == worker.Id.ToString()).OrderBy(x => x.Id).ToListAsync();
        Assert.Contains(audit, x => x.Action == "create");
        Assert.Contains(audit, x => x.Action == "update");
        Assert.All(audit, x =>
        {
            Assert.Equal("audit-admin", x.ActorSubjectId);
            Assert.DoesNotContain("123456/78/9", x.BeforeJson ?? "");
            Assert.DoesNotContain("999999/99/9", x.AfterJson ?? "");
        });
        Assert.Contains("[REDACTED]", audit.Last().AfterJson);

        audit[0].Action = "tampered";
        var error = await Assert.ThrowsAsync<DomainException>(() => db.SaveChangesAsync());
        Assert.Equal("audit-immutable", error.Code);
    }

    [Fact]
    public async Task ComplianceConsole_TracksEvidenceLegalHoldsAndRoleMatrix()
    {
        await using var db = TestDbContextFactory.Create("m34-console");
        var service = new SecurityComplianceService(db, new PermissiveAuthz(), new FixedTenantAccessor("m34-console"));
        var evidence = await service.RecordEvidenceAsync(new ComplianceEvidenceRequest("backup-restore", "passed",
            "restore-rehearsal-2026-08-16", "Restored into an isolated database and reconciled counts.",
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMonths(3)), "security-admin", default);
        var hold = await service.PlaceLegalHoldAsync(new LegalHoldRequest("CASE-2026-001", "relations-case:001",
            "Active employment investigation"), "security-admin", default);
        var dashboard = await service.GetDashboardAsync(null, null, default);

        Assert.Equal("passed", evidence.Status);
        Assert.Equal(1, dashboard.ActiveLegalHolds);
        Assert.Contains(dashboard.Controls, x => x.Key == "backup-restore" && x.Status == "passed");
        Assert.Contains(dashboard.RoleMatrix, x => x.Capability == "security-admin" && x.Roles.SequenceEqual(["hr_admin"]));
        Assert.All(dashboard.RetentionRules, x => Assert.True(x.LegalHoldOverrides));

        var released = await service.ReleaseLegalHoldAsync(hold.Id,
            new LegalHoldReleaseRequest("Investigation and appeal windows closed."), "security-admin-2", default);
        Assert.Equal("released", released.Status);
    }

    [Fact]
    public async Task ManagerDirectory_MasksIdentifiersAndDoesNotReturnBankOrIdentityLinks()
    {
        await using var db = TestDbContextFactory.Create("m34-masking");
        var worker = new Worker
        {
            EmployeeNo = "MASK-001", FirstName = "Masked", LastName = "Worker", WorkerType = "employee",
            Status = "active", Nrc = "123456/78/9", Tpin = "1002003000", SubjectId = "keycloak-subject",
        };
        worker.BankDetails.Add(new WorkerBankDetail { BankName = "Bank", BranchCode = "001", AccountNumber = "123456789", AccountName = "Masked Worker", IsPrimary = true });
        db.Workers.Add(worker);
        await db.SaveChangesAsync();
        var service = new WorkerServiceImpl(new WorkerRepository(db), new ManagerAuthz(), new UlidIdProvider());
        var result = await service.GetByIdAsync(worker.Id, default);

        Assert.NotNull(result);
        Assert.StartsWith("••••", result!.Nrc);
        Assert.EndsWith("000", result.Tpin);
        Assert.Null(result.SubjectId);
        Assert.Null(result.DateOfBirth);
        Assert.Null(result.BankDetails);
    }

    [Fact]
    public void ProductAdmission_DoesNotTreatExternalTenantOwnerAsHrmStaff()
    {
        Claim[] claims = [new("sub", "shared-id"), new("realm_access.roles", "tenant_owner")];
        Assert.False(HrmStaffAccess.IsStaff(claims));
    }

    private static DefaultHttpContext Context(string subject, params string[] roles)
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "m34-test-request";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", subject), ..roles.Select(x => new Claim("realm_access.roles", x))], "test"));
        return context;
    }

    private sealed class ManagerAuthz : IAuthzService
    {
        public string CurrentSubjectId => "manager-subject";
        public void RequireAnyRole(params string[] roles)
        {
            if (!roles.Contains("manager")) throw new DomainException("forbidden", "Manager access denied.");
        }
        public bool IsRole(params string[] roles) => roles.Contains("manager");
        public bool CanAccessSensitive(string category) => false;
    }
}
