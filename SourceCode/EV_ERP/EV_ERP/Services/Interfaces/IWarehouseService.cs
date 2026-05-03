using EV_ERP.Models.ViewModels.Stock;

namespace EV_ERP.Services.Interfaces
{
    public interface IWarehouseService
    {
        Task<WarehouseListViewModel> GetListAsync(string? keyword, string? status, int pageIndex = 1, int pageSize = 20);
        Task<WarehouseFormViewModel> GetFormAsync(int? warehouseId = null);
        Task<(bool Success, string? ErrorMessage)> CreateAsync(WarehouseFormViewModel model, int createdBy);
        Task<(bool Success, string? ErrorMessage)> UpdateAsync(WarehouseFormViewModel model, int updatedBy);
        Task<(bool Success, string? ErrorMessage)> ToggleActiveAsync(int warehouseId);
        Task<WarehouseDetailViewModel?> GetDetailAsync(int warehouseId);

        // Locations
        Task<(bool Success, string? ErrorMessage)> SaveLocationAsync(LocationFormModel model);
        Task<(bool Success, string? ErrorMessage)> ToggleLocationActiveAsync(int locationId);

        // Options for dropdowns
        Task<List<WarehouseOptionViewModel>> GetWarehouseOptionsAsync();
    }
}
