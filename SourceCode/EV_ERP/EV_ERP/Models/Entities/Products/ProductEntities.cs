using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Vendors;

namespace EV_ERP.Models.Entities.Products;

// ─── PRODUCT CATEGORY (phân cấp) ────────────────────
public class ProductCategory : BaseEntity, ISoftDeletable
{
    public int CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int? ParentCategoryId { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ProductCategory? ParentCategory { get; set; }
    public virtual ICollection<ProductCategory> SubCategories { get; set; } = [];
    public virtual ICollection<Product> Products { get; set; } = [];
}

// ─── UNIT (Đơn vị tính) ─────────────────────────────
public class Unit : ISoftDeletable
{
    public int UnitId { get; set; }
    public string UnitCode { get; set; } = string.Empty;   // KG, LIT, CAI...
    public string UnitName { get; set; } = string.Empty;   // Kilogram, Lít, Cái...
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Product> Products { get; set; } = [];
}

// ─── PRODUCT ─────────────────────────────────────────
public class Product : AuditableEntity, ISoftDeletable
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? CategoryId { get; set; }
    public int UnitId { get; set; }
    public string? Barcode { get; set; }
    public string? BarcodeType { get; set; }                // EAN8, EAN13, QR, CODE128
    public string? ImageUrl { get; set; }
    public decimal? DefaultSalePrice { get; set; }
    public decimal? DefaultPurchasePrice { get; set; }
    public int MinStockLevel { get; set; }
    public decimal? Weight { get; set; }
    public string? WeightUnit { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ProductCategory? Category { get; set; }
    public virtual Unit Unit { get; set; } = null!;
    public virtual ICollection<ProductImage> Images { get; set; } = [];
    public virtual ICollection<VendorPrice> VendorPrices { get; set; } = [];
    public virtual ICollection<CustomerPrice> CustomerPrices { get; set; } = [];
}

// ─── PRODUCT IMAGE ───────────────────────────────────
public class ProductImage
{
    public int ImageId { get; set; }
    public int ProductId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Product Product { get; set; } = null!;
}

// ─── VENDOR PRICE (Giá mua theo NCC) ────────────────
public class VendorPrice : BaseEntity, ISoftDeletable
{
    public int VendorPriceId { get; set; }
    public int ProductId { get; set; }
    public int VendorId { get; set; }
    public decimal PurchasePrice { get; set; }
    public string Currency { get; set; } = "VND";
    public int? MinOrderQty { get; set; }
    public int? LeadTimeDays { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.Today;
    public DateTime? EffectiveTo { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual Product Product { get; set; } = null!;
    public virtual Vendor Vendor { get; set; } = null!;
}

// ─── CUSTOMER PRICE (Giá bán theo KH / nhóm KH) ────
public class CustomerPrice : BaseEntity, ISoftDeletable
{
    public int CustomerPriceId { get; set; }
    public int ProductId { get; set; }
    public int? CustomerId { get; set; }
    public int? CustomerGroupId { get; set; }
    public decimal SalePrice { get; set; }
    public string Currency { get; set; } = "VND";
    public int? MinQty { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.Today;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual Product Product { get; set; } = null!;
    public virtual Customer? Customer { get; set; }
    public virtual CustomerGroup? CustomerGroup { get; set; }
}
