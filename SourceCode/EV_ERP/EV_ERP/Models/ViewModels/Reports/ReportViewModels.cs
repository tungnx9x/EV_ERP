using EV_ERP.Models.ViewModels.Quotations;

namespace EV_ERP.Models.ViewModels.Reports;

// ══════════════════════════════════════════════════════
// SALES REVENUE REPORT
// ══════════════════════════════════════════════════════

public class SalesRevenueFilterViewModel
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int? CustomerId { get; set; }
    public int? SalesPersonId { get; set; }
}

public class SalesRevenueReportViewModel
{
    // ── Filter ──
    public SalesRevenueFilterViewModel Filter { get; set; } = new();

    // ── Dropdowns ──
    public List<CustomerOptionViewModel> Customers { get; set; } = [];
    public List<SalesPersonOptionViewModel> SalesPersons { get; set; } = [];

    // ── Summary ──
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }

    // ── Chart data (revenue by month) ──
    public List<string> ChartLabels { get; set; } = [];
    public List<decimal> ChartData { get; set; } = [];

    // ── Detail rows ──
    public List<SalesRevenueRowViewModel> Rows { get; set; } = [];
}

public class SalesRevenueRowViewModel
{
    public int SalesOrderId { get; set; }
    public string SalesOrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string SalesPersonName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ══════════════════════════════════════════════════════
// SALES RESULT (BCKQKD) REPORT — per-user, current sales person only
// ══════════════════════════════════════════════════════

public class SalesResultFilterViewModel
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

public class SalesResultReportViewModel
{
    public SalesResultFilterViewModel Filter { get; set; } = new();
    public string UserFullName { get; set; } = string.Empty;
    public List<SalesResultRowViewModel> Rows { get; set; } = [];

    public decimal TotalSubTotal { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPurchaseCost { get; set; }
    public decimal TotalShippingFee { get; set; }
    public decimal TotalUnofficialW2WShipping { get; set; }
}

public class SalesResultRowViewModel
{
    public int SalesOrderId { get; set; }
    public string SalesOrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPoNo { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal UnofficialW2WShipping { get; set; }
}
