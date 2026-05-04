using EV_ERP.Models.Common;

namespace EV_ERP.Models.ViewModels.SalesOrders;

// ══════════════════════════════════════════════════════
// LIST
// ══════════════════════════════════════════════════════
public class SalesOrderListViewModel
{
    public PagedResult<SalesOrderRowViewModel> Paged { get; set; } = new();
    public string? SearchKeyword { get; set; }
    public string? FilterStatus { get; set; }
    public int? FilterCustomerId { get; set; }
    public int? FilterSalesPersonId { get; set; }
    public List<CustomerOptionVM> Customers { get; set; } = [];
    public List<SalesPersonOptionVM> SalesPersons { get; set; } = [];
}

public class SalesOrderRowViewModel
{
    public int SalesOrderId { get; set; }
    public string SalesOrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public string? PurchaseSource { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal? PurchaseCost { get; set; }
    public string Currency { get; set; } = "VND";
    public int ItemCount { get; set; }
    public string? CustomerPoNo { get; set; }

    public string StatusBadge => Status switch
    {
        "DRAFT" => "secondary",
        "WAIT" => "warning",
        "BUYING" => "info",
        "RECEIVED" => "primary",
        "DELIVERING" => "info",
        "DELIVERED" => "success",
        "COMPLETED" => "success",
        "RETURNED" => "warning",
        "REPORTED" => "primary",
        "CANCELLED" => "danger",
        _ => "secondary"
    };
    public string StatusText => Status switch
    {
        "DRAFT" => "Nháp",
        "WAIT" => "Chờ tạm ứng",
        "BUYING" => "Đang mua",
        "RECEIVED" => "Đã nhận hàng",
        "DELIVERING" => "Đang giao",
        "DELIVERED" => "Đã giao",
        "COMPLETED" => "Hoàn tất",
        "RETURNED" => "Trả hàng",
        "REPORTED" => "Đã báo cáo KQKD",
        "CANCELLED" => "Đã hủy",
        _ => Status
    };
}

// ══════════════════════════════════════════════════════
// DETAIL
// ══════════════════════════════════════════════════════
public class SalesOrderDetailViewModel
{
    public int SalesOrderId { get; set; }
    public string SalesOrderNo { get; set; } = string.Empty;

    // Linked entities
    public int? QuotationId { get; set; }
    public string? QuotationNo { get; set; }
    public int? RfqId { get; set; }
    public string? RfqNo { get; set; }

    // Customer
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }

    // Dates
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    // Status
    public string Status { get; set; } = string.Empty;
    public string StatusBadge => Status switch
    {
        "DRAFT" => "secondary",
        "WAIT" => "warning",
        "BUYING" => "info",
        "RECEIVED" => "primary",
        "DELIVERING" => "info",
        "DELIVERED" => "success",
        "COMPLETED" => "success",
        "RETURNED" => "warning",
        "REPORTED" => "primary",
        "CANCELLED" => "danger",
        _ => "secondary"
    };
    public string StatusText => Status switch
    {
        "DRAFT" => "Nháp",
        "WAIT" => "Chờ tạm ứng",
        "BUYING" => "Đang mua",
        "RECEIVED" => "Đã nhận hàng",
        "DELIVERING" => "Đang giao",
        "DELIVERED" => "Đã giao",
        "COMPLETED" => "Hoàn tất",
        "RETURNED" => "Trả hàng",
        "REPORTED" => "Đã báo cáo KQKD",
        "CANCELLED" => "Đã hủy",
        _ => Status
    };

    // Customer PO
    public string? CustomerPoNo { get; set; }
    public string? CustomerPoFile { get; set; }

    // Purchasing (mua online từ nhiều nguồn)
    public string? PurchaseSource { get; set; }
    public DateTime? ExpectedReceiveDate { get; set; }
    public string? BuyingNotes { get; set; }
    public DateTime? BuyingAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    // Advance
    public decimal? AdvanceAmount { get; set; }
    public string? AdvanceStatus { get; set; }
    public DateTime? AdvanceApprovedAt { get; set; }
    public DateTime? AdvanceReceivedAt { get; set; }

    // Money (bán)
    public decimal SubTotal { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public int PaymentTermDays { get; set; }
    public DateTime? PaymentDueDate { get; set; }

    // Money (mua)
    public decimal? PurchaseCost { get; set; }
    public decimal ProfitAmount => TotalAmount - (PurchaseCost ?? 0);

    // Dropship
    public bool IsDropship { get; set; }
    public string? DropshipAddress { get; set; }

    // Notes
    public string? ShippingAddress { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public int SalesPersonId { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;

    // Settlement
    public decimal? ActualCost { get; set; }
    public string? SettlementNotes { get; set; }

    // Timestamps
    public DateTime? DeliveringAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ReportedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Items
    public List<SalesOrderItemDetailViewModel> Items { get; set; } = [];

    // Computed: any item not yet mapped to a real Product
    public bool HasUnmappedProducts => Items.Any(i => !i.IsProductMapped);
    public int UnmappedProductCount => Items.Count(i => !i.IsProductMapped);
}

public class SalesOrderItemDetailViewModel
{
    public int SOItemId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductDescription { get; set; }
    public string? ImageUrl { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal DeliveredQty { get; set; }
    // Bán
    public decimal UnitPrice { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    // Mua
    public decimal? PurchasePrice { get; set; }
    public decimal? LineCost { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceName { get; set; }
    public string? Notes { get; set; }
    public bool IsProductMapped { get; set; }
}

// ══════════════════════════════════════════════════════
// FORM MODELS (for status transition POST actions)
// ══════════════════════════════════════════════════════
public class SalesOrderDraftModel
{
    public string? CustomerPoNo { get; set; }
    public decimal? AdvanceAmount { get; set; }
}

public class SalesOrderBuyingModel
{
    public string? PurchaseSource { get; set; }
    public DateTime? ExpectedReceiveDate { get; set; }
    public string? BuyingNotes { get; set; }
    public List<SalesOrderItemPurchaseModel> Items { get; set; } = [];
}

public class SalesOrderItemPurchaseModel
{
    public int SOItemId { get; set; }
    public decimal PurchasePrice { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceName { get; set; }
}

public class SalesOrderCompleteModel
{
    public decimal? ActualCost { get; set; }
    public string? SettlementNotes { get; set; }
}

public class SalesOrderReturnModel
{
    public string? ReturnReason { get; set; }
}

public class QuickProductModel
{
    public int SOItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UnitId { get; set; }
    public decimal? DefaultSalePrice { get; set; }
    public decimal? DefaultPurchasePrice { get; set; }
    public string? SourceUrl { get; set; }
}

public class UnitOptionVM
{
    public int UnitId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
}

// ══════════════════════════════════════════════════════
// SHARED OPTIONS
// ══════════════════════════════════════════════════════
public class CustomerOptionVM
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
}

public class SalesPersonOptionVM
{
    public int UserId { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

