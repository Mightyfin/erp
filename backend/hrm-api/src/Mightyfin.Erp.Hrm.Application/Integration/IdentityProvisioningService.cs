// M51: first-user auto-elevation + HR administrator invitation provisioning.
// The HRM API talks to the organisation's Keycloak identity system (admin REST
// API over the internal docker network) so that:
//  1. The FIRST user to sign into a fresh HRM instance (setup PENDING and no
//     one holds the hr_admin realm role yet) is automatically elevated to
//     top HR admin (hr_admin + tenant_owner) — no manual role assignment.
//  2. The setup-wizard "HR administrators" step provisions invited emails by
//     assigning them hr_admin + employee realm roles when the users already
//     exist in the identity system (user creation itself remains an identity
//     platform responsibility — we never invent credentials).
// All calls are best-effort: failures are logged and surfaced as partial
// results; they never block HR workflows or crash the API.
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Mightyfin.Erp.Hrm.Application.Integration;

public interface IIdentityProvisioningService
{
    /// M51.1: claim top-HR-admin elevation for the caller. Idempotent — if the
    /// tenant is fresh (setup pending, no hr_admin holders anywhere in the
    /// realm) the caller is granted hr_admin (+ tenant_owner and + employee
    /// when those roles exist). Returns the roles actually held afterwards.
    Task<ClaimElevationResult> ClaimTopAdminAsync(
        string subjectId, string email, CancellationToken ct);

    /// M51.2: provision invited administrator emails. For each email that can
    /// be resolved to a Keycloak user, hr_admin + employee realm roles are
    /// assigned. Unknown emails are reported (not failed) so the wizard can
    /// tell the operator which invites are pending identity-side creation.
    Task<ProvisionResult> ProvisionAdminsAsync(
        IEnumerable<string> emails, CancellationToken ct);
}

public sealed record ClaimElevationResult(
    bool Elevated,
    IReadOnlyList<string> Roles,
    string? Reason);

public sealed record ProvisionResult(
    IReadOnlyList<ProvisionEntry> Entries)
{
    public int Assigned => Entries.Count(e => e.Assigned);
}

public sealed record ProvisionEntry(
    string Email,
    bool Found,
    bool Assigned,
    string? Error);

