using System.ComponentModel.DataAnnotations;
using EV_ERP.Models.Common;

namespace EV_ERP.Models.ViewModels.Reference;

// ── List ─────────────────────────────────────────────
public class CurrencyListViewModel
{
    public PagedResult<CurrencyRowViewModel> Paged { get; set; } = new();
    public string? SearchKeyword { get; set; }
    public string? FilterStatus { get; set; }
}

public class CurrencyRowViewModel
{
    public string CurrencyCode { get; set; } = string.Empty;
    public string CurrencyName { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public byte DecimalPlaces { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }

    // Latest rate 1 {CurrencyCode} = RateToVnd VND (null = chưa có tỷ giá)
    public decimal? RateToVnd { get; set; }
    public DateTime? RateDate { get; set; }

    public bool IsBase => CurrencyCode == CurrencyConstants.BaseCurrency;
    public string StatusBadge => IsActive ? "success" : "secondary";
    public string StatusText => IsActive ? "Hoạt động" : "Vô hiệu";
}

// ── Form (Create / Edit) ─────────────────────────────
public class CurrencyFormViewModel
{
    // CurrencyCode is the primary key and is referenced by FKs → editable only on create.
    public bool IsEditMode { get; set; }

    [Required(ErrorMessage = "Mã tiền tệ là bắt buộc")]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Mã tiền tệ gồm đúng 3 chữ cái theo chuẩn ISO 4217 (VD: USD, HKD)")]
    [Display(Name = "Mã tiền tệ")]
    public string CurrencyCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên tiền tệ là bắt buộc")]
    [MaxLength(100, ErrorMessage = "Tên tối đa 100 ký tự")]
    [Display(Name = "Tên tiền tệ")]
    public string CurrencyName { get; set; } = string.Empty;

    [MaxLength(10, ErrorMessage = "Ký hiệu tối đa 10 ký tự")]
    [Display(Name = "Ký hiệu")]
    public string? Symbol { get; set; }

    [Range(0, 4, ErrorMessage = "Số chữ số thập phân từ 0 đến 4")]
    [Display(Name = "Số chữ số thập phân")]
    public byte DecimalPlaces { get; set; } = 2;

    [Range(0, 9999, ErrorMessage = "Thứ tự hiển thị từ 0 đến 9999")]
    [Display(Name = "Thứ tự hiển thị")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Kích hoạt")]
    public bool IsActive { get; set; } = true;

    // Optional: rate saved as today's ExchangeRate row (From = code, To = VND).
    // Posted as text and parsed with InvariantCulture ("25,400.5") — the app has no request
    // culture configured, so binding a decimal directly would depend on the server's locale.
    [Display(Name = "Tỷ giá quy đổi sang VND")]
    public string? RateToVndInput { get; set; }

    public decimal? RateToVnd { get; set; }
    public DateTime? RateDate { get; set; }

    /// <summary>Parses <see cref="RateToVndInput"/> into <see cref="RateToVnd"/>; returns an error message or null.</summary>
    public string? ParseRateInput()
    {
        RateToVnd = null;
        if (string.IsNullOrWhiteSpace(RateToVndInput)) return null;
        var s = RateToVndInput.Trim().Replace(",", "").Replace(" ", "");
        if (!decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
            return $"Tỷ giá \"{RateToVndInput}\" không phải là số hợp lệ (VD: 25,400 hoặc 3,520.75)";
        if (v <= 0 || v > 999_999_999)
            return "Tỷ giá phải lớn hơn 0 và nhỏ hơn 1 tỷ";
        RateToVnd = v;
        return null;
    }

    public bool IsBase => string.Equals(CurrencyCode, CurrencyConstants.BaseCurrency, StringComparison.OrdinalIgnoreCase);
}

public static class CurrencyConstants
{
    public const string BaseCurrency = "VND";
}
