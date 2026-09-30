using Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Explicit API allowlist for accounts assigned only timesheet operations.
/// Additional workforce permissions remain additive when deliberately assigned by an admin.</summary>
public sealed class TimesheetAccess { }

public sealed class TimesheetAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var principal = WorkerPrincipal.FromClaims(context.User.Claims);
        var restricted = principal.IsRole("timesheet_operator")
            && !principal.IsRole(HrmStaffAccess.Roles.Where(r => r != "timesheet_operator").ToArray());
        var isHrm = context.Request.Path.StartsWithSegments("/api/hrm")
            || context.Request.Path.StartsWithSegments("/api/v1/hrm");
        var allowed = context.GetEndpoint()?.Metadata.GetMetadata<TimesheetAccess>() is not null;
        if (context.Request.RouteValues.TryGetValue("typeKey", out var typeKey))
            allowed &= string.Equals(typeKey?.ToString(), "attendance", StringComparison.OrdinalIgnoreCase);
        if (context.User.Identity?.IsAuthenticated == true && restricted && isHrm && !allowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ApiError("forbidden", "Your account has timesheet access only.", []));
            return;
        }
        await next(context);
    }
}
