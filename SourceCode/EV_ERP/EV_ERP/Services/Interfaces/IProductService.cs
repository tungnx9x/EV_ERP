using EV_ERP.Models.ViewModels.Products;
using Microsoft.AspNetCore.Http;

namespace EV_ERP.Services.Interfaces
{
    public interface IProductService
    {
        Task<ProductListViewModel> GetListAsync(string? keyword, int? categoryId, string? status,
            int pageIndex = 1, int pageSize = 20);
        Task<ProductFormViewModel> GetFormAsync(int? productId = null);
        Task<(bool Success, string? ErrorMessage, int? ProductId)> CreateAsync(ProductFormViewModel model, int createdBy);
        Task<(bool Success, string? ErrorMessage)> UpdateAsync(ProductFormViewModel model, int updatedBy);
        Task<(bool Success, string? ErrorMessage)> ToggleActiveAsync(int productId, int updatedBy);
        Task<ProductDetailViewModel?> GetDetailAsync(int productId);

        // Duplicate check
        Task<(bool HasDuplicate, string? ProductCode, string? ProductName, int? ProductId)>
            CheckDuplicateAsync(int categoryId, Dictionary<int, int?> attributeValues, int? excludeProductId = null);

        // Barcode
        Task<(bool Success, string? ErrorMessage, GenerateBarcodeResult? Result)> GenerateBarcodeAsync(
            int productId, int updatedBy);
        Task<(bool Success, string? ErrorMessage, int Count)> GenerateBarcodesForAllAsync(int updatedBy);

        // Product images (gallery)
        Task<(bool Success, string? ErrorMessage, List<ProductImageViewModel> Added)> AddImagesAsync(
            int productId, IList<IFormFile> files);
        Task<(bool Success, string? ErrorMessage)> DeleteImageAsync(int imageId, int productId);
        Task<(bool Success, string? ErrorMessage)> SetAvatarAsync(int imageId, int productId, int updatedBy);

        // Copy an existing /uploads/... image into the product gallery (used when creating a product
        // from a Sales Order line that already has a snapshot image from the quotation).
        Task<bool> AddImageFromExistingUrlAsync(int productId, string sourceImageUrl, bool setAsPrimary);
    }
}
