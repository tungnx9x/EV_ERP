using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Finance;
using EV_ERP.Models.Entities.Inventory;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Reference;
using EV_ERP.Models.Entities.Templates;

namespace EV_ERP.Models.Entities.Sales;

// ─── RFQ (Yêu cầu báo giá) ──────────────────────────
public class RFQ
{
    public int RfqId { get; set; }
    public string RfqNo { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int? ContactId { get; set; }
    public DateTime RequestDate { get; set; } = DateTime.Today;
    public DateTime Deadline { get; set; }
    public string? Description { get; set; }
    /// <summary>INPROGRESS → COMPLETED | CANCELLED</summary>
    public string Status { get; set; } = "INPROGRESS";
    public int? AssignedTo { get; set; }
    /// <summary>LOW, NORMAL, HIGH, URGENT</summary>
    public string Priority { get; set; } = "NORMAL";
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public int CreatedBy { get; set; }

    public virtual Customer Customer { get; set; } = null!;
    public virtual CustomerContact? Contact { get; set; }
    public virtual User? AssignedToUser { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
    public virtual ICollection<Quotation> Quotations { get; set; } = [];
    public virtual ICollection<SalesOrder> SalesOrders { get; set; } = [];
}

// ─── QUOTATION (Báo giá) ────────────────────────────
public class Quotation : AuditableEntity
{
    public int QuotationId { get; set; }
    public string QuotationNo { get; set; } = string.Empty;
    public int? RfqId { get; set; }
    public int CustomerId { get; set; }
    public int? ContactId { get; set; }
    public DateTime QuotationDate { get; set; } = DateTime.Today;
    public DateTime? ExpiryDate { get; set; }
    public DateTime Deadline { get; set; }
    /// <summary>DRAFT → SENT → APPROVED / REJECTED / AMEND / EXPIRED</summary>
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
    public int? AmendFromId { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectReason { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public virtual RFQ? Rfq { get; set; }
    public virtual Customer Customer { get; set; } = null!;
    public virtual CustomerContact? Contact { get; set; }
    public virtual User SalesPerson { get; set; } = null!;
    public virtual PdfTemplate? Template { get; set; }
    public virtual Quotation? AmendFrom { get; set; }
    public virtual ICollection<QuotationItem> Items { get; set; } = [];
    public virtual ICollection<QuotationEmailHistory> EmailHistories { get; set; } = [];
    public virtual SalesOrder? SalesOrder { get; set; }
}

// ─── QUOTATION ITEM ──────────────────────────────────
public class QuotationItem
{
    public int QuotationItemId { get; set; }
    public int QuotationId { get; set; }
    /// <summary>NULL = sản phẩm chưa có trong hệ thống (nhập tay)</summary>
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;     // Snapshot
    public string? ProductDescription { get; set; }
    public string? ImageUrl { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? ShippingFee { get; set; }
    public decimal? Coefficient { get; set; } = 1;
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal? TaxRate { get; set; } = 10;
    public decimal? TaxAmount { get; set; }
    public decimal? LineTotalWithTax { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceName { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    /// <summary>0 = chưa gắn SP, 1 = đã gắn Product</summary>
    public bool IsProductMapped { get; set; }
    // v2.1 — Currency của giá nhập + ảnh KH yêu cầu (so sánh với SP NV mua)
    public string? PurchaseCurrency { get; set; }
    public decimal? PurchaseExchangeRate { get; set; } = 1;
    public string? RequiredImageUrl { get; set; }

    // v2.4 — Popup máy tính Giá nhập: chế độ Official/Unofficial + breakdown
    public string PurchaseMode { get; set; } = "OFFICIAL";
    public decimal? PurchaseQuantity { get; set; }
    public decimal? BasePrice { get; set; }
    public decimal? PurchaseTax { get; set; }
    public decimal? InspectionFee { get; set; }
    public decimal? BankingFee { get; set; }
    public decimal? OtherCosts { get; set; }
    public decimal? OfficialShipping { get; set; }
    public decimal? UnofficialDomesticShipping { get; set; }
    public decimal? UnofficialWeightKg { get; set; }
    public decimal? UnofficialCostPerKg { get; set; }
    public decimal? UnofficialHandCarryFee { get; set; }
    public decimal? UnofficialW2WShipping { get; set; }

    public virtual Quotation Quotation { get; set; } = null!;
    public virtual Product? Product { get; set; }
    public virtual Currency? PurchaseCurrencyRef { get; set; }
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

// ─── SALES ORDER (Đơn bán hàng — gộp luồng mua hàng v1.3) ─
public class SalesOrder : AuditableEntity
{
    public int SalesOrderId { get; set; }
    public string SalesOrderNo { get; set; } = string.Empty;
    public int? QuotationId { get; set; }
    public int? RfqId { get; set; }
    public int CustomerId { get; set; }
    public int? ContactId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.Today;
    public DateTime? ExpectedDeliveryDate { get; set; }
    /// <summary>DRAFT → WAIT → BUYING → RECEIVED → DELIVERING → DELIVERED → COMPLETED|RETURNED → REPORTED / CANCELLED</summary>
    public string Status { get; set; } = "DRAFT";

    // ── Thông tin PO phía khách sạn ──
    public string? CustomerPoNo { get; set; }
    public string? CustomerPoFile { get; set; }

    // ── Thông tin mua hàng (mua online từ nhiều nguồn) ──
    public string? PurchaseSource { get; set; }
    public DateTime? ExpectedReceiveDate { get; set; }
    public string? BuyingNotes { get; set; }
    public DateTime? BuyingAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    // ── Tạm ứng — [DEPRECATED v2.2] dùng AdvanceRequests + AdvanceRequestItems thay thế ──
    public decimal? AdvanceAmount { get; set; }
    /// <summary>[DEPRECATED v2.2] Dùng AdvanceRequests.Status thay thế.</summary>
    public string? AdvanceStatus { get; set; }
    /// <summary>[DEPRECATED v2.2] Dùng AdvanceRequests.ApprovedAt thay thế.</summary>
    public DateTime? AdvanceApprovedAt { get; set; }
    /// <summary>[DEPRECATED v2.2] Dùng AdvanceRequests.ReceivedAt thay thế.</summary>
    public DateTime? AdvanceReceivedAt { get; set; }

    // ── Giá trị đơn hàng (bán cho KH) ──
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

    // ── Chi phí mua (giá vốn) ──
    public decimal? PurchaseCost { get; set; }
    public string? PurchaseCostCurrency { get; set; }       // v2.1 — currency của tổng chi phí mua

    public string? ShippingAddress { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public int SalesPersonId { get; set; }

    // ── Quyết toán ──
    public decimal? ActualCost { get; set; }
    public string? SettlementNotes { get; set; }

    // ── Dropshipping ──
    public bool IsDropship { get; set; }
    public string? DropshipAddress { get; set; }

    // ── Timestamps ──
    public DateTime? DeliveringAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ReportedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnReason { get; set; }

    public virtual RFQ? Rfq { get; set; }
    public virtual Quotation? Quotation { get; set; }
    public virtual Customer Customer { get; set; } = null!;
    public virtual CustomerContact? Contact { get; set; }
    public virtual User SalesPerson { get; set; } = null!;
    public virtual Currency? PurchaseCostCurrencyRef { get; set; }
    public virtual ICollection<SalesOrderItem> Items { get; set; } = [];
}

// ─── SALES ORDER ITEM (bán + mua gộp v1.3) ──────────
public class SalesOrderItem
{
    public int SOItemId { get; set; }
    public int SalesOrderId { get; set; }
    /// <summary>NULL = sản phẩm chưa có trong hệ thống (nhập tay từ Quotation)</summary>
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductDescription { get; set; }
    public string? ImageUrl { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal DeliveredQty { get; set; }
    // v2.2 — theo dõi tiến độ nhập kho từng dòng
    public decimal ReceivedQty { get; set; }
    // v2.3 — hủy 1 phần / toàn bộ dòng (KHÔNG xóa, giữ snapshot báo giá)
    public decimal CancelledQty { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? CancelledBy { get; set; }
    /// <summary>Computed: (Quantity - CancelledQty) - ReceivedQty</summary>
    public decimal RemainingReceiveQty { get; set; }
    /// <summary>Computed: ReceivedQty - DeliveredQty</summary>
    public decimal InStockQty { get; set; }
    /// <summary>Computed: (Quantity - CancelledQty) - DeliveredQty</summary>
    public decimal RemainingDeliverQty { get; set; }
    // v2.2 — ngày dự kiến riêng cho từng dòng (đợt về khác nhau)
    public DateTime? ExpectedReceiveDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? ShippingFee { get; set; }
    public decimal? Coefficient { get; set; } = 1;
    public string? SourceUrl { get; set; }
    public string? SourceName { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal? LineCost { get; set; }
    public decimal? TaxRate { get; set; } = 10;
    public decimal? TaxAmount { get; set; }
    public decimal? LineTotalWithTax { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    /// <summary>0 = chưa gắn SP, 1 = đã gắn Product</summary>
    public bool IsProductMapped { get; set; }
    public string? PurchaseCurrency { get; set; }       // v2.1 — currency của giá mua thực tế
    public decimal? PurchaseExchangeRate { get; set; } = 1;
    public virtual SalesOrder SalesOrder { get; set; } = null!;
    public virtual Product? Product { get; set; }
    public virtual Currency? PurchaseCurrencyRef { get; set; }
    public virtual ICollection<StockTransactionItem> StockTransactionItems { get; set; } = [];
    public virtual ICollection<AdvanceRequestItem> AdvanceRequestItems { get; set; } = [];
}
