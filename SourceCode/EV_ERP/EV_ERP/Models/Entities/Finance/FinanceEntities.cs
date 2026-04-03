using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Purchases;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.Entities.Vendors;

namespace EV_ERP.Models.Entities.Finance;

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

// ─── VENDOR PAYMENT (Phải trả - AP) ─────────────────
public class VendorPayment
{
    public int PaymentId { get; set; }
    public string PaymentNo { get; set; } = string.Empty;
    public int VendorId { get; set; }
    public int? VendorInvoiceId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? BankReference { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentUrl { get; set; }
    public string Status { get; set; } = "CONFIRMED";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CreatedBy { get; set; }

    public virtual Vendor Vendor { get; set; } = null!;
    public virtual VendorInvoice? VendorInvoice { get; set; }
    public virtual PurchaseOrder? PurchaseOrder { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
}
