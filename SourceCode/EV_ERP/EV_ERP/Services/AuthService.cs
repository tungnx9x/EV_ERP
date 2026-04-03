using EV_ERP.Helpers;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.ViewModels.Auth;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUnitOfWork uow, ILogger<AuthService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<(bool Success, string? ErrorMessage, CurrentUser? User)> LoginAsync(
            LoginViewModel model, string? ipAddress, string? userAgent)
        {
            var userRepo = _uow.Repository<User>();
            var user = await userRepo.Query()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == model.Email.Trim().ToLower());

            var loginHistory = new LoginHistory
            {
                LoginAt = DateTime.Now,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                IsSuccess = false
            };

            if (user == null)
            {
                loginHistory.UserId = 0;
                loginHistory.FailReason = "Email không tồn tại";
                _logger.LogWarning("Login failed — email not found: {Email}", model.Email);
                return (false, "Email hoặc mật khẩu không đúng", null);
            }

            loginHistory.UserId = user.UserId;

            if (!user.IsActive)
            {
                loginHistory.FailReason = "Tài khoản bị vô hiệu hóa";
                await SaveLoginHistory(loginHistory);
                return (false, "Tài khoản đã bị vô hiệu hóa. Liên hệ quản trị viên.", null);
            }

            if (user.IsLocked)
            {
                loginHistory.FailReason = "Tài khoản bị khóa";
                await SaveLoginHistory(loginHistory);
                return (false, "Tài khoản đang bị khóa. Liên hệ quản trị viên.", null);
            }

            if (!BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                loginHistory.FailReason = "Sai mật khẩu";
                await SaveLoginHistory(loginHistory);
                _logger.LogWarning("Login failed — wrong password for: {Email}", model.Email);
                return (false, "Email hoặc mật khẩu không đúng", null);
            }

            loginHistory.IsSuccess = true;
            loginHistory.FailReason = null;

            user.LastLoginAt = DateTime.Now;
            userRepo.Update(user);

            await SaveLoginHistory(loginHistory);
            await _uow.SaveChangesAsync();

            var currentUser = new CurrentUser
            {
                UserId = user.UserId,
                UserCode = user.UserCode,
                FullName = user.FullName,
                Email = user.Email,
                RoleId = user.RoleId,
                RoleCode = user.Role.RoleCode,
                RoleName = user.Role.RoleName,
                AvatarUrl = user.AvatarUrl
            };

            _logger.LogInformation("User logged in: {Email} [{RoleCode}]", user.Email, user.Role.RoleCode);
            return (true, null, currentUser);
        }

        public async Task LogoutAsync(int userId)
        {
            _logger.LogInformation("User logged out: UserId={UserId}", userId);
            await Task.CompletedTask;
        }

        public async Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(
            int userId, ChangePasswordViewModel model)
        {
            var userRepo = _uow.Repository<User>();
            var user = await userRepo.GetByIdAsync(userId);

            if (user == null)
                return (false, "Người dùng không tồn tại");

            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash))
                return (false, "Mật khẩu hiện tại không đúng");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.PasswordChangedAt = DateTime.Now;
            user.UpdatedAt = DateTime.Now;
            userRepo.Update(user);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Password changed for UserId={UserId}", userId);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> ResetPasswordAsync(
            int userId, string newPassword, int adminUserId)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return (false, "Mật khẩu mới phải có ít nhất 6 ký tự");

            var userRepo = _uow.Repository<User>();
            var user = await userRepo.GetByIdAsync(userId);

            if (user == null)
                return (false, "Người dùng không tồn tại");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.PasswordChangedAt = DateTime.Now;
            user.UpdatedAt = DateTime.Now;
            userRepo.Update(user);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Password reset for UserId={UserId} by AdminId={AdminId}",
                userId, adminUserId);
            return (true, null);
        }

        // ── private ─────────────────────────────────────
        private async Task SaveLoginHistory(LoginHistory history)
        {
            if (history.UserId <= 0) return;
            var repo = _uow.Repository<LoginHistory>();
            await repo.AddAsync(history);
            await _uow.SaveChangesAsync();
        }
    }
}
