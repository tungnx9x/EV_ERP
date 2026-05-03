using ClosedXML.Excel;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Inventory;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.Entities.System;
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
        private readonly IWebHostEnvironment _env;

        public StockService(IUnitOfWork uow, ILogger<StockService> logger, IWebHostEnvironment env)
        {
            _uow = uow;
            _logger = logger;
            _env = env;
        }

        // ── List ─────────────────────────────────────────
        public async Task<StockTransactionListViewModel> GetListAsync(
            string? keyword, string? type, string? status, int? warehouseId,
            int pageIndex = 1, int pageSize = 20)
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

            var totalCount = await query.CountAsync();

            var transactions = await query
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
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

            var rows = transactions.Select(t => new StockTransactionRowViewModel
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
            }).ToList();

            return new StockTransactionListViewModel
            {
                Paged = new Models.Common.PagedResult<StockTransactionRowViewModel>
                {
                    Items = rows,
                    TotalCount = totalCount,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                },
                SearchKeyword = keyword,
                FilterType = type,
                FilterStatus = status,
                FilterWarehouseId = warehouseId,
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

            var attachments = await _uow.Repository<Attachment>().Query()
                .Where(a => a.ReferenceType == "STOCK_TRANSACTION"
                    && a.ReferenceId == (int)t.TransactionId && a.IsActive)
                .OrderBy(a => a.UploadedAt)
                .Select(a => new StockAttachmentViewModel
                {
                    AttachmentId = a.AttachmentId,
                    FileUrl = a.FileUrl,
                    FileName = a.FileName,
                    Description = a.Description,
                    UploadedAt = a.UploadedAt,
                    UploadedByName = a.UploadedByUser.FullName
                }).ToListAsync();

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
                }).ToList(),
                Attachments = attachments
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
                    var attachments = await _uow.Repository<Attachment>().Query()
                        .Where(a => a.ReferenceType == "STOCK_TRANSACTION"
                            && a.ReferenceId == (int)t.TransactionId && a.IsActive)
                        .OrderBy(a => a.UploadedAt)
                        .Select(a => new StockAttachmentViewModel
                        {
                            AttachmentId = a.AttachmentId,
                            FileUrl = a.FileUrl,
                            FileName = a.FileName,
                            Description = a.Description,
                            UploadedAt = a.UploadedAt,
                            UploadedByName = a.UploadedByUser.FullName
                        }).ToListAsync();

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
                        Attachments = attachments,
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

                    // Sync attachments: deactivate removed ones
                    if (model.AttachmentIds.Any())
                    {
                        var existingAtts = await _uow.Repository<Attachment>().Query()
                            .Where(a => a.ReferenceType == "STOCK_TRANSACTION"
                                && a.ReferenceId == (int)entity.TransactionId && a.IsActive)
                            .ToListAsync();

                        foreach (var att in existingAtts)
                        {
                            if (!model.AttachmentIds.Contains(att.AttachmentId))
                                att.IsActive = false;
                        }
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

                    // Link temp attachments (ReferenceId=0) to the new transaction
                    if (model.AttachmentIds.Any())
                    {
                        var tempAtts = await _uow.Repository<Attachment>().Query()
                            .Where(a => model.AttachmentIds.Contains(a.AttachmentId)
                                && a.ReferenceType == "STOCK_TRANSACTION"
                                && a.ReferenceId == 0 && a.IsActive)
                            .ToListAsync();

                        foreach (var att in tempAtts)
                            att.ReferenceId = (int)entity.TransactionId;

                        await _uow.SaveChangesAsync();
                    }

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

        // ══════════════════════════════════════════════════
        // EXPORT DELIVERY RECEIPT (BBGN)
        // ══════════════════════════════════════════════════
        public async Task<(byte[] FileBytes, string FileName)?> ExportDeliveryReceiptAsync(long transactionId)
        {
            var txn = await _uow.Repository<StockTransaction>().Query()
                .Include(t => t.Warehouse)
                .Include(t => t.SalesOrder!)
                    .ThenInclude(so => so.Customer)
                .Include(t => t.SalesOrder!)
                    .ThenInclude(so => so.Items.OrderBy(i => i.SortOrder))
                .Include(t => t.Items.OrderBy(i => i.TransItemId))
                    .ThenInclude(i => i.Product)
                .Include(t => t.DeliveryPerson)
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            if (txn == null || txn.TransactionType != "OUTBOUND") return null;

            var templatePath = Path.Combine(_env.WebRootPath, "templates", "BBGN-template.xlsx");
            if (!File.Exists(templatePath)) return null;

            using var wb = new XLWorkbook(templatePath);
            var ws = wb.Worksheet(1);

            var so = txn.SalesOrder;
            var customer = so?.Customer;

            // ── Header info ──
            // R8: Customer PO reference
            ws.Cell("D8").Value = so?.CustomerPoNo ?? "";
            ws.Cell("F8").Value = customer?.CustomerName ?? "";

            // R11: BBGN number
            ws.Cell("I11").Value = txn.TransactionNo;
            // R12: Date
            ws.Cell("I12").Value = txn.TransactionDate;
            ws.Cell("I12").Style.DateFormat.Format = "dd/MM/yyyy";
            // R13: PXK number (same as transaction no)
            ws.Cell("I13").Value = txn.TransactionNo;

            // ── Bên A (Receiver) ──
            // R17: Receiver representative
            // R18: Contact person
            ws.Cell("C18").Value = txn.ReceiverName ?? "";
            // R19: Receiver rep for delivery
            // R20: Shipping address
            ws.Cell("C20").Value = so?.ShippingAddress ?? customer?.Address ?? "";

            // ── Bên B (Sender - EVH) ──
            // R18: Person in charge (sales person)
            if (so != null)
            {
                var salesPerson = await _uow.Repository<User>().GetByIdAsync(so.SalesPersonId);
                ws.Cell("G18").Value = salesPerson?.FullName ?? "";
            }
            // R19: Delivery person
            ws.Cell("G19").Value = txn.DeliveryPerson?.FullName ?? "";

            // ── Items ──
            int dataStartRow = 23; // Template data row
            int itemCount = txn.Items.Count;

            // Use SO items for price info if available, map by ProductId
            var soItemMap = so?.Items.ToDictionary(i => i.ProductId ?? 0, i => i) ?? new();

            // Insert extra rows if more than 2 items (template has 2 data rows: 23, 24)
            int templateDataRows = 2;
            if (itemCount > templateDataRows)
            {
                ws.Row(dataStartRow).InsertRowsBelow(itemCount - templateDataRows);
            }

            int totalQtyPo = 0;
            int totalQtyDelivered = 0;

            for (int i = 0; i < itemCount; i++)
            {
                var item = txn.Items.ElementAt(i);
                var row = dataStartRow + i;

                // Try to get SO item for price/description
                soItemMap.TryGetValue(item.ProductId, out var soItem);

                ws.Cell(row, 1).Value = i + 1;                                          // A: STT
                ws.Cell(row, 2).Value = item.Product?.ProductCode ?? "";                 // B: Mã SP
                ws.Cell(row, 3).Value = item.Product?.ProductName ?? "";                 // C: Tên sản phẩm
                ws.Cell(row, 4).Value = soItem?.ProductDescription ?? item.Product?.Description ?? ""; // D: Quy cách
                ws.Cell(row, 5).Value = "";                                              // E: Thương hiệu/Xuất xứ
                // F: Hình ảnh — skip (complex to embed)
                ws.Cell(row, 7).Value = item.UnitName;                                   // G: ĐVT
                ws.Cell(row, 8).Value = soItem?.UnitPrice ?? item.Product?.DefaultSalePrice ?? 0;  // H: Đơn giá
                ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";

                var lineTotal = (soItem?.UnitPrice ?? 0) * item.Quantity;
                ws.Cell(row, 9).Value = lineTotal;                                       // I: Thành tiền
                ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";

                var qtyPo = (int)(soItem?.Quantity ?? item.Quantity);
                ws.Cell(row, 10).Value = qtyPo;                                         // J: SL PO/Hợp đồng
                ws.Cell(row, 11).Value = (int)item.Quantity;                             // K: SL thực giao
                ws.Cell(row, 12).Value = (int)item.Quantity;                             // L: SL thực nhận (= giao by default)
                ws.Cell(row, 13).Value = item.Notes ?? soItem?.Notes ?? "";              // M: Ghi chú

                totalQtyPo += qtyPo;
                totalQtyDelivered += (int)item.Quantity;

                // Apply borders to data row
                ws.Range(row, 1, row, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(row, 1, row, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                ws.Range(row, 1, row, 13).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            // ── Total row ──
            int totalRow = dataStartRow + itemCount;
            // Merge A:I for "TỔNG SỐ LƯỢNG"
            ws.Range(totalRow, 1, totalRow, 9).Merge();
            ws.Cell(totalRow, 1).Value = "TỔNG SỐ LƯỢNG";
            ws.Cell(totalRow, 1).Style.Font.Bold = true;
            ws.Cell(totalRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(totalRow, 10).Value = totalQtyPo;
            ws.Cell(totalRow, 11).Value = totalQtyDelivered;
            ws.Range(totalRow, 1, totalRow, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(totalRow, 1, totalRow, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Cell(totalRow, 1).Style.Font.Bold = true;

            // ── Summary row (TỔNG SL KIỆN, KG/CBM, dates) ──
            int summaryRow = totalRow + 1;
            ws.Range(summaryRow, 1, summaryRow, 3).Merge();
            ws.Cell(summaryRow, 1).Value = "TỔNG SL KIỆN";
            ws.Range(summaryRow, 4, summaryRow, 5).Merge();
            ws.Cell(summaryRow, 4).Value = "TỔNG KG/ CBM";
            ws.Range(summaryRow, 6, summaryRow, 9).Merge();
            ws.Cell(summaryRow, 6).Value = "NGÀY DỰ KIẾN GIAO HÀNG";
            ws.Range(summaryRow, 10, summaryRow, 13).Merge();
            ws.Cell(summaryRow, 10).Value = "NGÀY NHẬN HÀNG";
            ws.Range(summaryRow, 1, summaryRow, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(summaryRow, 1, summaryRow, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Cell(summaryRow, 1).Style.Font.Bold = true;
            ws.Cell(summaryRow, 4).Style.Font.Bold = true;
            ws.Cell(summaryRow, 6).Style.Font.Bold = true;
            ws.Cell(summaryRow, 10).Style.Font.Bold = true;

            // Summary values row
            int summaryValRow = summaryRow + 1;
            ws.Range(summaryValRow, 1, summaryValRow, 3).Merge();
            ws.Range(summaryValRow, 4, summaryValRow, 5).Merge();
            ws.Range(summaryValRow, 6, summaryValRow, 9).Merge();
            ws.Cell(summaryValRow, 6).Value = so?.ExpectedDeliveryDate?.ToString("dd/MM/yyyy") ?? "";
            ws.Range(summaryValRow, 10, summaryValRow, 13).Merge();
            ws.Cell(summaryValRow, 10).Value = txn.DeliveredAt?.ToString("dd/MM/yyyy") ?? "";
            ws.Range(summaryValRow, 1, summaryValRow, 13).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(summaryValRow, 1, summaryValRow, 13).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // ── Confirmation text ──
            int textRow = summaryValRow + 1;
            ws.Range(textRow, 1, textRow, 13).Merge();
            ws.Cell(textRow, 1).Value = "Bên A xác nhận rằng Bên B đã giao cho Bên A đầy đủ số lượng hàng hóa, đúng quy cách và chất lượng theo như quy định trong Đơn hàng/ Hợp đồng giữa 02 bên. \nHai bên đồng ý và thống nhất ký xác nhận Biên bản giao nhận hàng hóa này. Biên bản này được lập thành 02 bản, mỗi bên giữ 01 bản có giá trị pháp lý như nhau.";
            ws.Cell(textRow, 1).Style.Alignment.WrapText = true;
            ws.Cell(textRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            ws.Row(textRow).Height = 45;

            // ── Signatures ──
            int signRow = textRow + 2;
            ws.Range(signRow, 1, signRow, 5).Merge();
            ws.Cell(signRow, 1).Value = "ĐẠI DIỆN BÊN NHẬN";
            ws.Cell(signRow, 1).Style.Font.Bold = true;
            ws.Cell(signRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(signRow, 6, signRow, 13).Merge();
            ws.Cell(signRow, 6).Value = "ĐẠI DIỆN BÊN GIAO";
            ws.Cell(signRow, 6).Style.Font.Bold = true;
            ws.Cell(signRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // ── Generate filename ──
            var safeCustomer = SanitizeFileName(customer?.CustomerName ?? "NA");
            var safePoNo = SanitizeFileName(so?.CustomerPoNo ?? txn.TransactionNo);
            var fileName = $"{txn.TransactionDate:yyyy.MM.dd}_EVH-{safeCustomer}-{safePoNo}_BBGN.xlsx";

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return (ms.ToArray(), fileName);
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("", name.Where(c => !invalid.Contains(c))).Trim();
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
