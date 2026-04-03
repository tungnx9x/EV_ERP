namespace EV_ERP.Models.ViewModels.Users
{
    public class UserListViewModel
    {
        public List<UserRowViewModel> Users { get; set; } = [];
        public string? SearchKeyword { get; set; }
        public int? FilterRoleId { get; set; }
        public string? FilterStatus { get; set; }
        public List<RoleOptionViewModel> Roles { get; set; } = [];
        public int TotalCount { get; set; }
    }

    public class UserRowViewModel
    {
        public int UserId { get; set; }
        public string UserCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        public string StatusBadge => IsLocked
            ? "danger"
            : IsActive ? "success" : "secondary";

        public string StatusText => IsLocked
            ? "Bị khóa"
            : IsActive ? "Hoạt động" : "Vô hiệu";
    }

    public class RoleOptionViewModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
    }
}
