using System.ComponentModel.DataAnnotations;

namespace EV_ERP.Models.ViewModels.Stock
{
    // ── List ─────────────────────────────────────────────
    public class StockTransactionListViewModel
    {
        public List<StockTransactionRowViewModel> Transactions { get; set; } = [];
        public string? SearchKeyword { get; set; }
        public string? FilterType { get; set; }
        public string? FilterStatus { get; set; }
        public int? FilterWarehouseId { get; set; }
        public int TotalCount { get; set; }
        public List<WarehouseOptionViewModel> Warehouses { get; set; } = [];
    }

    public class StockTransactionRowViewModel
    {
        public long TransactionId { get; set; }
        public string TransactionNo { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string? SalesOrderNo { get; set; }
        public int? SalesOrderId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public decimal TotalQuantity { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }

        public string TypeBadge => TransactionType switch
        {
            "INBOUND" => "primary",
            "OUTBOUND" => "warning",
            "ADJUSTMENT" => "info",
            "RETURN" => "secondary",
            _ => "light"
        };
        public string TypeText => TransactionType switch
        {
            "INBOUND" => "Nhập kho",
            "OUTBOUND" => "Xuất kho",
            "ADJUSTMENT" => "Điều chỉnh",
            "RETURN" => "Trả hàng",
            _ => TransactionType
        };
        public string StatusBadge => Status switch
        {
            "DRAFT" => "secondary",
            "CONFIRMED" => "success",
            "DELIVERING" => "info",
            "DELIVERED" => "success",
            "CANCELLED" => "danger",
            _ => "light"
        };
        public string StatusText => Status switch
        {
            "DRAFT" => "Nháp",
            "CONFIRMED" => "Đã xác nhận",
            "DELIVERING" => "Đang giao",
            "DELIVERED" => "Đã giao",
            "CANCELLED" => "Đã hủy",
            _ => Status
        };
    }

    // ── Detail ───────────────────────────────────────────
    public class StockTransactionDetailViewModel
    {
        public long TransactionId { get; set; }
        public string TransactionNo { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string WarehouseCode { get; set; } = string.Empty;
        public int? SalesOrderId { get; set; }
        public string? SalesOrderNo { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public bool IsDropship { get; set; }

        // Delivery info (OUTBOUND)
        public string? DeliveryPersonName { get; set; }
        public int? DeliveryPersonId { get; set; }
        public string? DeliveryNote { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public string? ReceivedSignatureUrl { get; set; }
        public DateTime? DeliveredAt { get; set; }

        // Confirmation
        public DateTime? ConfirmedAt { get; set; }
        public string? ConfirmedByName { get; set; }

        // Audit
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<StockTransactionItemViewModel> Items { get; set; } = [];
        public List<StockAttachmentViewModel> Attachments { get; set; } = [];

        // Status helpers
        public string TypeText => TransactionType switch
        {
            "INBOUND" => "Nhập kho",
            "OUTBOUND" => "Xuất kho",
            "ADJUSTMENT" => "Điều chỉnh",
            "RETURN" => "Trả hàng",
            _ => TransactionType
        };
        public string StatusText => Status switch
        {
            "DRAFT" => "Nháp",
            "CONFIRMED" => "Đã xác nhận",
            "DELIVERING" => "Đang giao",
            "DELIVERED" => "Đã giao",
            "CANCELLED" => "Đã hủy",
            _ => Status
        };
        public bool CanConfirm => TransactionType == "INBOUND" && Status == "DRAFT";
        public bool CanStartDelivery => TransactionType == "OUTBOUND" && Status == "DRAFT";
        public bool CanConfirmDelivered => TransactionType == "OUTBOUND" && Status == "DELIVERING";
        public bool CanCancel => Status == "DRAFT";
        public bool IsEditable => Status == "DRAFT";
    }

    public class StockTransactionItemViewModel
    {
        public long TransItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? ImageUrl { get; set; }
        public string? LocationCode { get; set; }
        public string? LocationName { get; set; }
        public int? LocationId { get; set; }
        public decimal Quantity { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    // ── Create / Edit Form ───────────────────────────────
    public class StockTransactionFormViewModel
    {
        public long? TransactionId { get; set; }

        [Required(ErrorMessage = "Loại phiếu là bắt buộc")]
        public string TransactionType { get; set; } = "INBOUND";

        [Required(ErrorMessage = "Kho là bắt buộc")]
        public int WarehouseId { get; set; }

        public int? SalesOrderId { get; set; }
        public string? SalesOrderNo { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Today;
        public string? Notes { get; set; }
        public bool IsDropship { get; set; }

        // Delivery (OUTBOUND)
        public int? DeliveryPersonId { get; set; }
        public string? DeliveryNote { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }

        public List<StockTransactionItemFormModel> Items { get; set; } = [];
        public List<StockAttachmentViewModel> Attachments { get; set; } = [];
        public List<int> AttachmentIds { get; set; } = [];

        public bool IsEditMode => TransactionId.HasValue && TransactionId > 0;

        // Dropdown options
        public List<WarehouseOptionViewModel> Warehouses { get; set; } = [];
        public List<LocationOptionViewModel> Locations { get; set; } = [];
        public List<UserOptionViewModel> DeliveryPersons { get; set; } = [];
    }

    public class StockTransactionItemFormModel
    {
        public long? TransItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? ImageUrl { get; set; }
        public int? LocationId { get; set; }
        public decimal Quantity { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    // ── Delivery confirmation model ─────────────────────
    public class DeliveryConfirmModel
    {
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public string? DeliveryNote { get; set; }
    }

    // ── Barcode lookup result ────────────────────────────
    public class BarcodeLookupResult
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? ImageUrl { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal? DefaultPurchasePrice { get; set; }
        public decimal? DefaultSalePrice { get; set; }
        public decimal QuantityOnHand { get; set; }
    }

    // ── Location option ──────────────────────────────────
    public class LocationOptionViewModel
    {
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
    }

    // ── Attachment (image gallery) ──────────────────────
    public class StockAttachmentViewModel
    {
        public int AttachmentId { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedByName { get; set; } = string.Empty;
    }
}
