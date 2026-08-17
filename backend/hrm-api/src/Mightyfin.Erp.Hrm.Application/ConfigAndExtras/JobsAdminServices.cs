using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.ConfigAndExtras;

/// <summary>M28: jobs catalogue, tenant role assignments and retention rules —
/// the remaining admin CRUD surfaces, on top of the shared IConfigRepository
/// extensions.</summary>
public interface IJobsAdminService
{
    Task<List<JobDto>> ListJobsAsync(bool includeInactive, CancellationToken ct);
    Task<JobDto> CreateJobAsync(JobCreateRequest request, CancellationToken ct);
    Task<JobDto> UpdateJobAsync(Guid id, JobUpdateRequest request, CancellationToken ct);
    Task<JobDto> CloseJobAsync(Guid id, CancellationToken ct);

    Task<List<TenantRoleDto>> ListRolesAsync(CancellationToken ct);
    Task<TenantRoleDto> UpdateRoleAsync(string roleKey, RoleUpdateRequest request, CancellationToken ct);

    Task<List<DataRetentionDto>> ListRetentionRulesAsync(CancellationToken ct);
    Task<DataRetentionDto> CreateRetentionRuleAsync(DataRetentionCreateRequest request, CancellationToken ct);
    Task<DataRetentionDto> UpdateRetentionRuleAsync(Guid id, DataRetentionUpdateRequest request, CancellationToken ct);
    Task DeleteRetentionRuleAsync(Guid id, CancellationToken ct);
}

public sealed class JobsAdminServiceImpl(IConfigRepository repo, IAuthzService authz) : IJobsAdminService
{
    // ================= Jobs =================

    public async Task<List<JobDto>> ListJobsAsync(bool includeInactive, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var jobs = await repo.ListJobsAsync(ct);
        var units = await repo.ListOrgUnitsAsync(ct);
        return jobs
            .Where(j => includeInactive || j.Status == "active")
            .OrderBy(j => j.Code)
            .Select(j => ToJobDto(j, units))
            .ToList();
    }

    public async Task<JobDto> CreateJobAsync(JobCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        RequireNonEmpty(request.Code, "code");
        RequireNonEmpty(request.Title, "title");
        var existing = (await repo.ListJobsAsync(ct)).FirstOrDefault(j => j.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            throw new DomainException("job-code-taken", $"Job code '{request.Code}' is already in use.");
        var allUnits = await repo.ListOrgUnitsAsync(ct);
        if (request.OrgUnitId.HasValue)
        {
            if (allUnits.All(u => u.Id != request.OrgUnitId))
                throw new DomainException("org-unit-not-found", $"Org unit {request.OrgUnitId} does not exist.");
        }
        var job = new Job
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Title = request.Title.Trim(),
            OrgUnitId = request.OrgUnitId,
            Grade = request.Grade?.Trim(),
        };
        return ToJobDto(await repo.CreateJobAsync(job, ct), allUnits);
    }

    public async Task<JobDto> UpdateJobAsync(Guid id, JobUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var job = await repo.GetJobAsync(id, ct)
            ?? throw new DomainException("job-not-found", $"Job {id} does not exist.");
        if (request.Title is not null) job.Title = request.Title.Trim();
        if (request.Grade is not null) job.Grade = request.Grade.Trim();
        var allUnits = await repo.ListOrgUnitsAsync(ct);
        if (request.OrgUnitId.HasValue)
        {
            if (allUnits.All(u => u.Id != request.OrgUnitId))
                throw new DomainException("org-unit-not-found", $"Org unit {request.OrgUnitId} does not exist.");
            job.OrgUnitId = request.OrgUnitId;
        }
        return ToJobDto(await repo.UpdateJobAsync(job, ct), allUnits);
    }

    public async Task<JobDto> CloseJobAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var job = await repo.GetJobAsync(id, ct)
            ?? throw new DomainException("job-not-found", $"Job {id} does not exist.");
        job.Status = "inactive";
        var units = await repo.ListOrgUnitsAsync(ct);
        return ToJobDto(await repo.UpdateJobAsync(job, ct), units);
    }

    private static JobDto ToJobDto(Job j, List<OrgUnit> units) =>
        new(j.Id, j.Code, j.Title, j.OrgUnitId, units.FirstOrDefault(u => u.Id == j.OrgUnitId)?.Name, j.Grade, j.Status);

    // ================= Roles =================

    public async Task<List<TenantRoleDto>> ListRolesAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var rows = await repo.ListRoleAssignmentsAsync(ct);
        return rows.Select(r => new TenantRoleDto(r.Id, r.RoleKey, r.RoleName, r.Category, r.Active)).ToList();
    }

    public async Task<TenantRoleDto> UpdateRoleAsync(string roleKey, RoleUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var rows = await repo.ListRoleAssignmentsAsync(ct);
        var row = rows.FirstOrDefault(r => r.RoleKey.Equals(roleKey, StringComparison.OrdinalIgnoreCase));
        if (row is null)
            throw new DomainException("role-not-found", $"Role '{roleKey}' is not managed for this tenant.");
        row.Active = request.Active;
        row = await repo.UpdateRoleAssignmentAsync(row, ct);
        return new TenantRoleDto(row.Id, row.RoleKey, row.RoleName, row.Category, row.Active);
    }

    // ================= Retention rules =================

    public async Task<List<DataRetentionDto>> ListRetentionRulesAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var rows = await repo.ListRetentionRulesAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<DataRetentionDto> CreateRetentionRuleAsync(DataRetentionCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        RequireNonEmpty(request.RecordType, "recordType");
        if (request.RetentionMonths <= 0 || request.RetentionMonths > 600)
            throw new DomainException("retention-months", "RetentionMonths must be between 1 and 600.");
        var normalized = request.RecordType.Trim().ToLowerInvariant();
        var existing = (await repo.ListRetentionRulesAsync(ct)).FirstOrDefault(r => r.RecordType.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            throw new DomainException("retention-rule-taken", $"A retention rule for '{request.RecordType}' already exists.");
        var rule = new RetentionRule
        {
            RecordType = request.RecordType.Trim().ToLowerInvariant(),
            RetentionMonths = request.RetentionMonths,
            Description = request.Description?.Trim(),
        };
        return ToDto(await repo.CreateRetentionRuleAsync(rule, ct));
    }

    public async Task<DataRetentionDto> UpdateRetentionRuleAsync(Guid id, DataRetentionUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var rule = await repo.GetRetentionRuleAsync(id, ct)
            ?? throw new DomainException("retention-rule-not-found", $"Retention rule {id} does not exist.");
        if (request.RetentionMonths.HasValue)
        {
            if (request.RetentionMonths.Value <= 0 || request.RetentionMonths.Value > 600)
                throw new DomainException("retention-months", "RetentionMonths must be between 1 and 600.");
            rule.RetentionMonths = request.RetentionMonths.Value;
        }
        if (request.Description is not null) rule.Description = request.Description.Trim();
        if (request.Active.HasValue) rule.Active = request.Active.Value;
        return ToDto(await repo.UpdateRetentionRuleAsync(rule, ct));
    }

    public async Task DeleteRetentionRuleAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var rule = await repo.GetRetentionRuleAsync(id, ct)
            ?? throw new DomainException("retention-rule-not-found", $"Retention rule {id} does not exist.");
        await repo.DeleteRetentionRuleAsync(id, ct);
    }

    private static DataRetentionDto ToDto(RetentionRule r) => new(r.Id, r.RecordType, r.Description, r.RetentionMonths, r.Active);

    private static void RequireNonEmpty(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("field-required", $"{field} is required.");
    }
}
