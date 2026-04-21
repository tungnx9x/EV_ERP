using EV_ERP.Models.Common;

namespace EV_ERP.Models.Entities.Auth;

// ─── MODULE ──────────────────────────────────────────
public class Module : BaseEntity, ISoftDeletable
{
    public int ModuleId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Permission> Permissions { get; set; } = [];
}

// ─── ROLE ────────────────────────────────────────────
public class Role : BaseEntity, ISoftDeletable
{
    public int RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<User> Users { get; set; } = [];
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = [];
}

// ─── PERMISSION ──────────────────────────────────────
public class Permission : BaseEntity
{
    public int PermissionId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;   // VD: CUSTOMER.VIEW
    public string PermissionName { get; set; } = string.Empty;
    public int ModuleId { get; set; }
    public string ActionType { get; set; } = string.Empty;       // VIEW, CREATE, EDIT, DELETE, EXPORT
    public string? Description { get; set; }

    public virtual Module Module { get; set; } = null!;
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = [];
}

// ─── ROLE ↔ PERMISSION ──────────────────────────────
public class RolePermission
{
    public int RolePermissionId { get; set; }
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public string DataScope { get; set; } = "ALL";               // ALL, OWN, NONE

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Role Role { get; set; } = null!;
    public virtual Permission Permission { get; set; } = null!;
}

// ─── USER ────────────────────────────────────────────
public class User : BaseEntity, ISoftDeletable
{
    public int UserId { get; set; }
    public string UserCode { get; set; } = string.Empty;         // NV-0001
    public string UserName { get; set; } = string.Empty;         // "hoangnv", "linhtt"
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }
    public int? CreatedBy { get; set; }

    public virtual Role Role { get; set; } = null!;
    public virtual ICollection<LoginHistory> LoginHistories { get; set; } = [];
    public virtual ICollection<UserSession> Sessions { get; set; } = [];
}

// ─── LOGIN HISTORY ───────────────────────────────────
public class LoginHistory
{
    public long LoginHistoryId { get; set; }
    public int UserId { get; set; }
    public DateTime LoginAt { get; set; } = DateTime.Now;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceInfo { get; set; }
    public bool IsSuccess { get; set; } = true;
    public string? FailReason { get; set; }

    public virtual User User { get; set; } = null!;
}

// ─── USER SESSION ────────────────────────────────────
public class UserSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ExpiresAt { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.Now;
    public string? IpAddress { get; set; }
    public bool IsRevoked { get; set; }

    public virtual User User { get; set; } = null!;
}
