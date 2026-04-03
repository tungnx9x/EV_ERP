using EV_ERP.Models.Common;

namespace EV_ERP.Models.Entities.Vendors;

// ─── VENDOR (Nhà cung cấp) ──────────────────────────
public class Vendor : AuditableEntity, ISoftDeletable
{
    public int VendorId { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? BankAccountNo { get; set; }
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public int PaymentTermDays { get; set; } = 30;
    public decimal? AvgDeliveryDays { get; set; }
    public decimal? QualityRating { get; set; }
    public decimal? OnTimeRate { get; set; }
    public string? InternalNotes { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<VendorContact> Contacts { get; set; } = [];
}

// ─── VENDOR CONTACT ──────────────────────────────────
public class VendorContact : ISoftDeletable
{
    public int ContactId { get; set; }
    public int VendorId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Vendor Vendor { get; set; } = null!;
}
