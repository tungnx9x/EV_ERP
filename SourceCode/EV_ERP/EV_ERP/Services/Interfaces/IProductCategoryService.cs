using EV_ERP.Models.ViewModels.Products;

namespace EV_ERP.Services.Interfaces
{
    public interface IProductCategoryService
    {
        Task<ProductCategoryListViewModel> GetListAsync(string? keyword, string? status);
        Task<ProductCategoryFormViewModel> GetFormAsync(int? categoryId = null);
        Task<(bool Success, string? ErrorMessage)> CreateAsync(ProductCategoryFormViewModel model, int createdBy);
        Task<(bool Success, string? ErrorMessage)> UpdateAsync(ProductCategoryFormViewModel model, int updatedBy);
        Task<(bool Success, string? ErrorMessage)> ToggleActiveAsync(int categoryId, int updatedBy);
    }
}
