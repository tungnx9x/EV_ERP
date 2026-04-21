using System.ComponentModel.DataAnnotations;

namespace EV_ERP.Models.ViewModels.RFQs;

// ══════════════════════════════════════════════════════
// LIST
// ══════════════════════════════════════════════════════
public class RfqListViewModel
{
    public Models.Common.PagedResult<RfqRowViewModel> Paged { get; set; } = new();
    public string? SearchKeyword { get; set; }
    public string? FilterStatus { get; set; }
    public string? FilterPriority { get; set; }
    public int? FilterAssignedTo { get; set; }
    public int? FilterCustomerId { get; set; }
    public List<CustomerOption> Customers { get; set; } = [];
    public List<UserOption> Users { get; set; } = [];
}

public class RfqRowViewModel
{
    public int RfqId { get; set; }
    public string RfqNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public DateTime Deadline { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
    public string? Description { get; set; }
    public int QuotationCount { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string StatusBadge => Status switch
    {
        "INPROGRESS" => "info",
        "COMPLETED" => "success",
        "CANCELLED" => "secondary",
        _ => "secondary"
    };
    public string StatusText => Status switch
    {
        "INPROGRESS" => "Đang xử lý",
        "COMPLETED" => "Hoàn tất",
        "CANCELLED" => "Đã hủy",
        _ => Status
    };
    public string PriorityBadge => Priority switch
    {
        "URGENT" => "danger",
        "HIGH" => "warning",
        "NORMAL" => "primary",
        "LOW" => "secondary",
        _ => "secondary"
    };
    public string PriorityText => Priority switch
    {
        "URGENT" => "Khẩn cấp",
        "HIGH" => "Cao",
        "NORMAL" => "Bình thường",
        "LOW" => "Thấp",
        _ => Priority
    };
}

// ══════════════════════════════════════════════════════
// FORM (Create / Edit)
// ══════════════════════════════════════════════════════
public class RfqFormViewModel
{
    public int? RfqId { get; set; }
    public string RfqNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Khách hàng là bắt buộc")]
    public int CustomerId { get; set; }

    public int? ContactId { get; set; }

    public DateTime RequestDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Hạn xử lý là bắt buộc")]
    public DateTime Deadline { get; set; } = DateTime.Today.AddDays(7);

    public string? Description { get; set; }

    public string Priority { get; set; } = "NORMAL";

    public int? AssignedTo { get; set; }

    public string? Notes { get; set; }

    public string? CurrentStatus { get; set; }

    // ── Dropdown data ────────────────────────────────
    public List<CustomerOption> Customers { get; set; } = [];
    public List<UserOption> Users { get; set; } = [];

    public bool IsEditMode => RfqId.HasValue && RfqId > 0;
}

// ══════════════════════════════════════════════════════
// DETAIL
// ══════════════════════════════════════════════════════
public class RfqDetailViewModel
{
    public int RfqId { get; set; }
    public string RfqNo { get; set; } = string.Empty;

    // Customer
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }

    public DateTime RequestDate { get; set; }
    public DateTime Deadline { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Linked quotations
    public List<RfqQuotationRow> Quotations { get; set; } = [];

    public string StatusBadge => Status switch
    {
        "INPROGRESS" => "info",
        "COMPLETED" => "success",
        "CANCELLED" => "secondary",
        _ => "secondary"
    };
    public string StatusText => Status switch
    {
        "INPROGRESS" => "Đang xử lý",
        "COMPLETED" => "Hoàn tất",
        "CANCELLED" => "Đã hủy",
        _ => Status
    };
    public string PriorityBadge => Priority switch
    {
        "URGENT" => "danger",
        "HIGH" => "warning",
        "NORMAL" => "primary",
        "LOW" => "secondary",
        _ => "secondary"
    };
    public string PriorityText => Priority switch
    {
        "URGENT" => "Khẩn cấp",
        "HIGH" => "Cao",
        "NORMAL" => "Bình thường",
        "LOW" => "Thấp",
        _ => Priority
    };
}

public class RfqQuotationRow
{
    public int QuotationId { get; set; }
    public string QuotationNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime QuotationDate { get; set; }

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
// SHARED OPTIONS
// ══════════════════════════════════════════════════════
public class CustomerOption
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
}

public class UserOption
{
    public int UserId { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
