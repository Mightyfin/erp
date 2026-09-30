using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Shared;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Tests;

public class TimesheetAccessTests
{
    [Theory]
    [InlineData("/api/hrm", "GET", "/workers")]
    [InlineData("/api/v1/hrm", "GET", "/workers")]
    [InlineData("/api/hrm", "GET", "/payroll/runs")]
    [InlineData("/api/v1/hrm", "POST", "/time/overtime/import")]
    [InlineData("/api/hrm", "POST", "/time/overtime/123/decide")]
    [InlineData("/api/hrm", "POST", "/auth/users")]
    [InlineData("/api/v1/hrm", "GET", "/admin/roles")]
    public async Task TimesheetOnlyAccountCannotReachOtherEndpoints(string prefix, string method, string path)
    {
        var context = Context(prefix + path, method);
        var reached = false;
        await new TimesheetAccessMiddleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.False(reached);
    }

    [Theory]
    [InlineData("/api/hrm", "attendance", true)]
    [InlineData("/api/v1/hrm", "attendance", true)]
    [InlineData("/api/hrm", "workers", false)]
    [InlineData("/api/v1/hrm", "payroll-profiles", false)]
    public async Task ImportAllowlistChecksSchema(string prefix, string type, bool expected)
    {
        var context = Context(prefix + "/import/" + type + "/preview", "POST");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new TimesheetAccess()), "import"));
        context.Request.RouteValues["typeKey"] = type;
        var reached = false;
        await new TimesheetAccessMiddleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.Equal(expected, reached);
        Assert.Equal(expected ? 200 : 403, context.Response.StatusCode);
    }

    [Fact]
    public async Task AdminRetainsAccessWhenAlsoAssignedTimesheets()
    {
        var context = Context("/api/hrm/auth/users", "GET", "timesheet_operator", "hr_admin");
        var reached = false;
        await new TimesheetAccessMiddleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.True(reached);
    }

    [Fact]
    public async Task CachedNonAttendancePreviewCannotBeAppliedByTimesheetAccount()
    {
        var schemas = new IImportSchema[] { new FakeSchema("workers"), new FakeSchema("attendance") };
        var admin = new ImportExportServiceImpl(schemas, new RoleAuthz("clerk", "hr_admin"));
        var preview = await admin.PreviewAsync("workers", "workers.csv", "insert", [new()], default);
        var clerk = new ImportExportServiceImpl(schemas, new RoleAuthz("clerk", "timesheet_operator"));
        Assert.Equal("attendance", Assert.Single(clerk.ListSchemas()).TypeKey);
        var error = await Assert.ThrowsAsync<DomainException>(() => clerk.ApplyAsync(preview.Id, [0], default));
        Assert.Equal("forbidden", error.Code);
        await Assert.ThrowsAsync<DomainException>(() => clerk.ExportAsync("workers", null, default));
    }

    [Fact]
    public async Task PreviewBelongsToItsCreator()
    {
        var schemas = new IImportSchema[] { new FakeSchema("attendance") };
        var first = new ImportExportServiceImpl(schemas, new RoleAuthz("first", "timesheet_operator"));
        var second = new ImportExportServiceImpl(schemas, new RoleAuthz("second", "timesheet_operator"));
        var preview = await first.PreviewAsync("attendance", "hours.csv", "insert", [new()], default);
        await Assert.ThrowsAsync<DomainException>(() => second.ApplyAsync(preview.Id, [0], default));
        Assert.Equal(1, (await first.ApplyAsync(preview.Id, [0], default)).Created);
    }

    [Fact]
    public void AttendanceScopeCannotWidenConfinementWithOrgUnit()
    {
        var scope = new ShellContext { OrgUnitId = Guid.NewGuid() };
        var allowedLocation = Guid.NewGuid();
        scope.AllowedLocationIds.Add(allowedLocation);
        var worker = new Worker { OrgUnitId = scope.OrgUnitId, LocationId = Guid.NewGuid() };
        Assert.False(AttendanceScope.Allows(worker, scope));
        worker.LocationId = allowedLocation;
        Assert.True(AttendanceScope.Allows(worker, scope));
        worker.OrgUnitId = Guid.NewGuid();
        Assert.False(AttendanceScope.Allows(worker, scope));
    }

    private static DefaultHttpContext Context(string path, string method, params string[] roles)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            (roles.Length == 0 ? ["timesheet_operator"] : roles).Select(r => new Claim(ClaimTypes.Role, r)), "test"));
        return context;
    }

    private sealed class RoleAuthz(string subject, params string[] assigned) : IAuthzService
    {
        public string CurrentSubjectId => subject;
        public bool IsRole(params string[] roles) => roles.Any(assigned.Contains);
        public void RequireAnyRole(params string[] roles)
        {
            if (!IsRole(roles)) throw new DomainException("forbidden", "Access denied.");
        }
        public bool CanAccessSensitive(string category) => false;
    }

    private sealed class FakeSchema(string typeKey) : IImportSchema
    {
        public string TypeKey => typeKey;
        public string DisplayName => typeKey;
        public List<ImportFieldDef> Fields => [];
        public Task<ImportRowOutcome> PreviewRowAsync(IDictionary<string, string> row, string mode, CancellationToken ct)
            => Task.FromResult(new ImportRowOutcome("create", null));
        public Task ApplyRowAsync(IDictionary<string, string> row, string mode, CancellationToken ct) => Task.CompletedTask;
    }
}
