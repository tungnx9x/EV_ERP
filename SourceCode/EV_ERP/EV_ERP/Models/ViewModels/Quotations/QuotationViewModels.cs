using System.ComponentModel.DataAnnotations;
using EV_ERP.Models.Common;

namespace EV_ERP.Models.ViewModels.Quotations;

// ══════════════════════════════════════════════════════
// LIST
// ══════════════════════════════════════════════════════
public class QuotationListViewModel
{
    public PagedResult<QuotationRowViewModel> Paged { get; set; } = new();
    public string? SearchKeyword { get; set; }
    public string? FilterStatus { get; set; }
    public int? FilterCustomerId { get; set; }
    public int? FilterSalesPersonId { get; set; }
    public List<CustomerOptionViewModel> Customers { get; set; } = [];
    public List<SalesPersonOptionViewModel> SalesPersons { get; set; } = [];
}

public class QuotationRowViewModel
{
    public int QuotationId { get; set; }
    public string QuotationNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public string SalesPersonName { get; set; } = string.Empty;
    public DateTime QuotationDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public int ItemCount { get; set; }

    public string StatusBadge => Status switch
    {
        "DRAFT" => "secondary",
        "SENT" => "info",
        "APPROVED" => "success",
        "REJECTED" => "danger",
        "AMEND" => "warning",
        "EXPIRED" => "dark",
        _ => "secondary"
    };
    public string StatusText => Status switch
    {
        "DRAFT" => "Nháp",
        "SENT" => "Đã gửi",
        "APPROVED" => "Đã duyệt",
        "REJECTED" => "Từ chối",
        "AMEND" => "Chỉnh sửa",
        "EXPIRED" => "Hết hạn",
        _ => Status
    };
}

// ══════════════════════════════════════════════════════
// FORM  (Create / Edit)
// ══════════════════════════════════════════════════════
public class QuotationFormViewModel
{
    public int? QuotationId { get; set; }
    public string QuotationNo { get; set; } = string.Empty;
    public int? RfqId { get; set; }

    [Required(ErrorMessage = "Khách hàng là bắt buộc")]
    [Display(Name = "Khách hàng")]
    public int CustomerId { get; set; }

    [Display(Name = "Người liên hệ")]
    public int? ContactId { get; set; }

    [Required(ErrorMessage = "Ngày báo giá là bắt buộc")]
    [Display(Name = "Ngày báo giá")]
    public DateTime QuotationDate { get; set; } = DateTime.Today;

    [Display(Name = "Ngày hết hạn")]
    public DateTime? ExpiryDate { get; set; }

    [Display(Name = "Nhân viên phụ trách")]
    public int SalesPersonId { get; set; }

    [Display(Name = "Điều khoản thanh toán")]
    public string? PaymentTerms { get; set; }

    [Display(Name = "Thuế VAT (%)")]
    public decimal TaxRate { get; set; } = 10;

    [Display(Name = "Chiết khấu loại")]
    public string? DiscountType { get; set; }

    [Display(Name = "Chiết khấu giá trị")]
    public decimal? DiscountValue { get; set; }

    [Display(Name = "Ghi chú")]
    public string? Notes { get; set; }

    [Display(Name = "Ghi chú nội bộ")]
    public string? InternalNotes { get; set; }

    public int? TemplateId { get; set; }

    [Display(Name = "Hệ số")]
    public decimal Coefficient { get; set; } = 1;

    // ── Items ────────────────────────────────────────
    public List<QuotationItemFormModel> Items { get; set; } = [];

    // ── Dropdown data ────────────────────────────────
    public List<CustomerOptionViewModel> Customers { get; set; } = [];
    public List<SalesPersonOptionViewModel> SalesPersons { get; set; } = [];

    // Helpers
    public bool IsEditMode => QuotationId.HasValue && QuotationId > 0;
    public string? CurrentStatus { get; set; }
}

