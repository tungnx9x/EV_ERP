using EV_ERP.Models.ViewModels.Stock;

namespace EV_ERP.Services.Interfaces
{
    public interface IInventoryService
    {
        Task<InventoryListViewModel> GetListAsync(string? keyword, int? warehouseId, string? status);
        Task<ProductInventoryDetailViewModel?> GetProductDetailAsync(int productId);

        /// <summary>Barcode lookup: search by barcode or product code, return inventory info</summary>
        Task<BarcodeLookupResult?> QuickLookupAsync(string barcode);
    }
}
