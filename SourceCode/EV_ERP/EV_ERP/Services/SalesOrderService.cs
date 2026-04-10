using ClosedXML.Excel;
using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.Entities.Vendors;
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

    public SalesOrderService(IUnitOfWork uow, ILogger<SalesOrderService> logger,
        IWebHostEnvironment env, IConfiguration config)
    {
        _uow = uow;
        _logger = logger;
        _env = env;
        _storageRoot = config["FileStorage:RootPath"] ?? Path.Combine(env.ContentRootPath, "ERP_Files");
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
            .Include(s => s.Vendor)
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
                VendorName = s.Vendor != null ? s.Vendor.VendorName : null,
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
            .Include(x => x.Vendor)
            .Include(x => x.Quotation)
            .Include(x => x.Rfq)
            .Include(x => x.SalesPerson)
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(x => x.SalesOrderId == salesOrderId);

        if (s == null) return null;

        var vendors = await GetVendorOptionsAsync();

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
            VendorId = s.VendorId,
            VendorName = s.Vendor?.VendorName,
            VendorCode = s.Vendor?.VendorCode,
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
            SalesPersonName = s.SalesPerson.FullName,
            ActualCost = s.ActualCost,
            SettlementNotes = s.SettlementNotes,
            DeliveringAt = s.DeliveringAt,
            DeliveredAt = s.DeliveredAt,
            CompletedAt = s.CompletedAt,
            CancelledAt = s.CancelledAt,
            CancelReason = s.CancelReason,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            Items = s.Items.Select(i => new SalesOrderItemDetailViewModel
            {
                SOItemId = i.SOItemId,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitName = i.UnitName,
                Quantity = i.Quantity,
                DeliveredQty = i.DeliveredQty,
                UnitPrice = i.UnitPrice,
                DiscountType = i.DiscountType,
                DiscountValue = i.DiscountValue,
                DiscountAmount = i.DiscountAmount,
                LineTotal = i.LineTotal,
                PurchasePrice = i.PurchasePrice,
                LineCost = i.LineCost,
                Notes = i.Notes
            }).ToList(),
            Vendors = vendors
        };
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

        var soNo = await GenerateSalesOrderNoAsync();

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
                UnitName = i.UnitName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountType = i.DiscountType,
                DiscountValue = i.DiscountValue,
                DiscountAmount = i.DiscountAmount,
                LineTotal = i.LineTotal,
                SortOrder = i.SortOrder,
                Notes = i.Notes
            }).ToList()
        };

        await _uow.Repository<SalesOrder>().AddAsync(so);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("SO created from Quotation: {SONo} ← {QNo} by UserId={UserId}",
            soNo, q.QuotationNo, userId);

        return (true, null, so.SalesOrderId);
    }

    // ══════════════════════════════════════════════════
    // STATUS TRANSITIONS
    // ══════════════════════════════════════════════════

    // DRAFT → WAIT (nhập PO KH, đề nghị tạm ứng)
    public async Task<(bool Success, string? ErrorMessage)> SubmitWaitAsync(
        int salesOrderId, SalesOrderDraftModel model, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "DRAFT") return (false, "Chỉ có thể gửi đề nghị tạm ứng ở trạng thái Nháp");

        so.CustomerPoNo = model.CustomerPoNo?.Trim();
        so.AdvanceAmount = model.AdvanceAmount;
        so.AdvanceStatus = model.AdvanceAmount > 0 ? "PENDING" : null;
        so.Status = "WAIT";
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("SO submitted to WAIT: {No} by UserId={UserId}", so.SalesOrderNo, userId);
        return (true, null);
    }

    // WAIT → BUYING (chọn NCC, nhập giá mua)
    public async Task<(bool Success, string? ErrorMessage)> StartBuyingAsync(
        int salesOrderId, SalesOrderBuyingModel model, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);

        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "WAIT") return (false, "Chỉ có thể bắt đầu mua hàng ở trạng thái Chờ tạm ứng");

        so.VendorId = model.VendorId;
        so.ExpectedReceiveDate = model.ExpectedReceiveDate;
        so.BuyingNotes = model.BuyingNotes?.Trim();
        so.BuyingAt = DateTime.Now;
        so.Status = "BUYING";
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        // Update purchase prices per item
        decimal totalCost = 0;
        foreach (var itemModel in model.Items)
        {
            var item = so.Items.FirstOrDefault(i => i.SOItemId == itemModel.SOItemId);
            if (item != null)
            {
                item.PurchasePrice = itemModel.PurchasePrice;
                item.LineCost = item.Quantity * itemModel.PurchasePrice;
                totalCost += item.LineCost ?? 0;
            }
        }
        so.PurchaseCost = totalCost;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("SO started buying: {No} VendorId={VendorId} by UserId={UserId}",
            so.SalesOrderNo, model.VendorId, userId);
        return (true, null);
    }

    // BUYING → RECEIVED
    public async Task<(bool Success, string? ErrorMessage)> ConfirmReceivedAsync(int salesOrderId, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "BUYING") return (false, "Chỉ có thể xác nhận nhận hàng ở trạng thái Đang mua");

        so.Status = "RECEIVED";
        so.ReceivedAt = DateTime.Now;
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("SO received: {No} by UserId={UserId}", so.SalesOrderNo, userId);
        return (true, null);
    }

    // RECEIVED → DELIVERING
    public async Task<(bool Success, string? ErrorMessage)> StartDeliveringAsync(int salesOrderId, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "RECEIVED") return (false, "Chỉ có thể giao hàng ở trạng thái Đã nhận hàng");

        so.Status = "DELIVERING";
        so.DeliveringAt = DateTime.Now;
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("SO delivering: {No} by UserId={UserId}", so.SalesOrderNo, userId);
        return (true, null);
    }

    // DELIVERING → DELIVERED
    public async Task<(bool Success, string? ErrorMessage)> ConfirmDeliveredAsync(int salesOrderId, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "DELIVERING") return (false, "Chỉ có thể xác nhận giao hàng ở trạng thái Đang giao");

        so.Status = "DELIVERED";
        so.DeliveredAt = DateTime.Now;
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("SO delivered: {No} by UserId={UserId}", so.SalesOrderNo, userId);
        return (true, null);
    }

    // DELIVERED → COMPLETED (quyết toán + RFQ auto-complete)
    public async Task<(bool Success, string? ErrorMessage)> CompleteAsync(
        int salesOrderId, SalesOrderCompleteModel model, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "DELIVERED") return (false, "Chỉ có thể hoàn tất ở trạng thái Đã giao");

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

        _logger.LogInformation("SO completed: {No} by UserId={UserId}", so.SalesOrderNo, userId);
        return (true, null);
    }

    // Any → CANCELLED
    public async Task<(bool Success, string? ErrorMessage)> CancelAsync(
        int salesOrderId, int userId, string? reason)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status == "COMPLETED") return (false, "Không thể hủy đơn hàng đã hoàn tất");
        if (so.Status == "CANCELLED") return (false, "Đơn hàng đã bị hủy");

        so.Status = "CANCELLED";
        so.CancelledAt = DateTime.Now;
        so.CancelReason = reason?.Trim();
        so.UpdatedBy = userId;
        so.UpdatedAt = DateTime.Now;

        _uow.Repository<SalesOrder>().Update(so);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("SO cancelled: {No} by UserId={UserId}", so.SalesOrderNo, userId);
        return (true, null);
    }

    // ══════════════════════════════════════════════════
    // UPDATE DRAFT INFO (PO KH + file)
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage)> UpdateDraftInfoAsync(
        int salesOrderId, string? customerPoNo, IFormFile? customerPoFile, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng");
        if (so.Status != "DRAFT") return (false, "Chỉ có thể cập nhật ở trạng thái Nháp");

        so.CustomerPoNo = customerPoNo?.Trim();

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

            // Format number cells
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.###";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";

            // Copy borders
            ws.Range(row, 1, row, 20).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 1, row, 20).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        // ── Update total row formulas ──
        int lastDataRow = dataStartRow + itemCount - 1;
        ws.Cell(totalRow, 5).FormulaA1 = $"SUMPRODUCT(E{dataStartRow}:E{lastDataRow},F{dataStartRow}:F{lastDataRow})";
        ws.Cell(totalRow, 7).FormulaA1 = $"SUMPRODUCT(E{dataStartRow}:E{lastDataRow},F{dataStartRow}:F{lastDataRow},G{dataStartRow}:G{lastDataRow})";
        ws.Cell(totalRow, 8).FormulaA1 = $"SUM(H{dataStartRow}:H{lastDataRow})";

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

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("", name.Where(c => !invalid.Contains(c))).Trim();
    }

    // ══════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════
    private async Task<string> GenerateSalesOrderNoAsync()
    {
        var prefix = $"SO-{DateTime.Now:yyyyMM}-";
        var last = await _uow.Repository<SalesOrder>().Query()
            .Where(s => s.SalesOrderNo.StartsWith(prefix))
            .OrderByDescending(s => s.SalesOrderNo)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last != null)
        {
            var suffix = last.SalesOrderNo[prefix.Length..];
            if (int.TryParse(suffix, out int n)) next = n + 1;
        }
        return $"{prefix}{next:D3}";
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

    private async Task<List<VendorOptionVM>> GetVendorOptionsAsync()
    {
        return await _uow.Repository<Vendor>().Query()
            .Where(v => v.IsActive)
            .OrderBy(v => v.VendorName)
            .Select(v => new VendorOptionVM
            {
                VendorId = v.VendorId,
                VendorCode = v.VendorCode,
                VendorName = v.VendorName
            })
            .ToListAsync();
    }
}
