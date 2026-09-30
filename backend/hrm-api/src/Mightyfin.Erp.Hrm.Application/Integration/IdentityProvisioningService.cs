// M51: first-user auto-elevation + HR administrator invitation provisioning.
// The HRM API talks to the organisation's Keycloak identity system (admin REST
// API over the internal docker network) so that:
//  1. The FIRST user to sign into a fresh HRM instance (setup PENDING and no
//     one holds the hr_admin realm role yet) is automatically elevated to
//     top HR admin (hr_admin + tenant_owner) — no manual role assignment.
//  2. The setup-wizard "HR administrators" step provisions invited emails:
//     missing Keycloak users are CREATED (firstName/lastName derived from the
//     email prefix — required by Keycloak's declarative user profile — with a
//     temporary password the invitee must change on first login) and granted
//     hr_admin + employee realm roles.
// All calls are best-effort: failures are logged and surfaced as partial
// results; they never block HR workflows or crash the API.
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
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

    /// M51.2: provision invited administrator emails. Missing Keycloak users
    /// are created automatically (with a temporary password) and then granted
    /// hr_admin + employee realm roles. Per-entry outcomes are reported so the
    /// wizard can show which invites actually took; the endpoint is idempotent
    /// so re-saving the same list is safe.
    Task<ProvisionResult> ProvisionAdminsAsync(
        IEnumerable<string> emails, CancellationToken ct);

    /// Lists and administers the users admitted to the ERP realm. These
    /// operations deliberately target Keycloak rather than the optional local
    /// account tables used by standalone installations.
    Task<IdentityUserList> ListUsersAsync(CancellationToken ct);
    Task<IReadOnlyList<IdentityDirectoryUser>> SearchDirectoryAsync(
        string query, CancellationToken ct);
    Task<IdentityAccessUser> InviteUserAsync(
        IdentityUserInvite request, CancellationToken ct);
    Task<IdentityAccessUser> UpdateUserAsync(
        string userId, IdentityUserUpdate request, CancellationToken ct);
    Task SendPasswordResetAsync(string userId, CancellationToken ct);
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

public sealed record IdentityAccessUser(
    string Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool Federated);

public sealed record IdentityUserList(
    string Provider,
    string Realm,
    IReadOnlyList<IdentityAccessUser> Items);

public sealed record IdentityUserInvite(
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    string SourceUserId);

public sealed record IdentityDirectoryUser(
    string Id,
    string Email,
    string DisplayName);

