using System.ComponentModel.DataAnnotations;

namespace EV_ERP.Models.ViewModels.Vendors
{
    // ── List ─────────────────────────────────────────────
    public class VendorListViewModel
    {
        public List<VendorRowViewModel> Vendors { get; set; } = [];
        public string? SearchKeyword { get; set; }
        public string? FilterStatus { get; set; }
        public int TotalCount { get; set; }
    }

    public class VendorRowViewModel
    {
        public int VendorId { get; set; }
        public string VendorCode { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? ContactPerson { get; set; }
        public int PaymentTermDays { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ── Form (Create / Edit) ─────────────────────────────
    public class VendorFormViewModel
    {
        public int? VendorId { get; set; }

        [Required(ErrorMessage = "Tên nhà cung cấp là bắt buộc")]
        [MaxLength(300, ErrorMessage = "Tên tối đa 300 ký tự")]
        [Display(Name = "Tên nhà cung cấp")]
        public string VendorName { get; set; } = string.Empty;

        [MaxLength(20, ErrorMessage = "Mã số thuế tối đa 20 ký tự")]
        [Display(Name = "Mã số thuế")]
        public string? TaxCode { get; set; }

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Display(Name = "Tỉnh / Thành phố")]
        public string? City { get; set; }

        [Display(Name = "Quận / Huyện")]
        public string? District { get; set; }

        [MaxLength(20)]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [MaxLength(200)]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [MaxLength(200)]
        [Display(Name = "Website")]
        public string? Website { get; set; }

        [MaxLength(200)]
        [Display(Name = "Người liên hệ chính")]
        public string? ContactPerson { get; set; }

        [MaxLength(20)]
        [Display(Name = "SĐT liên hệ")]
        public string? ContactPhone { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [MaxLength(200)]
        [Display(Name = "Email liên hệ")]
        public string? ContactEmail { get; set; }

        // Bank info
        [MaxLength(50)]
        [Display(Name = "Số tài khoản")]
        public string? BankAccountNo { get; set; }

        [MaxLength(200)]
        [Display(Name = "Ngân hàng")]
        public string? BankName { get; set; }

        [MaxLength(200)]
        [Display(Name = "Chi nhánh")]
        public string? BankBranch { get; set; }

        // Commercial
        [Range(0, 365, ErrorMessage = "Hạn thanh toán từ 0 đến 365 ngày")]
        [Display(Name = "Hạn thanh toán (ngày)")]
        public int PaymentTermDays { get; set; } = 30;

        [Display(Name = "Ghi chú nội bộ")]
        public string? InternalNotes { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;

        public bool IsEditMode => VendorId.HasValue && VendorId > 0;
    }

    // ── Detail ───────────────────────────────────────────
    public class VendorDetailViewModel
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
        public string? ContactPerson { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? BankAccountNo { get; set; }
        public string? BankName { get; set; }
        public string? BankBranch { get; set; }
        public int PaymentTermDays { get; set; }
        public decimal? AvgDeliveryDays { get; set; }
        public decimal? QualityRating { get; set; }
        public decimal? OnTimeRate { get; set; }
        public string? InternalNotes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<VendorContactViewModel> Contacts { get; set; } = [];
        public List<VendorProductViewModel> Products { get; set; } = [];
    }

    // ── Contacts ─────────────────────────────────────────
    public class VendorContactViewModel
    {
        public int ContactId { get; set; }
        public string ContactName { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsActive { get; set; }
    }

    public class VendorContactFormModel
    {
        public int? ContactId { get; set; }
        public int VendorId { get; set; }

        [Required(ErrorMessage = "Tên liên hệ là bắt buộc")]
        [MaxLength(200)]
        public string ContactName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? JobTitle { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [MaxLength(200)]
        public string? Email { get; set; }

        public bool IsPrimary { get; set; }
    }

    // ── Products supplied ─────────────────────────────────
    public class VendorProductViewModel
    {
        public int VendorPriceId { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public string Currency { get; set; } = "VND";
        public int? MinOrderQty { get; set; }
        public int? LeadTimeDays { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }
}
