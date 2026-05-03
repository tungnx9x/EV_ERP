using System.ComponentModel.DataAnnotations;

namespace EV_ERP.Models.ViewModels.Products
{
    // ── Attribute List ──────────────────────────────────
    public class ProductAttributeListViewModel
    {
        public Models.Common.PagedResult<ProductAttributeRowViewModel> Paged { get; set; } = new();
        public string? SearchKeyword { get; set; }
    }

    public class ProductAttributeRowViewModel
    {
        public int AttributeId { get; set; }
        public string AttrCode { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string DataType { get; set; } = "LIST";
        public bool IncludeInSku { get; set; }
        public int SkuPosition { get; set; }
        public bool IsRequired { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public int ValueCount { get; set; }
    }

    // ── Attribute Form ──────────────────────────────────
    public class ProductAttributeFormViewModel
    {
        public int? AttributeId { get; set; }

        [Required(ErrorMessage = "Mã thuộc tính là bắt buộc")]
        [MaxLength(20, ErrorMessage = "Mã tối đa 20 ký tự")]
        [Display(Name = "Mã thuộc tính")]
        public string AttrCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên thuộc tính là bắt buộc")]
        [MaxLength(100, ErrorMessage = "Tên tối đa 100 ký tự")]
        [Display(Name = "Tên thuộc tính")]
        public string AttributeName { get; set; } = string.Empty;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Kiểu dữ liệu")]
        public string DataType { get; set; } = "LIST";

        [Display(Name = "Đưa vào SKU")]
        public bool IncludeInSku { get; set; } = true;

        [Display(Name = "Vị trí trong SKU")]
        public int SkuPosition { get; set; }

        [Display(Name = "Bắt buộc")]
        public bool IsRequired { get; set; }

        [Display(Name = "Thứ tự hiển thị")]
        public int DisplayOrder { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;

        public List<AttributeValueRowViewModel> Values { get; set; } = [];

        public bool IsEditMode => AttributeId.HasValue && AttributeId > 0;
    }

    // ── Attribute Value ─────────────────────────────────
    public class AttributeValueRowViewModel
    {
        public int ValueId { get; set; }
        public string SkuCode { get; set; } = string.Empty;
        public string ValueName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class AttributeValueFormViewModel
    {
        public int? ValueId { get; set; }
        public int AttributeId { get; set; }

        [Required(ErrorMessage = "Mã viết tắt SKU là bắt buộc")]
        [MaxLength(10, ErrorMessage = "Mã tối đa 10 ký tự")]
        [Display(Name = "Mã SKU")]
        public string SkuCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên giá trị là bắt buộc")]
        [MaxLength(100, ErrorMessage = "Tên tối đa 100 ký tự")]
        [Display(Name = "Tên giá trị")]
        public string ValueName { get; set; } = string.Empty;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Thứ tự")]
        public int DisplayOrder { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;
    }

    // ── SKU Config ──────────────────────────────────────
    public class SkuConfigViewModel
    {
        public int SkuConfigId { get; set; }
        public int CategoryId { get; set; }
        public int AttributeId { get; set; }
        public string AttrCode { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public int SkuPosition { get; set; }
        public bool IsRequired { get; set; }
        public int? DefaultValueId { get; set; }
        public bool IsActive { get; set; }
    }

    public class SkuConfigFormViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? SkuPrefix { get; set; }
        public List<SkuConfigViewModel> Configs { get; set; } = [];
        public List<ProductAttributeOptionViewModel> AvailableAttributes { get; set; } = [];
    }

    public class ProductAttributeOptionViewModel
    {
        public int AttributeId { get; set; }
        public string AttrCode { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
    }

    // ── SKU-related data for Product Form ───────────────
    public class SkuAttributeFormItem
    {
        public int AttributeId { get; set; }
        public string AttrCode { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string DataType { get; set; } = "LIST";
        public bool IsRequired { get; set; }
        public int SkuPosition { get; set; }
        public int? SelectedValueId { get; set; }
        public string? TextValue { get; set; }
        public int? DefaultValueId { get; set; }
        public List<AttributeValueOptionViewModel> Values { get; set; } = [];
    }

    public class AttributeValueOptionViewModel
    {
        public int ValueId { get; set; }
        public string SkuCode { get; set; } = string.Empty;
        public string ValueName { get; set; } = string.Empty;
    }
}
