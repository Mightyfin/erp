using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.ConfigAndExtras;

// Job positions (the organisation's catalogue of jobs). M28: finish CRUD.
public sealed class Job : Entity
{
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public Guid? OrgUnitId { get; set; }
    public string? Grade { get; set; }
    public string Status { get; set; } = "active"; // active | inactive
}

// Tenant-level role assignments: which ERP roles are enabled for this tenant.
public sealed class TenantRoleAssignment : Entity
{
    public string RoleKey { get; set; } = null!;
    public string RoleName { get; set; } = null!;
    public string Category { get; set; } = "hrm"; // hrm | payroll | system
    public string PermissionsCsv { get; set; } = "";
    public bool Active { get; set; } = true;
}

// Privacy / data-retention rules. M28: finish CRUD.
public sealed class RetentionRule : Entity
{
    public string RecordType { get; set; } = null!; // contract | payslip | attendance | leave | case | document | letter
    public string? Description { get; set; }
    public int RetentionMonths { get; set; } = 84;
    public bool Active { get; set; } = true;
}
