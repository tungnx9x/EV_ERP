using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Inventory;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.ViewModels.Stock;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class StockService : IStockService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<StockService> _logger;

        public StockService(IUnitOfWork uow, ILogger<StockService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // ── List ─────────────────────────────────────────
        public async Task<StockTransactionListViewModel> GetListAsync(
            string? keyword, string? type, string? status, int? warehouseId)
        {
            var query = _uow.Repository<StockTransaction>().Query()
                .Include(t => t.Warehouse)
                .Include(t => t.SalesOrder)
                .Include(t => t.CreatedByUser)
                .Include(t => t.Items)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(t => t.TransactionType == type);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);

            if (warehouseId.HasValue && warehouseId > 0)
                query = query.Where(t => t.WarehouseId == warehouseId);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(t =>
                    t.TransactionNo.ToLower().Contains(kw) ||
                    (t.SalesOrder != null && t.SalesOrder.SalesOrderNo.ToLower().Contains(kw)) ||
                    (t.Notes != null && t.Notes.ToLower().Contains(kw)));
            }

            var transactions = await query
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync();

            var warehouses = await _uow.Repository<Warehouse>().Query()
                .Where(w => w.IsActive)
                .OrderBy(w => w.WarehouseCode)
                .Select(w => new WarehouseOptionViewModel
                {
                    WarehouseId = w.WarehouseId,
                    WarehouseCode = w.WarehouseCode,
                    WarehouseName = w.WarehouseName
                }).ToListAsync();

            return new StockTransactionListViewModel
            {
                Transactions = transactions.Select(t => new StockTransactionRowViewModel
                {
                    TransactionId = t.TransactionId,
                    TransactionNo = t.TransactionNo,
                    TransactionType = t.TransactionType,
                    WarehouseName = t.Warehouse.WarehouseName,
                    SalesOrderNo = t.SalesOrder?.SalesOrderNo,
                    SalesOrderId = t.SalesOrderId,
                    TransactionDate = t.TransactionDate,
                    Status = t.Status,
                    ItemCount = t.Items.Count,
                    TotalQuantity = t.Items.Sum(i => i.Quantity),
                    CreatedByName = t.CreatedByUser.FullName,
                    CreatedAt = t.CreatedAt
                }).ToList(),
                SearchKeyword = keyword,
                FilterType = type,
                FilterStatus = status,
                FilterWarehouseId = warehouseId,
                TotalCount = transactions.Count,
                Warehouses = warehouses
            };
        }

        // ── Detail ───────────────────────────────────────
        public async Task<StockTransactionDetailViewModel?> GetDetailAsync(long transactionId)
        {
            var t = await _uow.Repository<StockTransaction>().Query()
                .Include(x => x.Warehouse)
                .Include(x => x.SalesOrder)
                .Include(x => x.DeliveryPerson)
                .Include(x => x.ConfirmedByUser)
                .Include(x => x.CreatedByUser)
                .Include(x => x.Items).ThenInclude(i => i.Product)
                .Include(x => x.Items).ThenInclude(i => i.Location)
                .FirstOrDefaultAsync(x => x.TransactionId == transactionId);

            if (t == null) return null;

            return new StockTransactionDetailViewModel
            {
                TransactionId = t.TransactionId,
                TransactionNo = t.TransactionNo,
                TransactionType = t.TransactionType,
                WarehouseId = t.WarehouseId,
                WarehouseName = t.Warehouse.WarehouseName,
                WarehouseCode = t.Warehouse.WarehouseCode,
                SalesOrderId = t.SalesOrderId,
                SalesOrderNo = t.SalesOrder?.SalesOrderNo,
                TransactionDate = t.TransactionDate,
                Status = t.Status,
                Notes = t.Notes,
                IsDropship = t.IsDropship,
                DeliveryPersonName = t.DeliveryPerson?.FullName,
                DeliveryPersonId = t.DeliveryPersonId,
                DeliveryNote = t.DeliveryNote,
                ReceiverName = t.ReceiverName,
                ReceiverPhone = t.ReceiverPhone,
                ReceivedSignatureUrl = t.ReceivedSignatureUrl,
                DeliveredAt = t.DeliveredAt,
                ConfirmedAt = t.ConfirmedAt,
                ConfirmedByName = t.ConfirmedByUser?.FullName,
                CreatedByName = t.CreatedByUser.FullName,
                CreatedAt = t.CreatedAt,
                Items = t.Items.Select(i => new StockTransactionItemViewModel
                {
                    TransItemId = i.TransItemId,
                    ProductId = i.ProductId,
                    ProductName = i.Product.ProductName,
                    ProductCode = i.Product.ProductCode,
                    Barcode = i.Barcode ?? i.Product.Barcode,
                    ImageUrl = i.Product.ImageUrl,
                    LocationCode = i.Location?.LocationCode,
                    LocationName = i.Location?.LocationName,
                    LocationId = i.LocationId,
                    Quantity = i.Quantity,
                    UnitName = i.UnitName,
                    Notes = i.Notes
                }).ToList()
            };
        }

        // ── Form ─────────────────────────────────────────
        public async Task<StockTransactionFormViewModel> GetFormAsync(
            long? transactionId = null, string? type = null, int? salesOrderId = null)
        {
            var warehouses = await _uow.Repository<Warehouse>().Query()
                .Where(w => w.IsActive)
                .OrderBy(w => w.WarehouseCode)
                .Select(w => new WarehouseOptionViewModel
                {
                    WarehouseId = w.WarehouseId,
                    WarehouseCode = w.WarehouseCode,
                    WarehouseName = w.WarehouseName
                }).ToListAsync();

            var deliveryPersons = await _uow.Repository<User>().Query()
                .Include(u => u.Role)
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new UserOptionViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    RoleCode = u.Role.RoleCode
                }).ToListAsync();

            // Edit mode
            if (transactionId.HasValue && transactionId > 0)
            {
                var t = await _uow.Repository<StockTransaction>().Query()
                    .Include(x => x.SalesOrder)
                    .Include(x => x.Items).ThenInclude(i => i.Product)
                    .Include(x => x.Items).ThenInclude(i => i.Location)
                    .FirstOrDefaultAsync(x => x.TransactionId == transactionId);

                if (t != null)
                {
                    var locations = await GetLocationOptionsAsync(t.WarehouseId);
                    return new StockTransactionFormViewModel
                    {
                        TransactionId = t.TransactionId,
                        TransactionType = t.TransactionType,
                        WarehouseId = t.WarehouseId,
                        SalesOrderId = t.SalesOrderId,
                        SalesOrderNo = t.SalesOrder?.SalesOrderNo,
                        TransactionDate = t.TransactionDate,
                        Notes = t.Notes,
                        IsDropship = t.IsDropship,
                        DeliveryPersonId = t.DeliveryPersonId,
                        DeliveryNote = t.DeliveryNote,
                        ReceiverName = t.ReceiverName,
                        ReceiverPhone = t.ReceiverPhone,
                        Items = t.Items.Select(i => new StockTransactionItemFormModel
                        {
                            TransItemId = i.TransItemId,
                            ProductId = i.ProductId,
                            ProductName = i.Product.ProductName,
                            ProductCode = i.Product.ProductCode,
                            Barcode = i.Barcode ?? i.Product.Barcode,
                            ImageUrl = i.Product.ImageUrl,
                            LocationId = i.LocationId,
                            Quantity = i.Quantity,
                            UnitName = i.UnitName,
                            Notes = i.Notes
                        }).ToList(),
                        Warehouses = warehouses,
                        Locations = locations,
                        DeliveryPersons = deliveryPersons
                    };
                }
            }

            // Pre-fill from Sales Order
            var form = new StockTransactionFormViewModel
            {
                TransactionType = type ?? "INBOUND",
                Warehouses = warehouses,
                DeliveryPersons = deliveryPersons
            };

            if (salesOrderId.HasValue && salesOrderId > 0)
            {
                var so = await _uow.Repository<SalesOrder>().Query()
                    .Include(s => s.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Unit)
                    .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);

                if (so != null)
                {
                    form.SalesOrderId = so.SalesOrderId;
                    form.SalesOrderNo = so.SalesOrderNo;
                    form.Items = so.Items
                        .Where(i => i.ProductId.HasValue)
                        .Select(i => new StockTransactionItemFormModel
                    {
                        ProductId = i.ProductId!.Value,
                        ProductName = i.ProductName,
                        ProductCode = i.Product!.ProductCode,
                        Barcode = i.Product.Barcode,
                        ImageUrl = i.Product.ImageUrl,
                        Quantity = type == "OUTBOUND" ? i.Quantity : i.Quantity,
                        UnitName = i.UnitName,
                    }).ToList();
                }
            }

            // Default warehouse
            if (warehouses.Count == 1)
                form.WarehouseId = warehouses[0].WarehouseId;

            if (form.WarehouseId > 0)
                form.Locations = await GetLocationOptionsAsync(form.WarehouseId);

            return form;
        }

        // ── Save (Create / Update) ───────────────────────
        public async Task<(bool Success, string? ErrorMessage, long? TransactionId)> SaveAsync(
            StockTransactionFormViewModel model, int userId)
        {
            try
            {
                if (model.Items == null || !model.Items.Any())
                    return (false, "Phiếu phải có ít nhất 1 sản phẩm", null);

                if (model.IsEditMode)
                {
                    // Update existing DRAFT
                    var entity = await _uow.Repository<StockTransaction>().Query()
                        .Include(t => t.Items)
                        .FirstOrDefaultAsync(t => t.TransactionId == model.TransactionId);

                    if (entity == null) return (false, "Không tìm thấy phiếu", null);
                    if (entity.Status != "DRAFT") return (false, "Chỉ có thể sửa phiếu ở trạng thái Nháp", null);

                    entity.WarehouseId = model.WarehouseId;
                    entity.TransactionDate = model.TransactionDate;
                    entity.Notes = model.Notes?.Trim();
                    entity.IsDropship = model.IsDropship;
                    entity.DeliveryPersonId = model.DeliveryPersonId;
                    entity.DeliveryNote = model.DeliveryNote?.Trim();
                    entity.ReceiverName = model.ReceiverName?.Trim();
                    entity.ReceiverPhone = model.ReceiverPhone?.Trim();

                    // Remove old items
                    foreach (var oldItem in entity.Items.ToList())
                        _uow.Repository<StockTransactionItem>().Remove(oldItem);

                    // Add new items
                    foreach (var item in model.Items)
                    {
                        entity.Items.Add(new StockTransactionItem
                        {
                            ProductId = item.ProductId,
                            LocationId = item.LocationId,
                            Barcode = item.Barcode,
                            Quantity = item.Quantity,
                            UnitName = item.UnitName,
                            Notes = item.Notes?.Trim()
                        });
                    }

                    _uow.Repository<StockTransaction>().Update(entity);
                    await _uow.SaveChangesAsync();
                    return (true, null, entity.TransactionId);
                }
                else
                {
                    // Create new
                    var transNo = await GenerateTransactionNoAsync();
                    var entity = new StockTransaction
                    {
                        TransactionNo = transNo,
                        TransactionType = model.TransactionType,
                        WarehouseId = model.WarehouseId,
                        SalesOrderId = model.SalesOrderId,
                        TransactionDate = model.TransactionDate,
                        Status = "DRAFT",
                        Notes = model.Notes?.Trim(),
                        IsDropship = model.IsDropship,
                        DeliveryPersonId = model.DeliveryPersonId,
                        DeliveryNote = model.DeliveryNote?.Trim(),
                        ReceiverName = model.ReceiverName?.Trim(),
                        ReceiverPhone = model.ReceiverPhone?.Trim(),
                        CreatedAt = DateTime.Now,
                        CreatedBy = userId
                    };

                    foreach (var item in model.Items)
                    {
                        entity.Items.Add(new StockTransactionItem
                        {
                            ProductId = item.ProductId,
                            LocationId = item.LocationId,
                            Barcode = item.Barcode,
                            Quantity = item.Quantity,
                            UnitName = item.UnitName,
                            Notes = item.Notes?.Trim()
                        });
                    }

                    await _uow.Repository<StockTransaction>().AddAsync(entity);
                    await _uow.SaveChangesAsync();
                    return (true, null, entity.TransactionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving stock transaction");
                return (false, "Lỗi hệ thống khi lưu phiếu kho", null);
            }
        }

        // ── Confirm Inbound (DRAFT → CONFIRMED) ─────────
        public async Task<(bool Success, string? ErrorMessage)> ConfirmInboundAsync(long transactionId, int userId)
        {
            try
            {
                var t = await _uow.Repository<StockTransaction>().Query()
                    .Include(x => x.Items)
                    .FirstOrDefaultAsync(x => x.TransactionId == transactionId);

                if (t == null) return (false, "Không tìm thấy phiếu");
                if (t.TransactionType != "INBOUND") return (false, "Phiếu không phải loại nhập kho");
                if (t.Status != "DRAFT") return (false, "Phiếu phải ở trạng thái Nháp");

                // Update inventory for each item
                foreach (var item in t.Items)
                {
                    await UpdateInventoryAsync(t.WarehouseId, item.ProductId, item.LocationId, item.Quantity);
                }

                t.Status = "CONFIRMED";
                t.ConfirmedAt = DateTime.Now;
                t.ConfirmedBy = userId;
                _uow.Repository<StockTransaction>().Update(t);

                // Update SO status if linked
                if (t.SalesOrderId.HasValue)
                {
                    var so = await _uow.Repository<SalesOrder>().GetByIdAsync(t.SalesOrderId.Value);
                    if (so != null && so.Status == "BUYING")
                    {
                        so.Status = "RECEIVED";
                        so.ReceivedAt = DateTime.Now;
                        _uow.Repository<SalesOrder>().Update(so);
                    }
                }

                await _uow.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming inbound {Id}", transactionId);
                return (false, "Lỗi hệ thống khi xác nhận nhập kho");
            }
        }

        // ── Start Delivery (OUTBOUND DRAFT → DELIVERING) ─
        public async Task<(bool Success, string? ErrorMessage)> StartDeliveryAsync(long transactionId, int userId)
        {
            try
            {
                var t = await _uow.Repository<StockTransaction>().Query()
                    .Include(x => x.Items)
                    .FirstOrDefaultAsync(x => x.TransactionId == transactionId);

                if (t == null) return (false, "Không tìm thấy phiếu");
                if (t.TransactionType != "OUTBOUND") return (false, "Phiếu không phải loại xuất kho");
                if (t.Status != "DRAFT") return (false, "Phiếu phải ở trạng thái Nháp");

                // Validate sufficient inventory
                foreach (var item in t.Items)
                {
                    var inv = await _uow.Repository<InventoryRecord>().Query()
                        .Where(i => i.WarehouseId == t.WarehouseId
                            && i.ProductId == item.ProductId
                            && i.LocationId == item.LocationId)
                        .FirstOrDefaultAsync();

                    var available = inv?.QuantityOnHand ?? 0;
                    if (available < item.Quantity)
                    {
                        var product = await _uow.Repository<Product>().GetByIdAsync(item.ProductId);
                        return (false, $"Sản phẩm '{product?.ProductName}' không đủ tồn kho (Tồn: {available:N0}, Cần: {item.Quantity:N0})");
                    }
                }

                // Decrease inventory
                foreach (var item in t.Items)
                {
                    await UpdateInventoryAsync(t.WarehouseId, item.ProductId, item.LocationId, -item.Quantity);
                }

                t.Status = "DELIVERING";
                _uow.Repository<StockTransaction>().Update(t);

                // Update SO status if linked
                if (t.SalesOrderId.HasValue)
                {
                    var so = await _uow.Repository<SalesOrder>().GetByIdAsync(t.SalesOrderId.Value);
                    if (so != null && so.Status == "RECEIVED")
                    {
                        so.Status = "DELIVERING";
                        so.DeliveringAt = DateTime.Now;
                        _uow.Repository<SalesOrder>().Update(so);
                    }
                }

                await _uow.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting delivery {Id}", transactionId);
                return (false, "Lỗi hệ thống khi bắt đầu giao hàng");
            }
        }

        // ── Confirm Delivered (DELIVERING → DELIVERED) ───
        public async Task<(bool Success, string? ErrorMessage)> ConfirmDeliveredAsync(
            long transactionId, DeliveryConfirmModel model, int userId)
        {
            try
            {
                var t = await _uow.Repository<StockTransaction>().Query()
                    .FirstOrDefaultAsync(x => x.TransactionId == transactionId);

                if (t == null) return (false, "Không tìm thấy phiếu");
                if (t.TransactionType != "OUTBOUND") return (false, "Phiếu không phải loại xuất kho");
                if (t.Status != "DELIVERING") return (false, "Phiếu phải đang ở trạng thái Đang giao");

                t.Status = "DELIVERED";
                t.ReceiverName = model.ReceiverName?.Trim() ?? t.ReceiverName;
                t.ReceiverPhone = model.ReceiverPhone?.Trim() ?? t.ReceiverPhone;
                t.DeliveryNote = model.DeliveryNote?.Trim() ?? t.DeliveryNote;
                t.DeliveredAt = DateTime.Now;
                t.ConfirmedAt = DateTime.Now;
                t.ConfirmedBy = userId;
                _uow.Repository<StockTransaction>().Update(t);

                // Update SO status if linked
                if (t.SalesOrderId.HasValue)
                {
                    var so = await _uow.Repository<SalesOrder>().GetByIdAsync(t.SalesOrderId.Value);
                    if (so != null && so.Status == "DELIVERING")
                    {
                        so.Status = "DELIVERED";
                        so.DeliveredAt = DateTime.Now;
                        _uow.Repository<SalesOrder>().Update(so);
                    }
                }

                await _uow.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming delivery {Id}", transactionId);
                return (false, "Lỗi hệ thống khi xác nhận giao hàng");
            }
        }

        // ── Cancel (DRAFT → CANCELLED) ──────────────────
        public async Task<(bool Success, string? ErrorMessage)> CancelAsync(long transactionId, int userId)
        {
            try
            {
                var t = await _uow.Repository<StockTransaction>().GetByIdAsync(transactionId);
                if (t == null) return (false, "Không tìm thấy phiếu");
                if (t.Status != "DRAFT") return (false, "Chỉ có thể hủy phiếu ở trạng thái Nháp");

                t.Status = "CANCELLED";
                _uow.Repository<StockTransaction>().Update(t);
                await _uow.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling transaction {Id}", transactionId);
                return (false, "Lỗi hệ thống khi hủy phiếu");
            }
        }

        // ── Create from Sales Order ──────────────────────
        public async Task<(bool Success, string? ErrorMessage, long? TransactionId)> CreateFromSalesOrderAsync(
            int salesOrderId, string transactionType, int userId)
        {
            try
            {
                var so = await _uow.Repository<SalesOrder>().Query()
                    .Include(s => s.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Unit)
                    .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);

                if (so == null) return (false, "Không tìm thấy đơn hàng", null);

                // Get default warehouse (first active)
                var defaultWarehouse = await _uow.Repository<Warehouse>().Query()
                    .Where(w => w.IsActive && !w.IsVirtual)
                    .OrderBy(w => w.WarehouseId)
                    .FirstOrDefaultAsync();

                if (defaultWarehouse == null)
                    return (false, "Chưa có kho nào được thiết lập. Vui lòng tạo kho trước.", null);

                var transNo = await GenerateTransactionNoAsync();
                var entity = new StockTransaction
                {
                    TransactionNo = transNo,
                    TransactionType = transactionType,
                    WarehouseId = defaultWarehouse.WarehouseId,
                    SalesOrderId = salesOrderId,
                    TransactionDate = DateTime.Today,
                    Status = "DRAFT",
                    Notes = $"Tạo tự động từ đơn hàng {so.SalesOrderNo}",
                    IsDropship = so.IsDropship,
                    CreatedAt = DateTime.Now,
                    CreatedBy = userId
                };

                // Copy delivery info from SO for OUTBOUND
                if (transactionType == "OUTBOUND")
                {
                    entity.ReceiverName = so.Customer?.CustomerName;
                }

                foreach (var item in so.Items.Where(i => i.ProductId.HasValue))
                {
                    entity.Items.Add(new StockTransactionItem
                    {
                        ProductId = item.ProductId!.Value,
                        Barcode = item.Product!.Barcode,
                        Quantity = item.Quantity,
                        UnitName = item.UnitName
                    });
                }

                await _uow.Repository<StockTransaction>().AddAsync(entity);
                await _uow.SaveChangesAsync();

                _logger.LogInformation("Auto-created {Type} {No} from SO {SoNo}",
                    transactionType, transNo, so.SalesOrderNo);

                return (true, null, entity.TransactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating stock transaction from SO {Id}", salesOrderId);
                return (false, "Lỗi hệ thống khi tạo phiếu kho", null);
            }
        }

        // ── Barcode Lookup ───────────────────────────────
        public async Task<BarcodeLookupResult?> LookupBarcodeAsync(string barcode, int? warehouseId = null)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;

            var trimmed = barcode.Trim();
            var product = await _uow.Repository<Product>().Query()
                .Include(p => p.Unit)
                .Where(p => p.IsActive &&
                    (p.Barcode == trimmed || p.ProductCode == trimmed || p.SKU == trimmed))
                .FirstOrDefaultAsync();

            if (product == null) return null;

            decimal qtyOnHand = 0;
            if (warehouseId.HasValue && warehouseId > 0)
            {
                qtyOnHand = await _uow.Repository<InventoryRecord>().Query()
                    .Where(i => i.ProductId == product.ProductId && i.WarehouseId == warehouseId)
                    .SumAsync(i => i.QuantityOnHand);
            }
            else
            {
                qtyOnHand = await _uow.Repository<InventoryRecord>().Query()
                    .Where(i => i.ProductId == product.ProductId)
                    .SumAsync(i => i.QuantityOnHand);
            }

            return new BarcodeLookupResult
            {
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                Barcode = product.Barcode,
                ImageUrl = product.ImageUrl,
                UnitName = product.Unit.UnitName,
                DefaultPurchasePrice = product.DefaultPurchasePrice,
                DefaultSalePrice = product.DefaultSalePrice,
                QuantityOnHand = qtyOnHand
            };
        }

        // ── Location Options ─────────────────────────────
        public async Task<List<LocationOptionViewModel>> GetLocationOptionsAsync(int warehouseId)
        {
            return await _uow.Repository<WarehouseLocation>().Query()
                .Where(l => l.WarehouseId == warehouseId && l.IsActive)
                .OrderBy(l => l.LocationCode)
                .Select(l => new LocationOptionViewModel
                {
                    LocationId = l.LocationId,
                    LocationCode = l.LocationCode,
                    LocationName = l.LocationName
                })
                .ToListAsync();
        }

        // ── Sales Order Lookup (for OUTBOUND linking) ────
        public async Task<object?> LookupSalesOrderAsync(string? soCode)
        {
            if (string.IsNullOrWhiteSpace(soCode)) return null;

            var so = await _uow.Repository<SalesOrder>().Query()
                .Include(s => s.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Unit)
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.SalesOrderNo == soCode.Trim());

            if (so == null) return null;

            if (so.Status != "RECEIVED")
                return null;

            return new
            {
                SalesOrderId = so.SalesOrderId,
                SalesOrderNo = so.SalesOrderNo,
                CustomerName = so.Customer?.CustomerName,
                Items = so.Items
                    .Where(i => i.ProductId.HasValue)
                    .Select(i => new
                    {
                        ProductId = i.ProductId!.Value,
                        ProductName = i.ProductName,
                        ProductCode = i.Product?.ProductCode ?? "",
                        Barcode = i.Product?.Barcode,
                        ImageUrl = i.Product?.ImageUrl,
                        Quantity = i.Quantity,
                        UnitName = i.UnitName
                    }).ToList()
            };
        }

        // ══ Private helpers ══════════════════════════════

        private async Task<string> GenerateTransactionNoAsync()
        {
            var prefix = $"STK-{DateTime.Now:yyyyMM}-";
            var lastNo = await _uow.Repository<StockTransaction>().Query()
                .Where(t => t.TransactionNo.StartsWith(prefix))
                .OrderByDescending(t => t.TransactionNo)
                .Select(t => t.TransactionNo)
                .FirstOrDefaultAsync();

            int nextSeq = 1;
            if (lastNo != null)
            {
                var seqStr = lastNo.Substring(prefix.Length);
                if (int.TryParse(seqStr, out var parsed))
                    nextSeq = parsed + 1;
            }

            return $"{prefix}{nextSeq:D3}";
        }

        private async Task UpdateInventoryAsync(int warehouseId, int productId, int? locationId, decimal quantityChange)
        {
            var inv = await _uow.Repository<InventoryRecord>().Query()
                .FirstOrDefaultAsync(i => i.WarehouseId == warehouseId
                    && i.ProductId == productId
                    && i.LocationId == locationId);

            if (inv != null)
            {
                inv.QuantityOnHand += quantityChange;
                if (inv.QuantityOnHand < 0) inv.QuantityOnHand = 0;
                inv.LastUpdatedAt = DateTime.Now;
                _uow.Repository<InventoryRecord>().Update(inv);
            }
            else if (quantityChange > 0)
            {
                // Create new inventory record
                var newInv = new InventoryRecord
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    LocationId = locationId,
                    QuantityOnHand = quantityChange,
                    QuantityReserved = 0,
                    LastUpdatedAt = DateTime.Now
                };
                await _uow.Repository<InventoryRecord>().AddAsync(newInv);
            }
        }
    }
}
