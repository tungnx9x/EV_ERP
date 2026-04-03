using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Templates;

namespace EV_ERP.Models.Entities.Sales;

// ─── QUOTATION (Báo giá) ────────────────────────────
public class Quotation : AuditableEntity
{
    public int QuotationId { get; set; }
    public string QuotationNo { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int? ContactId { get; set; }
    public DateTime QuotationDate { get; set; } = DateTime.Today;
    public DateTime? ExpiryDate { get; set; }
    /// <summary>DRAFT → SENT → CONFIRMED → CONVERTED → CANCELLED</summary>
    public string Status { get; set; } = "DRAFT";
    public decimal SubTotal { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; } = 10;
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public string? PaymentTerms { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public int? TemplateId { get; set; }
    public int SalesPersonId { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public virtual Customer Customer { get; set; } = null!;
    public virtual CustomerContact? Contact { get; set; }
    public virtual User SalesPerson { get; set; } = null!;
    public virtual PdfTemplate? Template { get; set; }
    public virtual ICollection<QuotationItem> Items { get; set; } = [];
    public virtual ICollection<QuotationEmailHistory> EmailHistories { get; set; } = [];
    public virtual SalesOrder? SalesOrder { get; set; }
}

// ─── QUOTATION ITEM ──────────────────────────────────
public class QuotationItem
{
    public int QuotationItemId { get; set; }
    public int QuotationId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;     // Snapshot
    public string UnitName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }

    public virtual Quotation Quotation { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}

// ─── QUOTATION EMAIL HISTORY ─────────────────────────
public class QuotationEmailHistory
{
    public int EmailHistoryId { get; set; }
    public int QuotationId { get; set; }
    public string SentTo { get; set; } = string.Empty;
    public string? SentCc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? PdfFileUrl { get; set; }
    public DateTime SentAt { get; set; } = DateTime.Now;
    public int SentBy { get; set; }
    public string Status { get; set; } = "SENT";
    public string? ErrorMessage { get; set; }

    public virtual Quotation Quotation { get; set; } = null!;
    public virtual User SentByUser { get; set; } = null!;
}

// ─── SALES ORDER (Đơn bán hàng) ─────────────────────
public class SalesOrder : AuditableEntity
{
    public int SalesOrderId { get; set; }
    public string SalesOrderNo { get; set; } = string.Empty;
    public int? QuotationId { get; set; }
    public int CustomerId { get; set; }
    public int? ContactId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.Today;
    public DateTime? ExpectedDeliveryDate { get; set; }
    /// <summary>CONFIRMED → PROCESSING → PARTIALLY_DELIVERED → DELIVERED → COMPLETED → CANCELLED</summary>
    public string Status { get; set; } = "CONFIRMED";
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
    public string? ShippingAddress { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public int SalesPersonId { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public virtual Quotation? Quotation { get; set; }
    public virtual Customer Customer { get; set; } = null!;
    public virtual CustomerContact? Contact { get; set; }
    public virtual User SalesPerson { get; set; } = null!;
    public virtual ICollection<SalesOrderItem> Items { get; set; } = [];
}

// ─── SALES ORDER ITEM ────────────────────────────────
public class SalesOrderItem
{
    public int SOItemId { get; set; }
    public int SalesOrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal UnitPrice { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }

    public virtual SalesOrder SalesOrder { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
