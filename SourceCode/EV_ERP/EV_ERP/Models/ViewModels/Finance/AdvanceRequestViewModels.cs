namespace EV_ERP.Models.ViewModels.Finance;

// ── Per-SO list/summary (shown on SO Detail) ───────────
public class SalesOrderAdvanceSummary
{
    public int SalesOrderId { get; set; }
    public decimal PurchaseCost { get; set; }      // basis to compare against (chi phí mua)
    public decimal CustomerShippingCost { get; set; }  // Σ ShippingFee (phí vận chuyển KH dự kiến)
    public string Currency { get; set; } = "VND";

    public decimal TotalRequested { get; set; }     // Σ RequestedAmount over non-rejected
    public decimal TotalReceived { get; set; }      // Σ ApprovedAmount over RECEIVED/SETTLING/SETTLED
    public decimal ReceivedCustomerShipping { get; set; }  // phần TotalReceived đã chi cho VC khách hàng

    public decimal ReceivedPurchase => Math.Max(0, TotalReceived - ReceivedCustomerShipping);
    public decimal RemainingVsCost => Math.Max(0, PurchaseCost - ReceivedPurchase);
    public decimal RemainingVsCustomerShipping => Math.Max(0, CustomerShippingCost - ReceivedCustomerShipping);

    public List<AdvanceRequestRow> Requests { get; set; } = [];
}

public class AdvanceRequestRow
{
    public int AdvanceRequestId { get; set; }
    public string RequestNo { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public List<AdvanceRequestItemRow> Items { get; set; } = [];

    public string StatusText => Status switch
    {
        // ── Quy trình duyệt tạm ứng (4 bước) ──
        "WAIT_ACCOUNTANT" => "Chờ kế toán duyệt",
        "WAIT_DIRECTOR" => "Chờ giám đốc duyệt",
        "WAIT_DISBURSE" => "Chờ chi tiền",
        "DISBURSED" => "Đã chi tiền",
        "REJECTED" => "Từ chối",
        // ── Tự thanh toán (ngoài quy trình duyệt) ──
        "SELF_PAID" => "Đã tự thanh toán",
        // ── Quyết toán (giai đoạn sau) ──
        "SETTLING" => "Đang quyết toán",
        "SETTLED" => "Đã quyết toán",
        // ── Legacy codes (dữ liệu cũ — chưa migrate) ──
        "PENDING" => "Chờ kế toán duyệt",
        "APPROVED" => "Chờ chi tiền",
        "RECEIVED" => "Đã chi tiền",
        _ => Status
    };

    public string StatusBadge => Status switch
    {
        "WAIT_ACCOUNTANT" => "bg-secondary",
        "WAIT_DIRECTOR" => "bg-info",
        "WAIT_DISBURSE" => "bg-warning text-dark",
        "DISBURSED" => "bg-success",
        "REJECTED" => "bg-danger",
        "SELF_PAID" => "bg-dark",
        "SETTLING" => "bg-warning text-dark",
        "SETTLED" => "bg-primary",
        // ── Legacy codes ──
        "PENDING" => "bg-secondary",
        "APPROVED" => "bg-warning text-dark",
        "RECEIVED" => "bg-success",
        _ => "bg-light text-dark"
    };
}

public class AdvanceRequestItemRow
{
    public int AdvanceRequestItemId { get; set; }
    public int? SOItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;   // "Phân bổ chung" when SOItemId is null
    public decimal Amount { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
}

// ── Create form ────────────────────────────────────────
public class AdvanceRequestCreateModel
{
    public DateTime? RequestDate { get; set; }
    public string Purpose { get; set; } = string.Empty;
    // Trạng thái khởi tạo luôn là WAIT_ACCOUNTANT (do server quyết định) — KD không tự đặt được.
    // Ngoại lệ: SelfPaid = người tạo đã tự thanh toán → trạng thái SELF_PAID, không qua duyệt.
    public bool SelfPaid { get; set; }
    public string? Notes { get; set; }
    public List<AdvanceRequestItemCreateModel> Items { get; set; } = [];
}

public class AdvanceRequestItemCreateModel
{
    /// <summary>null = whole-SO allocation</summary>
    public int? SOItemId { get; set; }
    public decimal Amount { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
}