public class QuotationItemFormModel
{
    public int? QuotationItemId { get; set; }

    [Required(ErrorMessage = "Sản phẩm là bắt buộc")]
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string? Proposal { get; set; }
    public string? ImageUrl { get; set; }
    public string UnitName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số lượng là bắt buộc")]
    [Range(0.001, double.MaxValue, ErrorMessage = "Số lượng phải > 0")]
    public decimal Quantity { get; set; }

    [Required(ErrorMessage = "Đơn giá là bắt buộc")]
    [Range(0, double.MaxValue, ErrorMessage = "Đơn giá không hợp lệ")]
    public decimal UnitPrice { get; set; }

    public decimal AmountExclVat { get; set; }
    public decimal VatRate { get; set; } = 8;
    public decimal AmountInclVat { get; set; }

    public string? Supplier { get; set; }
    public decimal ImportPrice { get; set; }
    public string Currency { get; set; } = "VND";
    public decimal Shipping { get; set; }
    public decimal Coefficient { get; set; } = 1;

    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

// ══════════════════════════════════════════════════════
// DETAIL
// ══════════════════════════════════════════════════════
public class QuotationDetailViewModel
{
    public int QuotationId { get; set; }
    public string QuotationNo { get; set; } = string.Empty;
    public int? RfqId { get; set; }
    public string? RfqNo { get; set; }

    // Customer
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }

    // Dates
    public DateTime QuotationDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectReason { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    // Status
    public string Status { get; set; } = string.Empty;
    public string StatusBadge => Status switch
    {
        "DRAFT" => "secondary",
        "SENT" => "info",
        "APPROVED" => "success",
        "REJECTED" => "danger",
        "AMEND" => "warning",
        "EXPIRED" => "dark",
        _ => "secondary"
    };
    public string StatusText => Status switch
    {
        "DRAFT" => "Nháp",
        "SENT" => "Đã gửi",
        "APPROVED" => "Đã duyệt",
        "REJECTED" => "Từ chối",
        "AMEND" => "Chỉnh sửa",
        "EXPIRED" => "Hết hạn",
        _ => Status
    };

    // Amend
    public int? AmendFromId { get; set; }
    public string? AmendFromNo { get; set; }

    // Sales person
    public string SalesPersonName { get; set; } = string.Empty;

    // Money
    public decimal SubTotal { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "VND";

    // Notes
    public string? PaymentTerms { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedByName { get; set; }

    // Items
    public List<QuotationItemDetailViewModel> Items { get; set; } = [];

    // Linked SO
    public int? SalesOrderId { get; set; }
    public string? SalesOrderNo { get; set; }
}

public class QuotationItemDetailViewModel
{
    public int QuotationItemId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Proposal { get; set; }
    public string? ImageUrl { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal AmountExclVat { get; set; }
    public decimal VatRate { get; set; }
    public decimal AmountInclVat { get; set; }
    public string? Supplier { get; set; }
    public decimal ImportPrice { get; set; }
    public string Currency { get; set; } = "VND";
    public decimal Shipping { get; set; }
    public decimal Coefficient { get; set; }
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
}

// ══════════════════════════════════════════════════════
// SHARED OPTION MODELS
// ══════════════════════════════════════════════════════
public class CustomerOptionViewModel
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
}

public class SalesPersonOptionViewModel
{
    public int UserId { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

// ══════════════════════════════════════════════════════
// EXPORT REQUEST
// ══════════════════════════════════════════════════════
public class QuotationExportRequest
{
    public int CustomerId { get; set; }
    public decimal Coefficient { get; set; } = 1;
    public string? Notes { get; set; }
    public List<QuotationItemFormModel> Items { get; set; } = [];
}

// ── Product search result (for Ajax autocomplete) ────
public class ProductSearchResult
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal? DefaultSalePrice { get; set; }
    public decimal? DefaultPurchasePrice { get; set; }
}
