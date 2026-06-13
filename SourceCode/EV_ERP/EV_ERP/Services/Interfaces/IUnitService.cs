using EV_ERP.Models.ViewModels.Products;

namespace EV_ERP.Services.Interfaces
{
    public interface IUnitService
    {
        Task<UnitListViewModel> GetListAsync(string? keyword, string? status);
        Task<UnitFormViewModel> GetFormAsync(int? unitId = null);
        Task<(bool Success, string? ErrorMessage)> CreateAsync(UnitFormViewModel model);
        Task<(bool Success, string? ErrorMessage)> UpdateAsync(UnitFormViewModel model);
        Task<(bool Success, string? ErrorMessage)> DeleteAsync(int unitId);
    }
}
