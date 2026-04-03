using EV_ERP.Models.ViewModels.Users;

namespace EV_ERP.Services.Interfaces
{
    public interface IUserService
    {
        Task<UserListViewModel> GetListAsync(string? keyword, int? roleId, string? status);
        Task<UserFormViewModel> GetFormAsync(int? userId = null);
        Task<(bool Success, string? ErrorMessage)> CreateAsync(UserFormViewModel model, int createdBy);
        Task<(bool Success, string? ErrorMessage)> UpdateAsync(UserFormViewModel model, int updatedBy);
        Task<(bool Success, string? ErrorMessage)> ToggleLockAsync(int userId, int adminUserId);
        Task<(bool Success, string? ErrorMessage)> DeleteAsync(int userId, int adminUserId);
    }
}
