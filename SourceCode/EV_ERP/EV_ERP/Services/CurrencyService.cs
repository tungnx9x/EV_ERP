using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Reference;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.ViewModels.Reference;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class CurrencyService : ICurrencyService
    {
        private const string ManualRateSource = "Nhập tay (Admin)";

        private readonly IUnitOfWork _uow;
        private readonly ILogger<CurrencyService> _logger;

        public CurrencyService(IUnitOfWork uow, ILogger<CurrencyService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        private static string Normalize(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();

        // ── List ─────────────────────────────────────────
        public async Task<CurrencyListViewModel> GetListAsync(string? keyword, string? status,
            int pageIndex = 1, int pageSize = 20)
        {
            var query = _uow.Repository<Currency>().Query();

            if (status == "active") query = query.Where(c => c.IsActive);
            else if (status == "inactive") query = query.Where(c => !c.IsActive);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(c =>
                    c.CurrencyCode.ToLower().Contains(kw) ||
                    c.CurrencyName.ToLower().Contains(kw));
            }

            // Latest rate to VND per currency, resolved in the same SQL query
            var rates = _uow.Repository<ExchangeRate>().Query()
                .Where(x => x.ToCurrency == CurrencyConstants.BaseCurrency);

            var totalCount = await query.CountAsync();
            // Clamp so a stale ?page= (e.g. after deleting the last row of the last page) still shows data
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
            pageIndex = Math.Clamp(pageIndex, 1, totalPages);

            var rows = await query
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.CurrencyCode)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CurrencyRowViewModel
                {
                    CurrencyCode = c.CurrencyCode,
                    CurrencyName = c.CurrencyName,
                    Symbol = c.Symbol,
                    DecimalPlaces = c.DecimalPlaces,
                    DisplayOrder = c.DisplayOrder,
                    IsActive = c.IsActive,
                    RateToVnd = rates.Where(r => r.FromCurrency == c.CurrencyCode)
                        .OrderByDescending(r => r.EffectiveDate)
                        .Select(r => (decimal?)r.Rate).FirstOrDefault(),
                    RateDate = rates.Where(r => r.FromCurrency == c.CurrencyCode)
                        .OrderByDescending(r => r.EffectiveDate)
                        .Select(r => (DateTime?)r.EffectiveDate).FirstOrDefault()
                })
                .ToListAsync();

            return new CurrencyListViewModel
            {
                Paged = new PagedResult<CurrencyRowViewModel>
                {
                    Items = rows,
                    TotalCount = totalCount,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                },
                SearchKeyword = keyword,
                FilterStatus = status
            };
        }

        // ── Form ─────────────────────────────────────────
        public async Task<CurrencyFormViewModel?> GetFormAsync(string code)
        {
            code = Normalize(code);
            var c = await _uow.Repository<Currency>().Query()
                .FirstOrDefaultAsync(x => x.CurrencyCode == code);
            if (c == null) return null;

            var latest = await GetLatestRateAsync(code);

            return new CurrencyFormViewModel
            {
                IsEditMode = true,
                CurrencyCode = c.CurrencyCode,
                CurrencyName = c.CurrencyName,
                Symbol = c.Symbol,
                DecimalPlaces = c.DecimalPlaces,
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive,
                RateToVnd = latest?.Rate,
                RateToVndInput = latest?.Rate.ToString("#,##0.######", System.Globalization.CultureInfo.InvariantCulture),
                RateDate = latest?.EffectiveDate
            };
        }

        // ── Create ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> CreateAsync(CurrencyFormViewModel model)
        {
            var repo = _uow.Repository<Currency>();
            var code = Normalize(model.CurrencyCode);

            if (await repo.AnyAsync(c => c.CurrencyCode == code))
                return (false, $"Mã tiền tệ {code} đã tồn tại");

            await repo.AddAsync(new Currency
            {
                CurrencyCode = code,
                CurrencyName = model.CurrencyName.Trim(),
                Symbol = string.IsNullOrWhiteSpace(model.Symbol) ? null : model.Symbol.Trim(),
                DecimalPlaces = model.DecimalPlaces,
                DisplayOrder = model.DisplayOrder,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });

            // Currency must exist before its FX row (FK_ExchRate_From)
            await _uow.SaveChangesAsync();

            if (code != CurrencyConstants.BaseCurrency && model.RateToVnd is > 0)
            {
                await UpsertTodayRateAsync(code, model.RateToVnd.Value);
                await _uow.SaveChangesAsync();
            }

            _logger.LogInformation("Currency created: {Code}", code);
            return (true, null);
        }

        // ── Update ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(CurrencyFormViewModel model)
        {
            var repo = _uow.Repository<Currency>();
            var code = Normalize(model.CurrencyCode);
            var c = await repo.Query().FirstOrDefaultAsync(x => x.CurrencyCode == code);
            if (c == null)
                return (false, "Không tìm thấy tiền tệ");

            if (code == CurrencyConstants.BaseCurrency && !model.IsActive)
                return (false, "Không thể vô hiệu hóa VND vì đây là tiền tệ gốc của hệ thống");

            c.CurrencyName = model.CurrencyName.Trim();
            c.Symbol = string.IsNullOrWhiteSpace(model.Symbol) ? null : model.Symbol.Trim();
            c.DecimalPlaces = model.DecimalPlaces;
            c.DisplayOrder = model.DisplayOrder;
            c.IsActive = model.IsActive;
            c.UpdatedAt = DateTime.Now;
            repo.Update(c);

            // Only write a new FX row when the admin actually changed the rate
            if (code != CurrencyConstants.BaseCurrency && model.RateToVnd is > 0)
            {
                var latest = await GetLatestRateAsync(code);
                if (latest == null || latest.Rate != model.RateToVnd.Value)
                    await UpsertTodayRateAsync(code, model.RateToVnd.Value);
            }

            await _uow.SaveChangesAsync();

            _logger.LogInformation("Currency updated: {Code}", code);
            return (true, null);
        }

        // ── Delete ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(string code)
        {
            code = Normalize(code);
            if (code == CurrencyConstants.BaseCurrency)
                return (false, "Không thể xóa VND vì đây là tiền tệ gốc của hệ thống");

            var repo = _uow.Repository<Currency>();
            var c = await repo.Query().FirstOrDefaultAsync(x => x.CurrencyCode == code);
            if (c == null)
                return (false, "Không tìm thấy tiền tệ");

            // Guard: refuse to delete a currency still referenced by business data
            var inUse =
                await _uow.Repository<QuotationItem>().AnyAsync(i => i.PurchaseCurrency == code) ||
                await _uow.Repository<SalesOrderItem>().AnyAsync(i => i.PurchaseCurrency == code) ||
                await _uow.Repository<Quotation>().AnyAsync(q => q.Currency == code) ||
                await _uow.Repository<SalesOrder>().AnyAsync(s => s.Currency == code || s.PurchaseCostCurrency == code) ||
                await _uow.Repository<Product>().AnyAsync(p => p.DefaultPurchaseCurrency == code) ||
                await _uow.Repository<CustomerPrice>().AnyAsync(p => p.Currency == code);
            if (inUse)
                return (false, $"Không thể xóa: tiền tệ {code} đang được sử dụng trong báo giá, đơn hàng hoặc sản phẩm. " +
                               "Vui lòng vô hiệu hóa tiền tệ này để ẩn khỏi các danh sách chọn.");

            // FX history belongs to the currency — remove it first (FK_ExchRate_From/To)
            var fxRepo = _uow.Repository<ExchangeRate>();
            var fxRows = await fxRepo.Query()
                .Where(x => x.FromCurrency == code || x.ToCurrency == code)
                .ToListAsync();
            if (fxRows.Count > 0) fxRepo.RemoveRange(fxRows);

            repo.Remove(c);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Currency deleted: {Code} ({FxCount} FX rows)", code, fxRows.Count);
            return (true, null);
        }

        // ── Helpers ──────────────────────────────────────
        private async Task<ExchangeRate?> GetLatestRateAsync(string code) =>
            await _uow.Repository<ExchangeRate>().Query()
                .Where(x => x.FromCurrency == code && x.ToCurrency == CurrencyConstants.BaseCurrency)
                .OrderByDescending(x => x.EffectiveDate)
                .FirstOrDefaultAsync();

        // UQ_ExchRate (From, To, EffectiveDate) → one row per day; overwrite today's if present
        private async Task UpsertTodayRateAsync(string code, decimal rate)
        {
            var today = DateTime.Today;
            var fxRepo = _uow.Repository<ExchangeRate>();
            var row = await fxRepo.Query().FirstOrDefaultAsync(x =>
                x.FromCurrency == code && x.ToCurrency == CurrencyConstants.BaseCurrency && x.EffectiveDate == today);

            if (row == null)
            {
                await fxRepo.AddAsync(new ExchangeRate
                {
                    FromCurrency = code,
                    ToCurrency = CurrencyConstants.BaseCurrency,
                    Rate = rate,
                    EffectiveDate = today,
                    Source = ManualRateSource,
                    FetchedAt = DateTime.Now
                });
            }
            else
            {
                row.Rate = rate;
                row.Source = ManualRateSource;
                row.FetchedAt = DateTime.Now;
                fxRepo.Update(row);
            }
        }
    }
}
