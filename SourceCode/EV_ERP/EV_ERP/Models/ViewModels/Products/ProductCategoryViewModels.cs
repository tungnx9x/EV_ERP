using System.ComponentModel.DataAnnotations;

namespace EV_ERP.Models.ViewModels.Products
{
    // ── List (tree) ───────────────────────────────────────
    public class ProductCategoryListViewModel
    {
        public List<ProductCategoryNodeViewModel> RootNodes { get; set; } = [];
        public string? SearchKeyword { get; set; }
        public string? FilterStatus { get; set; }
        public int TotalCount { get; set; }
    }

    public class ProductCategoryNodeViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int? ParentCategoryId { get; set; }
        public int DisplayOrder { get; set; }
        public int ProductCount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Depth { get; set; }
        public List<ProductCategoryNodeViewModel> Children { get; set; } = [];

        public bool HasChildren => Children.Any();
    }

    // ── Form (Create / Edit) ─────────────────────────────
    public class ProductCategoryFormViewModel
    {
        public int? CategoryId { get; set; }
        [Display(Name = "Mã danh mục")]
        public string CategoryCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên danh mục là bắt buộc")]
        [MaxLength(200, ErrorMessage = "Tên tối đa 200 ký tự")]
        [Display(Name = "Tên danh mục")]
        public string CategoryName { get; set; } = string.Empty;

        [Display(Name = "Danh mục cha")]
        public int? ParentCategoryId { get; set; }

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [MaxLength(10, ErrorMessage = "Mã SKU prefix tối đa 10 ký tự")]
        [Display(Name = "SKU Prefix")]
        public string? SkuPrefix { get; set; }

        [Range(0, 9999, ErrorMessage = "Thứ tự hiển thị từ 0–9999")]
        [Display(Name = "Thứ tự hiển thị")]
        public int DisplayOrder { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;

        public List<ParentCategoryOptionViewModel> ParentOptions { get; set; } = [];

        public bool IsEditMode => CategoryId.HasValue && CategoryId > 0;
    }

    public class ParentCategoryOptionViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty; // includes depth prefix
    }
}
