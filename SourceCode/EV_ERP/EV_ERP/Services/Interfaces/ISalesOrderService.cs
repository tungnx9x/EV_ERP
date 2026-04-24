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

    // ── Auto-create from Quotation ───────────────────
    Task<(bool Success, string? ErrorMessage, int? SalesOrderId)> CreateFromQuotationAsync(
        int quotationId, int userId);

    // ── Status transitions ───────────────────────────
    Task<(bool Success, string? ErrorMessage)> SubmitWaitAsync(
        int salesOrderId, SalesOrderDraftModel model, int userId);

    Task<(bool Success, string? ErrorMessage)> StartBuyingAsync(
        int salesOrderId, SalesOrderBuyingModel model, int userId);

    Task<(bool Success, string? ErrorMessage)> ConfirmReceivedAsync(int salesOrderId, int userId);
    Task<(bool Success, string? ErrorMessage)> StartDeliveringAsync(int salesOrderId, int userId);
    Task<(bool Success, string? ErrorMessage)> ConfirmDeliveredAsync(int salesOrderId, int userId);

    Task<(bool Success, string? ErrorMessage)> ReturnAsync(
        int salesOrderId, SalesOrderReturnModel model, int userId);

    Task<(bool Success, string? ErrorMessage)> CompleteAsync(
        int salesOrderId, SalesOrderCompleteModel model, int userId);

    Task<(bool Success, string? ErrorMessage)> ReportAsync(int salesOrderId, int userId);

    Task<(bool Success, string? ErrorMessage)> CancelAsync(
        int salesOrderId, int userId, string? reason);

    // ── Update draft info (PO KH + file) ──────────────
    Task<(bool Success, string? ErrorMessage)> UpdateDraftInfoAsync(
        int salesOrderId, string? customerPoNo, IFormFile? customerPoFile, int userId);

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
}
