using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Customers;

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
    public string? SkuPrefix { get; set; }              // VD: TP, NL, VT, TB
    public bool IsActive { get; set; } = true;

    public virtual ProductCategory? ParentCategory { get; set; }
    public virtual ICollection<ProductCategory> SubCategories { get; set; } = [];
    public virtual ICollection<Product> Products { get; set; } = [];
    public virtual ICollection<SkuConfig> SkuConfigs { get; set; } = [];
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
    public string? SKU { get; set; }                        // Mã SKU tự gen: TP-VN-5K-WH-0001
    public string? Barcode { get; set; }
    public string? BarcodeType { get; set; }                // EAN8, EAN13, QR, CODE128
    public string? ImageUrl { get; set; }
    public decimal? DefaultSalePrice { get; set; }
    public decimal? DefaultPurchasePrice { get; set; }
    public int MinStockLevel { get; set; }
    public decimal? Weight { get; set; }
    public string? WeightUnit { get; set; }
    public string? SourceUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ProductCategory? Category { get; set; }
    public virtual Unit Unit { get; set; } = null!;
    public virtual ICollection<ProductImage> Images { get; set; } = [];
    public virtual ICollection<CustomerPrice> CustomerPrices { get; set; } = [];
    public virtual ICollection<ProductAttributeMap> AttributeMaps { get; set; } = [];
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

// ─── PRODUCT ATTRIBUTE (Định nghĩa thuộc tính) ──────
public class ProductAttribute : BaseEntity
{
    public int AttributeId { get; set; }
    public string AttrCode { get; set; } = string.Empty;       // ORIGIN, WEIGHT, COLOR...
    public string AttributeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DataType { get; set; } = "LIST";             // LIST | TEXT
    public bool IncludeInSku { get; set; } = true;
    public int SkuPosition { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<ProductAttributeValue> Values { get; set; } = [];
    public virtual ICollection<SkuConfig> SkuConfigs { get; set; } = [];
}

// ─── PRODUCT ATTRIBUTE VALUE ─────────────────────────
public class ProductAttributeValue
{
    public int ValueId { get; set; }
    public int AttributeId { get; set; }
    public string SkuCode { get; set; } = string.Empty;       // VN, 5K, WH...
    public string ValueName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual ProductAttribute Attribute { get; set; } = null!;
}

// ─── PRODUCT ATTRIBUTE MAP (Gán thuộc tính cho SP) ───
public class ProductAttributeMap
{
    public int MapId { get; set; }
    public int ProductId { get; set; }
    public int AttributeId { get; set; }
    public int? ValueId { get; set; }
    public string? TextValue { get; set; }

    public virtual Product Product { get; set; } = null!;
    public virtual ProductAttribute Attribute { get; set; } = null!;
    public virtual ProductAttributeValue? Value { get; set; }
}

// ─── SKU CONFIG (Cấu hình SKU theo danh mục) ────────
public class SkuConfig
{
    public int SkuConfigId { get; set; }
    public int CategoryId { get; set; }
    public int AttributeId { get; set; }
    public int SkuPosition { get; set; }
    public bool IsRequired { get; set; } = true;
    public int? DefaultValueId { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ProductCategory Category { get; set; } = null!;
    public virtual ProductAttribute Attribute { get; set; } = null!;
    public virtual ProductAttributeValue? DefaultValue { get; set; }
}

// ─── SKU SEQUENCE (Bộ đếm tránh trùng) ──────────────
public class SkuSequence
{
    public int SequenceId { get; set; }
    public string SkuPrefix { get; set; } = string.Empty;
    public int LastNumber { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
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
