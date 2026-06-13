using System.ComponentModel.DataAnnotations;

namespace EV_ERP.Models.ViewModels.Customers
{
    // ── List ─────────────────────────────────────────────
    public class CustomerGroupListViewModel
    {
        public List<CustomerGroupRowViewModel> Groups { get; set; } = [];
        public string? SearchKeyword { get; set; }
        public string? FilterStatus { get; set; }
        public int TotalCount { get; set; }
    }

    public class CustomerGroupRowViewModel
    {
        public int CustomerGroupId { get; set; }
        public string GroupCode { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PriorityLevel { get; set; }
        public int CustomerCount { get; set; }
        public bool IsActive { get; set; }

        public string StatusBadge => IsActive ? "success" : "secondary";
        public string StatusText => IsActive ? "Hoạt động" : "Vô hiệu";
    }

    // ── Form (Create / Edit) ─────────────────────────────
    public class CustomerGroupFormViewModel
    {
        public int? CustomerGroupId { get; set; }

        [Required(ErrorMessage = "Mã nhóm là bắt buộc")]
        [MaxLength(20, ErrorMessage = "Mã nhóm tối đa 20 ký tự")]
        [Display(Name = "Mã nhóm")]
        public string GroupCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên nhóm là bắt buộc")]
        [MaxLength(100, ErrorMessage = "Tên nhóm tối đa 100 ký tự")]
        [Display(Name = "Tên nhóm")]
        public string GroupName { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Range(0, 9999, ErrorMessage = "Mức ưu tiên từ 0 đến 9999")]
        [Display(Name = "Mức ưu tiên")]
        public int PriorityLevel { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;

        public bool IsEditMode => CustomerGroupId.HasValue && CustomerGroupId > 0;
    }
}
