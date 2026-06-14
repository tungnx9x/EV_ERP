using EV_ERP.Models.ViewModels.SalesOrders;
using Microsoft.AspNetCore.Http;

namespace EV_ERP.Services.Interfaces;

public interface ISalesOrderService
{
    // ── List (paginated) ─────────────────────────────
    Task<SalesOrderListViewModel> GetListAsync(
        string? keyword, string? status, int? customerId, int? salesPersonId,
        int pageIndex = 1, int pageSize = 20);

    // ── Detail ───────────────────────────────────────
    Task<SalesOrderDetailViewModel?> GetDetailAsync(int salesOrderId);

    // ── Lightweight assignee lookup (for authz checks) ───
    Task<int?> GetSalesPersonIdAsync(int salesOrderId);

    /// <summary>Xác thực 1 SOItem có thuộc đúng đơn hàng không (chống gắn/xóa nhầm đơn).</summary>
    Task<bool> ItemBelongsToOrderAsync(int salesOrderId, int soItemId);

    // ── Auto-create from Quotation ───────────────────
    Task<(bool Success, string? ErrorMessage, int? SalesOrderId)> CreateFromQuotationAsync(
        int quotationId, int userId);

    // ── Status transitions ───────────────────────────
    Task<(bool Success, string? ErrorMessage)> SubmitWaitAsync(
        int salesOrderId, SalesOrderDraftModel? model, int userId);

    Task<(bool Success, string? ErrorMessage)> StartBuyingAsync(
        int salesOrderId, SalesOrderBuyingModel model, int userId);

    /// <summary>
    /// v2.2/2.3 — Tự động roll-up trạng thái SO từ tổng SL ReceivedQty / DeliveredQty
    /// của các SOItem. Được gọi từ StockService sau mỗi lần confirm phiếu nhập/xuất.
    /// </summary>
    Task<(bool Success, string? NewStatus)> RollUpStatusAsync(int salesOrderId, int userId);

    /// <summary>Cập nhật thông tin mua hàng của 1 dòng (BUYING+).</summary>
    Task<(bool Success, string? ErrorMessage)> UpdateItemPurchaseInfoAsync(
        int salesOrderId, int soItemId, UpdateItemPurchaseModel model, int userId);

    /// <summary>Hủy 1 phần hoặc toàn bộ SL còn lại của 1 dòng. KHÔNG xóa dòng.</summary>
    Task<(bool Success, string? ErrorMessage)> CancelItemAsync(
        int salesOrderId, int soItemId, CancelItemModel model, int userId);

    /// <summary>Cập nhật "Giá nhập hiện tại" + breakdown cho 1 dòng SO (popup máy tính giá nhập).</summary>
    Task<(bool Success, string? ErrorMessage)> UpdateItemPurchasePriceAsync(
        int salesOrderId, int soItemId, UpdateItemPurchasePriceModel model, int userId);

    /// <summary>Cập nhật Phí vận chuyển (Shipping Cost) cho 1 dòng SO (sửa inline).</summary>
    Task<(bool Success, string? ErrorMessage)> UpdateItemShippingFeeAsync(
        int salesOrderId, int soItemId, decimal? shippingFee, int userId);

    Task<(bool Success, string? ErrorMessage)> ReturnAsync(
        int salesOrderId, SalesOrderReturnModel model, int userId);

    Task<(bool Success, string? ErrorMessage)> CompleteAsync(
        int salesOrderId, SalesOrderCompleteModel model, int userId);

    Task<(bool Success, string? ErrorMessage)> ReportAsync(int salesOrderId, int userId);

    Task<(bool Success, string? ErrorMessage)> CancelAsync(
        int salesOrderId, int userId, string? reason);

    // ── Update draft info (PO KH + file + expected delivery date) ──────────────
    Task<(bool Success, string? ErrorMessage)> UpdateDraftInfoAsync(
        int salesOrderId, string? customerPoNo, IFormFile? customerPoFile,
        DateTime? expectedDeliveryDate, DateTime? customerPoDate, int userId);

    // ── Create product & map to SO item ────────────────
    Task<(bool Success, string? ErrorMessage)> CreateProductAndMapAsync(
        int salesOrderId, int soItemId, QuickProductModel model, int userId);

    // ── Map existing product to SO item ─────────────
    Task<(bool Success, string? ErrorMessage)> MapProductToSOItemAsync(
        int salesOrderId, int soItemId, int productId, int userId);

    // ── Unit options (for quick product create) ──────
    Task<List<UnitOptionVM>> GetUnitOptionsAsync();

    // ── Export ĐNTU Excel ────────────────────────────
    Task<(byte[] FileBytes, string FileName)?> ExportDntuAsync(int salesOrderId, int userId);

    // ── Export ĐNHU (Hoàn ứng) Excel ─────────────────
    Task<(byte[] FileBytes, string FileName)?> ExportDnhuAsync(int salesOrderId, int userId);

    // ── Export ĐNTT (Thanh toán) Excel ───────────────
    Task<(byte[] FileBytes, string FileName)?> ExportDnttAsync(int salesOrderId, int userId);

    // ── Product Import/Export Template ───────────────
    Task<(byte[] FileBytes, string FileName)?> ExportProductTemplateAsync(int salesOrderId);
    Task<(bool Success, string? ErrorMessage, int Created, int Mapped)> ImportProductsAsync(
        int salesOrderId, IFormFile file, int userId);
}
