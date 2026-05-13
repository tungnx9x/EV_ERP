namespace EV_ERP.Models.ViewModels.Stock
{
    // ── Inventory List ───────────────────────────────────
    public class InventoryListViewModel
    {
        public List<InventoryRowViewModel> Records { get; set; } = [];
        public string? SearchKeyword { get; set; }
        public int? FilterWarehouseId { get; set; }
        public string? FilterStatus { get; set; }
        public int TotalProducts { get; set; }
        public decimal TotalQuantity { get; set; }
        public List<WarehouseOptionViewModel> Warehouses { get; set; } = [];
    }

    public class InventoryRowViewModel
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? ImageUrl { get; set; }
        public string? CategoryName { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal QuantityOnHand { get; set; }
        public decimal QuantityReserved { get; set; }
        public decimal QuantityAvailable { get; set; }
        public int MinStockLevel { get; set; }

        // Aggregated from multiple warehouses
        public List<InventoryWarehouseDetail> WarehouseDetails { get; set; } = [];

        public bool IsLowStock => QuantityOnHand <= MinStockLevel && MinStockLevel > 0;
    }

    public class InventoryWarehouseDetail
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string? LocationCode { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal QuantityReserved { get; set; }
    }

    // ── Product Inventory Detail ─────────────────────────
    public class ProductInventoryDetailViewModel
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? ImageUrl { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public int MinStockLevel { get; set; }

        public decimal TotalOnHand { get; set; }
        public decimal TotalReserved { get; set; }
        public decimal TotalAvailable { get; set; }

        public List<InventoryLocationRecord> LocationRecords { get; set; } = [];
        public List<StockTransactionHistoryRow> RecentTransactions { get; set; } = [];
    }

    public class InventoryLocationRecord
    {
        public int InventoryId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string WarehouseCode { get; set; } = string.Empty;
        public string? LocationCode { get; set; }
        public string? LocationName { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal QuantityReserved { get; set; }
        public decimal QuantityAvailable { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    public class StockTransactionHistoryRow
    {
        public long TransactionId { get; set; }
        public string TransactionNo { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }

        public string TypeText => TransactionType switch
        {
            "INBOUND" => "Nhập",
            "OUTBOUND" => "Xuất",
            "ADJUSTMENT" => "Điều chỉnh",
            "RETURN" => "Trả",
            _ => TransactionType
        };
    }

    // ── SO Inventory Lookup (track receive progress) ─────
    public class SalesOrderInventoryLookupResult
    {
        public int SalesOrderId { get; set; }
        public string SalesOrderNo { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedReceiveDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;

        public int TotalLines { get; set; }
        public int FullyReceivedLines { get; set; }
        public int PartialLines { get; set; }
        public int NotStartedLines { get; set; }
        public int CancelledLines { get; set; }

        public decimal TotalOrderedQty { get; set; }
        public decimal TotalReceivedQty { get; set; }
        public decimal TotalRemainingQty { get; set; }
        public decimal TotalCancelledQty { get; set; }

        public bool IsFullyReceived => TotalLines > 0 && FullyReceivedLines + CancelledLines >= TotalLines;

        public List<SalesOrderInventoryLineRow> Lines { get; set; } = [];
    }

    public class SalesOrderInventoryLineRow
    {
        public int SOItemId { get; set; }
        public int? ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductCode { get; set; }
        public string? Barcode { get; set; }
        public string? ImageUrl { get; set; }
        public string UnitName { get; set; } = string.Empty;

        public decimal Quantity { get; set; }
        public decimal ReceivedQty { get; set; }
        public decimal DeliveredQty { get; set; }
        public decimal CancelledQty { get; set; }
        public decimal RemainingReceiveQty { get; set; }
        public decimal InStockQty { get; set; }
        public DateTime? ExpectedReceiveDate { get; set; }

        public decimal EffectiveQty => Quantity - CancelledQty;

        /// <summary>NOT_STARTED | PARTIAL | RECEIVED | CANCELLED</summary>
        public string LineStatus
        {
            get
            {
                if (CancelledQty >= Quantity && Quantity > 0) return "CANCELLED";
                var eff = EffectiveQty;
                if (eff <= 0) return "CANCELLED";
                if (ReceivedQty <= 0) return "NOT_STARTED";
                if (ReceivedQty >= eff) return "RECEIVED";
                return "PARTIAL";
            }
        }

        public string LineStatusText => LineStatus switch
        {
            "RECEIVED" => "Đã nhận đủ",
            "PARTIAL" => "Nhận một phần",
            "NOT_STARTED" => "Chưa nhận",
            "CANCELLED" => "Đã hủy",
            _ => LineStatus
        };

        public string LineStatusCss => LineStatus switch
        {
            "RECEIVED" => "bg-success",
            "PARTIAL" => "bg-warning text-dark",
            "NOT_STARTED" => "bg-secondary",
            "CANCELLED" => "bg-dark",
            _ => "bg-light text-dark"
        };
    }
}
