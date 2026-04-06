using System.ComponentModel.DataAnnotations;

namespace EV_ERP.Models.ViewModels.Customers
{
    // ── List ─────────────────────────────────────────────
    public class CustomerListViewModel
    {
        public List<CustomerRowViewModel> Customers { get; set; } = [];
        public string? SearchKeyword { get; set; }
        public int? FilterGroupId { get; set; }
        public string? FilterStatus { get; set; }
        public int TotalCount { get; set; }
        public List<CustomerGroupOptionViewModel> Groups { get; set; } = [];
    }

    public class CustomerRowViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? GroupName { get; set; }
        public string? GroupCode { get; set; }
        public string? SalesPersonName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public string StatusBadge => IsActive ? "success" : "secondary";
        public string StatusText => IsActive ? "Hoạt động" : "Vô hiệu";
    }

    public class CustomerGroupOptionViewModel
    {
        public int CustomerGroupId { get; set; }
        public string GroupCode { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
    }

    // ── Form (Create / Edit) ─────────────────────────────
    public class CustomerFormViewModel
    {
        public int? CustomerId { get; set; }

        [Required(ErrorMessage = "Tên khách hàng là bắt buộc")]
        [MaxLength(300, ErrorMessage = "Tên tối đa 300 ký tự")]
        [Display(Name = "Tên khách sạn / Khách hàng")]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(20, ErrorMessage = "Mã số thuế tối đa 20 ký tự")]
        [Display(Name = "Mã số thuế")]
        public string? TaxCode { get; set; }

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Display(Name = "Tỉnh / Thành phố")]
        public string? City { get; set; }

        [Display(Name = "Quận / Huyện")]
        public string? District { get; set; }

        [Display(Name = "Phường / Xã")]
        public string? Ward { get; set; }

        [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [MaxLength(200)]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [MaxLength(200)]
        [Display(Name = "Website")]
        public string? Website { get; set; }

        [Display(Name = "Nhóm khách hàng")]
        public int? CustomerGroupId { get; set; }

        [Display(Name = "Nhân viên phụ trách")]
        public int? SalesPersonId { get; set; }

        [Range(0, 365, ErrorMessage = "Hạn thanh toán từ 0 đến 365 ngày")]
        [Display(Name = "Hạn thanh toán (ngày)")]
        public int PaymentTermDays { get; set; } = 30;

        [Range(0, double.MaxValue, ErrorMessage = "Hạn mức công nợ phải >= 0")]
        [Display(Name = "Hạn mức công nợ (VNĐ)")]
        public decimal? CreditLimit { get; set; }

        [Display(Name = "Ghi chú nội bộ")]
        public string? InternalNotes { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;

        // Dropdown options
        public List<CustomerGroupOptionViewModel> Groups { get; set; } = [];
        public List<SalesPersonOptionViewModel> SalesPersons { get; set; } = [];

        public bool IsEditMode => CustomerId.HasValue && CustomerId > 0;
    }

    public class SalesPersonOptionViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string UserCode { get; set; } = string.Empty;
    }

    // ── Detail ───────────────────────────────────────────
    public class CustomerDetailViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? GroupName { get; set; }
        public string? GroupCode { get; set; }
        public string? SalesPersonName { get; set; }
        public int PaymentTermDays { get; set; }
        public decimal? CreditLimit { get; set; }
        public string? InternalNotes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<ContactViewModel> Contacts { get; set; } = [];
        public List<NoteViewModel> Notes { get; set; } = [];
    }

    // ── Contacts ─────────────────────────────────────────
    public class ContactViewModel
    {
        public int ContactId { get; set; }
        public string ContactName { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsPrimary { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
    }

    public class ContactFormModel
    {
        public int? ContactId { get; set; }
        public int CustomerId { get; set; }

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

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    // ── Notes ────────────────────────────────────────────
    public class NoteViewModel
    {
        public int NoteId { get; set; }
        public string NoteContent { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
