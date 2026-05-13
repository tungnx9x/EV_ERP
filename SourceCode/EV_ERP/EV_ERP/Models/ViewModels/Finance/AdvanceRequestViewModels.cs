namespace EV_ERP.Models.ViewModels.Finance;

// ── Per-SO list/summary (shown on SO Detail) ───────────
public class SalesOrderAdvanceSummary
{
    public int SalesOrderId { get; set; }
    public decimal PurchaseCost { get; set; }      // basis to compare against
    public string Currency { get; set; } = "VND";

    public decimal TotalRequested { get; set; }     // Σ RequestedAmount over non-rejected
    public decimal TotalReceived { get; set; }      // Σ ApprovedAmount over RECEIVED/SETTLING/SETTLED
    public decimal RemainingVsCost => Math.Max(0, PurchaseCost - TotalReceived);

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
        "PENDING" => "Chờ duyệt",
        "APPROVED" => "Đã duyệt",
        "RECEIVED" => "Đã nhận tiền",
        "SETTLING" => "Đang quyết toán",
        "SETTLED" => "Đã quyết toán",
        "REJECTED" => "Từ chối",
        _ => Status
    };

    public string StatusBadge => Status switch
    {
        "PENDING" => "bg-secondary",
        "APPROVED" => "bg-info",
        "RECEIVED" => "bg-success",
        "SETTLING" => "bg-warning text-dark",
        "SETTLED" => "bg-primary",
        "REJECTED" => "bg-danger",
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
    public string Status { get; set; } = "RECEIVED";          // default — money already in
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
