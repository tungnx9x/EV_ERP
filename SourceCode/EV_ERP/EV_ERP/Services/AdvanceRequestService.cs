using ClosedXML.Excel;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Finance;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.ViewModels.Finance;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services;

public class AdvanceRequestService : IAdvanceRequestService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AdvanceRequestService> _logger;
    private readonly IWebHostEnvironment _env;

    public AdvanceRequestService(IUnitOfWork uow, ILogger<AdvanceRequestService> logger, IWebHostEnvironment env)
    {
        _uow = uow;
        _logger = logger;
        _env = env;
    }

    // Nhãn hiển thị cho dòng phân bổ: theo SP, hoặc "Phân bổ vận chuyển" / "Phân bổ chung cho cả đơn".
    private static string AllocationLabel(string? productName, string? purpose)
    {
        if (!string.IsNullOrWhiteSpace(productName)) return productName!;
        if (string.Equals(purpose?.Trim(), "Vận chuyển", StringComparison.OrdinalIgnoreCase))
            return "Phân bổ vận chuyển";
        return "Phân bổ chung cho cả đơn";
    }

    private static bool IsActiveStatus(string s) => s != "REJECTED";
    // "Đã chi tiền" trở đi được tính là đã giải ngân (gồm cả mã cũ RECEIVED + giai đoạn quyết toán)
    private static bool IsReceivedStatus(string s) => s is "DISBURSED" or "SETTLING" or "SETTLED" or "RECEIVED";

    public async Task<SalesOrderAdvanceSummary> GetForSalesOrderAsync(int salesOrderId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Where(s => s.SalesOrderId == salesOrderId)
            .Select(s => new { s.SalesOrderId, s.PurchaseCost, s.Currency })
            .FirstOrDefaultAsync();

        decimal purchaseCost = so?.PurchaseCost ?? 0;

        // Fallback: before buying happens (DRAFT/WAIT), SO.PurchaseCost is null.
        // Estimate from Quotation snapshot already on each SOItem (Quantity × PurchasePrice, alive lines).
        if (purchaseCost <= 0)
        {
            purchaseCost = await _uow.Repository<SalesOrderItem>().Query()
                .Where(i => i.SalesOrderId == salesOrderId && i.CancelledQty < i.Quantity)
                .SumAsync(i => (i.Quantity - i.CancelledQty) * (i.PurchasePrice ?? 0));
        }

        var summary = new SalesOrderAdvanceSummary
        {
            SalesOrderId = salesOrderId,
            PurchaseCost = purchaseCost,
            Currency = so?.Currency ?? "VND"
        };

        var requests = await _uow.Repository<AdvanceRequest>().Query()
            .Include(a => a.Items).ThenInclude(i => i.SOItem)
            .Include(a => a.CreatedByUser)
            .Where(a => a.SalesOrderId == salesOrderId)
            .OrderByDescending(a => a.RequestDate).ThenByDescending(a => a.AdvanceRequestId)
            .ToListAsync();

        foreach (var r in requests)
        {
            if (IsActiveStatus(r.Status))
                summary.TotalRequested += r.RequestedAmount;
            if (IsReceivedStatus(r.Status))
                summary.TotalReceived += r.ApprovedAmount ?? r.RequestedAmount;

            summary.Requests.Add(new AdvanceRequestRow
            {
                AdvanceRequestId = r.AdvanceRequestId,
                RequestNo = r.RequestNo,
                RequestDate = r.RequestDate,
                RequestedAmount = r.RequestedAmount,
                ApprovedAmount = r.ApprovedAmount,
                Purpose = r.Purpose,
                Status = r.Status,
                Notes = r.Notes,
                CreatedByName = r.CreatedByUser?.FullName ?? "",
                CreatedAt = r.CreatedAt,
                Items = r.Items.Select(i => new AdvanceRequestItemRow
                {
                    AdvanceRequestItemId = i.AdvanceRequestItemId,
                    SOItemId = i.SOItemId,
                    ProductName = AllocationLabel(i.SOItem?.ProductName, i.Purpose),
                    Amount = i.Amount,
                    Purpose = i.Purpose,
                    Notes = i.Notes
                }).ToList()
            });
        }

        return summary;
    }

    public async Task<Dictionary<int, decimal>> GetAdvancedByItemAsync(int salesOrderId)
    {
        var rows = await _uow.Repository<AdvanceRequestItem>().Query()
            .Where(i => i.AdvanceRequest.SalesOrderId == salesOrderId
                     && i.AdvanceRequest.Status != "REJECTED"
                     && i.SOItemId.HasValue)
            .GroupBy(i => i.SOItemId!.Value)
            .Select(g => new { SOItemId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        return rows.ToDictionary(r => r.SOItemId, r => r.Total);
    }

    public async Task<int> CountActiveAsync(int salesOrderId) =>
        await _uow.Repository<AdvanceRequest>().Query()
            .Where(a => a.SalesOrderId == salesOrderId && a.Status != "REJECTED")
            .CountAsync();

    public async Task<(bool Success, string? ErrorMessage, int? AdvanceRequestId)> CreateAsync(
        int salesOrderId, AdvanceRequestCreateModel model, int userId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SalesOrderId == salesOrderId);
        if (so == null) return (false, "Không tìm thấy đơn hàng", null);

        if (so.Status is "CANCELLED" or "REPORTED")
            return (false, "Không thể tạo tạm ứng cho đơn đã hủy hoặc đã báo cáo KQKD", null);

        var items = (model.Items ?? new())
            .Where(i => i.Amount > 0)
            .ToList();
        if (items.Count == 0)
            return (false, "Cần nhập ít nhất 1 dòng tạm ứng có số tiền > 0", null);

        // Validate any per-line SOItemId actually belongs to this SO
        var soItemIds = so.Items.Select(i => i.SOItemId).ToHashSet();
        foreach (var i in items)
        {
            if (i.SOItemId.HasValue && !soItemIds.Contains(i.SOItemId.Value))
                return (false, $"Dòng SP #{i.SOItemId} không thuộc đơn hàng này", null);
        }

        // Phiếu mới luôn bắt đầu ở bước "Chờ kế toán duyệt" — KD không tự đặt trạng thái.
        const string status = "WAIT_ACCOUNTANT";

        var total = items.Sum(i => i.Amount);
        var now = DateTime.Now;

        var req = new AdvanceRequest
        {
            RequestNo = await GenerateRequestNoAsync(),
            SalesOrderId = salesOrderId,
            RequestDate = model.RequestDate?.Date ?? DateTime.Today,
            RequestedAmount = total,
            Purpose = (model.Purpose ?? "").Trim(),
            Status = status,
            Notes = model.Notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId,
            Items = items.Select(i => new AdvanceRequestItem
            {
                SOItemId = i.SOItemId,
                Amount = i.Amount,
                Purpose = i.Purpose?.Trim(),
                Notes = i.Notes?.Trim(),
                CreatedAt = now
            }).ToList()
        };

        await _uow.Repository<AdvanceRequest>().AddAsync(req);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("AdvanceRequest created: {No} SO={SoNo} Total={Total} Status={Status} by UserId={UserId}",
            req.RequestNo, so.SalesOrderNo, total, status, userId);

        return (true, null, req.AdvanceRequestId);
    }

    public async Task<AdvanceRequestRow?> GetForEditAsync(int advanceRequestId)
    {
        var req = await _uow.Repository<AdvanceRequest>().Query()
            .Include(a => a.Items).ThenInclude(i => i.SOItem)
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.AdvanceRequestId == advanceRequestId);
        if (req == null) return null;

        return new AdvanceRequestRow
        {
            AdvanceRequestId = req.AdvanceRequestId,
            RequestNo = req.RequestNo,
            RequestDate = req.RequestDate,
            RequestedAmount = req.RequestedAmount,
            ApprovedAmount = req.ApprovedAmount,
            Purpose = req.Purpose,
            Status = req.Status,
            Notes = req.Notes,
            CreatedByName = req.CreatedByUser?.FullName ?? "",
            CreatedAt = req.CreatedAt,
            Items = req.Items.Select(i => new AdvanceRequestItemRow
            {
                AdvanceRequestItemId = i.AdvanceRequestItemId,
                SOItemId = i.SOItemId,
                ProductName = AllocationLabel(i.SOItem?.ProductName, i.Purpose),
                Amount = i.Amount,
                Purpose = i.Purpose,
                Notes = i.Notes
            }).ToList()
        };
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(
        int advanceRequestId, AdvanceRequestCreateModel model, int userId)
    {
        var req = await _uow.Repository<AdvanceRequest>().Query()
            .Include(a => a.Items)
            .Include(a => a.SalesOrder).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(a => a.AdvanceRequestId == advanceRequestId);
        if (req == null) return (false, "Không tìm thấy đề nghị tạm ứng");

        if (req.Status != "WAIT_ACCOUNTANT")
            return (false, "Chỉ có thể sửa đề nghị tạm ứng khi đang ở bước Chờ kế toán duyệt");

        var so = req.SalesOrder;
        if (so == null) return (false, "Không tìm thấy đơn hàng liên quan");
        if (so.Status is "CANCELLED" or "REPORTED")
            return (false, "Không thể sửa tạm ứng cho đơn đã hủy hoặc đã báo cáo KQKD");

        var items = (model.Items ?? new())
            .Where(i => i.Amount > 0)
            .ToList();
        if (items.Count == 0)
            return (false, "Cần nhập ít nhất 1 dòng tạm ứng có số tiền > 0");

        var soItemIds = so.Items.Select(i => i.SOItemId).ToHashSet();
        foreach (var i in items)
        {
            if (i.SOItemId.HasValue && !soItemIds.Contains(i.SOItemId.Value))
                return (false, $"Dòng SP #{i.SOItemId} không thuộc đơn hàng này");
        }

        var total = items.Sum(i => i.Amount);
        var now = DateTime.Now;

        // Replace items wholesale (delete-all + readd)
        var itemRepo = _uow.Repository<AdvanceRequestItem>();
        foreach (var oldItem in req.Items.ToList())
            itemRepo.Remove(oldItem);

        foreach (var i in items)
        {
            await itemRepo.AddAsync(new AdvanceRequestItem
            {
                AdvanceRequestId = req.AdvanceRequestId,
                SOItemId = i.SOItemId,
                Amount = i.Amount,
                Purpose = i.Purpose?.Trim(),
                Notes = i.Notes?.Trim(),
                CreatedAt = now
            });
        }

        req.RequestDate = model.RequestDate?.Date ?? req.RequestDate;
        req.Purpose = (model.Purpose ?? "").Trim();
        req.Notes = model.Notes?.Trim();
        req.RequestedAmount = total;
        // Trạng thái giữ nguyên WAIT_ACCOUNTANT khi sửa — không đổi qua form.
        req.UpdatedAt = now;

        _uow.Repository<AdvanceRequest>().Update(req);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("AdvanceRequest updated: {No} Total={Total} by UserId={UserId}",
            req.RequestNo, total, userId);

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAsync(int advanceRequestId, int userId)
    {
        var req = await _uow.Repository<AdvanceRequest>().Query()
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.AdvanceRequestId == advanceRequestId);
        if (req == null) return (false, "Không tìm thấy đề nghị tạm ứng");

        if (req.Status is "SETTLING" or "SETTLED")
            return (false, "Không thể xóa: đề nghị tạm ứng đã / đang được quyết toán");

        // Items cascade per DbContext config
        _uow.Repository<AdvanceRequest>().Remove(req);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("AdvanceRequest deleted: {No} by UserId={UserId}", req.RequestNo, userId);
        return (true, null);
    }

    // ══════════════════════════════════════════════════
    // STATUS TRANSITIONS — quy trình duyệt 4 bước
    //   WAIT_ACCOUNTANT → WAIT_DIRECTOR → WAIT_DISBURSE → DISBURSED
    //   (quyền theo vai trò được kiểm tra ở Controller)
    // ══════════════════════════════════════════════════

    // Bước 1→2: Kế toán duyệt, chuyển sang chờ giám đốc duyệt.
    public async Task<(bool Success, string? ErrorMessage)> AccountantReviewAsync(int advanceRequestId, int userId)
    {
        var req = await _uow.Repository<AdvanceRequest>().GetByIdAsync(advanceRequestId);
        if (req == null) return (false, "Không tìm thấy đề nghị tạm ứng");
        if (req.Status != "WAIT_ACCOUNTANT")
            return (false, "Chỉ có thể duyệt khi đề nghị đang ở bước Chờ kế toán duyệt");

        req.Status = "WAIT_DIRECTOR";
        req.UpdatedAt = DateTime.Now;
        _uow.Repository<AdvanceRequest>().Update(req);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("AdvanceRequest accountant-reviewed: {No} by UserId={UserId}", req.RequestNo, userId);
        return (true, null);
    }

    // Bước 2→3: Giám đốc (MANAGER) duyệt, chuyển sang chờ chi tiền.
    public async Task<(bool Success, string? ErrorMessage)> DirectorApproveAsync(int advanceRequestId, int userId)
    {
        var req = await _uow.Repository<AdvanceRequest>().GetByIdAsync(advanceRequestId);
        if (req == null) return (false, "Không tìm thấy đề nghị tạm ứng");
        if (req.Status != "WAIT_DIRECTOR")
            return (false, "Chỉ có thể duyệt khi đề nghị đang ở bước Chờ giám đốc duyệt");

        var now = DateTime.Now;
        req.Status = "WAIT_DISBURSE";
        req.ApprovedBy = userId;
        req.ApprovedAt = now;
        req.ApprovedAmount = req.RequestedAmount;
        req.UpdatedAt = now;
        _uow.Repository<AdvanceRequest>().Update(req);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("AdvanceRequest director-approved: {No} by UserId={UserId}", req.RequestNo, userId);
        return (true, null);
    }

    // Bước 3→4: Kế toán xác nhận đã chi tiền.
    public async Task<(bool Success, string? ErrorMessage)> DisburseAsync(int advanceRequestId, int userId)
    {
        var req = await _uow.Repository<AdvanceRequest>().GetByIdAsync(advanceRequestId);
        if (req == null) return (false, "Không tìm thấy đề nghị tạm ứng");
        if (req.Status != "WAIT_DISBURSE")
            return (false, "Chỉ có thể chi tiền khi đề nghị đang ở bước Chờ chi tiền");

        var now = DateTime.Now;
        req.Status = "DISBURSED";
        req.ReceivedAt = now;
        if (req.ApprovedAmount == null)
        {
            req.ApprovedBy ??= userId;
            req.ApprovedAt ??= now;
            req.ApprovedAmount = req.RequestedAmount;
        }
        req.UpdatedAt = now;
        _uow.Repository<AdvanceRequest>().Update(req);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("AdvanceRequest disbursed: {No} by UserId={UserId}", req.RequestNo, userId);
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> RejectAsync(int advanceRequestId, string? reason, int userId)
    {
        var req = await _uow.Repository<AdvanceRequest>().GetByIdAsync(advanceRequestId);
        if (req == null) return (false, "Không tìm thấy đề nghị tạm ứng");
        // Chỉ từ chối được khi còn trong các bước chờ duyệt (chưa chi tiền / chưa quyết toán).
        if (req.Status is not ("WAIT_ACCOUNTANT" or "WAIT_DIRECTOR" or "WAIT_DISBURSE"))
            return (false, "Không thể từ chối: trạng thái hiện tại không cho phép");

        var now = DateTime.Now;
        req.Status = "REJECTED";
        req.RejectedBy = userId;
        req.RejectedAt = now;
        req.RejectReason = reason?.Trim();
        req.UpdatedAt = now;
        _uow.Repository<AdvanceRequest>().Update(req);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("AdvanceRequest rejected: {No} by UserId={UserId} Reason={Reason}",
            req.RequestNo, userId, reason);
        return (true, null);
    }

    private async Task<string> GenerateRequestNoAsync()
    {
        var prefix = $"TU-{DateTime.Now:yyyyMM}-";
        var last = await _uow.Repository<AdvanceRequest>().Query()
            .Where(a => a.RequestNo.StartsWith(prefix))
            .OrderByDescending(a => a.RequestNo)
            .Select(a => a.RequestNo)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last != null && int.TryParse(last.Substring(prefix.Length), out var n))
            next = n + 1;

        return $"{prefix}{next:D3}";
    }

    // ══════════════════════════════════════════════════
    // EXPORT DNTU EXCEL — per advance request
    //   All SO items rendered as rows. Items NOT in this advance
    //   request keep qty but get 0 for all money cells.
    // ══════════════════════════════════════════════════
    public async Task<(byte[] FileBytes, string FileName)?> ExportDntuAsync(int advanceRequestId, int userId)
    {
        var req = await _uow.Repository<AdvanceRequest>().Query()
            .Include(a => a.Items)
            .Include(a => a.SalesOrder).ThenInclude(s => s.Customer)
            .Include(a => a.SalesOrder).ThenInclude(s => s.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(a => a.AdvanceRequestId == advanceRequestId);

        if (req == null) return null;
        var so = req.SalesOrder;
        if (so == null || so.Items.Count == 0) return null;

        // Sum advance per SOItemId in this request (general/null allocation handled separately)
        var perItemAdvance = req.Items
            .Where(i => i.SOItemId.HasValue)
            .GroupBy(i => i.SOItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var generalAdvance = req.Items.Where(i => !i.SOItemId.HasValue).Sum(i => i.Amount);

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
        int dataStartRow = 12;
        int totalRow = 13;

        // ── Header info ──
        ws.Cell("E5").Value = $"Số ĐNTU: {req.RequestNo}";
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
            totalRow = dataStartRow + itemCount;
        }

        // ── Fill item data ──
        var soItems = so.Items.OrderBy(i => i.SortOrder).ToList();
        for (int i = 0; i < itemCount; i++)
        {
            var item = soItems[i];
            int row = dataStartRow + i;
            bool included = perItemAdvance.ContainsKey(item.SOItemId);

            ws.Cell(row, 1).Value = i + 1;                          // A: STT
            ws.Cell(row, 2).Value = so.Customer.CustomerName;        // B: Tên dự án/KS
            ws.Cell(row, 3).Value = item.ProductName;                // C: Tên hàng hóa
            ws.Cell(row, 4).Value = item.UnitName;                   // D: ĐVT
            ws.Cell(row, 5).Value = item.Quantity;                   // E: SL bán
            ws.Cell(row, 6).Value = included ? item.UnitPrice : 0;   // F: Đơn giá bán
            ws.Cell(row, 7).Value = included ? so.TaxRate / 100m : 0; // G: VAT

            // H: Thành tiền = (F*E) + (F*E*G)
            ws.Cell(row, 8).FormulaA1 = $"(F{row}*E{row})+(F{row}*E{row}*G{row})";

            qItemsBySort.TryGetValue(item.SortOrder, out var qItem);
            ws.Cell(row, 10).Value = included ? (qItem?.BasePrice ?? item.PurchasePrice ?? 0) : 0;
            ws.Cell(row, 11).Value = included ? (qItem?.PurchaseExchangeRate ?? 1) : 0;
            ws.Cell(row, 12).Value = included ? (item.PurchasePrice ?? 0) : 0;
            ws.Cell(row, 13).Value = item.Quantity;
            ws.Cell(row, 14).Value = included ? (item.TaxRate ?? so.TaxRate) / 100m : 0;
            ws.Cell(row, 15).FormulaA1 = $"(L{row}*M{row})+(L{row}*M{row}*N{row})";

            if (included && qItem != null)
            {
                if (qItem.UnofficialWeightKg.HasValue)
                {
                    ws.Cell(row, 16).Value = qItem.UnofficialWeightKg.Value;
                }
                if (qItem.PurchaseMode is "OFFICIAL" or "DOMESTIC" && qItem.OfficialShipping.HasValue)
                {
                    ws.Cell(row, 17).Value = qItem.OfficialShipping.Value;
                }
                if (qItem.PurchaseMode == "UNOFFICIAL" && qItem.UnofficialW2WShipping.HasValue)
                {
                    ws.Cell(row, 17).Value = qItem.UnofficialW2WShipping.Value;
                }
            }

            ws.Cell(row, 18).Value = included ? (item.ShippingFee ?? 0) : 0;

            ws.Cell(row, 5).Style.NumberFormat.Format = "#";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 12).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 13).Style.NumberFormat.Format = "#";
            ws.Cell(row, 14).Style.NumberFormat.Format = "0%";
            ws.Cell(row, 15).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 18).Style.NumberFormat.Format = "#,##0";

            ws.Range(row, 1, row, 20).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 1, row, 20).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        int lastDataRow = dataStartRow + itemCount - 1;
        ws.Cell(totalRow, 5).FormulaA1 = $"SUMPRODUCT(E{dataStartRow}:E{lastDataRow},F{dataStartRow}:F{lastDataRow})";
        ws.Cell(totalRow, 7).FormulaA1 = $"SUMPRODUCT(E{dataStartRow}:E{lastDataRow},F{dataStartRow}:F{lastDataRow},G{dataStartRow}:G{lastDataRow})";
        ws.Cell(totalRow, 8).FormulaA1 = $"SUM(H{dataStartRow}:H{lastDataRow})";

        ws.Cell(totalRow, 12).FormulaA1 = $"SUMPRODUCT(L{dataStartRow}:L{lastDataRow},M{dataStartRow}:M{lastDataRow})";
        ws.Cell(totalRow, 14).FormulaA1 = $"SUMPRODUCT(L{dataStartRow}:L{lastDataRow},M{dataStartRow}:M{lastDataRow},N{dataStartRow}:N{lastDataRow})";
        ws.Cell(totalRow, 15).FormulaA1 = $"SUM(O{dataStartRow}:O{lastDataRow})";
        ws.Cell(totalRow, 17).FormulaA1 = $"SUM(Q{dataStartRow}:Q{lastDataRow})";
        ws.Cell(totalRow, 18).FormulaA1 = $"SUM(R{dataStartRow}:R{lastDataRow})";

        // ── Advance amount = this request's total (NOT the SO-level field) ──
        int advanceRow = totalRow + 5;
        ws.Cell(advanceRow, 6).Value = req.RequestedAmount;
        ws.Cell(advanceRow, 6).Style.NumberFormat.Format = "#,##0";
        ws.Cell(advanceRow, 8).Value = $"Ngày: {now:dd/MM/yyyy}";

        int signRow = totalRow + 13;
        ws.Cell(signRow, 1).Value = userName;

        var safeCustomer = SanitizeFileName(so.Customer.CustomerName);
        var safePo = SanitizeFileName(so.CustomerPoNo ?? so.SalesOrderNo);
        var safeUser = SanitizeFileName(userName);
        var fileName = $"{now:yyyy.MM.dd}_EVH-{safeCustomer}-{safePo}_DNTU_{req.RequestNo}_{safeUser}.xlsx";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("", name.Where(c => !invalid.Contains(c))).Trim();
    }
}
