using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EV_ERP.Models.ViewModels.Products
{
    // ── List ─────────────────────────────────────────────
    public class ProductListViewModel
    {
        public List<ProductRowViewModel> Products { get; set; } = [];
        public string? SearchKeyword { get; set; }
        public int? FilterCategoryId { get; set; }
        public string? FilterStatus { get; set; }
        public int TotalCount { get; set; }
        public List<CategoryOptionViewModel> Categories { get; set; } = [];
    }

    public class ProductRowViewModel
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? DefaultSalePrice { get; set; }
        public decimal? DefaultPurchasePrice { get; set; }
        public int MinStockLevel { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public string StatusBadge => IsActive ? "success" : "secondary";
        public string StatusText => IsActive ? "Hoạt động" : "Vô hiệu";
    }

    public class CategoryOptionViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int? ParentCategoryId { get; set; }
        public int Level { get; set; }
        public bool HasChildren { get; set; }
        public string? SkuPrefix { get; set; }
    }

    public class UnitOptionViewModel
    {
        public int UnitId { get; set; }
        public string UnitCode { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
    }

    // ── Form (Create / Edit) ─────────────────────────────
    public class ProductFormViewModel
    {
        public int? ProductId { get; set; }

        [Required(ErrorMessage = "Tên sản phẩm là bắt buộc")]
        [MaxLength(300, ErrorMessage = "Tên tối đa 300 ký tự")]
        [Display(Name = "Tên sản phẩm")]
        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Danh mục")]
        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "Đơn vị tính là bắt buộc")]
        [Display(Name = "Đơn vị tính")]
        public int UnitId { get; set; }

        [MaxLength(50)]
        [Display(Name = "Mã barcode")]
        public string? Barcode { get; set; }

        [Display(Name = "Loại barcode")]
        public string? BarcodeType { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá bán phải >= 0")]
        [Display(Name = "Giá bán mặc định (VNĐ)")]
        public decimal? DefaultSalePrice { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá mua phải >= 0")]
        [Display(Name = "Giá mua mặc định (VNĐ)")]
        public decimal? DefaultPurchasePrice { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Tồn kho tối thiểu phải >= 0")]
        [Display(Name = "Tồn kho tối thiểu")]
        public int MinStockLevel { get; set; }

        [Range(0, double.MaxValue)]
        [Display(Name = "Khối lượng")]
        public decimal? Weight { get; set; }

        [MaxLength(10)]
        [Display(Name = "Đơn vị KL")]
        public string? WeightUnit { get; set; }

        [MaxLength(500)]
        [Display(Name = "Link nguồn mua")]
        public string? SourceUrl { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;

        // Create mode: initial gallery uploads
        public List<IFormFile>? GalleryFiles { get; set; }
        // Index (0-based) of which uploaded file is the avatar; default 0
        public int AvatarIndex { get; set; } = 0;

        // Populated in Edit mode — gallery images
        public List<ProductImageViewModel> Images { get; set; } = [];

        // SKU attribute selections (posted from form)
        public Dictionary<int, int?> AttributeValues { get; set; } = new();

        // Dropdown options
        public List<CategoryOptionViewModel> Categories { get; set; } = [];
        public List<UnitOptionViewModel> Units { get; set; } = [];
        // SKU attribute config for selected category (populated by service)
        public List<SkuAttributeFormItem> SkuAttributes { get; set; } = [];

        // Read-only fields
        public string? SKU { get; set; }

        public bool IsEditMode => ProductId.HasValue && ProductId > 0;
    }

    // ── Detail ───────────────────────────────────────────
    public class ProductDetailViewModel
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string? SKU { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryCode { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public string UnitCode { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string? BarcodeType { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? DefaultSalePrice { get; set; }
        public decimal? DefaultPurchasePrice { get; set; }
        public int MinStockLevel { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public string? Brand { get; set; }
        public string? Origin { get; set; }
        public string? SourceUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public bool HasBarcode => !string.IsNullOrEmpty(Barcode);
        public bool HasSKU => !string.IsNullOrEmpty(SKU);
        public List<ProductImageViewModel> Images { get; set; } = [];
        public List<CustomerPriceViewModel> CustomerPrices { get; set; } = [];
        public List<ProductAttributeDisplayViewModel> Attributes { get; set; } = [];
    }

    // ── Product Attribute Display ────────────────────────
    public class ProductAttributeDisplayViewModel
    {
        public string AttrCode { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string? ValueName { get; set; }
        public string? SkuCode { get; set; }
        public string? TextValue { get; set; }
        public string DisplayValue => TextValue ?? ValueName ?? "—";
    }

    // ── Product Images ───────────────────────────────────
    public class ProductImageViewModel
    {
        public int ImageId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsPrimary { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ── Customer Prices ──────────────────────────────────
    public class CustomerPriceViewModel
    {
        public int CustomerPriceId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerCode { get; set; }
        public string? GroupName { get; set; }
        public decimal SalePrice { get; set; }
        public string Currency { get; set; } = "VND";
        public int? MinQty { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
    }

    // ── Barcode generation ───────────────────────────────
    public class GenerateBarcodeResult
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string BarcodeType { get; set; } = string.Empty;
    }
}
