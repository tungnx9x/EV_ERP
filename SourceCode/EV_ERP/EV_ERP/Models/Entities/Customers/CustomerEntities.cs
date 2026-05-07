using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Reference;

namespace EV_ERP.Models.Entities.Customers;

// ─── CUSTOMER GROUP ──────────────────────────────────
public class CustomerGroup : BaseEntity, ISoftDeletable
{
    public int CustomerGroupId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PriorityLevel { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Customer> Customers { get; set; } = [];
}

// ─── CUSTOMER (Khách sạn) ───────────────────────────
public class Customer : AuditableEntity, ISoftDeletable
{
    public int CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
    // v2.1 — FK to master location tables (legacy text fields kept for back-compat)
    public string? CityCode { get; set; }
    public string? DistrictCode { get; set; }
    public string? WardCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public int? CustomerGroupId { get; set; }
    public int? SalesPersonId { get; set; }
    public int PaymentTermDays { get; set; } = 30;
    public decimal? CreditLimit { get; set; }
    public string? InternalNotes { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual CustomerGroup? CustomerGroup { get; set; }
    public virtual User? SalesPerson { get; set; }
    public virtual City? CityRef { get; set; }
    public virtual District? DistrictRef { get; set; }
    public virtual Ward? WardRef { get; set; }
    public virtual ICollection<CustomerContact> Contacts { get; set; } = [];
    public virtual ICollection<CustomerNote> Notes { get; set; } = [];
}

// ─── CUSTOMER CONTACT (nhiều liên hệ / KH) ─────────
public class CustomerContact : BaseEntity, ISoftDeletable
{
    public int ContactId { get; set; }
    public int CustomerId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }         // Bếp trưởng, QL Mua hàng...
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual Customer Customer { get; set; } = null!;
}

// ─── CUSTOMER NOTE (ghi chú nội bộ timeline) ────────
public class CustomerNote
{
    public int NoteId { get; set; }
    public int CustomerId { get; set; }
    public string NoteContent { get; set; } = string.Empty;
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Customer Customer { get; set; } = null!;
    public virtual User CreatedByUser { get; set; } = null!;
}