public sealed record IdentityUserUpdate(
    bool? IsActive,
    IReadOnlyList<string>? Roles);

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
    private string SourceRealm =>
        cfg["HRM:IdentitySourceRealm"] ?? "mightyfin-sandbox";
    private string BrokerAlias =>
        cfg["HRM:IdentityBrokerAlias"] ?? "mightyfin-staff";

    private static readonly string[] ManagedRoles =
    [
        "employee", "manager", "hr_ops", "payroll", "finance_approver",
        "hr_admin", "investigator", "erp_access"
    ];

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
                var userId = await FindUserIdAsync(email, ct) ??
                    await GetOrCreateUserIdAsync(email, ct);
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

    public async Task<IdentityUserList> ListUsersAsync(CancellationToken ct)
    {
        var token = await AdminTokenAsync(ct) ??
            throw new InvalidOperationException("identity-admin-unreachable");
        using var client = AdminClient(token);
        using var response = await client.GetAsync(
            $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users?max=500", ct);
        await EnsureIdentitySuccessAsync(response, "identity-user-list-failed", ct);
        var users = await JsonSerializer.DeserializeAsync<JsonElement>(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var items = new List<IdentityAccessUser>();
        if (users.ValueKind == JsonValueKind.Array)
        {
            foreach (var user in users.EnumerateArray())
            {
                var id = user.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
                if (string.IsNullOrWhiteSpace(id)) continue;
                var roles = await UserRolesAsync(client, TenantRealm, id, ct);
                if (!roles.Any(ManagedRoles.Contains)) continue;
                var email = user.TryGetProperty("email", out var emailNode)
                    ? emailNode.GetString() ?? "" : "";
                var username = user.TryGetProperty("username", out var usernameNode)
                    ? usernameNode.GetString() ?? "" : "";
                var firstName = user.TryGetProperty("firstName", out var firstNode)
                    ? firstNode.GetString() ?? "" : "";
                var lastName = user.TryGetProperty("lastName", out var lastNode)
                    ? lastNode.GetString() ?? "" : "";
                var displayName = string.Join(' ', new[] { firstName, lastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
                if (string.IsNullOrWhiteSpace(displayName)) displayName = email.Length > 0 ? email : username;
                var active = !user.TryGetProperty("enabled", out var enabledNode) || enabledNode.GetBoolean();
                var federated = await HasFederatedIdentityAsync(client, TenantRealm, id, ct);
                items.Add(new IdentityAccessUser(id, email.Length > 0 ? email : username,
                    displayName, roles.Where(ManagedRoles.Contains).Order().ToArray(), active, federated));
            }
        }
        return new IdentityUserList("oidc", TenantRealm,
            items.OrderBy(item => item.Email, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public async Task<IReadOnlyList<IdentityDirectoryUser>> SearchDirectoryAsync(
        string query, CancellationToken ct)
    {
        var value = query.Trim();
        if (value.Length < 2) return [];
        var token = await AdminTokenAsync(ct) ??
            throw new InvalidOperationException("identity-admin-unreachable");
        using var client = AdminClient(token);
        using var response = await client.GetAsync(
            $"{IdentityBaseUrl}/admin/realms/{SourceRealm}/users?search={Uri.EscapeDataString(value)}&max=20",
            ct);
        await EnsureIdentitySuccessAsync(response, "identity-directory-search-failed", ct);
        var users = await JsonSerializer.DeserializeAsync<JsonElement>(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (users.ValueKind != JsonValueKind.Array) return [];
        return users.EnumerateArray().Select(user =>
        {
            var id = user.TryGetProperty("id", out var idNode) ? idNode.GetString() ?? "" : "";
            var email = user.TryGetProperty("email", out var emailNode)
                ? emailNode.GetString() ?? "" : "";
            var username = user.TryGetProperty("username", out var usernameNode)
                ? usernameNode.GetString() ?? "" : "";
            var first = user.TryGetProperty("firstName", out var firstNode)
                ? firstNode.GetString() ?? "" : "";
            var last = user.TryGetProperty("lastName", out var lastNode)
                ? lastNode.GetString() ?? "" : "";
            var name = string.Join(' ', new[] { first, last }.Where(x => x.Length > 0));
            return new IdentityDirectoryUser(id, email.Length > 0 ? email : username,
                name.Length > 0 ? name : email.Length > 0 ? email : username);
        }).Where(user => user.Id.Length > 0 && user.Email.Length > 0).ToArray();
    }

    public async Task<IdentityAccessUser> InviteUserAsync(
        IdentityUserInvite request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (email.Length == 0 || !email.Contains('@'))
            throw new InvalidOperationException("identity-email-invalid");
        var token = await AdminTokenAsync(ct) ??
            throw new InvalidOperationException("identity-admin-unreachable");
        using var client = AdminClient(token);

        if (string.IsNullOrWhiteSpace(request.SourceUserId))
            throw new InvalidOperationException("identity-source-user-required");
        var source = await GetUserRepresentationAsync(client, SourceRealm, request.SourceUserId, ct);
        var sourceEmail = source.TryGetProperty("email", out var sourceEmailNode)
            ? sourceEmailNode.GetString() ?? "" : "";
        if (!sourceEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("identity-source-user-mismatch");
        var sourceId = request.SourceUserId;
        var targetId = await FindUserIdInRealmAsync(client, TenantRealm, email, ct)
            ?? await CreateUserInRealmAsync(client, TenantRealm, email, request.DisplayName, ct);
        await EnsureBrokerLinkAsync(client, targetId, sourceId, email, ct);
        await ReplaceManagedRolesAsync(client, targetId, request.Roles, ct);
        return await GetIdentityUserAsync(client, targetId, ct);
    }

    public async Task<IdentityAccessUser> UpdateUserAsync(
        string userId, IdentityUserUpdate request, CancellationToken ct)
    {
        var token = await AdminTokenAsync(ct) ??
            throw new InvalidOperationException("identity-admin-unreachable");
        using var client = AdminClient(token);
        if (request.IsActive is not null)
        {
            using var get = await client.GetAsync(
                $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users/{Uri.EscapeDataString(userId)}", ct);
            await EnsureIdentitySuccessAsync(get, "identity-user-not-found", ct);
            var node = await JsonSerializer.DeserializeAsync<JsonNode>(
                await get.Content.ReadAsStreamAsync(ct), cancellationToken: ct) ?? new JsonObject();
            node["enabled"] = request.IsActive.Value;
            using var content = new StringContent(node.ToJsonString(), System.Text.Encoding.UTF8,
                new MediaTypeHeaderValue("application/json"));
            using var put = await client.PutAsync(
                $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users/{Uri.EscapeDataString(userId)}",
                content, ct);
            await EnsureIdentitySuccessAsync(put, "identity-user-update-failed", ct);
        }
        if (request.Roles is not null)
            await ReplaceManagedRolesAsync(client, userId, request.Roles, ct);
        return await GetIdentityUserAsync(client, userId, ct);
    }

    public async Task SendPasswordResetAsync(string userId, CancellationToken ct)
    {
        var token = await AdminTokenAsync(ct) ??
            throw new InvalidOperationException("identity-admin-unreachable");
        using var client = AdminClient(token);
        var sourceId = await FederatedSourceIdAsync(client, userId, ct);
        await ExecutePasswordActionAsync(client,
            sourceId is null ? TenantRealm : SourceRealm,
            sourceId ?? userId, ct);
    }

    // ---------------- Keycloak admin REST helpers ----------------

    private HttpClient AdminClient(string token)
    {
        var client = http.CreateClient("keycloak-admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task EnsureIdentitySuccessAsync(
        HttpResponseMessage response, string code, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"{code}:{(int)response.StatusCode}:{detail[..Math.Min(detail.Length, 160)]}");
    }

    private async Task<string[]> UserRolesAsync(
        HttpClient client, string realm, string userId, CancellationToken ct)
    {
        using var response = await client.GetAsync(
            $"{IdentityBaseUrl}/admin/realms/{realm}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm/composite",
            ct);
        await EnsureIdentitySuccessAsync(response, "identity-role-list-failed", ct);
        var roles = await JsonSerializer.DeserializeAsync<JsonElement>(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return roles.ValueKind == JsonValueKind.Array
            ? roles.EnumerateArray()
                .Select(role => role.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
    }

    private async Task<bool> HasFederatedIdentityAsync(
        HttpClient client, string realm, string userId, CancellationToken ct)
        => (await FederatedIdentitiesAsync(client, realm, userId, ct))
            .Any(identity => identity.Alias.Equals(BrokerAlias, StringComparison.OrdinalIgnoreCase));

    private async Task<string?> FederatedSourceIdAsync(
        HttpClient client, string targetUserId, CancellationToken ct)
        => (await FederatedIdentitiesAsync(client, TenantRealm, targetUserId, ct))
            .FirstOrDefault(identity => identity.Alias.Equals(BrokerAlias,
                StringComparison.OrdinalIgnoreCase))?.UserId;

    private async Task<FederatedIdentityRef[]> FederatedIdentitiesAsync(
        HttpClient client, string realm, string userId, CancellationToken ct)
    {
        using var response = await client.GetAsync(
            $"{IdentityBaseUrl}/admin/realms/{realm}/users/{Uri.EscapeDataString(userId)}/federated-identity",
            ct);
        await EnsureIdentitySuccessAsync(response, "identity-link-list-failed", ct);
        var links = await JsonSerializer.DeserializeAsync<JsonElement>(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (links.ValueKind != JsonValueKind.Array) return [];
        return links.EnumerateArray().Select(link => new FederatedIdentityRef(
            link.TryGetProperty("identityProvider", out var alias) ? alias.GetString() ?? "" : "",
            link.TryGetProperty("userId", out var id) ? id.GetString() ?? "" : ""))
            .ToArray();
    }

    private async Task<string?> FindUserIdInRealmAsync(
        HttpClient client, string realm, string email, CancellationToken ct)
    {
        using var response = await client.GetAsync(
            $"{IdentityBaseUrl}/admin/realms/{realm}/users?email={Uri.EscapeDataString(email)}&exact=true",
            ct);
        await EnsureIdentitySuccessAsync(response, "identity-user-search-failed", ct);
        var users = await JsonSerializer.DeserializeAsync<JsonElement>(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (users.ValueKind != JsonValueKind.Array || users.GetArrayLength() == 0) return null;
        return users[0].TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    private async Task<JsonElement> GetUserRepresentationAsync(
        HttpClient client, string realm, string userId, CancellationToken ct)
    {
        using var response = await client.GetAsync(
            $"{IdentityBaseUrl}/admin/realms/{realm}/users/{Uri.EscapeDataString(userId)}", ct);
        await EnsureIdentitySuccessAsync(response, "identity-directory-user-not-found", ct);
        return await JsonSerializer.DeserializeAsync<JsonElement>(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    private async Task<string> CreateUserInRealmAsync(
        HttpClient client, string realm, string email, string displayName, CancellationToken ct)
    {
        var names = displayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var fallback = Capitalize(email[..email.IndexOf('@')]);
        var firstName = names.Length > 0 ? names[0] : fallback;
        var lastName = names.Length > 1 ? names[1] : firstName;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            username = email,
            email,
            emailVerified = false,
            enabled = true,
            firstName,
            lastName,
            requiredActions = Array.Empty<string>()
        });
        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await client.PostAsync(
            $"{IdentityBaseUrl}/admin/realms/{realm}/users", content, ct);
        if (response.StatusCode != System.Net.HttpStatusCode.Conflict)
            await EnsureIdentitySuccessAsync(response, "identity-user-create-failed", ct);
        return await FindUserIdInRealmAsync(client, realm, email, ct)
            ?? throw new InvalidOperationException("identity-user-create-unresolved");
    }

    private async Task EnsureBrokerLinkAsync(
        HttpClient client, string targetId, string sourceId, string email, CancellationToken ct)
    {
        if (await HasFederatedIdentityAsync(client, TenantRealm, targetId, ct)) return;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            identityProvider = BrokerAlias,
            userId = sourceId,
            userName = email
        });
        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await client.PostAsync(
            $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users/{Uri.EscapeDataString(targetId)}/federated-identity/{Uri.EscapeDataString(BrokerAlias)}",
            content, ct);
        await EnsureIdentitySuccessAsync(response, "identity-link-create-failed", ct);
    }

    private async Task ReplaceManagedRolesAsync(
        HttpClient client, string userId, IEnumerable<string> requested, CancellationToken ct)
    {
        var desired = requested
            .Where(role => ManagedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        desired.Add("employee");
        desired.Add("erp_access");

        using var existingResponse = await client.GetAsync(
            $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm",
            ct);
        await EnsureIdentitySuccessAsync(existingResponse, "identity-role-list-failed", ct);
        var existing = await JsonSerializer.DeserializeAsync<JsonElement>(
            await existingResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var removable = existing.ValueKind == JsonValueKind.Array
            ? existing.EnumerateArray()
                .Where(role => role.TryGetProperty("name", out var name)
                    && ManagedRoles.Contains(name.GetString() ?? "", StringComparer.OrdinalIgnoreCase)
                    && !desired.Contains(name.GetString() ?? ""))
                .Select(role => role.Clone()).ToArray()
            : [];
        if (removable.Length > 0)
        {
            using var removeRequest = new HttpRequestMessage(HttpMethod.Delete,
                $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm")
            {
                Content = new StringContent(JsonSerializer.Serialize(removable),
                    System.Text.Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
            };
            using var removeResponse = await client.SendAsync(removeRequest, ct);
            await EnsureIdentitySuccessAsync(removeResponse, "identity-role-remove-failed", ct);
        }

        var existingNames = existing.ValueKind == JsonValueKind.Array
            ? existing.EnumerateArray()
                .Select(role => role.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "")
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleName in desired.Where(role => !existingNames.Contains(role)))
        {
            var role = await RoleAsync(roleName, ct);
            if (role is not null)
                await AssignRealmRoleAsync(Guid.Parse(userId), role.Id, role.Name, ct);
        }
    }

    private async Task<IdentityAccessUser> GetIdentityUserAsync(
        HttpClient client, string userId, CancellationToken ct)
    {
        using var response = await client.GetAsync(
            $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users/{Uri.EscapeDataString(userId)}", ct);
        await EnsureIdentitySuccessAsync(response, "identity-user-not-found", ct);
        var user = await JsonSerializer.DeserializeAsync<JsonElement>(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var roles = await UserRolesAsync(client, TenantRealm, userId, ct);
        var email = user.TryGetProperty("email", out var emailNode)
            ? emailNode.GetString() ?? "" : "";
        var first = user.TryGetProperty("firstName", out var firstNode)
            ? firstNode.GetString() ?? "" : "";
        var last = user.TryGetProperty("lastName", out var lastNode)
            ? lastNode.GetString() ?? "" : "";
        var name = string.Join(' ', new[] { first, last }.Where(value => value.Length > 0));
        return new IdentityAccessUser(userId, email, name.Length > 0 ? name : email,
            roles.Where(ManagedRoles.Contains).Order().ToArray(),
            !user.TryGetProperty("enabled", out var enabled) || enabled.GetBoolean(),
            await HasFederatedIdentityAsync(client, TenantRealm, userId, ct));
    }

    private async Task ExecutePasswordActionAsync(
        HttpClient client, string realm, string userId, CancellationToken ct)
    {
        using var content = new StringContent("[\"UPDATE_PASSWORD\"]",
            System.Text.Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        using var response = await client.PutAsync(
            $"{IdentityBaseUrl}/admin/realms/{realm}/users/{Uri.EscapeDataString(userId)}/execute-actions-email?lifespan=43200",
            content, ct);
        await EnsureIdentitySuccessAsync(response, "identity-password-email-failed", ct);
    }

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

    // Clears requiredActions (UPDATE_PASSWORD etc.) on a newly created user
    // so direct-grant login with the temp password is not blocked by
    // "Account is not fully set up".
    private async Task ClearRequiredActionsAsync(Guid userId, CancellationToken ct)
    {
        var token = await AdminTokenAsync(ct);
        if (token is null) return;
        try
        {
            // GET the full user representation, wipe requiredActions, PUT back.
            using var client = http.CreateClient("keycloak-admin");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            using var getResp = await client.GetAsync(
                $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users/{userId}", ct);
            if (!getResp.IsSuccessStatusCode) return;
            using var repStream = await getResp.Content.ReadAsStreamAsync(ct);
            var mutable = await JsonSerializer.DeserializeAsync<JsonNode>(
                repStream, cancellationToken: ct);
            if (mutable is null) return;
            mutable["requiredActions"] = JsonSerializer.SerializeToNode(Array.Empty<string>());
            using var content = new StringContent(mutable.ToJsonString(),
                System.Text.Encoding.UTF8,
                new MediaTypeHeaderValue("application/json"));
            using var putResp = await client.PutAsync(
                $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users/{userId}", content, ct);
            if (!putResp.IsSuccessStatusCode)
                log.LogWarning("Clearing requiredActions failed for {UserId}: {Status}",
                    userId, putResp.StatusCode);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Clearing requiredActions failed for {UserId}", userId);
        }
    }

    // Creates the Keycloak user when one cannot be found by email. Uses the
    // local-part of the email address as firstName/lastName (Keycloak's
    // declarative user profile requires both). The account is ENABLED with a
    // temporary password (TempHrm#2026x) that the invitee is forced to change
    // on first login. Returns the user id on success, null on failure.
    private async Task<Guid?> GetOrCreateUserIdAsync(string email, CancellationToken ct)
    {
        var token = await AdminTokenAsync(ct);
        if (token is null) return null;
        try
        {
            var prefix = email[..email.IndexOf('@')];
            var body = JsonSerializer.SerializeToUtf8Bytes(new CreateUserPayload(
                email,
                email,
                true,
                [new CredentialPayload("password", true, "TempHrm#2026x")],
                Capitalize(prefix),
                Capitalize(prefix)));
            using var client = http.CreateClient("keycloak-admin");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            using var content = new ByteArrayContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var resp = await client.PostAsync(
                $"{IdentityBaseUrl}/admin/realms/{TenantRealm}/users", content, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Another instance raced us to creation — resolve by email again.
                return await FindUserIdAsync(email, ct);
            }
            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("Keycloak user creation failed for {Email}: {Status}",
                    email, resp.StatusCode);
                return null;
            }
            // Keycloak blocks direct-grant login ("Account is not fully set
            // up") while UPDATE_PASSWORD sits in requiredActions. Erp-web
            // users sign in with the operator-supplied temp password, so
            // clear requiredActions after creation — the invitation message
            // tells the invitee the temp password they received.
            var newId = await FindUserIdAsync(email, ct);
            if (newId is not null)
                await ClearRequiredActionsAsync(newId.Value, ct);
            return newId;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "User creation failed for {Email}", email);
            return null;
        }
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Hr";
        var parts = s.Split('.', '-', '_');
        return string.Join(' ', parts
            .Where(p => p.Length > 0)
            .Select(p => char.ToUpperInvariant(p[0]) + p[1..]))[..Math.Min(32, s.Length)];
    }

    private sealed record CreateUserPayload(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("enabled")] bool Enabled,
        [property: JsonPropertyName("credentials")] CredentialPayload[] Credentials,
        [property: JsonPropertyName("firstName")] string FirstName,
        [property: JsonPropertyName("lastName")] string LastName);

    private sealed record CredentialPayload(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("temporary")] bool Temporary,
        [property: JsonPropertyName("value")] string Value);

    private sealed record RoleRef(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("name")] string Name);

    private sealed record FederatedIdentityRef(string Alias, string UserId);
}
