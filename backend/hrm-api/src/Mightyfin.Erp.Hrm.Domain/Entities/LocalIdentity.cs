namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Standalone HRMS account. Passwords are stored only as PBKDF2 records.</summary>
public sealed class LocalUser : Entity
{
    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string RolesCsv { get; set; } = "employee";
    public Guid? WorkerId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }
}

/// <summary>Opaque, revocable server-side session token record.</summary>
public sealed class LocalSession : Entity
{
    public Guid LocalUserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>One-time, expiring token for an account activation or password reset.</summary>
public sealed class LocalCredentialLink : Entity
{
    public Guid LocalUserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
