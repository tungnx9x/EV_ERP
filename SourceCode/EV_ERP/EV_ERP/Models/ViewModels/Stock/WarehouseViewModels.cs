using System.ComponentModel.DataAnnotations;

namespace EV_ERP.Models.ViewModels.Stock
{
    // ── List ─────────────────────────────────────────────
    public class WarehouseListViewModel
    {
        public List<WarehouseRowViewModel> Warehouses { get; set; } = [];
        public string? SearchKeyword { get; set; }
        public string? FilterStatus { get; set; }
        public int TotalCount { get; set; }
    }

    public class WarehouseRowViewModel
    {
        public int WarehouseId { get; set; }
        public string WarehouseCode { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? ManagerName { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsActive { get; set; }
        public int LocationCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ── Form (Create / Edit) ─────────────────────────────
    public class WarehouseFormViewModel
    {
        public int? WarehouseId { get; set; }

        [Required(ErrorMessage = "Mã kho là bắt buộc")]
        [MaxLength(20, ErrorMessage = "Mã kho tối đa 20 ký tự")]
        [Display(Name = "Mã kho")]
        public string WarehouseCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên kho là bắt buộc")]
        [MaxLength(200, ErrorMessage = "Tên kho tối đa 200 ký tự")]
        [Display(Name = "Tên kho")]
        public string WarehouseName { get; set; } = string.Empty;

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Display(Name = "Quản lý kho")]
        public int? ManagerId { get; set; }

        [Display(Name = "Kho ảo (drop-ship)")]
        public bool IsVirtual { get; set; }

        public bool IsEditMode => WarehouseId.HasValue && WarehouseId > 0;

        // Dropdown options
        public List<UserOptionViewModel> Managers { get; set; } = [];
    }

    // ── Detail ───────────────────────────────────────────
    public class WarehouseDetailViewModel
    {
        public int WarehouseId { get; set; }
        public string WarehouseCode { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? ManagerName { get; set; }
        public int? ManagerId { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<LocationRowViewModel> Locations { get; set; } = [];
        public int TotalProductCount { get; set; }
        public decimal TotalQuantityOnHand { get; set; }
    }

    // ── Location ─────────────────────────────────────────
    public class LocationRowViewModel
    {
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public string? Zone { get; set; }
        public string? Aisle { get; set; }
        public string? Rack { get; set; }
        public string? Shelf { get; set; }
        public string? Bin { get; set; }
        public decimal? MaxCapacity { get; set; }
        public bool IsActive { get; set; }
    }

    public class LocationFormModel
    {
        public int? LocationId { get; set; }
        public int WarehouseId { get; set; }

        [Required(ErrorMessage = "Mã vị trí là bắt buộc")]
        [MaxLength(50)]
        public string LocationCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên vị trí là bắt buộc")]
        [MaxLength(200)]
        public string LocationName { get; set; } = string.Empty;

        public string? Zone { get; set; }
        public string? Aisle { get; set; }
        public string? Rack { get; set; }
        public string? Shelf { get; set; }
        public string? Bin { get; set; }
        public decimal? MaxCapacity { get; set; }
    }

    // ── Shared Options ───────────────────────────────────
    public class UserOptionViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? RoleCode { get; set; }
    }

    public class WarehouseOptionViewModel
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
        public string WarehouseCode { get; set; } = string.Empty;
    }
}
