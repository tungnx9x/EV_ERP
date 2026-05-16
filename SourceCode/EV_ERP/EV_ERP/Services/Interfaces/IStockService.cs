using EV_ERP.Models.ViewModels.Stock;

namespace EV_ERP.Services.Interfaces
{
    public interface IStockService
    {
        // ── List ─────────────────────────────────────────
        Task<StockTransactionListViewModel> GetListAsync(
            string? keyword, string? type, string? status, int? warehouseId,
            int pageIndex = 1, int pageSize = 20);

        // ── Detail ───────────────────────────────────────
        Task<StockTransactionDetailViewModel?> GetDetailAsync(long transactionId);

        // ── Form ─────────────────────────────────────────
        Task<StockTransactionFormViewModel> GetFormAsync(long? transactionId = null, string? type = null, int? salesOrderId = null);

        // ── Create / Update ──────────────────────────────
        Task<(bool Success, string? ErrorMessage, long? TransactionId)> SaveAsync(
            StockTransactionFormViewModel model, int userId);

        // ── Status transitions ───────────────────────────
        /// <summary>INBOUND DRAFT → CONFIRMED (updates inventory)</summary>
        Task<(bool Success, string? ErrorMessage)> ConfirmInboundAsync(long transactionId, int userId);

        /// <summary>OUTBOUND DRAFT → DELIVERING (reserves/decreases inventory)</summary>
        Task<(bool Success, string? ErrorMessage)> StartDeliveryAsync(long transactionId, int userId);

        /// <summary>OUTBOUND DELIVERING → DELIVERED</summary>
        Task<(bool Success, string? ErrorMessage)> ConfirmDeliveredAsync(
            long transactionId, DeliveryConfirmModel model, int userId);

        /// <summary>DRAFT → CANCELLED</summary>
        Task<(bool Success, string? ErrorMessage)> CancelAsync(long transactionId, int userId);

        // ── v2.2: Create batch INBOUND/OUTBOUND linked to specific SO lines ─
        Task<(bool Success, string? ErrorMessage, long? TransactionId)>
            CreateBatchForSalesOrderAsync(
                int salesOrderId,
                string transactionType,
                List<(int SOItemId, decimal Quantity)> batchItems,
                int? warehouseId,
                DateTime? transactionDate,
                string? notes,
                StockBatchDeliveryOptions? delivery,
                int userId);

        // ── Barcode lookup ───────────────────────────────
        Task<BarcodeLookupResult?> LookupBarcodeAsync(string barcode, int? warehouseId = null);

        // ── Locations for a warehouse ────────────────────
        Task<List<LocationOptionViewModel>> GetLocationOptionsAsync(int warehouseId);

        // ── Sales Order lookup (for INBOUND / OUTBOUND linking) ───
        // type = "INBOUND" → lines with RemainingReceiveQty > 0
        // type = "OUTBOUND" → lines with InStockQty (ReceivedQty - DeliveredQty) > 0
        Task<object?> LookupSalesOrderAsync(string? soCode, string? type = null);

        // ── Export Delivery Receipt (BBGN) ──────────────
        Task<(byte[] FileBytes, string FileName)?> ExportDeliveryReceiptAsync(long transactionId);
    }
}
