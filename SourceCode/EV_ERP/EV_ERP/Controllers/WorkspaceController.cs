using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Inventory;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.ViewModels.Workspace;
using EV_ERP.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Controllers;

[RequireLogin]
public class WorkspaceController : Controller
{
    private readonly IUnitOfWork _uow;

    public WorkspaceController(IUnitOfWork uow)
    {
        _uow = uow;
    }

    private int CurrentUserId =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Workspace";
        var userId = CurrentUserId;
        var vm = new WorkspaceViewModel();

        // ── 1. RFQs assigned to current user, not yet having a Quotation ──
        var rfqTasks = await _uow.Repository<RFQ>().Query()
            .Include(r => r.Customer)
            .Include(r => r.Quotations)
            .Where(r => r.AssignedTo == userId
                     && r.Status == "INPROGRESS"
                     && !r.Quotations.Any())
            .OrderByDescending(r => r.RequestDate)
            .Select(r => new WorkspaceTaskItem
            {
                RfqNo = r.RfqNo,
                CustomerName = r.Customer.CustomerName,
                DetailUrl = $"/Rfq/Detail/{r.RfqId}",
                ExtraInfo = r.Priority
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 1,
            Title = "Yêu cầu báo giá",
            Icon = "bi-clipboard-check",
            BadgeColor = "info",
            Tasks = rfqTasks
        });

        // ── 2. Quotations DRAFT (pending to send) ──
        var quotDraftTasks = await _uow.Repository<Quotation>().Query()
            .Include(q => q.Customer)
            .Include(q => q.Rfq)
            .Where(q => q.SalesPersonId == userId && q.Status == "DRAFT")
            .OrderByDescending(q => q.QuotationDate)
            .Select(q => new WorkspaceTaskItem
            {
                RfqNo = q.Rfq != null ? q.Rfq.RfqNo : q.QuotationNo,
                CustomerName = q.Customer.CustomerName,
                DetailUrl = $"/Quotation/Detail/{q.QuotationId}",
                ExtraInfo = q.TotalAmount.ToString("N0") + " " + q.Currency
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 2,
            Title = "Báo giá chờ gửi",
            Icon = "bi-envelope",
            BadgeColor = "secondary",
            Tasks = quotDraftTasks
        });

        // ── 3. Quotations SENT (awaiting feedback) ──
        var quotSentTasks = await _uow.Repository<Quotation>().Query()
            .Include(q => q.Customer)
            .Include(q => q.Rfq)
            .Where(q => q.SalesPersonId == userId && q.Status == "SENT")
            .OrderByDescending(q => q.SentAt)
            .Select(q => new WorkspaceTaskItem
            {
                RfqNo = q.Rfq != null ? q.Rfq.RfqNo : q.QuotationNo,
                CustomerName = q.Customer.CustomerName,
                DetailUrl = $"/Quotation/Detail/{q.QuotationId}",
                ExtraInfo = q.SentAt.HasValue ? "Gửi lúc " + q.SentAt.Value.ToString("dd/MM") : null
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 3,
            Title = "Báo giá chờ phản hồi",
            Icon = "bi-hourglass-split",
            BadgeColor = "warning",
            Tasks = quotSentTasks
        });

        // ── 4. Quotations APPROVED ──
        var quotApprovedTasks = await _uow.Repository<Quotation>().Query()
            .Include(q => q.Customer)
            .Include(q => q.Rfq)
            .Include(q => q.SalesOrder)
            .Where(q => q.SalesPersonId == userId && q.Status == "APPROVED")
            .OrderByDescending(q => q.ApprovedAt)
            .Select(q => new WorkspaceTaskItem
            {
                RfqNo = q.Rfq != null ? q.Rfq.RfqNo : q.QuotationNo,
                SalesOrderNo = q.SalesOrder != null ? q.SalesOrder.SalesOrderNo : null,
                CustomerName = q.Customer.CustomerName,
                DetailUrl = $"/Quotation/Detail/{q.QuotationId}",
                ExtraInfo = q.TotalAmount.ToString("N0") + " " + q.Currency
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 4,
            Title = "Báo giá đã duyệt",
            Icon = "bi-check-circle",
            BadgeColor = "success",
            Tasks = quotApprovedTasks
        });

