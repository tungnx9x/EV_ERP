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
}
