using System.Security.Claims;

namespace Mightyfin.Erp.Hrm.Application;

/// <summary>Typed principal resolved from the OIDC token claims (mirrors the Go
/// skeleton's auth.Principal: subject, tenant, environment, scopes, roles). When
/// ERP_AUTH_MODE=disabled, the developer middleware synthesizes a local-developer
/// principal so local testing never needs Keycloak running.</summary>
public sealed class WorkerPrincipal
{
    public string SubjectId { get; init; } = "";
    public string TenantId { get; init; } = "";
    public string Environment { get; init; } = "local";
    public string[] Roles { get; init; } = [];
    public string[] Scopes { get; init; } = [];
    public bool IsDeveloperFallback { get; init; }

    public bool IsRole(params string[] roles) => roles.Any(r => Roles.Contains(r, StringComparer.OrdinalIgnoreCase));
    public bool CanPayroll => IsRole("payroll", "hr_admin");
    public bool CanHr => IsRole("hr_ops", "hr_admin", "payroll");
    public bool CanInvestigate => IsRole("investigator", "hr_admin");

    public static WorkerPrincipal FromClaims(IEnumerable<Claim> claims)
    {
        static string First(IEnumerable<Claim> c, string type) =>
            c.FirstOrDefault(x => x.Type == type)?.Value ?? "";
        var roles = claims.Where(c => c.Type is "realm_access.roles" or ClaimTypes.Role or "roles")
                          .Select(c => c.Value).ToArray();
        return new WorkerPrincipal
        {
            SubjectId = First(claims, "sub"),
            TenantId = First(claims, "tenant"),
            Environment = First(claims, "environment") switch { "" => "local", var v => v },
            Roles = roles,
            Scopes = claims.Where(c => c.Type == "scope").Select(c => c.Value).ToArray(),
        };
    }
}

/// <summary>Standard error payload shape matching the ERP API conventions
/// ({code, message, details[]}).</summary>
public sealed record ApiError(string Code, string Message, string[] Details);

/// <summary>Typed pagination result used across list endpoints.</summary>
public sealed record Paged<T>(List<T> Items, int TotalCount, int Page, int PageSize);
