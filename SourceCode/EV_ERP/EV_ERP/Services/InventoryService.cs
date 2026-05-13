using EV_ERP.Models.Entities.Inventory;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.ViewModels.Stock;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(IUnitOfWork uow, ILogger<InventoryService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // ── List (grouped by product) ────────────────────
        public async Task<InventoryListViewModel> GetListAsync(string? keyword, int? warehouseId, string? status)
        {
            var query = _uow.Repository<InventoryRecord>().Query()
                .Include(i => i.Product).ThenInclude(p => p.Category)
                .Include(i => i.Product).ThenInclude(p => p.Unit)
                .Include(i => i.Warehouse)
                .Include(i => i.Location)
                .AsQueryable();

            if (warehouseId.HasValue && warehouseId > 0)
                query = query.Where(i => i.WarehouseId == warehouseId);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(i =>
                    i.Product.ProductName.ToLower().Contains(kw) ||
                    i.Product.ProductCode.ToLower().Contains(kw) ||
                    (i.Product.Barcode != null && i.Product.Barcode.Contains(kw)));
            }

            var records = await query.ToListAsync();

            // Group by product
            var grouped = records
                .GroupBy(r => r.ProductId)
                .Select(g =>
                {
                    var first = g.First();
                    var totalOnHand = g.Sum(r => r.QuantityOnHand);
                    var totalReserved = g.Sum(r => r.QuantityReserved);
                    return new InventoryRowViewModel
                    {
                        ProductId = first.ProductId,
                        ProductCode = first.Product.ProductCode,
                        ProductName = first.Product.ProductName,
                        Barcode = first.Product.Barcode,
                        ImageUrl = first.Product.ImageUrl,
                        CategoryName = first.Product.Category?.CategoryName,
                        UnitName = first.Product.Unit.UnitName,
                        QuantityOnHand = totalOnHand,
                        QuantityReserved = totalReserved,
                        QuantityAvailable = totalOnHand - totalReserved,
                        MinStockLevel = first.Product.MinStockLevel,
                        WarehouseDetails = g.Select(r => new InventoryWarehouseDetail
                        {
                            WarehouseId = r.WarehouseId,
                            WarehouseName = r.Warehouse.WarehouseName,
                            LocationCode = r.Location?.LocationCode,
                            QuantityOnHand = r.QuantityOnHand,
                            QuantityReserved = r.QuantityReserved
                        }).ToList()
                    };
                })
                .ToList();

            // Filter by stock status
            if (status == "low")
                grouped = grouped.Where(g => g.IsLowStock).ToList();
            else if (status == "out")
                grouped = grouped.Where(g => g.QuantityOnHand <= 0).ToList();

            grouped = grouped.OrderBy(g => g.ProductCode).ToList();

            var warehouses = await _uow.Repository<Warehouse>().Query()
                .Where(w => w.IsActive)
                .OrderBy(w => w.WarehouseCode)
                .Select(w => new WarehouseOptionViewModel
                {
                    WarehouseId = w.WarehouseId,
                    WarehouseCode = w.WarehouseCode,
                    WarehouseName = w.WarehouseName
                }).ToListAsync();

            return new InventoryListViewModel
            {
                Records = grouped,
                SearchKeyword = keyword,
                FilterWarehouseId = warehouseId,
                FilterStatus = status,
                TotalProducts = grouped.Count,
                TotalQuantity = grouped.Sum(g => g.QuantityOnHand),
                Warehouses = warehouses
            };
        }

        // ── Product Inventory Detail ─────────────────────
        public async Task<ProductInventoryDetailViewModel?> GetProductDetailAsync(int productId)
        {
            var product = await _uow.Repository<Product>().Query()
                .Include(p => p.Unit)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null) return null;

            var inventoryRecords = await _uow.Repository<InventoryRecord>().Query()
                .Include(i => i.Warehouse)
                .Include(i => i.Location)
                .Where(i => i.ProductId == productId)
                .OrderBy(i => i.Warehouse.WarehouseCode)
                .ThenBy(i => i.Location != null ? i.Location.LocationCode : "")
                .ToListAsync();

            // Recent transactions for this product
            var recentTx = await _uow.Repository<StockTransactionItem>().Query()
                .Include(i => i.Transaction).ThenInclude(t => t.Warehouse)
                .Include(i => i.Transaction).ThenInclude(t => t.CreatedByUser)
                .Where(i => i.ProductId == productId && i.Transaction.Status != "CANCELLED")
                .OrderByDescending(i => i.Transaction.TransactionDate)
                .Take(20)
                .Select(i => new StockTransactionHistoryRow
                {
                    TransactionId = i.TransactionId,
                    TransactionNo = i.Transaction.TransactionNo,
                    TransactionType = i.Transaction.TransactionType,
                    WarehouseName = i.Transaction.Warehouse.WarehouseName,
                    Quantity = i.Quantity,
                    TransactionDate = i.Transaction.TransactionDate,
                    Status = i.Transaction.Status,
                    CreatedByName = i.Transaction.CreatedByUser.FullName
                })
                .ToListAsync();

            return new ProductInventoryDetailViewModel
            {
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                Barcode = product.Barcode,
                ImageUrl = product.ImageUrl,
                UnitName = product.Unit.UnitName,
                MinStockLevel = product.MinStockLevel,
                TotalOnHand = inventoryRecords.Sum(r => r.QuantityOnHand),
                TotalReserved = inventoryRecords.Sum(r => r.QuantityReserved),
                TotalAvailable = inventoryRecords.Sum(r => r.QuantityOnHand - r.QuantityReserved),
                LocationRecords = inventoryRecords.Select(r => new InventoryLocationRecord
                {
                    InventoryId = r.InventoryId,
                    WarehouseName = r.Warehouse.WarehouseName,
                    WarehouseCode = r.Warehouse.WarehouseCode,
                    LocationCode = r.Location?.LocationCode,
                    LocationName = r.Location?.LocationName,
                    QuantityOnHand = r.QuantityOnHand,
                    QuantityReserved = r.QuantityReserved,
                    QuantityAvailable = r.QuantityOnHand - r.QuantityReserved,
                    LastUpdatedAt = r.LastUpdatedAt
                }).ToList(),
                RecentTransactions = recentTx
            };
        }

        // ── SO Inventory Lookup (per-line receive progress) ──
        public async Task<SalesOrderInventoryLookupResult?> LookupSalesOrderInventoryAsync(string? soCode)
        {
            if (string.IsNullOrWhiteSpace(soCode)) return null;

            var code = soCode.Trim();

            var so = await _uow.Repository<SalesOrder>().Query()
                .Include(s => s.Customer)
                .Include(s => s.Items.OrderBy(i => i.SortOrder))
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.SalesOrderNo == code);

            if (so == null) return null;

            var lines = so.Items.Select(i => new SalesOrderInventoryLineRow
            {
                SOItemId = i.SOItemId,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                ProductCode = i.Product?.ProductCode,
                Barcode = i.Product?.Barcode,
                ImageUrl = i.Product?.ImageUrl ?? i.ImageUrl,
                UnitName = i.UnitName,
                Quantity = i.Quantity,
                ReceivedQty = i.ReceivedQty,
                DeliveredQty = i.DeliveredQty,
                CancelledQty = i.CancelledQty,
                RemainingReceiveQty = i.RemainingReceiveQty,
                InStockQty = i.InStockQty,
                ExpectedReceiveDate = i.ExpectedReceiveDate
            }).ToList();

            var result = new SalesOrderInventoryLookupResult
            {
                SalesOrderId = so.SalesOrderId,
                SalesOrderNo = so.SalesOrderNo,
                CustomerName = so.Customer?.CustomerName,
                OrderDate = so.OrderDate,
                ExpectedReceiveDate = so.ExpectedReceiveDate,
                Status = so.Status,
                StatusText = so.Status switch
                {
                    "DRAFT" => "Nháp",
                    "WAIT" => "Chờ tạm ứng",
                    "BUYING" => "Đang mua",
                    "RECEIVED" => "Đã nhận hàng",
                    "DELIVERING" => "Đang giao",
                    "DELIVERED" => "Đã giao",
                    "COMPLETED" => "Hoàn tất",
                    "RETURNED" => "Trả hàng",
                    "REPORTED" => "Đã báo cáo KQKD",
                    "CANCELLED" => "Đã hủy",
                    _ => so.Status
                },
                TotalLines = lines.Count,
                FullyReceivedLines = lines.Count(l => l.LineStatus == "RECEIVED"),
                PartialLines = lines.Count(l => l.LineStatus == "PARTIAL"),
                NotStartedLines = lines.Count(l => l.LineStatus == "NOT_STARTED"),
                CancelledLines = lines.Count(l => l.LineStatus == "CANCELLED"),
                TotalOrderedQty = lines.Sum(l => l.Quantity),
                TotalReceivedQty = lines.Sum(l => l.ReceivedQty),
                TotalRemainingQty = lines.Sum(l => l.RemainingReceiveQty),
                TotalCancelledQty = lines.Sum(l => l.CancelledQty),
                Lines = lines
            };

            return result;
        }

        // ── Quick Barcode Lookup ─────────────────────────
        public async Task<BarcodeLookupResult?> QuickLookupAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;

            var trimmed = barcode.Trim();
            var product = await _uow.Repository<Product>().Query()
                .Include(p => p.Unit)
                .Where(p => p.IsActive && (p.Barcode == trimmed || p.ProductCode == trimmed || p.SKU == trimmed))
                .FirstOrDefaultAsync();

            if (product == null) return null;

            var totalQty = await _uow.Repository<InventoryRecord>().Query()
                .Where(i => i.ProductId == product.ProductId)
                .SumAsync(i => i.QuantityOnHand);

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
                QuantityOnHand = totalQty
            };
        }
    }
}
