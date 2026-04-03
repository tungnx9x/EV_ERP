using EV_ERP.Helpers;
using EV_ERP.Models.ViewModels.Auth;

namespace EV_ERP.Services.Interfaces
{
    public interface IAuthService
    {
        Task<(bool Success, string? ErrorMessage, CurrentUser? User)> LoginAsync(
            LoginViewModel model, string? ipAddress, string? userAgent);

        Task LogoutAsync(int userId);

        Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(
            int userId, ChangePasswordViewModel model);

        Task<(bool Success, string? ErrorMessage)> ResetPasswordAsync(
            int userId, string newPassword, int adminUserId);
    }
}
