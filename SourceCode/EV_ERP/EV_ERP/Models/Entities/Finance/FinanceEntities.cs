using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Sales;

namespace EV_ERP.Models.Entities.Finance;

// ─── ADVANCE REQUEST (Tạm ứng / Hoàn ứng) ──────────
public class AdvanceRequest
{
    public int AdvanceRequestId { get; set; }
    public string RequestNo { get; set; } = string.Empty;
    public int SalesOrderId { get; set; }
    public DateTime RequestDate { get; set; } = DateTime.Today;
    public decimal RequestedAmount { get; set; }
    public string Purpose { get; set; } = string.Empty;
    /// <summary>PENDING → APPROVED → RECEIVED → SETTLING → SETTLED → REJECTED</summary>
    public string Status { get; set; } = "PENDING";
    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public DateTime? ReceivedAt { get; set; }
    // ── Quyết toán ──
    public decimal? ActualSpent { get; set; }
    public decimal? RefundAmount { get; set; }
    public decimal? AdditionalAmount { get; set; }
    public DateTime? SettledAt { get; set; }
    public int? SettledBy { get; set; }
    public int? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectReason { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public int CreatedBy { get; set; }

    public virtual SalesOrder SalesOrder { get; set; } = null!;
    public virtual User? ApprovedByUser { get; set; }
    public virtual User? SettledByUser { get; set; }
    public virtual User? RejectedByUser { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
    // v2.2 — chi tiết tạm ứng theo dòng SO (hybrid model)
    public virtual ICollection<AdvanceRequestItem> Items { get; set; } = [];
}

// ─── ADVANCE REQUEST ITEM (Tạm ứng theo dòng SO — v2.2) ─
// SOItemId NULL  → tạm ứng cho cả đơn không gắn dòng cụ thể
// SOItemId NOT NULL → tạm ứng cho dòng SO cụ thể
public class AdvanceRequestItem
{
    public int AdvanceRequestItemId { get; set; }
    public int AdvanceRequestId { get; set; }
    public int? SOItemId { get; set; }
    public decimal Amount { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual AdvanceRequest AdvanceRequest { get; set; } = null!;
    public virtual SalesOrderItem? SOItem { get; set; }
}

// ─── CUSTOMER PAYMENT (Phải thu - AR) ────────────────
public class CustomerPayment
{
    public int PaymentId { get; set; }
    public string PaymentNo { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int? SalesOrderId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? BankReference { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentUrl { get; set; }
    public string Status { get; set; } = "CONFIRMED";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CreatedBy { get; set; }

    public virtual Customer Customer { get; set; } = null!;
    public virtual SalesOrder? SalesOrder { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
}

