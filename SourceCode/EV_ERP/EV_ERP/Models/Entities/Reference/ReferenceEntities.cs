namespace EV_ERP.Models.Entities.Reference;

// ─── CURRENCY (danh mục đơn vị tiền tệ) ──────────────
public class Currency
{
    public string CurrencyCode { get; set; } = string.Empty;     // VND, USD, JPY, CNY, EUR, KRW, THB, GBP, AUD, SGD
    public string CurrencyName { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public byte DecimalPlaces { get; set; } = 2;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

// ─── EXCHANGE RATE (tỉ giá theo ngày) ────────────────
public class ExchangeRate
{
    public int ExchangeRateId { get; set; }
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string? Source { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.Now;

    public virtual Currency FromCurrencyRef { get; set; } = null!;
    public virtual Currency ToCurrencyRef { get; set; } = null!;
}

// ─── CITY (Tỉnh / Thành phố) ─────────────────────────
public class City
{
    public int CityId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameWithType { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;             // thanh-pho, tinh
    public bool IsActive { get; set; } = true;

    public virtual ICollection<District> Districts { get; set; } = [];
}

// ─── DISTRICT (Quận / Huyện) ─────────────────────────
public class District
{
    public int DistrictId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameWithType { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;             // huyen, quan, thanh-pho, thi-xa
    public string CityCode { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? PathWithType { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual City City { get; set; } = null!;
    public virtual ICollection<Ward> Wards { get; set; } = [];
}

// ─── WARD (Phường / Xã) ──────────────────────────────
public class Ward
{
    public int WardId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameWithType { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;             // phuong, thi-tran, xa
    public string DistrictCode { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? PathWithType { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual District District { get; set; } = null!;
}
