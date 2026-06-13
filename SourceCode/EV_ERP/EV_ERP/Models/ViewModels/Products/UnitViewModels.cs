using System.ComponentModel.DataAnnotations;

namespace EV_ERP.Models.ViewModels.Products
{
    // ── List ─────────────────────────────────────────────
    public class UnitListViewModel
    {
        public List<UnitRowViewModel> Units { get; set; } = [];
        public string? SearchKeyword { get; set; }
        public string? FilterStatus { get; set; }
        public int TotalCount { get; set; }
    }

    public class UnitRowViewModel
    {
        public int UnitId { get; set; }
        public string UnitCode { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public bool IsActive { get; set; }

        public string StatusBadge => IsActive ? "success" : "secondary";
        public string StatusText => IsActive ? "Hoạt động" : "Vô hiệu";
    }

    // ── Form (Create / Edit) ─────────────────────────────
    public class UnitFormViewModel
    {
        public int? UnitId { get; set; }

        [Required(ErrorMessage = "Mã đơn vị là bắt buộc")]
        [MaxLength(10, ErrorMessage = "Mã đơn vị tối đa 10 ký tự")]
        [Display(Name = "Mã đơn vị")]
        public string UnitCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên đơn vị là bắt buộc")]
        [MaxLength(50, ErrorMessage = "Tên đơn vị tối đa 50 ký tự")]
        [Display(Name = "Tên đơn vị")]
        public string UnitName { get; set; } = string.Empty;

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;

        public bool IsEditMode => UnitId.HasValue && UnitId > 0;
    }
}