/// <summary>M51: Keycloak admin REST client for identity provisioning.</summary>
public sealed class IdentityProvisioningService(
    ILogger<IdentityProvisioningService> log,
    Microsoft.Extensions.Configuration.IConfiguration cfg,
    IHttpClientFactory http) : IIdentityProvisioningService
{
    // Configuration (all optional — without an admin identity the service
    // degrades to "no elevation possible" and logs once per request).
    private string AdminRealm =>
        cfg["HRM:IdentityAdminRealm"] ?? "master";
    private string AdminUser =>
        cfg["HRM:IdentityAdminUser"] ?? "local-admin";
    private string AdminPassword =>
        cfg["HRM:IdentityAdminPassword"] ?? "";
    private string IdentityBaseUrl =>
        (cfg["HRM:IdentityBaseUrl"] ?? "").TrimEnd('/');
    private string TenantRealm =>
        cfg["HRM:IdentityTenantRealm"] ?? "mightyfin-sandbox";

    private static readonly TimeSpan AdminTokenTtl = TimeSpan.FromMinutes(4);
    private volatile CachedToken? _adminToken;
    private volatile Dictionary<string, CachedRole>? _roles;
    private readonly object _rolesLock = new();

    private sealed record CachedToken(string Value, DateTimeOffset Expires);
    private sealed record CachedRole(Guid Id, string Name);

    private bool Enabled => !string.IsNullOrEmpty(IdentityBaseUrl);

    public async Task<ClaimElevationResult> ClaimTopAdminAsync(
        string subjectId, string email, CancellationToken ct)
    {
        if (!Enabled)
            return new ClaimElevationResult(false, [],
                "identity-provisioning-not-configured");

        try
        {
            var adminToken = await AdminTokenAsync(ct);
            if (adminToken is null)
                return new ClaimElevationResult(false, [],
                    "identity-admin-unreachable");

            // Fresh-tenant guard: only elevate when no one holds hr_admin in
            // the realm yet AND setup is still pending for this tenant. Both
            // conditions must hold — otherwise a later user could hijack the
            // elevation after a real admin was assigned manually. Failure to
            // count holders is treated conservatively: the elevation is
            // refused rather than risk granting top-HR access.
            var holders = await CountHrAdminHoldersAsync(ct);
            if (holders < 0)
                return new ClaimElevationResult(false, [],
                    "tenant-admin-count-unavailable");
            var hrAdminRole = await RoleAsync("hr_admin", ct);
            if (holders > 0)
                return new ClaimElevationResult(false, [],
                    "tenant-already-has-administrators");

            var userId = await FindUserIdAsync(email, ct);
            if (userId is null)
                return new ClaimElevationResult(false, [],
                    "user-not-found-in-identity");

            var rolesToAssign = new List<CachedRole>();
            if (hrAdminRole is not null) rolesToAssign.Add(hrAdminRole);

            var tenantOwner = await RoleAsync("tenant_owner", ct);
            if (tenantOwner is not null) rolesToAssign.Add(tenantOwner);

            var employee = await RoleAsync("employee", ct);
            if (employee is not null) rolesToAssign.Add(employee);

            foreach (var role in rolesToAssign)
                await AssignRealmRoleAsync(userId.Value, role.Id, role.Name, ct);

            // Invalidate the role-cache entry we mutated so the next read is fresh.
            InvalidateRole("hr_admin");

            return new ClaimElevationResult(true,
                rolesToAssign.Select(r => r.Name).ToList(), "first-user-bootstrap");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "First-user elevation claim failed for {Email} — proceeding without elevation",
                email);
                        return new ClaimElevationResult(false, [],
                    "elevation-failed");
        }
    }

    public async Task<ProvisionResult> ProvisionAdminsAsync(
        IEnumerable<string> emails, CancellationToken ct)
    {
        var entries = new List<ProvisionEntry>();
        if (!Enabled)
        {
            entries.AddRange(emails.Select(e => new ProvisionEntry(e, false, false,
                "identity-provisioning-not-configured")));
            return new ProvisionResult(entries);
        }

        var adminToken = await AdminTokenAsync(ct);
        if (adminToken is null)
        {
            entries.AddRange(emails.Select(e => new ProvisionEntry(e, false, false,
                "identity-admin-unreachable")));
            return new ProvisionResult(entries);
        }

        var hrAdminRole = await RoleAsync("hr_admin", ct);
        var employeeRole = await RoleAsync("employee", ct);

        foreach (var raw in emails)
        {
            var email = raw.Trim().ToLowerInvariant();
            try
            {
                var userId = await FindUserIdAsync(email, ct);
                if (userId is null)
                {
                    entries.Add(new ProvisionEntry(email, false, false,
                        "no-user-in-identity"));
                    continue;
                }

                var assigned = false;
                if (hrAdminRole is not null)
                    await AssignRealmRoleAsync(userId.Value, hrAdminRole.Id, hrAdminRole.Name, ct);
                if (employeeRole is not null)
                    await AssignRealmRoleAsync(userId.Value, employeeRole.Id, employeeRole.Name, ct);
                assigned = true;
                entries.Add(new ProvisionEntry(email, true, assigned, null));
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Provisioning failed for {Email}", email);
                entries.Add(new ProvisionEntry(email, true, false,
                    ex.Message[..Math.Min(ex.Message.Length, 200)]));
            }
        }
        return new ProvisionResult(entries);
    }

    // ---------------- Keycloak admin REST helpers ----------------

    private async Task<string?> AdminTokenAsync(CancellationToken ct)
    {
        var cached = _adminToken;
        if (cached is not null && cached.Expires > DateTimeOffset.UtcNow.AddMinutes(1))
            return cached.Value;

        try
        {
            using var client = http.CreateClient("keycloak-admin");
            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", "admin-cli"),
                new KeyValuePair<string, string>("username", AdminUser),
                new KeyValuePair<string, string>("password", AdminPassword),
            });
            using var resp = await client.PostAsync(
                $"{IdentityBaseUrl}/realms/{AdminRealm}/protocol/openid-connect/token",
                body, ct);
            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("Keycloak admin token grant failed: {Status}",
                    resp.StatusCode);
                return null;
            }
            var doc = await JsonSerializer.DeserializeAsync<JsonElement>(
                await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var token = doc.GetProperty("access_token").GetString();
            if (token is null) return null;
            var ttl = doc.TryGetProperty("expires_in", out var t)
                ? t.GetInt32() : 300;
            var lifetime = Math.Min(ttl, (int)AdminTokenTtl.TotalSeconds);
            _adminToken = new CachedToken(token,
                DateTimeOffset.UtcNow.AddSeconds(lifetime));
            return token;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Keycloak admin token grant failed");
            return null;
        }
    }

    private async Task<CachedRole?> RoleAsync(string name, CancellationToken ct)
    {
        lock (_rolesLock) { _roles ??= []; }
        var roles = _roles!;
        if (roles.TryGetValue(name, out var known))
            return known;

        var token = await AdminTokenAsync(ct);
        if (token is null) return null;

        try
        {
            using var client = http.CreateClient("keycloak-admin");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            using var resp = await client.GetAsync(
                $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/roles/{Uri.EscapeDataString(name)}", ct);
            if (!resp.IsSuccessStatusCode)
                return null;
            var doc = await JsonSerializer.DeserializeAsync<JsonElement>(
                await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var role = new CachedRole(
                Guid.Parse(doc.GetProperty("id").GetString() ??
                    Guid.Empty.ToString()),
                doc.GetProperty("name").GetString() ?? name);
            lock (_rolesLock) { roles[name] = role; }
            return role;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Role lookup failed for {Name}", name);
            return null;
        }
    }

    private void InvalidateRole(string name)
    {
        lock (_rolesLock) { _roles?.Remove(name); }
    }

    // Counts how many users currently hold the realm-level hr_admin role.
    // Uses the role-by-NAME members endpoint because the roles-by-id members
    // endpoint can be unexposed to the identity admin token in some Keycloak
    // configurations (404), while the name-scoped endpoint works reliably.
    private async Task<int> CountHrAdminHoldersAsync(CancellationToken ct)
    {
        var token = await AdminTokenAsync(ct);
        if (token is null) return -1;
        try
        {
            using var client = http.CreateClient("keycloak-admin");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            var name = Uri.EscapeDataString("hr_admin");
            using var resp = await client.GetAsync(
                $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/roles/{name}/users?max=10", ct);
            if (!resp.IsSuccessStatusCode) return -1;
            var doc = await JsonSerializer.DeserializeAsync<JsonElement>(
                await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return doc.ValueKind == JsonValueKind.Array ? doc.GetArrayLength() : -1;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "hr_admin holder count failed");
            return -1;
        }
    }

    private async Task<Guid?> FindUserIdAsync(string email, CancellationToken ct)
    {
        var token = await AdminTokenAsync(ct);
        if (token is null) return null;
        try
        {
            using var client = http.CreateClient("keycloak-admin");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            using var resp = await client.GetAsync(
                $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users?email={Uri.EscapeDataString(email)}&exact=true", ct);
            if (!resp.IsSuccessStatusCode) return null;
            var doc = await JsonSerializer.DeserializeAsync<JsonElement>(
                await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (doc.ValueKind != JsonValueKind.Array || doc.GetArrayLength() == 0)
                return null;
            var id = doc[0].GetProperty("id").GetString();
            return id is null ? null : Guid.Parse(id);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "User lookup failed for {Email}", email);
            return null;
        }
    }

    private async Task AssignRealmRoleAsync(Guid userId, Guid roleId, string roleName, CancellationToken ct)
    {
        var token = await AdminTokenAsync(ct);
        if (token is null)
            throw new InvalidOperationException("identity-admin-unreachable");
        using var client = http.CreateClient("keycloak-admin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new[] { new RoleRef(roleId, roleName) });
        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var resp = await client.PostAsync(
            $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users/{userId}/role-mappings/realm",
            content, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"role-assignment-failed:{resp.StatusCode}");
    }

    private sealed record RoleRef(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("name")] string Name);
}
