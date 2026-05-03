using EV_ERP.Models.ViewModels.Products;

namespace EV_ERP.Services.Interfaces
{
    public interface IProductAttributeService
    {
        // Attribute CRUD
        Task<ProductAttributeListViewModel> GetAttributeListAsync(string? keyword,
            int pageIndex = 1, int pageSize = 20);
        Task<ProductAttributeFormViewModel> GetAttributeFormAsync(int? attributeId = null);
        Task<(bool Success, string? ErrorMessage)> CreateAttributeAsync(ProductAttributeFormViewModel model);
        Task<(bool Success, string? ErrorMessage)> UpdateAttributeAsync(ProductAttributeFormViewModel model);
        Task<(bool Success, string? ErrorMessage)> ToggleAttributeActiveAsync(int attributeId);

        // Attribute Values CRUD
        Task<(bool Success, string? ErrorMessage)> AddValueAsync(AttributeValueFormViewModel model);
        Task<(bool Success, string? ErrorMessage)> UpdateValueAsync(AttributeValueFormViewModel model);
        Task<(bool Success, string? ErrorMessage)> ToggleValueActiveAsync(int valueId);

        // SKU Config per category
        Task<SkuConfigFormViewModel> GetSkuConfigAsync(int categoryId);
        Task<(bool Success, string? ErrorMessage)> SaveSkuConfigAsync(int categoryId, List<SkuConfigViewModel> configs);

        // For Product form: get attribute fields by category
        Task<List<SkuAttributeFormItem>> GetAttributesByCategoryAsync(int categoryId, int? productId = null);

        // SKU generation
        Task<string> GenerateSkuAsync(int productId);
    }
}
