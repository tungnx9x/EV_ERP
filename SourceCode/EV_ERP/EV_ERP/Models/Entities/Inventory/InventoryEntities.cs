using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Purchases;
using EV_ERP.Models.Entities.Sales;

namespace EV_ERP.Models.Entities.Inventory;

// ─── WAREHOUSE (Kho) ────────────────────────────────
public class Warehouse : ISoftDeletable
{
    public int WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int? ManagerId { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual User? Manager { get; set; }
    public virtual ICollection<WarehouseLocation> Locations { get; set; } = [];
    public virtual ICollection<InventoryRecord> InventoryRecords { get; set; } = [];
}

// ─── WAREHOUSE LOCATION (Vị trí trong kho) ──────────
// Thiết kế phẳng: Zone → Aisle → Rack → Shelf → Bin
// Mỗi vị trí = 1 bản ghi đầy đủ, không cần recursive CTE
public class WarehouseLocation : BaseEntity, ISoftDeletable
{
    public int LocationId { get; set; }
    public int WarehouseId { get; set; }
    public string LocationCode { get; set; } = string.Empty;  // A-01-02-03
    public string LocationName { get; set; } = string.Empty;  // Khu A > Dãy 1 > Kệ 2 > Tầng 3
    public string? Zone { get; set; }           // Khu vực (A, B, Khu Khô, Khu Lạnh...)
    public string? Aisle { get; set; }          // Dãy / Lối đi
    public string? Rack { get; set; }           // Kệ / Giá
    public string? Shelf { get; set; }          // Tầng trên kệ
    public string? Bin { get; set; }            // Ô / Ngăn cụ thể
    public decimal? MaxCapacity { get; set; }   // Sức chứa tối đa
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ICollection<InventoryRecord> InventoryRecords { get; set; } = [];
}

// ─── INVENTORY (Tồn kho realtime theo SP + Kho + Vị trí) ─
public class InventoryRecord
{
    public int InventoryId { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int? LocationId { get; set; }            // Vị trí cụ thể (NULL = chưa xếp)
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    /// <summary>Computed column: QuantityOnHand - QuantityReserved</summary>
    public decimal QuantityAvailable { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;

    public virtual Product Product { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
}

// ─── STOCK TRANSACTION (Phiếu nhập/xuất kho) ────────
public class StockTransaction
{
    public long TransactionId { get; set; }
    public string TransactionNo { get; set; } = string.Empty;
    /// <summary>INBOUND, OUTBOUND, ADJUSTMENT, RETURN</summary>
    public string TransactionType { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public int? SalesOrderId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Today;
    /// <summary>INBOUND: DRAFT→CONFIRMED→CANCELLED | OUTBOUND: DRAFT→DELIVERING→DELIVERED→CANCELLED</summary>
    public string Status { get; set; } = "DRAFT";
    public string? Notes { get; set; }
    public bool IsDropship { get; set; }

    // ── Thông tin giao hàng (chỉ dùng cho OUTBOUND) ──
    public int? DeliveryPersonId { get; set; }
    public string? DeliveryNote { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public string? ReceivedSignatureUrl { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public int? ConfirmedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CreatedBy { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual PurchaseOrder? PurchaseOrder { get; set; }
    public virtual SalesOrder? SalesOrder { get; set; }
    public virtual User? DeliveryPerson { get; set; }
    public virtual User? ConfirmedByUser { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<StockTransactionItem> Items { get; set; } = [];
}

// ─── STOCK TRANSACTION ITEM ──────────────────────────
public class StockTransactionItem
{
    public long TransItemId { get; set; }
    public long TransactionId { get; set; }
    public int ProductId { get; set; }
    public int? LocationId { get; set; }            // Vị trí nhập vào / xuất từ
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public virtual StockTransaction Transaction { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
}

// ─── STOCK CHECK (Kiểm kê) ──────────────────────────
public class StockCheck
{
    public int StockCheckId { get; set; }
    public string StockCheckNo { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public DateTime CheckDate { get; set; } = DateTime.Today;
    public string Status { get; set; } = "DRAFT";
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CreatedBy { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<StockCheckItem> Items { get; set; } = [];
}

// ─── STOCK CHECK ITEM ────────────────────────────────
public class StockCheckItem
{
    public int SCItemId { get; set; }
    public int StockCheckId { get; set; }
    public int ProductId { get; set; }
    public int? LocationId { get; set; }            // Kiểm kê theo vị trí
    public decimal SystemQty { get; set; }
    public decimal? ActualQty { get; set; }
    /// <summary>Computed column: ActualQty - SystemQty</summary>
    public decimal Difference { get; set; }
    public string? Notes { get; set; }

    public virtual StockCheck StockCheck { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
}
