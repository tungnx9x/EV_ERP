using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Inventory;
using EV_ERP.Models.ViewModels.Stock;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class WarehouseService : IWarehouseService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<WarehouseService> _logger;

        public WarehouseService(IUnitOfWork uow, ILogger<WarehouseService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // ── List ─────────────────────────────────────────
        public async Task<WarehouseListViewModel> GetListAsync(string? keyword, string? status)
        {
            var query = _uow.Repository<Warehouse>().Query()
                .Include(w => w.Manager)
                .Include(w => w.Locations)
                .Where(w => status == "inactive" ? !w.IsActive : w.IsActive);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(w =>
                    w.WarehouseName.ToLower().Contains(kw) ||
                    w.WarehouseCode.ToLower().Contains(kw) ||
                    (w.Address != null && w.Address.ToLower().Contains(kw)));
            }

            var warehouses = await query.OrderBy(w => w.WarehouseCode).ToListAsync();

            return new WarehouseListViewModel
            {
                Warehouses = warehouses.Select(w => new WarehouseRowViewModel
                {
                    WarehouseId = w.WarehouseId,
                    WarehouseCode = w.WarehouseCode,
                    WarehouseName = w.WarehouseName,
                    Address = w.Address,
                    ManagerName = w.Manager?.FullName,
                    IsVirtual = w.IsVirtual,
                    IsActive = w.IsActive,
                    LocationCount = w.Locations.Count(l => l.IsActive),
                    CreatedAt = w.CreatedAt
                }).ToList(),
                SearchKeyword = keyword,
                FilterStatus = status,
                TotalCount = warehouses.Count
            };
        }

        // ── Form ─────────────────────────────────────────
        public async Task<WarehouseFormViewModel> GetFormAsync(int? warehouseId = null)
        {
            var managers = await GetManagerOptionsAsync();

            if (!warehouseId.HasValue || warehouseId <= 0)
                return new WarehouseFormViewModel { Managers = managers };

            var wh = await _uow.Repository<Warehouse>().GetByIdAsync(warehouseId.Value);
            if (wh == null)
                return new WarehouseFormViewModel { Managers = managers };

            return new WarehouseFormViewModel
            {
                WarehouseId = wh.WarehouseId,
                WarehouseCode = wh.WarehouseCode,
                WarehouseName = wh.WarehouseName,
                Address = wh.Address,
                ManagerId = wh.ManagerId,
                IsVirtual = wh.IsVirtual,
                Managers = managers
            };
        }

        // ── Create ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> CreateAsync(WarehouseFormViewModel model, int createdBy)
        {
            try
            {
                // Check duplicate code
                var exists = await _uow.Repository<Warehouse>().Query()
                    .AnyAsync(w => w.WarehouseCode == model.WarehouseCode.Trim());
                if (exists)
                    return (false, $"Mã kho '{model.WarehouseCode}' đã tồn tại");

                var entity = new Warehouse
                {
                    WarehouseCode = model.WarehouseCode.Trim().ToUpper(),
                    WarehouseName = model.WarehouseName.Trim(),
                    Address = model.Address?.Trim(),
                    ManagerId = model.ManagerId,
                    IsVirtual = model.IsVirtual,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                await _uow.Repository<Warehouse>().AddAsync(entity);
                await _uow.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating warehouse {Code}", model.WarehouseCode);
                return (false, "Lỗi hệ thống khi tạo kho");
            }
        }

        // ── Update ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(WarehouseFormViewModel model, int updatedBy)
        {
            try
            {
                var entity = await _uow.Repository<Warehouse>().GetByIdAsync(model.WarehouseId!.Value);
                if (entity == null)
                    return (false, "Không tìm thấy kho");

                // Check duplicate code (exclude self)
                var exists = await _uow.Repository<Warehouse>().Query()
                    .AnyAsync(w => w.WarehouseCode == model.WarehouseCode.Trim() && w.WarehouseId != entity.WarehouseId);
                if (exists)
                    return (false, $"Mã kho '{model.WarehouseCode}' đã tồn tại");

                entity.WarehouseCode = model.WarehouseCode.Trim().ToUpper();
                entity.WarehouseName = model.WarehouseName.Trim();
                entity.Address = model.Address?.Trim();
                entity.ManagerId = model.ManagerId;
                entity.IsVirtual = model.IsVirtual;

                _uow.Repository<Warehouse>().Update(entity);
                await _uow.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating warehouse {Id}", model.WarehouseId);
                return (false, "Lỗi hệ thống khi cập nhật kho");
            }
        }

        // ── Toggle Active ────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> ToggleActiveAsync(int warehouseId)
        {
            try
            {
                var entity = await _uow.Repository<Warehouse>().GetByIdAsync(warehouseId);
                if (entity == null)
                    return (false, "Không tìm thấy kho");

                entity.IsActive = !entity.IsActive;
                _uow.Repository<Warehouse>().Update(entity);
                await _uow.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling warehouse {Id}", warehouseId);
                return (false, "Lỗi hệ thống");
            }
        }

        // ── Detail ───────────────────────────────────────
        public async Task<WarehouseDetailViewModel?> GetDetailAsync(int warehouseId)
        {
            var wh = await _uow.Repository<Warehouse>().Query()
                .Include(w => w.Manager)
                .Include(w => w.Locations.OrderBy(l => l.LocationCode))
                .FirstOrDefaultAsync(w => w.WarehouseId == warehouseId);

            if (wh == null) return null;

            // Inventory summary
            var inventorySummary = await _uow.Repository<InventoryRecord>().Query()
                .Where(i => i.WarehouseId == warehouseId && i.QuantityOnHand > 0)
                .GroupBy(i => 1)
                .Select(g => new
                {
                    TotalProducts = g.Select(i => i.ProductId).Distinct().Count(),
                    TotalQty = g.Sum(i => i.QuantityOnHand)
                })
                .FirstOrDefaultAsync();

            return new WarehouseDetailViewModel
            {
                WarehouseId = wh.WarehouseId,
                WarehouseCode = wh.WarehouseCode,
                WarehouseName = wh.WarehouseName,
                Address = wh.Address,
                ManagerName = wh.Manager?.FullName,
                ManagerId = wh.ManagerId,
                IsVirtual = wh.IsVirtual,
                IsActive = wh.IsActive,
                CreatedAt = wh.CreatedAt,
                TotalProductCount = inventorySummary?.TotalProducts ?? 0,
                TotalQuantityOnHand = inventorySummary?.TotalQty ?? 0,
                Locations = wh.Locations.Select(l => new LocationRowViewModel
                {
                    LocationId = l.LocationId,
                    LocationCode = l.LocationCode,
                    LocationName = l.LocationName,
                    Zone = l.Zone,
                    Aisle = l.Aisle,
                    Rack = l.Rack,
                    Shelf = l.Shelf,
                    Bin = l.Bin,
                    MaxCapacity = l.MaxCapacity,
                    IsActive = l.IsActive
                }).ToList()
            };
        }

        // ── Save Location ────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> SaveLocationAsync(LocationFormModel model)
        {
            try
            {
                if (model.LocationId.HasValue && model.LocationId > 0)
                {
                    // Update
                    var entity = await _uow.Repository<WarehouseLocation>().GetByIdAsync(model.LocationId.Value);
                    if (entity == null) return (false, "Không tìm thấy vị trí");

                    var dupCode = await _uow.Repository<WarehouseLocation>().Query()
                        .AnyAsync(l => l.WarehouseId == model.WarehouseId
                            && l.LocationCode == model.LocationCode.Trim()
                            && l.LocationId != entity.LocationId);
                    if (dupCode) return (false, $"Mã vị trí '{model.LocationCode}' đã tồn tại trong kho này");

                    entity.LocationCode = model.LocationCode.Trim();
                    entity.LocationName = model.LocationName.Trim();
                    entity.Zone = model.Zone?.Trim();
                    entity.Aisle = model.Aisle?.Trim();
                    entity.Rack = model.Rack?.Trim();
                    entity.Shelf = model.Shelf?.Trim();
                    entity.Bin = model.Bin?.Trim();
                    entity.MaxCapacity = model.MaxCapacity;
                    entity.UpdatedAt = DateTime.Now;

                    _uow.Repository<WarehouseLocation>().Update(entity);
                }
                else
                {
                    // Create
                    var dupCode = await _uow.Repository<WarehouseLocation>().Query()
                        .AnyAsync(l => l.WarehouseId == model.WarehouseId
                            && l.LocationCode == model.LocationCode.Trim());
                    if (dupCode) return (false, $"Mã vị trí '{model.LocationCode}' đã tồn tại trong kho này");

                    var entity = new WarehouseLocation
                    {
                        WarehouseId = model.WarehouseId,
                        LocationCode = model.LocationCode.Trim(),
                        LocationName = model.LocationName.Trim(),
                        Zone = model.Zone?.Trim(),
                        Aisle = model.Aisle?.Trim(),
                        Rack = model.Rack?.Trim(),
                        Shelf = model.Shelf?.Trim(),
                        Bin = model.Bin?.Trim(),
                        MaxCapacity = model.MaxCapacity,
                        IsActive = true
                    };
                    await _uow.Repository<WarehouseLocation>().AddAsync(entity);
                }

                await _uow.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving location for warehouse {Id}", model.WarehouseId);
                return (false, "Lỗi hệ thống khi lưu vị trí");
            }
        }

        // ── Toggle Location Active ───────────────────────
        public async Task<(bool Success, string? ErrorMessage)> ToggleLocationActiveAsync(int locationId)
        {
            try
            {
                var entity = await _uow.Repository<WarehouseLocation>().GetByIdAsync(locationId);
                if (entity == null) return (false, "Không tìm thấy vị trí");

                entity.IsActive = !entity.IsActive;
                entity.UpdatedAt = DateTime.Now;
                _uow.Repository<WarehouseLocation>().Update(entity);
                await _uow.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling location {Id}", locationId);
                return (false, "Lỗi hệ thống");
            }
        }

        // ── Warehouse Options ────────────────────────────
        public async Task<List<WarehouseOptionViewModel>> GetWarehouseOptionsAsync()
        {
            return await _uow.Repository<Warehouse>().Query()
                .Where(w => w.IsActive)
                .OrderBy(w => w.WarehouseCode)
                .Select(w => new WarehouseOptionViewModel
                {
                    WarehouseId = w.WarehouseId,
                    WarehouseCode = w.WarehouseCode,
                    WarehouseName = w.WarehouseName
                })
                .ToListAsync();
        }

        // ── Private: Manager Options ─────────────────────
        private async Task<List<UserOptionViewModel>> GetManagerOptionsAsync()
        {
            return await _uow.Repository<User>().Query()
                .Include(u => u.Role)
                .Where(u => u.IsActive && (u.Role.RoleCode == "ADMIN" || u.Role.RoleCode == "MANAGER" || u.Role.RoleCode == "WAREHOUSE"))
                .OrderBy(u => u.FullName)
                .Select(u => new UserOptionViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    RoleCode = u.Role.RoleCode
                })
                .ToListAsync();
        }
    }
}
