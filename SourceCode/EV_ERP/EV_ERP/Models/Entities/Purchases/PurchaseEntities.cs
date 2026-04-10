using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.Entities.Vendors;

namespace EV_ERP.Models.Entities.Purchases;

// ─── VENDOR INVOICE (Hóa đơn NCC — gắn trực tiếp vào SO, v1.3) ───
public class VendorInvoice : BaseEntity
{
    public int VendorInvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public int SalesOrderId { get; set; }
    public int VendorId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Currency { get; set; } = "VND";
    /// <summary>UNPAID → PARTIALLY_PAID → PAID → CANCELLED</summary>
    public string Status { get; set; } = "UNPAID";
    public string? Notes { get; set; }
    public string? AttachmentUrl { get; set; }
    public int CreatedBy { get; set; }

    public virtual SalesOrder SalesOrder { get; set; } = null!;
    public virtual Vendor Vendor { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
}
