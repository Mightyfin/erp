using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>M44: Reads X-Shell-Location / X-Shell-Entity headers and stashes the
/// resulting work scope on the per-request ShellContext. The frontend sends
/// these headers on operational requests from the branch-selector state.
/// Headers are validated against tenant data so a client cannot fabricate or
/// widen a scope; non-HRM paths (health checks, OpenAPI, auth) pass through
/// untouched and leave the scope empty (org-wide by default).</summary>
public sealed class ShellContextMiddleware(RequestDelegate next, ILogger<ShellContextMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext http, ShellContext scope, ITenantAccessor tenant, HrmDbContext db)
    {
        if (!http.Request.Path.StartsWithSegments("/api/hrm"))
        {
            await next(http);
            return;
        }

        var tenantId = tenant.GetTenantId();

        // M45: load the operator's branch confinement (empty = org-wide top HR).
        // JwtSecurityTokenHandler maps "sub" to NameIdentifier, so check both.
        var rawSubject = http.User.FindFirst("sub")?.Value
            ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid operatorId = Guid.Empty;
        if (!string.IsNullOrEmpty(rawSubject) && Guid.TryParse(rawSubject, out operatorId))
        {
            var allowed = await db.UserBranchAssignments
                .Where(x => x.UserId == operatorId)
                .Select(x => x.LocationId)
                .ToListAsync(http.RequestAborted);
            scope.AllowedLocationIds.AddRange(allowed);
        }

        var rawLocation = http.Request.Headers["X-Shell-Location"].FirstOrDefault()?.Trim();
        var rawEntity = http.Request.Headers["X-Shell-Entity"].FirstOrDefault()?.Trim();

        if (Guid.TryParse(rawLocation, out var locationId) && locationId != Guid.Empty)
        {
            if (scope.IsConfined)
            {
                // M45: confined operators may NEVER widen their scope. A header
                // outside their assignment is an access violation (403), not a
                // suggestion the server may silently downgrade.
                if (!scope.AllowedLocationIds.Contains(locationId))
                {
                    logger.LogWarning("Operator {Op} confined to {Allowed} tried header {Loc}",
                        operatorId, string.Join(',', scope.AllowedLocationIds), locationId);
                    http.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await http.Response.WriteAsJsonAsync(new { error = "forbidden", message = "You are confined to your assigned branches." }, http.RequestAborted);
                    return;
                }
                scope.LocationId = locationId;
            }
            else if (await db.WorkLocations.AnyAsync(x => x.Id == locationId && x.TenantId == tenantId, http.RequestAborted))
            {
                scope.LocationId = locationId;
            }
            else
            {
                logger.LogWarning("X-Shell-Location {Loc} not found under tenant {Tenant}; ignoring scope header", locationId, tenantId);
            }
        }
        else if (scope.IsConfined)
        {
            // Confined operator supplied no scope header: default to their
            // first assigned branch so their work is never accidentally org-wide.
            scope.LocationId = scope.AllowedLocationIds.FirstOrDefault();
        }

        if (Guid.TryParse(rawEntity, out var entityId) && entityId != Guid.Empty)
        {
            if (await db.LegalEntities.AnyAsync(x => x.Id == entityId && x.TenantId == tenantId, http.RequestAborted))
                scope.EntityId = entityId;
            else
                logger.LogWarning("X-Shell-Entity {Ent} not found under tenant {Tenant}; ignoring scope header", entityId, tenantId);
        }
        else if (!scope.LocationId.HasValue)
        {
            // No scope header: default to the tenant's primary legal entity.
            scope.EntityId = await db.LegalEntities
                .Where(x => x.TenantId == tenantId)
                .OrderBy(x => x.IsDefault ? 0 : 1)
                .ThenBy(x => x.RegisteredName)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(http.RequestAborted);
        }

        await next(http);
    }
}
