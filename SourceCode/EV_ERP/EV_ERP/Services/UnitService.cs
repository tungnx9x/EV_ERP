using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.ViewModels.Products;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class UnitService : IUnitService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<UnitService> _logger;

        public UnitService(IUnitOfWork uow, ILogger<UnitService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // ── List ─────────────────────────────────────────
        public async Task<UnitListViewModel> GetListAsync(string? keyword, string? status)
        {
            var units = await _uow.Repository<Unit>().Query()
                .Include(u => u.Products)
                .OrderBy(u => u.UnitName)
                .ToListAsync();

            bool? activeFilter = status == "active" ? true : status == "inactive" ? false : null;
            var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim().ToLower();

            var rows = units
                .Where(u => !activeFilter.HasValue || u.IsActive == activeFilter.Value)
                .Where(u => kw == null ||
                            u.UnitName.ToLower().Contains(kw) ||
                            u.UnitCode.ToLower().Contains(kw))
                .Select(u => new UnitRowViewModel
                {
                    UnitId = u.UnitId,
                    UnitCode = u.UnitCode,
                    UnitName = u.UnitName,
                    ProductCount = u.Products.Count,
                    IsActive = u.IsActive
                })
                .ToList();

            return new UnitListViewModel
            {
                Units = rows,
                SearchKeyword = keyword,
                FilterStatus = status,
                TotalCount = rows.Count
            };
        }

        // ── Form ─────────────────────────────────────────
        public async Task<UnitFormViewModel> GetFormAsync(int? unitId = null)
        {
            if (!unitId.HasValue || unitId <= 0)
                return new UnitFormViewModel();

            var unit = await _uow.Repository<Unit>().GetByIdAsync(unitId.Value);
            if (unit == null)
                return new UnitFormViewModel();

            return new UnitFormViewModel
            {
                UnitId = unit.UnitId,
                UnitCode = unit.UnitCode,
                UnitName = unit.UnitName,
                IsActive = unit.IsActive
            };
        }

        // ── Create ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> CreateAsync(UnitFormViewModel model)
        {
            var repo = _uow.Repository<Unit>();

            var code = model.UnitCode.Trim().ToUpper();
            var codeConflict = await repo.AnyAsync(u => u.UnitCode.ToUpper() == code);
            if (codeConflict)
                return (false, "Mã đơn vị này đã tồn tại");

            var unit = new Unit
            {
                UnitCode = code,
                UnitName = model.UnitName.Trim(),
                IsActive = model.IsActive
            };

            await repo.AddAsync(unit);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Unit created: {Code}", code);
            return (true, null);
        }

        // ── Update ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(UnitFormViewModel model)
        {
            if (!model.UnitId.HasValue)
                return (false, "UnitId không hợp lệ");

            var repo = _uow.Repository<Unit>();
            var unit = await repo.GetByIdAsync(model.UnitId.Value);
            if (unit == null)
                return (false, "Không tìm thấy đơn vị tính");

            var code = model.UnitCode.Trim().ToUpper();
            var codeConflict = await repo.AnyAsync(u =>
                u.UnitCode.ToUpper() == code &&
                u.UnitId != model.UnitId.Value);
            if (codeConflict)
                return (false, "Mã đơn vị này đã được sử dụng bởi đơn vị khác");

            unit.UnitCode = code;
            unit.UnitName = model.UnitName.Trim();
            unit.IsActive = model.IsActive;

            repo.Update(unit);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Unit updated: UnitId={Id}", model.UnitId);
            return (true, null);
        }

        // ── Delete ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(int unitId)
        {
            var repo = _uow.Repository<Unit>();
            var unit = await repo.GetByIdAsync(unitId);
            if (unit == null)
                return (false, "Không tìm thấy đơn vị tính");

            // Guard: refuse to delete a unit that is still referenced by products
            var inUse = await _uow.Repository<Product>()
                .AnyAsync(p => p.UnitId == unitId);
            if (inUse)
                return (false, "Không thể xóa: đơn vị đang được sử dụng bởi sản phẩm. " +
                               "Vui lòng chuyển các sản phẩm sang đơn vị khác hoặc vô hiệu hóa đơn vị này.");

            repo.Remove(unit);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Unit deleted: UnitId={Id} Code={Code}", unitId, unit.UnitCode);
            return (true, null);
        }
    }
}