        // ── 5. SOs DRAFT (requiring advance payment input) ──
        var soDraftTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Where(s => s.CreatedBy == userId && s.Status == "DRAFT")
            .OrderByDescending(s => s.OrderDate)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Rfq != null ? s.Rfq.RfqNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                ExtraInfo = s.TotalAmount.ToString("N0") + " " + s.Currency
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 5,
            Title = "Đơn hàng cần lập ĐNTU",
            Icon = "bi-file-earmark-excel",
            BadgeColor = "primary",
            Tasks = soDraftTasks
        });

        // ── 6. SOs WAIT (awaiting advance payment) ──
        var soWaitTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Where(s => s.CreatedBy == userId && s.Status == "WAIT")
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Rfq != null ? s.Rfq.RfqNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                ExtraInfo = s.AdvanceAmount.HasValue ? s.AdvanceAmount.Value.ToString("N0") + " " + s.Currency : null
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 6,
            Title = "Đơn hàng chờ tạm ứng",
            Icon = "bi-cash-stack",
            BadgeColor = "warning",
            Tasks = soWaitTasks
        });

        // Pre-load SO IDs that have StockTransactions for steps 7-9
        var soIdsWithInbound = await _uow.Repository<StockTransaction>().Query()
            .Where(st => st.SalesOrderId != null && st.TransactionType == "INBOUND")
            .Select(st => st.SalesOrderId!.Value)
            .Distinct()
            .ToListAsync();

        var soIdsWithOutbound = await _uow.Repository<StockTransaction>().Query()
            .Where(st => st.SalesOrderId != null && st.TransactionType == "OUTBOUND")
            .Select(st => st.SalesOrderId!.Value)
            .Distinct()
            .ToListAsync();

        // ── 7. SOs BUYING (no StockTransaction yet) ──
        var soBuyingTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Include(s => s.Vendor)
            .Where(s => s.CreatedBy == userId
                     && s.Status == "BUYING"
                     && !soIdsWithInbound.Contains(s.SalesOrderId))
            .OrderByDescending(s => s.BuyingAt)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Rfq != null ? s.Rfq.RfqNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                ExtraInfo = s.Vendor != null ? s.Vendor.VendorName : null
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 7,
            Title = "Đơn hàng đang mua",
            Icon = "bi-cart3",
            BadgeColor = "info",
            Tasks = soBuyingTasks
        });

        // ── 8. SOs with StockTransaction INBOUND (received in warehouse) ──
        var soReceivedTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Where(s => s.CreatedBy == userId
                     && soIdsWithInbound.Contains(s.SalesOrderId)
                     && !soIdsWithOutbound.Contains(s.SalesOrderId)
                     && s.Status != "COMPLETED" && s.Status != "CANCELLED")
            .OrderByDescending(s => s.ReceivedAt)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Rfq != null ? s.Rfq.RfqNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                ExtraInfo = s.ReceivedAt.HasValue ? "Nhan " + s.ReceivedAt.Value.ToString("dd/MM") : null
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 8,
            Title = "Đơn hàng đã nhập kho",
            Icon = "bi-box-seam",
            BadgeColor = "primary",
            Tasks = soReceivedTasks
        });

        // ── 9. SOs with StockTransaction OUTBOUND (being delivered) ──
        var soDeliveringTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Where(s => s.CreatedBy == userId
                     && soIdsWithOutbound.Contains(s.SalesOrderId)
                     && s.Status != "DELIVERED" && s.Status != "COMPLETED" && s.Status != "CANCELLED")
            .OrderByDescending(s => s.DeliveringAt)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Rfq != null ? s.Rfq.RfqNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                ExtraInfo = s.DeliveringAt.HasValue ? "Giao " + s.DeliveringAt.Value.ToString("dd/MM") : null
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 9,
            Title = "Đơn hàng đã giao",
            Icon = "bi-truck",
            BadgeColor = "info",
            Tasks = soDeliveringTasks
        });

        // ── 10. SOs DELIVERED (not yet settled) ──
        var soDeliveredTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Where(s => s.CreatedBy == userId && s.Status == "DELIVERED")
            .OrderByDescending(s => s.DeliveredAt)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Rfq != null ? s.Rfq.RfqNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                ExtraInfo = s.TotalAmount.ToString("N0") + " " + s.Currency
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 10,
            Title = "Đơn hàng chờ quyết toán",
            Icon = "bi-calculator",
            BadgeColor = "success",
            Tasks = soDeliveredTasks
        });

        return View(vm);
    }
}
