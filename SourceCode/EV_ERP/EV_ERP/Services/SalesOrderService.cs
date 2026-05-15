using ClosedXML.Excel;
using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.ViewModels.SalesOrders;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services;

public class SalesOrderService : ISalesOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SalesOrderService> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly string _storageRoot;
    private readonly ISlaService _slaService;
    private readonly IProductAttributeService _attrService;
    private readonly IAdvanceRequestService _advanceService;

    public SalesOrderService(IUnitOfWork uow, ILogger<SalesOrderService> logger,
        IWebHostEnvironment env, IConfiguration config, ISlaService slaService,
        IProductAttributeService attrService,
        IAdvanceRequestService advanceService)
    {
        _uow = uow;
        _logger = logger;
        _env = env;
        _storageRoot = config["FileStorage:RootPath"] ?? Path.Combine(env.ContentRootPath, "ERP_Files");
        _slaService = slaService;
        _attrService = attrService;
        _advanceService = advanceService;
    }

    // ══════════════════════════════════════════════════
    // LIST (paginated)
    // ══════════════════════════════════════════════════
    public async Task<SalesOrderListViewModel> GetListAsync(
        string? keyword, string? status, int? customerId, int? salesPersonId,
        int pageIndex = 1, int pageSize = 20)
    {
        var query = _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.SalesPerson)
            .Include(s => s.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(s => s.Status == status);

        if (customerId.HasValue && customerId > 0)
            query = query.Where(s => s.CustomerId == customerId);

        if (salesPersonId.HasValue && salesPersonId > 0)
            query = query.Where(s => s.SalesPersonId == salesPersonId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(s =>
                s.SalesOrderNo.ToLower().Contains(kw) ||
                s.Customer.CustomerName.ToLower().Contains(kw) ||
                s.Customer.CustomerCode.ToLower().Contains(kw) ||
                (s.CustomerPoNo != null && s.CustomerPoNo.ToLower().Contains(kw)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(s => s.OrderDate)
            .ThenByDescending(s => s.SalesOrderId)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SalesOrderRowViewModel
            {
                SalesOrderId = s.SalesOrderId,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                CustomerCode = s.Customer.CustomerCode,
                PurchaseSource = s.PurchaseSource,
                SalesPersonName = s.SalesPerson.FullName,
                OrderDate = s.OrderDate,
                Status = s.Status,
                TotalAmount = s.TotalAmount,
                PurchaseCost = s.PurchaseCost,
                Currency = s.Currency,
                ItemCount = s.Items.Count,
                CustomerPoNo = s.CustomerPoNo
            })
            .ToListAsync();

        return new SalesOrderListViewModel
        {
            Paged = new PagedResult<SalesOrderRowViewModel>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            },
            SearchKeyword = keyword,
            FilterStatus = status,
            FilterCustomerId = customerId,
            FilterSalesPersonId = salesPersonId,
            Customers = await GetCustomerOptionsAsync(),
            SalesPersons = await GetSalesPersonOptionsAsync()
        };
    }

    // ══════════════════════════════════════════════════
    // DETAIL
    // ══════════════════════════════════════════════════
    public async Task<SalesOrderDetailViewModel?> GetDetailAsync(int salesOrderId)
    {
        var s = await _uow.Repository<SalesOrder>().Query()
            .Include(x => x.Customer)
            .Include(x => x.Contact)
            .Include(x => x.Quotation)
            .Include(x => x.Rfq)
            .Include(x => x.SalesPerson)
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(x => x.SalesOrderId == salesOrderId);

        if (s == null) return null;

        var advanceSummary = await _advanceService.GetForSalesOrderAsync(salesOrderId);
        var advancedByItem = await _advanceService.GetAdvancedByItemAsync(salesOrderId);

        return new SalesOrderDetailViewModel
        {
            SalesOrderId = s.SalesOrderId,
            SalesOrderNo = s.SalesOrderNo,
            QuotationId = s.QuotationId,
            QuotationNo = s.Quotation?.QuotationNo,
            RfqId = s.RfqId,
            RfqNo = s.Rfq?.RfqNo,
            CustomerId = s.CustomerId,
            CustomerName = s.Customer.CustomerName,
            CustomerCode = s.Customer.CustomerCode,
            ContactName = s.Contact?.ContactName,
            ContactPhone = s.Contact?.Phone,
            OrderDate = s.OrderDate,
            ExpectedDeliveryDate = s.ExpectedDeliveryDate,
            Status = s.Status,
            CustomerPoNo = s.CustomerPoNo,
            CustomerPoFile = s.CustomerPoFile,
            PurchaseSource = s.PurchaseSource,
            ExpectedReceiveDate = s.ExpectedReceiveDate,
            BuyingNotes = s.BuyingNotes,
            BuyingAt = s.BuyingAt,
            ReceivedAt = s.ReceivedAt,
            AdvanceAmount = s.AdvanceAmount,
            AdvanceStatus = s.AdvanceStatus,
            AdvanceApprovedAt = s.AdvanceApprovedAt,
            AdvanceReceivedAt = s.AdvanceReceivedAt,
            SubTotal = s.SubTotal,
            DiscountType = s.DiscountType,
            DiscountValue = s.DiscountValue,
            DiscountAmount = s.DiscountAmount,
            TaxRate = s.TaxRate,
            TaxAmount = s.TaxAmount,
            TotalAmount = s.TotalAmount,
            Currency = s.Currency,
            PaymentTermDays = s.PaymentTermDays,
            PaymentDueDate = s.PaymentDueDate,
            PurchaseCost = s.PurchaseCost,
            IsDropship = s.IsDropship,
            DropshipAddress = s.DropshipAddress,
            ShippingAddress = s.ShippingAddress,
            Notes = s.Notes,
            InternalNotes = s.InternalNotes,
            SalesPersonId = s.SalesPersonId,
            SalesPersonName = s.SalesPerson.FullName,
            ActualCost = s.ActualCost,
            SettlementNotes = s.SettlementNotes,
            DeliveringAt = s.DeliveringAt,
            DeliveredAt = s.DeliveredAt,
            CompletedAt = s.CompletedAt,
            ReportedAt = s.ReportedAt,
            CancelledAt = s.CancelledAt,
            CancelReason = s.CancelReason,
            ReturnedAt = s.ReturnedAt,
            ReturnReason = s.ReturnReason,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            Items = s.Items.Select(i => new SalesOrderItemDetailViewModel
            {
                SOItemId = i.SOItemId,
                ProductId = i.ProductId ?? 0,
                ProductName = i.ProductName,
                ProductDescription = i.ProductDescription,
                ImageUrl = i.ImageUrl,
                UnitName = i.UnitName,
                Quantity = i.Quantity,
                ReceivedQty = i.ReceivedQty,
                DeliveredQty = i.DeliveredQty,
                RemainingReceiveQty = i.RemainingReceiveQty,
                InStockQty = i.InStockQty,
                RemainingDeliverQty = i.RemainingDeliverQty,
                ExpectedReceiveDate = i.ExpectedReceiveDate,
                ExpectedDeliveryDate = i.ExpectedDeliveryDate,
                CancelledQty = i.CancelledQty,
                CancelReason = i.CancelReason,
                CancelledAt = i.CancelledAt,
                UnitPrice = i.UnitPrice,
                DiscountType = i.DiscountType,
                DiscountValue = i.DiscountValue,
                DiscountAmount = i.DiscountAmount,
                LineTotal = i.LineTotal,
                PurchasePrice = i.PurchasePrice,
                LineCost = i.LineCost,
                SourceUrl = i.SourceUrl,
                SourceName = i.SourceName,
                Notes = i.Notes,
                IsProductMapped = i.IsProductMapped,
                AdvancedAmount = advancedByItem.TryGetValue(i.SOItemId, out var adv) ? adv : 0
            }).ToList(),
            AdvanceSummary = advanceSummary
        };
    }

    public async Task<int?> GetSalesPersonIdAsync(int salesOrderId)
    {
        return await _uow.Repository<SalesOrder>().Query()
            .Where(s => s.SalesOrderId == salesOrderId)
            .Select(s => (int?)s.SalesPersonId)
            .FirstOrDefaultAsync();
    }

    // ══════════════════════════════════════════════════
    // AUTO-CREATE FROM QUOTATION
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage, int? SalesOrderId)> CreateFromQuotationAsync(
        int quotationId, int userId)
    {
        var q = await _uow.Repository<Quotation>().Query()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.QuotationId == quotationId);

        if (q == null) return (false, "Không tìm thấy báo giá", null);
        if (q.Status != "APPROVED") return (false, "Báo giá chưa được duyệt", null);

        // Check if SO already exists for this quotation
        var existing = await _uow.Repository<SalesOrder>().Query()
            .AnyAsync(s => s.QuotationId == quotationId);
        if (existing) return (false, "Đơn hàng đã được tạo cho báo giá này", null);

        var soNo = q.QuotationNo.StartsWith("BG-")
            ? "SO-" + q.QuotationNo.Substring(3)
            : await GenerateSalesOrderNoAsync();

        var so = new SalesOrder
        {
            SalesOrderNo = soNo,
            QuotationId = q.QuotationId,
            RfqId = q.RfqId,
            CustomerId = q.CustomerId,
            ContactId = q.ContactId,
            OrderDate = DateTime.Today,
            ExpectedDeliveryDate = null,
            Status = "DRAFT",
            SubTotal = q.SubTotal,
            DiscountType = q.DiscountType,
            DiscountValue = q.DiscountValue,
            DiscountAmount = q.DiscountAmount,
            TaxRate = q.TaxRate,
            TaxAmount = q.TaxAmount,
            TotalAmount = q.TotalAmount,
            Currency = q.Currency,
            Notes = q.Notes,
            InternalNotes = q.InternalNotes,
            SalesPersonId = q.SalesPersonId,
            CreatedBy = userId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            Items = q.Items.Select(i => new SalesOrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                ProductDescription = i.ProductDescription,
                ImageUrl = i.ImageUrl,
                UnitName = i.UnitName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                PurchasePrice = i.PurchasePrice,
                ShippingFee = i.ShippingFee,
                Coefficient = i.Coefficient,
                DiscountType = i.DiscountType,
                DiscountValue = i.DiscountValue,
                DiscountAmount = i.DiscountAmount,
                LineTotal = i.LineTotal,
                TaxRate = i.TaxRate,
                TaxAmount = i.TaxAmount,
                LineTotalWithTax = i.LineTotalWithTax,
                SourceUrl = i.SourceUrl,
                SourceName = i.SourceName,
                SortOrder = i.SortOrder,
                Notes = i.Notes,
                IsProductMapped = i.IsProductMapped
            }).ToList()
        };

        await _uow.Repository<SalesOrder>().AddAsync(so);
        await _uow.SaveChangesAsync();

        // SLA: start tracking DRAFT
        await _slaService.StartTrackingAsync("SALES_ORDER", so.SalesOrderId, "DRAFT", q.SalesPersonId);

        _logger.LogInformation("SO created from Quotation: {SONo} ← {QNo} by UserId={UserId}",
            soNo, q.QuotationNo, userId);

        return (true, null, so.SalesOrderId);
    }

    // ══════════════════════════════════════════════════
    // STATUS TRANSITIONS
    // ══════════════════════════════════════════════════

    // DRAFT → WAIT (gửi DNTU — PO KH đã được lưu trước đó)
    public async Task<(bool Success, string? ErrorMessage)> SubmitWaitAsync(
        int salesOrderId, SalesOrderDraftModel? model, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "DRAFT") return (false, "Chỉ có thể gửi đề nghị tạm ứng ở trạng thái Nháp");

        // Validate: all items must be mapped to a real product
        var unmappedCount = so.Items.Count(i => !i.IsProductMapped);
        if (unmappedCount > 0)
            return (false, $"Còn {unmappedCount} sản phẩm chưa được gắn vào hệ thống. Vui lòng hoàn thiện thông tin sản phẩm trước khi gửi DNTU.");

        // Validate: PO info must be filled
        if (string.IsNullOrWhiteSpace(so.CustomerPoNo))
            return (false, "Vui lòng nhập Mã PO khách hàng trước khi gửi DNTU.");
        if (string.IsNullOrWhiteSpace(so.CustomerPoFile))
            return (false, "Vui lòng upload File PO khách hàng trước khi gửi DNTU.");

        so.AdvanceStatus = so.AdvanceAmount > 0 ? "PENDING" : null;
        so.Status = "WAIT";
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        // SLA: complete DRAFT, start WAIT
        await _slaService.CompleteTrackingAsync("SALES_ORDER", salesOrderId, "DRAFT");
        await _slaService.StartTrackingAsync("SALES_ORDER", salesOrderId, "WAIT", so.CreatedBy);

        _logger.LogInformation("SO submitted to WAIT: {No} by UserId={UserId}", so.SalesOrderNo, userId);
        return (true, null);
    }

    // WAIT → BUYING (per-line: chọn nguồn, nhập giá mua, ngày dự kiến từng dòng)
    public async Task<(bool Success, string? ErrorMessage)> StartBuyingAsync(
        int salesOrderId, SalesOrderBuyingModel model, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);

        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "WAIT") return (false, "Chỉ có thể bắt đầu mua hàng ở trạng thái Chờ tạm ứng");
        if (model.Items == null || model.Items.Count == 0)
            return (false, "Vui lòng nhập thông tin mua cho ít nhất 1 dòng");
        if (!model.Items.Any(i => i.IncludeInBatch))
            return (false, "Vui lòng tích chọn ít nhất 1 dòng để bắt đầu mua");

        so.PurchaseSource = model.PurchaseSource?.Trim();
        so.BuyingNotes = model.BuyingNotes?.Trim();
        so.BuyingAt = DateTime.Now;
        so.Status = "BUYING";
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        // Update purchase prices + per-line expected date for every line the user touched.
        // Lines NOT checked: still persist Source/Price if provided, but skip ExpectedReceiveDate
        // (they stay NOT_STARTED until user opens "Bắt đầu mua dòng này" later).
        decimal totalCost = 0;
        DateTime? earliestExpected = null;

        foreach (var itemModel in model.Items)
        {
            var item = so.Items.FirstOrDefault(i => i.SOItemId == itemModel.SOItemId);
            if (item == null) continue;

            item.PurchasePrice = itemModel.PurchasePrice;
            item.SourceUrl = itemModel.SourceUrl?.Trim();
            item.SourceName = itemModel.SourceName?.Trim();
            item.LineCost = item.Quantity * itemModel.PurchasePrice;

            if (itemModel.IncludeInBatch)
            {
                item.ExpectedReceiveDate = itemModel.ExpectedReceiveDate;
                if (itemModel.ExpectedReceiveDate.HasValue &&
                    (earliestExpected == null || itemModel.ExpectedReceiveDate < earliestExpected))
                {
                    earliestExpected = itemModel.ExpectedReceiveDate;
                }
            }

            totalCost += item.LineCost ?? 0;
        }
        so.PurchaseCost = totalCost;
        so.ExpectedReceiveDate = earliestExpected;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        // SLA: complete WAIT, start BUYING
        await _slaService.CompleteTrackingAsync("SALES_ORDER", salesOrderId, "WAIT");
        await _slaService.StartTrackingAsync("SALES_ORDER", salesOrderId, "BUYING", so.CreatedBy);

        _logger.LogInformation("SO started buying: {No} by UserId={UserId} (lines={Count})",
            so.SalesOrderNo, userId, model.Items.Count(i => i.IncludeInBatch));
        return (true, null);
    }

    // ══════════════════════════════════════════════════
    // ROLL-UP STATUS (v2.2/2.3) — derive SO.Status from line quantities.
    // Called from StockService after each INBOUND CONFIRMED / OUTBOUND DELIVERED.
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? NewStatus)> RollUpStatusAsync(int salesOrderId, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);
        if (so == null) return (false, null);

        // Don't auto-roll out of DRAFT/WAIT/CANCELLED — those are user-driven.
        if (so.Status is "DRAFT" or "WAIT" or "CANCELLED" or "COMPLETED" or "RETURNED" or "REPORTED")
            return (true, so.Status);

        var lines = so.Items.ToList();
        if (lines.Count == 0) return (true, so.Status);

        // EffectiveQty = Quantity - CancelledQty
        decimal totalEff = lines.Sum(i => i.Quantity - i.CancelledQty);
        decimal totalRecv = lines.Sum(i => i.ReceivedQty);
        decimal totalDeliv = lines.Sum(i => i.DeliveredQty);

        // If everything cancelled, leave it for the user (don't auto-cancel).
        if (totalEff <= 0) return (true, so.Status);

        string newStatus;
        if (totalDeliv >= totalEff) newStatus = "DELIVERED";
        else if (totalDeliv > 0) newStatus = "DELIVERING";
        else if (totalRecv >= totalEff) newStatus = "RECEIVED";
        else newStatus = "BUYING";

        if (newStatus == so.Status) return (true, so.Status);

        var oldStatus = so.Status;
        so.Status = newStatus;
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        // Stamp first-time timestamps
        if (newStatus == "RECEIVED" && so.ReceivedAt == null) so.ReceivedAt = DateTime.Now;
        if (newStatus == "DELIVERING" && so.DeliveringAt == null) so.DeliveringAt = DateTime.Now;
        if (newStatus == "DELIVERED" && so.DeliveredAt == null) so.DeliveredAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        // SLA transitions
        await _slaService.CompleteTrackingAsync("SALES_ORDER", salesOrderId, oldStatus);
        await _slaService.StartTrackingAsync("SALES_ORDER", salesOrderId, newStatus, so.CreatedBy);

        _logger.LogInformation("SO {No} roll-up: {Old} → {New} (recv={Recv}/{Eff}, deliv={Deliv})",
            so.SalesOrderNo, oldStatus, newStatus, totalRecv, totalEff, totalDeliv);
        return (true, newStatus);
    }

    // ══════════════════════════════════════════════════
    // PER-LINE: Update purchase info (BUYING+)
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage)> UpdateItemPurchaseInfoAsync(
        int salesOrderId, int soItemId, UpdateItemPurchaseModel model, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status is "DRAFT" or "WAIT" or "CANCELLED" or "COMPLETED" or "REPORTED")
            return (false, "Chỉ sửa được khi đang trong luồng mua/giao");

        var item = so.Items.FirstOrDefault(i => i.SOItemId == soItemId);
        if (item == null) return (false, "Không tìm thấy dòng sản phẩm");
        if (item.CancelledQty >= item.Quantity) return (false, "Dòng đã hủy");

        if (model.PurchasePrice.HasValue)
        {
            item.PurchasePrice = model.PurchasePrice;
            item.LineCost = item.Quantity * model.PurchasePrice.Value;
        }
        if (model.SourceUrl != null) item.SourceUrl = model.SourceUrl.Trim();
        if (model.SourceName != null) item.SourceName = model.SourceName.Trim();
        if (model.ExpectedReceiveDate.HasValue) item.ExpectedReceiveDate = model.ExpectedReceiveDate;
        if (model.ExpectedDeliveryDate.HasValue) item.ExpectedDeliveryDate = model.ExpectedDeliveryDate;
        if (model.Notes != null) item.Notes = model.Notes.Trim();

        // Recompute SO PurchaseCost from sum of line costs (alive lines only)
        so.PurchaseCost = so.Items.Where(i => i.CancelledQty < i.Quantity)
                                  .Sum(i => i.LineCost ?? 0);
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();
        return (true, null);
    }

    // ══════════════════════════════════════════════════
    // PER-LINE: Cancel (1 phần hoặc toàn bộ SL còn lại)
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage)> CancelItemAsync(
        int salesOrderId, int soItemId, CancelItemModel model, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status is "CANCELLED" or "COMPLETED" or "REPORTED" or "RETURNED")
            return (false, "Không thể hủy dòng ở trạng thái này");

        var item = so.Items.FirstOrDefault(i => i.SOItemId == soItemId);
        if (item == null) return (false, "Không tìm thấy dòng sản phẩm");

        // Bao nhiêu chưa nhập về thì có thể hủy. Đã nhận về rồi → không cho hủy (phải đi luồng trả hàng).
        var maxCancelable = item.Quantity - item.CancelledQty - item.ReceivedQty;
        if (maxCancelable <= 0) return (false, "Dòng đã nhận hết, không thể hủy nữa");

        var qty = model.CancelQty ?? maxCancelable;
        if (qty <= 0) return (false, "Số lượng hủy phải lớn hơn 0");
        if (qty > maxCancelable)
            return (false, $"Tối đa có thể hủy {maxCancelable:N3} (chưa nhập về)");

        item.CancelledQty += qty;
        if (string.IsNullOrWhiteSpace(item.CancelReason))
            item.CancelReason = model.Reason?.Trim();
        else if (!string.IsNullOrWhiteSpace(model.Reason))
            item.CancelReason += " | " + model.Reason.Trim();
        item.CancelledAt = DateTime.Now;
        item.CancelledBy = userId;

        // Recompute SO PurchaseCost (alive lines)
        so.PurchaseCost = so.Items.Where(i => i.CancelledQty < i.Quantity)
                                  .Sum(i => (i.Quantity - i.CancelledQty) * (i.PurchasePrice ?? 0));

        // DRAFT/WAIT: sell-side totals not locked yet → recompute from alive (proportional) line totals
        if (so.Status is "DRAFT" or "WAIT")
        {
            decimal subTotal = 0;
            foreach (var line in so.Items)
            {
                if (line.Quantity <= 0) continue;
                var aliveQty = line.Quantity - line.CancelledQty;
                if (aliveQty <= 0) continue;
                subTotal += line.LineTotal * aliveQty / line.Quantity;
            }

            decimal orderDiscount;
            if (so.DiscountType == null || !so.DiscountValue.HasValue || so.DiscountValue <= 0)
                orderDiscount = 0;
            else if (so.DiscountType == "PERCENT")
                orderDiscount = Math.Round(subTotal * so.DiscountValue.Value / 100m, 0);
            else
                orderDiscount = Math.Min(so.DiscountValue.Value, subTotal);

            var afterDiscount = subTotal - orderDiscount;
            var taxAmount = Math.Round(afterDiscount * so.TaxRate / 100m, 0);

            so.SubTotal = subTotal;
            so.DiscountAmount = orderDiscount;
            so.TaxAmount = taxAmount;
            so.TotalAmount = afterDiscount + taxAmount;
        }

        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        // Auto-cancel whole SO when every line is fully cancelled — customer dropped everything.
        bool allCancelled = so.Items.Count > 0 && so.Items.All(i => i.CancelledQty >= i.Quantity);
        if (allCancelled && so.Status != "CANCELLED")
        {
            var oldStatus = so.Status;
            so.Status = "CANCELLED";
            so.CancelledAt = DateTime.Now;
            if (string.IsNullOrWhiteSpace(so.CancelReason))
                so.CancelReason = "Tự động hủy: tất cả sản phẩm đã được bỏ khỏi đơn";
            so.UpdatedBy = userId;
            so.UpdatedAt = DateTime.Now;
            _uow.Repository<SalesOrder>().Update(so);
            await _uow.SaveChangesAsync();

            await _slaService.SkipTrackingAsync("SALES_ORDER", salesOrderId);

            _logger.LogInformation("SO {No} auto-cancelled (all lines cancelled): {Old} → CANCELLED by UserId={UserId}",
                so.SalesOrderNo, oldStatus, userId);
        }
        else
        {
            // Roll up — cancelling may push to RECEIVED/DELIVERED if remaining lines are complete
            await RollUpStatusAsync(salesOrderId, userId);
        }

        _logger.LogInformation("SO {No} cancel line {Item}: qty={Qty} by UserId={UserId}",
            so.SalesOrderNo, item.ProductName, qty, userId);
        return (true, null);
    }

    // DELIVERED → RETURNED (khách trả hàng)
    public async Task<(bool Success, string? ErrorMessage)> ReturnAsync(
        int salesOrderId, SalesOrderReturnModel model, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "DELIVERED") return (false, "Chỉ có thể trả hàng ở trạng thái Đã giao");

        so.Status = "RETURNED";
        so.ReturnedAt = DateTime.Now;
        so.ReturnReason = model.ReturnReason?.Trim();
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        // SLA: complete DELIVERED, start RETURNED
        await _slaService.CompleteTrackingAsync("SALES_ORDER", salesOrderId, "DELIVERED");
        await _slaService.StartTrackingAsync("SALES_ORDER", salesOrderId, "RETURNED", so.CreatedBy);

        _logger.LogInformation("SO returned: {No} Reason={Reason} by UserId={UserId}",
            so.SalesOrderNo, model.ReturnReason, userId);
        return (true, null);
    }

    // DELIVERED|RETURNED → COMPLETED (quyết toán + RFQ auto-complete)
    public async Task<(bool Success, string? ErrorMessage)> CompleteAsync(
        int salesOrderId, SalesOrderCompleteModel model, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status is not "DELIVERED" and not "RETURNED")
            return (false, "Chỉ có thể hoàn tất ở trạng thái Đã giao hoặc Trả hàng");

        so.Status = "COMPLETED";
        so.ActualCost = model.ActualCost;
        so.SettlementNotes = model.SettlementNotes?.Trim();
        so.CompletedAt = DateTime.Now;
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);

        // Auto-complete RFQ
        if (so.RfqId.HasValue)
        {
            var rfq = await _uow.Repository<RFQ>().GetByIdAsync(so.RfqId.Value);
            if (rfq != null && rfq.Status == "INPROGRESS")
            {
                rfq.Status = "COMPLETED";
                rfq.CompletedAt = DateTime.Now;
                rfq.UpdatedAt = DateTime.Now;
                _uow.Repository<RFQ>().Update(rfq);
                _logger.LogInformation("RFQ auto-completed: {No} from SO {SONo}",
                    rfq.RfqNo, so.SalesOrderNo);
            }
        }

        await _uow.SaveChangesAsync();

        // SLA: complete DELIVERED or RETURNED tracking
        await _slaService.CompleteTrackingAsync("SALES_ORDER", salesOrderId, "DELIVERED");
        await _slaService.CompleteTrackingAsync("SALES_ORDER", salesOrderId, "RETURNED");

        _logger.LogInformation("SO completed: {No} by UserId={UserId}", so.SalesOrderNo, userId);
        return (true, null);
    }

    // COMPLETED → REPORTED (nộp báo cáo KQKD)
    public async Task<(bool Success, string? ErrorMessage)> ReportAsync(int salesOrderId, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "COMPLETED") return (false, "Chỉ có thể báo cáo KQKD ở trạng thái Hoàn tất");

        so.Status = "REPORTED";
        so.ReportedAt = DateTime.Now;
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        // SLA: complete COMPLETED tracking
        await _slaService.CompleteTrackingAsync("SALES_ORDER", salesOrderId, "COMPLETED");

        _logger.LogInformation("SO reported: {No} by UserId={UserId}", so.SalesOrderNo, userId);
        return (true, null);
    }

    // Any → CANCELLED
    public async Task<(bool Success, string? ErrorMessage)> CancelAsync(
        int salesOrderId, int userId, string? reason)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status is "COMPLETED" or "REPORTED" or "RETURNED") return (false, "Không thể hủy đơn hàng đã hoàn tất hoặc trả hàng");
        if (so.Status == "CANCELLED") return (false, "Đơn hàng đã bị hủy");

        so.Status = "CANCELLED";
        so.CancelledAt = DateTime.Now;
        so.CancelReason = reason?.Trim();
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        // SLA: skip all active tracking
        await _slaService.SkipTrackingAsync("SALES_ORDER", salesOrderId);

        _logger.LogInformation("SO cancelled: {No} by UserId={UserId}", so.SalesOrderNo, userId);
        return (true, null);
    }

    // ══════════════════════════════════════════════════
    // UPDATE DRAFT INFO (PO KH + file)
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage)> UpdateDraftInfoAsync(
        int salesOrderId, string? customerPoNo, IFormFile? customerPoFile,
        DateTime? expectedDeliveryDate, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "DRAFT") return (false, "Chỉ có thể cập nhật ở trạng thái Nháp");

        so.CustomerPoNo = customerPoNo?.Trim();
        so.ExpectedDeliveryDate = expectedDeliveryDate;

        if (customerPoFile != null && customerPoFile.Length > 0)
        {
            var ext = Path.GetExtension(customerPoFile.FileName).ToLower();
            var allowed = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx" };
            if (!allowed.Contains(ext))
                return (false, "Định dạng file không được hỗ trợ");

            const long maxBytes = 10 * 1024 * 1024;
            if (customerPoFile.Length > maxBytes)
                return (false, "File quá lớn (tối đa 10MB)");

            var dir = Path.Combine(_storageRoot, "SalesOrders", "CustomerPO");
            Directory.CreateDirectory(dir);

            var fileName = $"so-{salesOrderId}-po-{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(dir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await customerPoFile.CopyToAsync(stream);

            so.CustomerPoFile = $"/uploads/SalesOrders/CustomerPO/{fileName}";
        }

        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("SO draft info updated: {No} by UserId={UserId}", so.SalesOrderNo, userId);
        return (true, null);
    }

    // ══════════════════════════════════════════════════
    // EXPORT ĐNTU EXCEL
    // ══════════════════════════════════════════════════
    public async Task<(byte[] FileBytes, string FileName)?> ExportDntuAsync(int salesOrderId, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(x => x.Customer)
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(x => x.SalesOrderId == salesOrderId);

        if (so == null || so.Items.Count == 0) return null;

        // Pull BasePrice + PurchaseExchangeRate from the source quotation (matched by SortOrder).
        var qItemsBySort = new Dictionary<int, QuotationItem>();
        if (so.QuotationId.HasValue)
        {
            var qItems = await _uow.Repository<QuotationItem>().Query()
                .Where(qi => qi.QuotationId == so.QuotationId.Value)
                .ToListAsync();
            qItemsBySort = qItems.GroupBy(qi => qi.SortOrder)
                                 .ToDictionary(g => g.Key, g => g.First());
        }

        var user = await _uow.Repository<User>().GetByIdAsync(userId);
        var userName = user?.FullName ?? "";

        var templatePath = Path.Combine(_env.WebRootPath, "templates", "DNTU-template.xlsx");
        if (!File.Exists(templatePath)) return null;

        using var wb = new XLWorkbook(templatePath);
        var ws = wb.Worksheet(1);

        var now = DateTime.Now;
        int itemCount = so.Items.Count;
        int dataStartRow = 12; // template data row
        int totalRow = 13;     // template total row

        // ── Header info ──
        ws.Cell("E5").Value = $"Số ĐNTU: {so.SalesOrderNo}";
        ws.Cell("K5").Value = $"Ngày    {now:dd}     Tháng     {now:MM}      Năm {now:yyyy}";
        ws.Cell("C6").Value = userName;
        ws.Cell("C8").Value = so.CustomerPoNo ?? "";
        ws.Cell("C9").Value = so.Customer.CustomerName;
        ws.Cell("C10").Value = so.OrderDate.ToString("dd.MM.yyyy");
        ws.Cell("G10").Value = so.ExpectedDeliveryDate?.ToString("dd.MM.yyyy") ?? "";

        // ── Insert extra data rows if needed ──
        if (itemCount > 1)
        {
            ws.Row(dataStartRow).InsertRowsBelow(itemCount - 1);
            totalRow = dataStartRow + itemCount; // total row shifts down
        }

        // ── Fill item data ──
        for (int i = 0; i < itemCount; i++)
        {
            var item = so.Items.ElementAt(i);
            int row = dataStartRow + i;

            ws.Cell(row, 1).Value = i + 1;                          // A: STT
            ws.Cell(row, 2).Value = so.Customer.CustomerName;        // B: Tên dự án/KS
            ws.Cell(row, 3).Value = item.ProductName;                // C: Tên hàng hóa
            ws.Cell(row, 4).Value = item.UnitName;                   // D: ĐVT
            ws.Cell(row, 5).Value = item.Quantity;                   // E: SL bán
            ws.Cell(row, 6).Value = item.UnitPrice;                  // F: Đơn giá bán
            ws.Cell(row, 7).Value = so.TaxRate / 100m;               // G: Thuế VAT (decimal)

            // H: Thành tiền = (F*E) + (F*E*G) — set formula
            ws.Cell(row, 8).FormulaA1 = $"(F{row}*E{row})+(F{row}*E{row}*G{row})";

            // Cột mua hàng (J, K, L, M, N, O) — replace template placeholders
            qItemsBySort.TryGetValue(item.SortOrder, out var qItem);
            ws.Cell(row, 10).Value = qItem?.BasePrice ?? item.PurchasePrice ?? 0;          // J: Đơn giá mua nguyên tệ (Quotation.BasePrice)
            ws.Cell(row, 11).Value = qItem?.PurchaseExchangeRate ?? 1; // K: Tỷ giá (Quotation.PurchaseExchangeRate)
            ws.Cell(row, 12).Value = item.PurchasePrice ?? 0;        // L: Đơn giá mua VNĐ
            ws.Cell(row, 13).Value = item.Quantity;                  // M: SL mua (giả định = SL bán)
            ws.Cell(row, 14).Value = (item.TaxRate ?? so.TaxRate) / 100m; // N: VAT mua
            ws.Cell(row, 15).FormulaA1 = $"(L{row}*M{row})+(L{row}*M{row}*N{row})"; // O: Thành tiền chi phí
            if (qItem != null) 
            {
                if (qItem.UnofficialWeightKg.HasValue)// P: Kg/Volume
                {
                    ws.Cell(row, 16).Value = qItem.UnofficialWeightKg.Value;
                }
                if (qItem.PurchaseMode == "OFFICIAL" && qItem.OfficialShipping.HasValue)  // Q: Phí vận chuyển về văn phòng
                {
                    ws.Cell(row, 17).Value = qItem.OfficialShipping.Value;
                }
                if (qItem.PurchaseMode == "UNOFFICIAL" && qItem.UnofficialW2WShipping.HasValue)
                {
                    ws.Cell(row, 17).Value = qItem.UnofficialW2WShipping.Value;
                }
            }

            // R: Phí vận chuyển — replace template placeholder
            ws.Cell(row, 18).Value = item.ShippingFee ?? 0;

            // Format number cells
            ws.Cell(row, 5).Style.NumberFormat.Format = "#";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 12).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 13).Style.NumberFormat.Format = "#";
            ws.Cell(row, 14).Style.NumberFormat.Format = "0%";
            ws.Cell(row, 15).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 18).Style.NumberFormat.Format = "#,##0";

            // Copy borders
            ws.Range(row, 1, row, 20).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 1, row, 20).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        // ── Update total row formulas ──
        int lastDataRow = dataStartRow + itemCount - 1;
        ws.Cell(totalRow, 5).FormulaA1 = $"SUMPRODUCT(E{dataStartRow}:E{lastDataRow},F{dataStartRow}:F{lastDataRow})";
        ws.Cell(totalRow, 7).FormulaA1 = $"SUMPRODUCT(E{dataStartRow}:E{lastDataRow},F{dataStartRow}:F{lastDataRow},G{dataStartRow}:G{lastDataRow})";
        ws.Cell(totalRow, 8).FormulaA1 = $"SUM(H{dataStartRow}:H{lastDataRow})";

        ws.Cell(totalRow, 12).FormulaA1 = $"SUMPRODUCT(L{dataStartRow}:L{lastDataRow},M{dataStartRow}:M{lastDataRow})";
        ws.Cell(totalRow, 14).FormulaA1 = $"SUMPRODUCT(L{dataStartRow}:L{lastDataRow},M{dataStartRow}:M{lastDataRow},N{dataStartRow}:N{lastDataRow})";
        ws.Cell(totalRow, 15).FormulaA1 = $"SUM(O{dataStartRow}:O{lastDataRow})";
        ws.Cell(totalRow, 17).FormulaA1 = $"SUM(Q{dataStartRow}:Q{lastDataRow})";
        ws.Cell(totalRow, 18).FormulaA1 = $"SUM(R{dataStartRow}:R{lastDataRow})";

        // ── Advance amount ──
        int advanceRow = totalRow + 5; // Row 18 in template → shifts
        ws.Cell(advanceRow, 6).Value = so.AdvanceAmount ?? 0;
        ws.Cell(advanceRow, 6).Style.NumberFormat.Format = "#,##0";
        ws.Cell(advanceRow, 8).Value = $"Ngày: {now:dd/MM/yyyy}";

        // ── Signature: user name ──
        int signRow = totalRow + 13; // Row 26 in template → shifts
        ws.Cell(signRow, 1).Value = userName;

        // ── Generate filename: yyyy.MM.dd_EVH-{CustomerName}-{PONumber}_DNTU_{UserName} ──
        var safeCustomer = SanitizeFileName(so.Customer.CustomerName);
        var safePo = SanitizeFileName(so.CustomerPoNo ?? so.SalesOrderNo);
        var safeUser = SanitizeFileName(userName);
        var fileName = $"{now:yyyy.MM.dd}_EVH-{safeCustomer}-{safePo}_DNTU_{safeUser}.xlsx";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), fileName);
    }

    // ══════════════════════════════════════════════════
    // EXPORT ĐNHU (HOÀN ỨNG) EXCEL
    //   — chạy khi SO ở trạng thái DELIVERED (chờ quyết toán),
    //     đề nghị hoàn lại phần tạm ứng còn dư so với chi phí thực.
    // ══════════════════════════════════════════════════
    public async Task<(byte[] FileBytes, string FileName)?> ExportDnhuAsync(int salesOrderId, int userId)
    {
        return await BuildSettlementExportAsync(salesOrderId, userId, isDnhu: true);
    }

    // ══════════════════════════════════════════════════
    // EXPORT ĐNTT (THANH TOÁN) EXCEL
    //   — chạy khi SO ở trạng thái DELIVERED (chờ quyết toán),
    //     đề nghị thanh toán phần còn lại nếu chi phí vượt tạm ứng (hoặc không có tạm ứng).
    // ══════════════════════════════════════════════════
    public async Task<(byte[] FileBytes, string FileName)?> ExportDnttAsync(int salesOrderId, int userId)
    {
        return await BuildSettlementExportAsync(salesOrderId, userId, isDnhu: false);
    }

    private async Task<(byte[] FileBytes, string FileName)?> BuildSettlementExportAsync(
        int salesOrderId, int userId, bool isDnhu)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(x => x.Customer)
            .Include(x => x.SalesPerson)
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(x => x.SalesOrderId == salesOrderId);

        if (so == null || so.Items.Count == 0) return null;

        var requestor = await _uow.Repository<User>().GetByIdAsync(userId);
        var requestorName = requestor?.FullName ?? so.SalesPerson?.FullName ?? "";

        var templateName = isDnhu ? "DNHU-template.xlsx" : "DNTT-template.xlsx";
        var templatePath = Path.Combine(_env.WebRootPath, "templates", templateName);
        if (!File.Exists(templatePath)) return null;

        using var wb = new XLWorkbook(templatePath);
        var ws = wb.Worksheet(1);

        var now = DateTime.Now;
        int itemCount = so.Items.Count;
        const int dataStartRow = 12;
        int totalRow = dataStartRow + itemCount;       // shifts down after inserts
        int lastDataRow = dataStartRow + itemCount - 1;

        // ── Header info ──
        var docPrefix = isDnhu ? "ĐNHU" : "ĐNTT";
        ws.Cell("F5").Value = $"Số {docPrefix}: {so.SalesOrderNo}";
        ws.Cell("K5").Value = $"Ngày    {now:dd}     Tháng     {now:MM}      Năm {now:yyyy}";
        ws.Cell("C6").Value = requestorName;                                  // Họ và tên
        ws.Cell("C7").Value = "Sales";                                        // Thuộc phòng
        ws.Cell("C8").Value = so.CustomerPoNo ?? so.SalesOrderNo;
        ws.Cell("C9").Value = so.Customer.CustomerName;
        ws.Cell("C10").Value = so.OrderDate.ToString("dd.MM.yyyy");
        ws.Cell("G10").Value = so.DeliveringAt?.ToString("dd.MM.yyyy")
                              ?? so.ExpectedDeliveryDate?.ToString("dd.MM.yyyy") ?? "";

        // ── Insert extra rows for items beyond the first template row ──
        if (itemCount > 1)
        {
            ws.Row(dataStartRow).InsertRowsBelow(itemCount - 1);
        }

        // ── Fill item data ──
        decimal taxRateDecimal = so.TaxRate / 100m;
        for (int i = 0; i < itemCount; i++)
        {
            var item = so.Items.ElementAt(i);
            int row = dataStartRow + i;

            ws.Cell(row, 1).Value = i + 1;                                     // A: STT
            ws.Cell(row, 2).Value = so.Customer.CustomerName;                  // B: Tên dự án/KS
            ws.Cell(row, 3).Value = item.ProductName;                          // C: Tên hàng hóa
            ws.Cell(row, 4).Value = item.UnitName;                             // D: ĐVT
            ws.Cell(row, 5).Value = item.Quantity;                             // E: SL bán
            ws.Cell(row, 6).Value = item.UnitPrice;                            // F: Đơn giá bán VNĐ
            ws.Cell(row, 7).Value = (item.TaxRate ?? so.TaxRate) / 100m;       // G: VAT (decimal)
            ws.Cell(row, 8).FormulaA1 = $"(F{row}*E{row})+(F{row}*E{row}*G{row})"; // H: Thành tiền

            // Cột mua hàng (J, K, L, M, N, O)
            ws.Cell(row, 10).Value = 0;                                        // J: Đơn giá mua nguyên tệ (chưa lưu, để trống/0)
            ws.Cell(row, 11).Value = 0;                                        // K: Tỷ giá
            ws.Cell(row, 12).Value = item.PurchasePrice ?? 0;                  // L: Đơn giá mua VNĐ
            ws.Cell(row, 13).Value = item.Quantity;                            // M: SL mua (giả định = SL bán)
            ws.Cell(row, 14).Value = (item.TaxRate ?? so.TaxRate) / 100m;      // N: VAT mua
            ws.Cell(row, 15).FormulaA1 = $"(L{row}*M{row})+(L{row}*M{row}*N{row})"; // O: Thành tiền chi phí

            // Cột phí v/c và giá vốn — giữ formula gốc tham chiếu R13/L13
            ws.Cell(row, 16).Value = 0;                                        // P: Kg/Volume
            ws.Cell(row, 17).FormulaA1 = $"P{row}*50000/M{row}+200000/M{row}"; // Q: Phí v/c về VP
            ws.Cell(row, 18).FormulaA1 = $"$R$13/$L$13*L{row}";                // R: Phí v/c đến KH
            ws.Cell(row, 19).FormulaA1 = $"L{row}+Q{row}+R{row}";              // S: Đơn giá vốn

            ws.Cell(row, 20).Value = item.SourceName ?? "";                    // T: NCC
            ws.Cell(row, 21).Value = "";                                       // U: Hình thức TT
            ws.Cell(row, 22).Value = "";                                       // V: Số HĐ đầu vào
            ws.Cell(row, 23).Value = so.DeliveredAt?.ToString("dd/MM/yyyy") ?? ""; // W: Ngày BBGN

            // Number formats
            ws.Cell(row, 5).Style.NumberFormat.Format = "#";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 7).Style.NumberFormat.Format = "0%";
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 12).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 13).Style.NumberFormat.Format = "#";
            ws.Cell(row, 14).Style.NumberFormat.Format = "0%";
            ws.Cell(row, 15).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 17).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 18).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 19).Style.NumberFormat.Format = "#,##0";

            ws.Range(row, 1, row, 23).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 1, row, 23).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        // ── Total row formulas ──
        ws.Cell(totalRow, 5).FormulaA1 = $"SUMPRODUCT(E{dataStartRow}:E{lastDataRow},F{dataStartRow}:F{lastDataRow})";
        ws.Cell(totalRow, 7).FormulaA1 = $"SUMPRODUCT(E{dataStartRow}:E{lastDataRow},F{dataStartRow}:F{lastDataRow},G{dataStartRow}:G{lastDataRow})";
        ws.Cell(totalRow, 8).FormulaA1 = $"SUM(H{dataStartRow}:H{lastDataRow})";
        ws.Cell(totalRow, 12).FormulaA1 = $"SUMPRODUCT(L{dataStartRow}:L{lastDataRow},M{dataStartRow}:M{lastDataRow})";
        ws.Cell(totalRow, 14).FormulaA1 = $"SUMPRODUCT(L{dataStartRow}:L{lastDataRow},M{dataStartRow}:M{lastDataRow},N{dataStartRow}:N{lastDataRow})";
        ws.Cell(totalRow, 15).FormulaA1 = $"SUM(O{dataStartRow}:O{lastDataRow})";
        ws.Cell(totalRow, 17).FormulaA1 = $"SUMPRODUCT(Q{dataStartRow}:Q{lastDataRow},M{dataStartRow}:M{lastDataRow})";
        ws.Cell(totalRow, 19).FormulaA1 = $"SUMPRODUCT(S{dataStartRow}:S{lastDataRow},M{dataStartRow}:M{lastDataRow})";
        ws.Cell(totalRow, 20).FormulaA1 = $"O{totalRow}+Q{totalRow}+R{totalRow}";

        // ── Settlement footer (specific to DNHU vs DNTT) ──
        decimal advanceTotal = so.AdvanceAmount ?? 0;
        decimal actualCost = so.ActualCost ?? so.PurchaseCost ?? 0;

        if (isDnhu)
        {
            // F20: Số tiền đã tạm ứng Lần 1, F21: Lần 2, F22: còn phải thanh toán, O22: Số tiền cần Hoàn lại
            int rowsShifted = totalRow - 13;
            int row20 = 20 + rowsShifted;
            int row21 = 21 + rowsShifted;
            int row22 = 22 + rowsShifted;

            ws.Cell(row20, 6).Value = advanceTotal;
            ws.Cell(row20, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row21, 6).Value = 0;
            ws.Cell(row21, 6).Style.NumberFormat.Format = "#,##0";

            // Số tiền cần hoàn lại = max(0, advance - actualCost)
            decimal refund = Math.Max(0m, advanceTotal - actualCost);
            ws.Cell(row22, 15).Value = refund;
            ws.Cell(row22, 15).Style.NumberFormat.Format = "#,##0";
        }
        else
        {
            // DNTT: O14 = SỐ TIỀN CẦN THANH TOÁN; nằm ngay dưới totalRow ở bản gốc nên cần tính lại
            int rowsShifted = totalRow - 13;
            int payRow = 14 + rowsShifted;

            // Số tiền cần thanh toán = max(0, actualCost - advance)
            decimal payable = Math.Max(0m, actualCost - advanceTotal);
            ws.Cell(payRow, 15).Value = payable;
            ws.Cell(payRow, 15).Style.NumberFormat.Format = "#,##0";
        }

        // ── Filename ──
        var safeCustomer = SanitizeFileName(so.Customer.CustomerName);
        var safePo = SanitizeFileName(so.CustomerPoNo ?? so.SalesOrderNo);
        var safeUser = SanitizeFileName(requestorName);
        var fileName = $"{now:yyyy.MM.dd}_EVH-{safeCustomer}-{safePo}_{docPrefix}_{safeUser}.xlsx";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("", name.Where(c => !invalid.Contains(c))).Trim();
    }

    // ══════════════════════════════════════════════════
    // CREATE PRODUCT & MAP TO SO ITEM
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage)> CreateProductAndMapAsync(
        int salesOrderId, int soItemId, QuickProductModel model, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);

        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "DRAFT") return (false, "Chỉ có thể tạo sản phẩm ở trạng thái Nháp");

        var item = so.Items.FirstOrDefault(i => i.SOItemId == soItemId);
        if (item == null) return (false, "Không tìm thấy dòng sản phẩm");

        if (string.IsNullOrWhiteSpace(model.ProductName))
            return (false, "Tên sản phẩm là bắt buộc");
        if (model.UnitId <= 0)
            return (false, "Đơn vị tính là bắt buộc");

        // Generate product code
        var productCode = await GenerateProductCodeAsync();

        // Create product
        var product = new Product
        {
            ProductCode = productCode,
            ProductName = model.ProductName.Trim(),
            Description = model.Description?.Trim(),
            UnitId = model.UnitId,
            DefaultSalePrice = model.DefaultSalePrice,
            DefaultPurchasePrice = model.DefaultPurchasePrice,
            SourceUrl = model.SourceUrl?.Trim(),
            IsActive = true,
            CreatedBy = userId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        await _uow.Repository<Product>().AddAsync(product);
        await _uow.SaveChangesAsync();

        // Auto-generate barcode
        product.Barcode = product.ProductCode;
        product.BarcodeType = "CODE128";
        _uow.Repository<Product>().Update(product);

        // Map to SO item
        item.ProductId = product.ProductId;
        item.IsProductMapped = true;

        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;
        _uow.Repository<SalesOrder>().Update(so);

        await _uow.SaveChangesAsync();

        _logger.LogInformation("Product #{ProductId} ({Code}) created and mapped to SO item #{ItemId} in SO {No} by UserId={UserId}",
            product.ProductId, productCode, soItemId, so.SalesOrderNo, userId);
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> MapProductToSOItemAsync(
        int salesOrderId, int soItemId, int productId, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "DRAFT") return (false, "Chỉ gắn sản phẩm ở trạng thái Nháp");

        var item = so.Items.FirstOrDefault(i => i.SOItemId == soItemId);
        if (item == null) return (false, "Không tìm thấy dòng sản phẩm");
        if (item.IsProductMapped) return (false, "Dòng này đã được gắn sản phẩm");

        item.ProductId = productId;
        item.IsProductMapped = true;

        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;
        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Product #{ProductId} mapped to SO item #{ItemId} in SO {No} by UserId={UserId}",
            productId, soItemId, so.SalesOrderNo, userId);
        return (true, null);
    }

    public async Task<List<UnitOptionVM>> GetUnitOptionsAsync()
    {
        return await _uow.Repository<Unit>().Query()
            .Where(u => u.IsActive)
            .OrderBy(u => u.UnitName)
            .Select(u => new UnitOptionVM
            {
                UnitId = u.UnitId,
                UnitCode = u.UnitCode,
                UnitName = u.UnitName
            })
            .ToListAsync();
    }

    private async Task<string> GenerateProductCodeAsync()
    {
        var last = await _uow.Repository<Product>().Query()
            .Where(p => p.ProductCode.StartsWith("SP-"))
            .OrderByDescending(p => p.ProductCode)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last != null && last.ProductCode.Length > 3)
        {
            if (int.TryParse(last.ProductCode[3..], out int n))
                next = n + 1;
        }
        return $"SP-{next:D5}";
    }

    // ══════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════
    private async Task<string> GenerateSalesOrderNoAsync()
    {
        var seq = await _uow.NextSequenceValueAsync("SalesOrderSequence");
        return $"SO-{DateTime.Now:yyyyMMdd}-{seq:D3}";
    }

    private async Task<List<CustomerOptionVM>> GetCustomerOptionsAsync()
    {
        return await _uow.Repository<Customer>().Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.CustomerName)
            .Select(c => new CustomerOptionVM
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName
            })
            .ToListAsync();
    }

    private async Task<List<SalesPersonOptionVM>> GetSalesPersonOptionsAsync()
    {
        return await _uow.Repository<User>().Query()
            .Where(u => u.IsActive && !u.IsLocked)
            .OrderBy(u => u.FullName)
            .Select(u => new SalesPersonOptionVM
            {
                UserId = u.UserId,
                UserCode = u.UserCode,
                FullName = u.FullName
            })
            .ToListAsync();
    }

    // ══════════════════════════════════════════════════
    // EXPORT PRODUCT TEMPLATE (Excel)
    // ══════════════════════════════════════════════════
    public async Task<(byte[] FileBytes, string FileName)?> ExportProductTemplateAsync(int salesOrderId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);
        if (so == null) return null;

        var unmappedItems = so.Items.Where(i => !i.IsProductMapped).OrderBy(i => i.SortOrder).ToList();
        if (unmappedItems.Count == 0) return null;

        // Load reference data
        var categories = await _uow.Repository<ProductCategory>().Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        var units = await _uow.Repository<Unit>().Query()
            .Where(u => u.IsActive)
            .OrderBy(u => u.UnitName)
            .ToListAsync();

        // Load all active attributes with values (for reference sheet)
        var attributes = await _uow.Repository<ProductAttribute>().Query()
            .Include(a => a.Values.Where(v => v.IsActive).OrderBy(v => v.DisplayOrder))
            .Where(a => a.IsActive && a.DataType == "LIST")
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync();

        // Load SKU configs per category to know which attributes apply
        var skuConfigs = await _uow.Repository<SkuConfig>().Query()
            .Include(c => c.Attribute)
            .Where(c => c.IsActive)
            .OrderBy(c => c.CategoryId).ThenBy(c => c.SkuPosition)
            .ToListAsync();

        // Determine max attribute columns needed across all categories
        var maxAttrCount = skuConfigs.GroupBy(c => c.CategoryId)
            .Select(g => g.Count())
            .DefaultIfEmpty(0).Max();

        using var wb = new XLWorkbook();

        // ── Sheet 1: Sản phẩm (main data entry) ──
        var ws = wb.AddWorksheet("Sản phẩm");

        // Fixed columns
        var headers = new List<string>
        {
            "SOItemId", "STT", "Tên sản phẩm (*)", "Mã danh mục (*)", "Mã ĐVT (*)",
            "Mô tả", "Giá bán", "Giá mua", "Tồn kho tối thiểu",
            "Khối lượng", "ĐV khối lượng (kg/g)", "Link nguồn mua"
        };

        // Dynamic attribute columns
        for (int a = 1; a <= maxAttrCount; a++)
        {
            headers.Add($"Thuộc tính {a} (Mã TT)");
            headers.Add($"Thuộc tính {a} (Mã giá trị)");
        }

        // Write headers
        for (int c = 0; c < headers.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            cell.Style.Alignment.WrapText = true;
        }

        // Write unmapped items (pre-filled)
        int row = 2;
        foreach (var item in unmappedItems)
        {
            ws.Cell(row, 1).Value = item.SOItemId;          // A: SOItemId (hidden ref)
            ws.Cell(row, 2).Value = row - 1;                // B: STT
            ws.Cell(row, 3).Value = item.ProductName;       // C: Product name
            // D: CategoryCode — blank for user to fill
            // E: UnitCode — blank for user to fill
            ws.Cell(row, 6).Value = item.Notes ?? "";        // F: Description
            ws.Cell(row, 7).Value = item.UnitPrice;          // G: Sale price
            ws.Cell(row, 8).Value = item.PurchasePrice ?? 0; // H: Purchase price
            ws.Cell(row, 9).Value = 0;                       // I: MinStockLevel
            // J, K: Weight — blank
            ws.Cell(row, 12).Value = item.SourceUrl ?? "";   // L: SourceUrl
            row++;
        }

        // Style SOItemId column (hidden helper)
        ws.Column(1).Width = 10;
        ws.Column(1).Style.Font.FontColor = XLColor.Gray;
        ws.Column(1).Style.Protection.Locked = true;

        // Auto-width other columns
        ws.Column(2).Width = 5;
        ws.Column(3).Width = 35;
        ws.Column(4).Width = 15;
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 25;
        ws.Columns(7, 8).Width = 15;
        ws.Column(9).Width = 12;
        ws.Columns(10, 11).Width = 12;
        ws.Column(12).Width = 30;
        for (int c = 13; c <= headers.Count; c++)
            ws.Column(c).Width = 16;

        // Highlight required columns
        ws.Column(3).Style.Fill.BackgroundColor = XLColor.LightYellow;
        ws.Column(4).Style.Fill.BackgroundColor = XLColor.LightYellow;
        ws.Column(5).Style.Fill.BackgroundColor = XLColor.LightYellow;
        // Reset header row background
        for (int c = 1; c <= headers.Count; c++)
            ws.Cell(1, c).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;

        // ── Sheet 2: Danh mục (reference) ──
        var wsCat = wb.AddWorksheet("Danh mục");
        wsCat.Cell(1, 1).Value = "Mã danh mục";
        wsCat.Cell(1, 2).Value = "Tên danh mục";
        wsCat.Cell(1, 3).Value = "Danh mục cha";
        wsCat.Cell(1, 4).Value = "SKU Prefix";
        wsCat.Cell(1, 5).Value = "Thuộc tính SKU";
        wsCat.Row(1).Style.Font.Bold = true;
        wsCat.Row(1).Style.Fill.BackgroundColor = XLColor.LightGreen;

        int catRow = 2;
        foreach (var cat in categories)
        {
            wsCat.Cell(catRow, 1).Value = cat.CategoryCode;
            wsCat.Cell(catRow, 2).Value = cat.CategoryName;
            var parent = categories.FirstOrDefault(c => c.CategoryId == cat.ParentCategoryId);
            wsCat.Cell(catRow, 3).Value = parent?.CategoryName ?? "";
            wsCat.Cell(catRow, 4).Value = cat.SkuPrefix ?? "";

            // List SKU attributes for this category
            var catConfigs = skuConfigs.Where(c => c.CategoryId == cat.CategoryId).ToList();
            if (catConfigs.Any())
            {
                wsCat.Cell(catRow, 5).Value = string.Join(", ",
                    catConfigs.Select(c => $"{c.Attribute.AttrCode}{(c.IsRequired ? "*" : "")}"));
            }
            catRow++;
        }
        wsCat.Columns().AdjustToContents();

        // ── Sheet 3: Đơn vị tính (reference) ──
        var wsUnit = wb.AddWorksheet("Đơn vị tính");
        wsUnit.Cell(1, 1).Value = "Mã ĐVT";
        wsUnit.Cell(1, 2).Value = "Tên ĐVT";
        wsUnit.Row(1).Style.Font.Bold = true;
        wsUnit.Row(1).Style.Fill.BackgroundColor = XLColor.LightGreen;

        int unitRow = 2;
        foreach (var u in units)
        {
            wsUnit.Cell(unitRow, 1).Value = u.UnitCode;
            wsUnit.Cell(unitRow, 2).Value = u.UnitName;
            unitRow++;
        }
        wsUnit.Columns().AdjustToContents();

        // ── Sheet 4: Thuộc tính & Giá trị (reference) ──
        var wsAttr = wb.AddWorksheet("Thuộc tính");
        wsAttr.Cell(1, 1).Value = "Mã thuộc tính";
        wsAttr.Cell(1, 2).Value = "Tên thuộc tính";
        wsAttr.Cell(1, 3).Value = "Mã giá trị";
        wsAttr.Cell(1, 4).Value = "Tên giá trị";
        wsAttr.Row(1).Style.Font.Bold = true;
        wsAttr.Row(1).Style.Fill.BackgroundColor = XLColor.LightGreen;

        int attrRow = 2;
        foreach (var attr in attributes)
        {
            foreach (var val in attr.Values)
            {
                wsAttr.Cell(attrRow, 1).Value = attr.AttrCode;
                wsAttr.Cell(attrRow, 2).Value = attr.AttributeName;
                wsAttr.Cell(attrRow, 3).Value = val.SkuCode;
                wsAttr.Cell(attrRow, 4).Value = val.ValueName;
                attrRow++;
            }
        }
        wsAttr.Columns(1, 4).AdjustToContents();

        // ── Per-attribute value columns (for INDIRECT dropdown) ──
        int perAttrCol = 6; // Column F onwards (leave col E empty as separator)
        foreach (var attr in attributes)
        {
            var colHeader = wsAttr.Cell(1, perAttrCol);
            colHeader.Value = attr.AttrCode;
            colHeader.Style.Font.Bold = true;
            colHeader.Style.Font.FontColor = XLColor.Gray;

            int vr = 2;
            foreach (var val in attr.Values)
            {
                wsAttr.Cell(vr, perAttrCol).Value = val.SkuCode;
                vr++;
            }

            // Define named range matching AttrCode (for INDIRECT lookup)
            if (vr > 2)
            {
                try
                {
                    wb.DefinedNames.Add(attr.AttrCode,
                        wsAttr.Range(2, perAttrCol, vr - 1, perAttrCol));
                }
                catch { /* skip if name is invalid for Excel */ }
            }
            perAttrCol++;
        }

        // Unique attribute codes list column (source for attr-code dropdowns)
        int attrListCol = perAttrCol;
        wsAttr.Cell(1, attrListCol).Value = "_Mã_TT";
        wsAttr.Cell(1, attrListCol).Style.Font.Bold = true;
        wsAttr.Cell(1, attrListCol).Style.Font.FontColor = XLColor.Gray;
        int uaIdx = 2;
        foreach (var attr in attributes)
        {
            wsAttr.Cell(uaIdx, attrListCol).Value = attr.AttrCode;
            uaIdx++;
        }

        // ── Data Validation dropdowns on "Sản phẩm" sheet ──
        int valLastRow = Math.Max(row - 1, 2) + 20; // buffer extra rows

        // Category dropdown (col D → Danh mục!A)
        if (catRow > 2)
            ws.Range(2, 4, valLastRow, 4).CreateDataValidation()
                .List(wsCat.Range(2, 1, catRow - 1, 1), true);

        // UOM dropdown (col E → Đơn vị tính!A)
        if (unitRow > 2)
            ws.Range(2, 5, valLastRow, 5).CreateDataValidation()
                .List(wsUnit.Range(2, 1, unitRow - 1, 1), true);

        // Attribute code & value dropdowns
        if (maxAttrCount > 0 && uaIdx > 2)
        {
            var attrCodesRange = wsAttr.Range(2, attrListCol, uaIdx - 1, attrListCol);
            for (int a = 0; a < maxAttrCount; a++)
            {
                int colCode = 13 + (a * 2);  // Attribute code column
                int colVal  = 14 + (a * 2);  // Attribute value column

                // Attribute code → dropdown of all attribute codes
                ws.Range(2, colCode, valLastRow, colCode).CreateDataValidation()
                    .List(attrCodesRange, true);

                // Attribute value → INDIRECT dropdown (resolves named range from attr code cell)
                var codeColLetter = ws.Cell(2, colCode).Address.ColumnLetter;
                ws.Range(2, colVal, valLastRow, colVal).CreateDataValidation()
                    .List($"INDIRECT({codeColLetter}2)", true);
            }
        }

        // ── Sheet 5: Hướng dẫn ──
        var wsGuide = wb.AddWorksheet("Hướng dẫn");
        wsGuide.Cell(1, 1).Value = "HƯỚNG DẪN NHẬP SẢN PHẨM";
        wsGuide.Cell(1, 1).Style.Font.Bold = true;
        wsGuide.Cell(1, 1).Style.Font.FontSize = 14;

        var guideLines = new[]
        {
            "",
            "1. Điền thông tin vào sheet \"Sản phẩm\". Các cột có dấu (*) là bắt buộc.",
            "2. Cột SOItemId: Không chỉnh sửa — dùng để gắn sản phẩm vào đơn hàng.",
            "3. Cột \"Mã danh mục\": Chọn từ dropdown (dữ liệu từ sheet \"Danh mục\").",
            "4. Cột \"Mã ĐVT\": Chọn từ dropdown (dữ liệu từ sheet \"Đơn vị tính\").",
            "5. Thuộc tính SKU:",
            "   - Xem sheet \"Danh mục\" cột \"Thuộc tính SKU\" để biết danh mục cần thuộc tính nào.",
            "   - Cột \"Mã TT\": Chọn mã thuộc tính từ dropdown (VD: ORIGIN, COLOR).",
            "   - Cột \"Mã giá trị\": Sau khi chọn Mã TT, dropdown sẽ hiển thị các giá trị tương ứng.",
            "   - Thuộc tính có dấu * trong sheet \"Danh mục\" là bắt buộc.",
            "6. Giá bán, giá mua: đã điền sẵn từ đơn hàng, có thể chỉnh sửa.",
            "7. Sau khi điền xong, lưu file và upload lại vào hệ thống.",
        };
        for (int i = 0; i < guideLines.Length; i++)
        {
            wsGuide.Cell(i + 2, 1).Value = guideLines[i];
        }
        wsGuide.Column(1).Width = 80;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var fileName = $"Template_SanPham_{so.SalesOrderNo}_{DateTime.Now:yyyyMMdd}.xlsx";
        return (ms.ToArray(), fileName);
    }

    // ══════════════════════════════════════════════════
    // IMPORT PRODUCTS FROM EXCEL
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage, int Created, int Mapped)> ImportProductsAsync(
        int salesOrderId, IFormFile file, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng", 0, 0);
        if (so.Status != "DRAFT") return (false, "Chỉ import sản phẩm ở trạng thái Nháp", 0, 0);

        // Load lookup data
        var categories = await _uow.Repository<ProductCategory>().Query()
            .Where(c => c.IsActive)
            .ToListAsync();
        var catByCode = categories.ToDictionary(c => c.CategoryCode.ToUpper(), c => c);

        var units = await _uow.Repository<Unit>().Query()
            .Where(u => u.IsActive)
            .ToListAsync();
        var unitByCode = units.ToDictionary(u => u.UnitCode.ToUpper(), u => u);

        // Load all attribute values for lookup
        var allAttributes = await _uow.Repository<ProductAttribute>().Query()
            .Include(a => a.Values.Where(v => v.IsActive))
            .Where(a => a.IsActive)
            .ToListAsync();
        var attrByCode = allAttributes.ToDictionary(a => a.AttrCode.ToUpper(), a => a);

        // Parse Excel
        using var stream = file.OpenReadStream();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Sản phẩm");
        if (ws == null) return (false, "Không tìm thấy sheet \"Sản phẩm\" trong file Excel", 0, 0);

        var errors = new List<string>();
        int created = 0;
        int mapped = 0;
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        // Detect how many attribute column pairs exist
        int fixedCols = 12;
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? fixedCols;
        int attrPairCount = Math.Max(0, (lastCol - fixedCols) / 2);

        for (int row = 2; row <= lastRow; row++)
        {
            var soItemIdStr = ws.Cell(row, 1).GetText().Trim();
            var productName = ws.Cell(row, 3).GetText().Trim();

            // Skip empty rows
            if (string.IsNullOrEmpty(productName) && string.IsNullOrEmpty(soItemIdStr))
                continue;

            var rowLabel = $"Dòng {row}";

            // Validate SOItemId
            if (!int.TryParse(soItemIdStr, out int soItemId))
            {
                errors.Add($"{rowLabel}: SOItemId không hợp lệ");
                continue;
            }

            var soItem = so.Items.FirstOrDefault(i => i.SOItemId == soItemId);
            if (soItem == null)
            {
                errors.Add($"{rowLabel}: Không tìm thấy dòng SO item #{soItemId}");
                continue;
            }
            if (soItem.IsProductMapped)
            {
                errors.Add($"{rowLabel}: \"{productName}\" đã được gắn sản phẩm, bỏ qua");
                continue;
            }

            // Validate required fields
            if (string.IsNullOrEmpty(productName))
            {
                errors.Add($"{rowLabel}: Tên sản phẩm trống");
                continue;
            }

            var categoryCode = ws.Cell(row, 4).GetText().Trim().ToUpper();
            if (string.IsNullOrEmpty(categoryCode) || !catByCode.TryGetValue(categoryCode, out var category))
            {
                errors.Add($"{rowLabel}: Mã danh mục \"{ws.Cell(row, 4).GetText().Trim()}\" không hợp lệ");
                continue;
            }

            var unitCode = ws.Cell(row, 5).GetText().Trim().ToUpper();
            if (string.IsNullOrEmpty(unitCode) || !unitByCode.TryGetValue(unitCode, out var unit))
            {
                errors.Add($"{rowLabel}: Mã ĐVT \"{ws.Cell(row, 5).GetText().Trim()}\" không hợp lệ");
                continue;
            }

            // Optional fields
            var description = ws.Cell(row, 6).GetText().Trim();
            decimal.TryParse(ws.Cell(row, 7).GetText().Trim(), out decimal salePrice);
            decimal.TryParse(ws.Cell(row, 8).GetText().Trim(), out decimal purchasePrice);
            int.TryParse(ws.Cell(row, 9).GetText().Trim(), out int minStock);
            decimal.TryParse(ws.Cell(row, 10).GetText().Trim(), out decimal weight);
            var weightUnit = ws.Cell(row, 11).GetText().Trim();
            var sourceUrl = ws.Cell(row, 12).GetText().Trim();

            // Parse attribute pairs
            var attrValues = new Dictionary<int, int>(); // AttributeId → ValueId
            bool attrError = false;
            for (int a = 0; a < attrPairCount; a++)
            {
                int colAttrCode = fixedCols + 1 + (a * 2);
                int colValCode = fixedCols + 2 + (a * 2);

                var attrCode = ws.Cell(row, colAttrCode).GetText().Trim().ToUpper();
                var valCode = ws.Cell(row, colValCode).GetText().Trim().ToUpper();

                if (string.IsNullOrEmpty(attrCode) && string.IsNullOrEmpty(valCode))
                    continue;

                if (string.IsNullOrEmpty(attrCode) || !attrByCode.TryGetValue(attrCode, out var attr))
                {
                    errors.Add($"{rowLabel}: Mã thuộc tính \"{ws.Cell(row, colAttrCode).GetText().Trim()}\" không hợp lệ");
                    attrError = true;
                    break;
                }

                if (string.IsNullOrEmpty(valCode))
                {
                    errors.Add($"{rowLabel}: Mã giá trị trống cho thuộc tính {attrCode}");
                    attrError = true;
                    break;
                }

                var attrVal = attr.Values.FirstOrDefault(v => v.SkuCode.ToUpper() == valCode);
                if (attrVal == null)
                {
                    errors.Add($"{rowLabel}: Giá trị \"{ws.Cell(row, colValCode).GetText().Trim()}\" không hợp lệ cho thuộc tính {attrCode}");
                    attrError = true;
                    break;
                }

                attrValues[attr.AttributeId] = attrVal.ValueId;
            }

            if (attrError) continue;

            // Validate required SKU attributes for selected category
            var catSkuConfigs = await _uow.Repository<SkuConfig>().Query()
                .Where(c => c.CategoryId == category.CategoryId && c.IsActive && c.IsRequired)
                .ToListAsync();

            bool missingRequired = false;
            foreach (var cfg in catSkuConfigs)
            {
                if (!attrValues.ContainsKey(cfg.AttributeId))
                {
                    var attrName = allAttributes.FirstOrDefault(a => a.AttributeId == cfg.AttributeId)?.AttrCode ?? "?";
                    errors.Add($"{rowLabel}: Thiếu thuộc tính bắt buộc \"{attrName}\" cho danh mục {categoryCode}");
                    missingRequired = true;
                }
            }
            if (missingRequired) continue;

            // Create product
            var productCode = await GenerateProductCodeAsync();
            var product = new Product
            {
                ProductCode = productCode,
                ProductName = productName,
                Description = string.IsNullOrEmpty(description) ? null : description,
                CategoryId = category.CategoryId,
                UnitId = unit.UnitId,
                DefaultSalePrice = salePrice > 0 ? salePrice : null,
                DefaultPurchasePrice = purchasePrice > 0 ? purchasePrice : null,
                MinStockLevel = minStock,
                Weight = weight > 0 ? weight : null,
                WeightUnit = string.IsNullOrEmpty(weightUnit) ? null : weightUnit,
                SourceUrl = string.IsNullOrEmpty(sourceUrl) ? null : sourceUrl,
                IsActive = true,
                CreatedBy = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _uow.Repository<Product>().AddAsync(product);
            await _uow.SaveChangesAsync();

            // Save attribute maps
            if (attrValues.Count > 0)
            {
                var mapRepo = _uow.Repository<ProductAttributeMap>();
                foreach (var (attrId, valueId) in attrValues)
                {
                    await mapRepo.AddAsync(new ProductAttributeMap
                    {
                        ProductId = product.ProductId,
                        AttributeId = attrId,
                        ValueId = valueId
                    });
                }
                await _uow.SaveChangesAsync();

                // Generate SKU
                try
                {
                    await _attrService.GenerateSkuAsync(product.ProductId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SKU generation failed for imported ProductId={Id}", product.ProductId);
                }
            }

            // Auto-generate barcode
            product.Barcode = GenerateCode128Value(product);
            product.BarcodeType = "CODE128";
            _uow.Repository<Product>().Update(product);

            // Map to SO item
            soItem.ProductId = product.ProductId;
            soItem.IsProductMapped = true;

            created++;
            mapped++;
        }

        // Save all remaining changes
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;
        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        if (errors.Count > 0 && created == 0)
            return (false, "Không tạo được sản phẩm nào.\n" + string.Join("\n", errors), 0, 0);

        var message = $"Đã tạo {created} sản phẩm và gắn vào đơn hàng.";
        if (errors.Count > 0)
            message += $"\n\n⚠ {errors.Count} dòng lỗi:\n" + string.Join("\n", errors);

        _logger.LogInformation("Imported {Count} products for SO #{Id} by UserId={UserId}", created, so.SalesOrderNo, userId);
        return (true, message, created, mapped);
    }

    private static string GenerateCode128Value(Product product)
    {
        return !string.IsNullOrEmpty(product.SKU) ? product.SKU : product.ProductCode;
    }

}
