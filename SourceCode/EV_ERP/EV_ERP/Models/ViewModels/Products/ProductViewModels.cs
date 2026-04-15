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

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Ảnh sản phẩm")]
        public IFormFile? ImageFile { get; set; }

        // Populated in Edit mode — current saved image
        public string? ExistingImageUrl { get; set; }
        public bool RemoveImage { get; set; }

        // Populated in Edit mode — gallery images
        public List<ProductImageViewModel> Images { get; set; } = [];

        // Dropdown options
        public List<CategoryOptionViewModel> Categories { get; set; } = [];
        public List<UnitOptionViewModel> Units { get; set; } = [];

        public bool IsEditMode => ProductId.HasValue && ProductId > 0;
    }

    // ── Detail ───────────────────────────────────────────
    public class ProductDetailViewModel
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
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
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public bool HasBarcode => !string.IsNullOrEmpty(Barcode);
        public List<ProductImageViewModel> Images { get; set; } = [];
        public List<CustomerPriceViewModel> CustomerPrices { get; set; } = [];
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
