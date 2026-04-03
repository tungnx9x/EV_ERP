using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.ViewModels.Users;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<UserService> _logger;

        public UserService(IUnitOfWork uow, ILogger<UserService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<UserListViewModel> GetListAsync(string? keyword, int? roleId, string? status)
        {
            var query = _uow.Repository<User>().Query()
                .Include(u => u.Role)
                .Where(u => u.IsActive || status == "inactive");

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(keyword) ||
                    u.Email.ToLower().Contains(keyword) ||
                    u.UserCode.ToLower().Contains(keyword));
            }

            if (roleId.HasValue && roleId > 0)
                query = query.Where(u => u.RoleId == roleId);

            if (status == "locked")
                query = query.Where(u => u.IsLocked);
            else if (status == "inactive")
                query = query.Where(u => !u.IsActive);

            var users = await query.OrderBy(u => u.UserCode).ToListAsync();

            var roles = await _uow.Repository<Role>().Query()
                .Where(r => r.IsActive)
                .OrderBy(r => r.RoleName)
                .Select(r => new RoleOptionViewModel
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    RoleCode = r.RoleCode
                })
                .ToListAsync();

            return new UserListViewModel
            {
                Users = users.Select(u => new UserRowViewModel
                {
                    UserId = u.UserId,
                    UserCode = u.UserCode,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone,
                    RoleName = u.Role.RoleName,
                    RoleCode = u.Role.RoleCode,
                    IsActive = u.IsActive,
                    IsLocked = u.IsLocked,
                    LastLoginAt = u.LastLoginAt,
                    AvatarUrl = u.AvatarUrl,
                    CreatedAt = u.CreatedAt
                }).ToList(),
                SearchKeyword = keyword,
                FilterRoleId = roleId,
                FilterStatus = status,
                Roles = roles,
                TotalCount = users.Count
            };
        }

        public async Task<UserFormViewModel> GetFormAsync(int? userId = null)
        {
            var roles = await _uow.Repository<Role>().Query()
                .Where(r => r.IsActive)
                .OrderBy(r => r.RoleName)
                .Select(r => new RoleOptionViewModel
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    RoleCode = r.RoleCode
                })
                .ToListAsync();

            if (!userId.HasValue || userId <= 0)
                return new UserFormViewModel { Roles = roles };

            var user = await _uow.Repository<User>().GetByIdAsync(userId.Value);
            if (user == null)
                return new UserFormViewModel { Roles = roles };

            return new UserFormViewModel
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                RoleId = user.RoleId,
                IsActive = user.IsActive,
                Roles = roles
            };
        }

        public async Task<(bool Success, string? ErrorMessage)> CreateAsync(
            UserFormViewModel model, int createdBy)
        {
            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
                return (false, "Mật khẩu phải có ít nhất 6 ký tự");

            var userRepo = _uow.Repository<User>();

            var emailExists = await userRepo.AnyAsync(
                u => u.Email.ToLower() == model.Email.Trim().ToLower());
            if (emailExists)
                return (false, "Email này đã được sử dụng");

            var userCode = await GenerateUserCodeAsync();

            var user = new User
            {
                UserCode = userCode,
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim().ToLower(),
                Phone = model.Phone?.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                RoleId = model.RoleId,
                IsActive = model.IsActive,
                IsLocked = false,
                PasswordChangedAt = DateTime.Now,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await userRepo.AddAsync(user);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("User created: {UserCode} by AdminId={AdminId}", userCode, createdBy);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(
            UserFormViewModel model, int updatedBy)
        {
            if (!model.UserId.HasValue)
                return (false, "UserId không hợp lệ");

            var userRepo = _uow.Repository<User>();
            var user = await userRepo.GetByIdAsync(model.UserId.Value);
            if (user == null)
                return (false, "Người dùng không tồn tại");

            var emailConflict = await userRepo.AnyAsync(
                u => u.Email.ToLower() == model.Email.Trim().ToLower() && u.UserId != model.UserId);
            if (emailConflict)
                return (false, "Email này đã được sử dụng bởi tài khoản khác");

            user.FullName = model.FullName.Trim();
            user.Email = model.Email.Trim().ToLower();
            user.Phone = model.Phone?.Trim();
            user.RoleId = model.RoleId;
            user.IsActive = model.IsActive;
            user.UpdatedAt = DateTime.Now;

            userRepo.Update(user);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("User updated: UserId={UserId} by AdminId={AdminId}",
                model.UserId, updatedBy);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> ToggleLockAsync(
            int userId, int adminUserId)
        {
            var userRepo = _uow.Repository<User>();
            var user = await userRepo.GetByIdAsync(userId);
            if (user == null)
                return (false, "Người dùng không tồn tại");

            if (userId == adminUserId)
                return (false, "Không thể khóa tài khoản của chính mình");

            user.IsLocked = !user.IsLocked;
            user.UpdatedAt = DateTime.Now;
            userRepo.Update(user);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("User {Action}: UserId={UserId} by AdminId={AdminId}",
                user.IsLocked ? "locked" : "unlocked", userId, adminUserId);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(
            int userId, int adminUserId)
        {
            if (userId == adminUserId)
                return (false, "Không thể vô hiệu hóa tài khoản của chính mình");

            var userRepo = _uow.Repository<User>();
            var user = await userRepo.GetByIdAsync(userId);
            if (user == null)
                return (false, "Người dùng không tồn tại");

            user.IsActive = false;
            user.UpdatedAt = DateTime.Now;
            userRepo.Update(user);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("User deactivated: UserId={UserId} by AdminId={AdminId}",
                userId, adminUserId);
            return (true, null);
        }

        // ── private ─────────────────────────────────────
        private async Task<string> GenerateUserCodeAsync()
        {
            var lastUser = await _uow.Repository<User>().Query()
                .Where(u => u.UserCode.StartsWith("NV-"))
                .OrderByDescending(u => u.UserCode)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastUser != null && lastUser.UserCode.Length > 3)
            {
                if (int.TryParse(lastUser.UserCode[3..], out int last))
                    nextNumber = last + 1;
            }

            return $"NV-{nextNumber:D4}";
        }
    }
}
