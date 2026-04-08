using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.Entities.Vendors;

namespace EV_ERP.Models.Entities.Purchases;

// ─── PURCHASE ORDER (Lệnh mua hàng) ─────────────────
public class PurchaseOrder : AuditableEntity
{
    public int PurchaseOrderId { get; set; }
    public string PurchaseOrderNo { get; set; } = string.Empty;
    public int? VendorId { get; set; }               // NULL khi chưa chọn NCC
    public int? SalesOrderId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.Today;
    public DateTime? ExpectedReceiveDate { get; set; } // Dự kiến hàng về kho
    public DateTime? ExpectedDeliveryDate { get; set; }
    /// <summary>DRAFT → BUYING → PARTIALLY_RECEIVED → RECEIVED → CANCELLED</summary>
    public string Status { get; set; } = "DRAFT";
    public bool IsDropship { get; set; }
    public string? DropshipAddress { get; set; }
    public int? DropshipCustomerId { get; set; }
    public decimal SubTotal { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; } = 10;
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public int PaymentTermDays { get; set; } = 30;
    public DateTime? PaymentDueDate { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public DateTime? BuyingAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public virtual Vendor? Vendor { get; set; }
    public virtual SalesOrder? SalesOrder { get; set; }
    public virtual Customer? DropshipCustomer { get; set; }
    public virtual ICollection<PurchaseOrderItem> Items { get; set; } = [];
    public virtual ICollection<VendorInvoice> Invoices { get; set; } = [];
}

// ─── PURCHASE ORDER ITEM ─────────────────────────────
public class PurchaseOrderItem
{
    public int POItemId { get; set; }
    public int PurchaseOrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public int? SOItemId { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
    public virtual SalesOrderItem? SalesOrderItem { get; set; }
}

// ─── VENDOR INVOICE (Hóa đơn NCC) ───────────────────
public class VendorInvoice : BaseEntity
{
    public int VendorInvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public int PurchaseOrderId { get; set; }
    public int VendorId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Currency { get; set; } = "VND";
    /// <summary>UNPAID → PARTIALLY_PAID → PAID → CANCELLED</summary>
    public string Status { get; set; } = "UNPAID";
    public string? Notes { get; set; }
    public string? AttachmentUrl { get; set; }
    public int CreatedBy { get; set; }

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual Vendor Vendor { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
}
