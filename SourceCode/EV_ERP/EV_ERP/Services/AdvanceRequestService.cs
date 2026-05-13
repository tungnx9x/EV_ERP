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

    public AdvanceRequestService(IUnitOfWork uow, ILogger<AdvanceRequestService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    private static bool IsActiveStatus(string s) => s != "REJECTED";
    private static bool IsReceivedStatus(string s) => s is "RECEIVED" or "SETTLING" or "SETTLED";

    public async Task<SalesOrderAdvanceSummary> GetForSalesOrderAsync(int salesOrderId)
    {
        var so = await _uow.Repository<SalesOrder>().Query()
            .Where(s => s.SalesOrderId == salesOrderId)
            .Select(s => new { s.SalesOrderId, s.PurchaseCost, s.Currency })
            .FirstOrDefaultAsync();

        var summary = new SalesOrderAdvanceSummary
        {
            SalesOrderId = salesOrderId,
            PurchaseCost = so?.PurchaseCost ?? 0,
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
                    ProductName = i.SOItem?.ProductName ?? "Phân bổ chung cho cả đơn",
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

        var status = string.IsNullOrWhiteSpace(model.Status) ? "RECEIVED" : model.Status.Trim().ToUpperInvariant();
        if (status is not ("PENDING" or "APPROVED" or "RECEIVED"))
            return (false, "Trạng thái không hợp lệ", null);

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

        // Stamp approved/received timestamps if the user records the advance as already approved/received
        if (status is "APPROVED" or "RECEIVED")
        {
            req.ApprovedBy = userId;
            req.ApprovedAt = now;
            req.ApprovedAmount = total;
        }
        if (status == "RECEIVED")
        {
            req.ReceivedAt = now;
        }

        await _uow.Repository<AdvanceRequest>().AddAsync(req);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("AdvanceRequest created: {No} SO={SoNo} Total={Total} Status={Status} by UserId={UserId}",
            req.RequestNo, so.SalesOrderNo, total, status, userId);

        return (true, null, req.AdvanceRequestId);
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
}
