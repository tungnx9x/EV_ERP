using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Inventory;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.Entities.System;
using EV_ERP.Models.ViewModels.Workspace;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Controllers;

[RequireLogin]
public class WorkspaceController : Controller
{
    private readonly IUnitOfWork _uow;
    private readonly ISlaService _slaService;

    public WorkspaceController(IUnitOfWork uow, ISlaService slaService)
    {
        _uow = uow;
        _slaService = slaService;
    }

    private CurrentUser CurrentUserObj =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!;

    public async Task<IActionResult> Index(int? viewUserId)
    {
        ViewData["Title"] = "Workspace";
        var currentUser = CurrentUserObj;
        var canViewOthers = currentUser.RoleCode is "ADMIN" or "MANAGER";

        // WAREHOUSE staff don't act on RFQ/Quotation/SO assignment fields, so the standard
        // 10-card workspace is empty for them. Route them to a calendar of upcoming receipts/deliveries.
        if (currentUser.RoleCode == "WAREHOUSE")
            return RedirectToAction(nameof(Warehouse));

        // Determine which user's tasks to show
        var userId = currentUser.UserId;
        var viewingUserName = currentUser.FullName;
        var isManagerOverview = false;

        if (canViewOthers)
        {
            if (viewUserId.HasValue)
            {
                // Viewing a specific user's workspace
                var targetUser = await _uow.Repository<User>().Query()
                    .Where(u => u.UserId == viewUserId.Value && u.IsActive)
                    .Select(u => new { u.UserId, u.FullName })
                    .FirstOrDefaultAsync();
                if (targetUser != null)
                {
                    userId = targetUser.UserId;
                    viewingUserName = targetUser.FullName;
                }
            }
            else
            {
                // Default: team overview
                isManagerOverview = true;
            }
        }

        var vm = new WorkspaceViewModel
        {
            CanViewOthers = canViewOthers,
            IsManagerOverview = isManagerOverview,
            ViewingUserId = userId,
            ViewingUserName = viewingUserName
        };

        // Load user list for manager/admin
        if (canViewOthers)
        {
            vm.Users = await _uow.Repository<User>().Query()
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new UserOption
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    UserCode = u.UserCode
                })
                .ToListAsync();
        }

        // Manager overview: team summary per card
        if (isManagerOverview)
        {
            await BuildManagerOverviewAsync(vm);
            return View(vm);
        }

        // ── Individual user workspace ──

        // ── 1. RFQs assigned to current user, not yet having a Quotation ──
        var rfqTasks = await _uow.Repository<RFQ>().Query()
            .Include(r => r.Customer)
            .Include(r => r.Quotations)
            .Where(r => r.AssignedTo == userId
                     && r.Status == "INPROGRESS"
                     && !r.Quotations.Any())
            .OrderByDescending(r => r.RequestDate)
            .Select(r => new { r.RfqNo, r.Customer.CustomerName, r.RfqId, r.Notes, r.Deadline, r.CreatedAt })
            .ToListAsync();
        var rfqTaskItems = rfqTasks.Select(r => new WorkspaceTaskItem
        {
            RfqNo = r.RfqNo,
            CustomerName = r.CustomerName,
            DetailUrl = $"/Rfq/Detail/{r.RfqId}",
            EntityType = "RFQ",
            EntityId = r.RfqId,
            Notes = r.Notes,
            IsShowElapsed = true,
            ExtraInfos = [new TaskExtraInfo { Title = "Deadline", Value = r.Deadline.ToString("dd/MM/yyyy HH:mm"), CssClass = "bg-danger text-white" }],
            CreatedAt = r.CreatedAt
        }).ToList();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 1,
            Title = "Yêu cầu báo giá",
            Icon = "bi-clipboard-check",
            BadgeColor = "info",
            Tasks = rfqTaskItems
        });

        // ── 2. Quotations DRAFT (pending to send) ──
        var quotDraftRaw = await _uow.Repository<Quotation>().Query()
            .Include(q => q.Customer)
            .Include(q => q.Rfq)
            .Where(q => q.SalesPersonId == userId && q.Status == "DRAFT")
            .OrderByDescending(q => q.QuotationDate)
            .Select(q => new { RfqNo = q.Rfq != null ? q.Rfq.RfqNo + "/" + q.QuotationNo : q.QuotationNo, q.Customer.CustomerName, q.QuotationId, q.Notes, q.Deadline, q.CreatedAt })
            .ToListAsync();
        var quotDraftTasks = quotDraftRaw.Select(q => new WorkspaceTaskItem
        {
            RfqNo = q.RfqNo,
            CustomerName = q.CustomerName,
            DetailUrl = $"/Quotation/Detail/{q.QuotationId}",
            EntityType = "QUOTATION",
            EntityId = q.QuotationId,
            Notes = q.Notes,
            IsShowElapsed = true,
            ExtraInfos = [new TaskExtraInfo { Title = "Deadline", Value = q.Deadline.ToString("dd/MM/yyyy HH:mm"), CssClass = "bg-danger text-white" }],
            CreatedAt = q.CreatedAt
        }).ToList();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 2,
            Title = "Báo giá đang làm",
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
                RfqNo = q.Rfq != null ? q.Rfq.RfqNo + "/" + q.QuotationNo : q.QuotationNo,
                CustomerName = q.Customer.CustomerName,
                DetailUrl = $"/Quotation/Detail/{q.QuotationId}",
                EntityType = "QUOTATION",
                EntityId = q.QuotationId,
                Notes = q.Notes
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 3,
            Title = "Báo giá chờ phản hồi",
            Icon = "bi-hourglass-split",
            BadgeColor = "warning",
            Tasks = quotSentTasks
        });

        // ── 4. Quotations APPROVED ──
        //var quotApprovedTasks = await _uow.Repository<Quotation>().Query()
        //    .Include(q => q.Customer)
        //    .Include(q => q.Rfq)
        //    .Include(q => q.SalesOrder)
        //    .Where(q => q.SalesPersonId == userId && q.Status == "APPROVED")
        //    .OrderByDescending(q => q.ApprovedAt)
        //    .Select(q => new WorkspaceTaskItem
        //    {
        //        RfqNo = q.Rfq != null ? q.Rfq.RfqNo + "/" + q.QuotationNo : q.QuotationNo,
        //        SalesOrderNo = q.SalesOrder != null ? q.SalesOrder.SalesOrderNo : null,
        //        CustomerName = q.Customer.CustomerName,
        //        DetailUrl = $"/Quotation/Detail/{q.QuotationId}",
        //        EntityType = "QUOTATION",
        //        EntityId = q.QuotationId,
        //        Notes = q.Notes
        //    })
        //    .ToListAsync();

        //vm.Cards.Add(new WorkspaceCard
        //{
        //    StepNumber = 4,
        //    Title = "Báo giá đã duyệt",
        //    Icon = "bi-check-circle",
        //    BadgeColor = "success",
        //    Tasks = quotApprovedTasks
        //});

        // ── 4. SOs DRAFT (requiring advance payment input) ──
        var soDraftTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Quotation)
            .Where(s => s.CreatedBy == userId && s.Status == "DRAFT")
            .OrderByDescending(s => s.OrderDate)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Quotation != null ? s.Quotation.QuotationNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                EntityType = "SALES_ORDER",
                EntityId = s.SalesOrderId,
                Notes = s.Notes
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 4,
            Title = "Đơn hàng cần lập ĐNTU",
            Icon = "bi-file-earmark-excel",
            BadgeColor = "primary",
            Tasks = soDraftTasks
        });

        // ── 5. SOs WAIT (awaiting advance payment) ──
        var soWaitTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Where(s => s.CreatedBy == userId && s.Status == "WAIT")
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Quotation != null ? s.Quotation.QuotationNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                EntityType = "SALES_ORDER",
                EntityId = s.SalesOrderId,
                Notes = s.Notes
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 5,
            Title = "Đơn hàng chờ tạm ứng",
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

        // ── 6. SOs BUYING (no StockTransaction yet) ──
        var soBuyingTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Where(s => s.CreatedBy == userId
                     && s.Status == "BUYING"
                     && !soIdsWithInbound.Contains(s.SalesOrderId))
            .OrderByDescending(s => s.BuyingAt)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Quotation != null ? s.Quotation.QuotationNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                EntityType = "SALES_ORDER",
                EntityId = s.SalesOrderId,
                Notes = s.Notes
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 6,
            Title = "Đơn hàng đang mua",
            Icon = "bi-cart3",
            BadgeColor = "info",
            Tasks = soBuyingTasks
        });

        // ── 7. SOs with StockTransaction INBOUND (received in warehouse) ──
        var soReceivedTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Where(s => s.CreatedBy == userId
                     && soIdsWithInbound.Contains(s.SalesOrderId)
                     && !soIdsWithOutbound.Contains(s.SalesOrderId)
                     && s.Status != "COMPLETED" && s.Status != "REPORTED" && s.Status != "CANCELLED")
            .OrderByDescending(s => s.ReceivedAt)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Quotation != null ? s.Quotation.QuotationNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                EntityType = "SALES_ORDER",
                EntityId = s.SalesOrderId,
                Notes = s.Notes
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 7,
            Title = "Đơn hàng đã nhập kho",
            Icon = "bi-box-seam",
            BadgeColor = "primary",
            Tasks = soReceivedTasks
        });

        // ── 8. SOs with StockTransaction OUTBOUND (being delivered) ──
        var soDeliveringTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Where(s => s.CreatedBy == userId
                     && soIdsWithOutbound.Contains(s.SalesOrderId)
                     && s.Status != "DELIVERED" && s.Status != "COMPLETED" && s.Status != "REPORTED" && s.Status != "CANCELLED")
            .OrderByDescending(s => s.DeliveringAt)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Quotation != null ? s.Quotation.QuotationNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                EntityType = "SALES_ORDER",
                EntityId = s.SalesOrderId,
                Notes = s.Notes
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 8,
            Title = "Đơn hàng đã giao",
            Icon = "bi-truck",
            BadgeColor = "info",
            Tasks = soDeliveringTasks
        });

        // ── 9. SOs DELIVERED (not yet settled) ──
        var soDeliveredTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Where(s => s.CreatedBy == userId && s.Status == "DELIVERED")
            .OrderByDescending(s => s.DeliveredAt)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Quotation != null ? s.Quotation.QuotationNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                EntityType = "SALES_ORDER",
                EntityId = s.SalesOrderId,
                Notes = s.Notes
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 9,
            Title = "Đơn hàng chờ quyết toán",
            Icon = "bi-calculator",
            BadgeColor = "success",
            Tasks = soDeliveredTasks
        });

        // ── 10. SOs COMPLETED (pending KQKD report) ──
        var soCompletedTasks = await _uow.Repository<SalesOrder>().Query()
            .Include(s => s.Customer)
            .Include(s => s.Rfq)
            .Where(s => s.CreatedBy == userId && s.Status == "COMPLETED")
            .OrderByDescending(s => s.CompletedAt)
            .Select(s => new WorkspaceTaskItem
            {
                RfqNo = s.Quotation != null ? s.Quotation.QuotationNo : s.SalesOrderNo,
                SalesOrderNo = s.SalesOrderNo,
                CustomerName = s.Customer.CustomerName,
                DetailUrl = $"/SalesOrder/Detail/{s.SalesOrderId}",
                EntityType = "SALES_ORDER",
                EntityId = s.SalesOrderId,
                Notes = s.Notes
            })
            .ToListAsync();

        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 10,
            Title = "Chờ báo cáo KQKD",
            Icon = "bi-file-earmark-bar-graph",
            BadgeColor = "primary",
            Tasks = soCompletedTasks
        });

        // ── SLA: Batch query severity for all workspace tasks ──
        await ApplySlaSeverityAsync(vm);

        return View(vm);
    }

    // ═══════════════════════════════════════════════════
    // WAREHOUSE CALENDAR — upcoming SO receipts (incoming) / deliveries (outgoing)
    // ═══════════════════════════════════════════════════
    public async Task<IActionResult> Warehouse(string mode = "incoming", int? year = null, int? month = null)
    {
        ViewData["Title"] = "Lịch kho";
        if (mode != "incoming" && mode != "outgoing") mode = "incoming";

        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        if (m < 1 || m > 12) { y = today.Year; m = today.Month; }

        var monthStart = new DateTime(y, m, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        // Calendar grid spans full weeks (Mon-first). Pad before/after for prior/next month days.
        var firstDayOfWeek = (int)monthStart.DayOfWeek;             // Sun=0..Sat=6
        var leadingDays = (firstDayOfWeek + 6) % 7;                  // Mon=0..Sun=6
        var gridStart = monthStart.AddDays(-leadingDays);
        var trailingDays = (7 - ((leadingDays + DateTime.DaysInMonth(y, m)) % 7)) % 7;
        var gridEnd = monthEnd.AddDays(trailingDays);

        // v2.3 — calendar buckets are per SO-line, not per SO. Same order can appear on multiple
        // days when its lines have different ExpectedReceive/Delivery dates.
        var lineQuery = _uow.Repository<SalesOrderItem>().Query()
            .Include(i => i.SalesOrder).ThenInclude(s => s.Customer)
            .Where(i => i.SalesOrder.Status != "CANCELLED");

        lineQuery = mode == "incoming"
            ? lineQuery.Where(i => i.ExpectedReceiveDate != null
                                && i.ExpectedReceiveDate >= gridStart
                                && i.ExpectedReceiveDate <= gridEnd
                                && i.RemainingReceiveQty > 0)
            : lineQuery.Where(i => i.ExpectedDeliveryDate != null
                                && i.ExpectedDeliveryDate >= gridStart
                                && i.ExpectedDeliveryDate <= gridEnd
                                && i.RemainingDeliverQty > 0);

        var rawLines = await lineQuery
            .Select(i => new
            {
                i.SOItemId,
                i.SalesOrderId,
                SalesOrderNo = i.SalesOrder.SalesOrderNo,
                CustomerName = i.SalesOrder.Customer.CustomerName,
                Status = i.SalesOrder.Status,
                Currency = i.SalesOrder.Currency,
                Date = (mode == "incoming" ? i.ExpectedReceiveDate : i.ExpectedDeliveryDate)!.Value,
                i.ProductName,
                i.UnitName,
                i.LineTotal,
                PendingQty = mode == "incoming" ? i.RemainingReceiveQty : i.RemainingDeliverQty
            })
            .ToListAsync();

        var ordersByDate = rawLines
            .GroupBy(x => x.Date.Date)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(x => x.SalesOrderId)
                .Select(soGroup =>
                {
                    var head = soGroup.First();
                    return new WarehouseCalendarOrder
                    {
                        SalesOrderId = head.SalesOrderId,
                        SalesOrderNo = head.SalesOrderNo,
                        CustomerName = head.CustomerName,
                        Status = head.Status,
                        StatusText = SoStatusText(head.Status),
                        StatusBadge = SoStatusBadge(head.Status),
                        ItemCount = soGroup.Count(),
                        PendingQuantity = soGroup.Sum(l => l.PendingQty),
                        TotalAmount = soGroup.Sum(l => l.LineTotal),
                        Currency = head.Currency,
                        Lines = soGroup
                            .Select(l => new WarehouseCalendarLine
                            {
                                ProductName = l.ProductName,
                                UnitName = l.UnitName,
                                PendingQuantity = l.PendingQty
                            })
                            .ToList()
                    };
                })
                .OrderBy(o => o.SalesOrderNo)
                .ToList());

        var days = new List<WarehouseCalendarDay>();
        for (var d = gridStart; d <= gridEnd; d = d.AddDays(1))
        {
            days.Add(new WarehouseCalendarDay
            {
                Date = d,
                IsCurrentMonth = d.Month == m && d.Year == y,
                IsToday = d == today,
                Orders = ordersByDate.GetValueOrDefault(d) ?? []
            });
        }

        var vm = new WarehouseCalendarViewModel
        {
            Mode = mode,
            Year = y,
            Month = m,
            Days = days
        };

        return View(vm);
    }

    private static string SoStatusText(string status) => status switch
    {
        "DRAFT" => "Nháp",
        "WAIT" => "Chờ tạm ứng",
        "BUYING" => "Đang mua",
        "RECEIVED" => "Đã nhận hàng",
        "DELIVERING" => "Đang giao",
        "DELIVERED" => "Đã giao",
        "RETURNED" => "Trả hàng",
        "COMPLETED" => "Hoàn tất",
        "REPORTED" => "Đã báo cáo",
        _ => status
    };

    private static string SoStatusBadge(string status) => status switch
    {
        "DRAFT" => "secondary",
        "WAIT" => "warning",
        "BUYING" => "info",
        "RECEIVED" => "primary",
        "DELIVERING" => "info",
        "DELIVERED" => "success",
        "RETURNED" => "warning",
        "COMPLETED" => "success",
        "REPORTED" => "primary",
        _ => "secondary"
    };

    // ═══════════════════════════════════════════════════
    // SAVE QUICK NOTE
    // ═══════════════════════════════════════════════════
    [HttpPost]
    public async Task<IActionResult> SaveNote([FromBody] SaveNoteRequest request)
    {
        try
        {
            switch (request.EntityType)
            {
                case "RFQ":
                    var rfq = await _uow.Repository<RFQ>().Query()
                        .FirstOrDefaultAsync(r => r.RfqId == request.EntityId);
                    if (rfq == null) return Json(ApiResult<object>.Fail("Không tìm thấy RFQ"));
                    rfq.Notes = request.Notes;
                    rfq.UpdatedAt = DateTime.Now;
                    break;

                case "QUOTATION":
                    var quot = await _uow.Repository<Quotation>().Query()
                        .FirstOrDefaultAsync(q => q.QuotationId == request.EntityId);
                    if (quot == null) return Json(ApiResult<object>.Fail("Không tìm thấy Báo giá"));
                    quot.Notes = request.Notes;
                    break;

                case "SALES_ORDER":
                    var so = await _uow.Repository<SalesOrder>().Query()
                        .FirstOrDefaultAsync(s => s.SalesOrderId == request.EntityId);
                    if (so == null) return Json(ApiResult<object>.Fail("Không tìm thấy Đơn hàng"));
                    so.Notes = request.Notes;
                    so.UpdatedAt = DateTime.Now;
                    break;

                default:
                    return Json(ApiResult<object>.Fail("Loại entity không hợp lệ"));
            }

            await _uow.SaveChangesAsync();
            return Json(ApiResult<object>.Ok(new { }, "Đã lưu ghi chú"));
        }
        catch (Exception ex)
        {
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    public class SaveNoteRequest
    {
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string? Notes { get; set; }
    }

    // ═══════════════════════════════════════════════════
    // MANAGER OVERVIEW: Team summary per step card
    // ═══════════════════════════════════════════════════
    private async Task BuildManagerOverviewAsync(WorkspaceViewModel vm)
    {
        // User name lookup
        var userMap = vm.Users.ToDictionary(u => u.UserId, u => u.FullName);

        // ── SLA: batch query all active violations ──
        var now = DateTime.Now;
        var slaViolations = await _uow.Repository<SlaTracking>().Query()
            .Where(t => t.Status == "ACTIVE" || t.Status == "WARNING" || t.Status == "OVERDUE")
            .Where(t => now >= t.WarningAt) // WARNING or DANGER only
            .Select(t => new { t.EntityType, t.EntityId })
            .ToListAsync();
        var violationSet = slaViolations.Select(v => (v.EntityType, v.EntityId)).ToHashSet();

        // ── Helper to build EmployeeSummaries from (userId, entityType, entityId) tuples ──
        List<EmployeeCardSummary> BuildSummaries(List<(int UserId, string EntityType, int EntityId)> items)
        {
            return items
                .GroupBy(x => x.UserId)
                .Select(g => new EmployeeCardSummary
                {
                    UserId = g.Key,
                    FullName = userMap.GetValueOrDefault(g.Key, "?"),
                    TaskCount = g.Count(),
                    SlaViolationCount = g.Count(x => violationSet.Contains((x.EntityType, x.EntityId)))
                })
                .OrderByDescending(e => e.SlaViolationCount)
                .ThenByDescending(e => e.TaskCount)
                .ToList();
        }

        // ── 1. RFQs INPROGRESS, no Quotation ──
        var step1 = await _uow.Repository<RFQ>().Query()
            .Include(r => r.Quotations)
            .Where(r => r.AssignedTo != null && r.Status == "INPROGRESS" && !r.Quotations.Any())
            .Select(r => new { UserId = r.AssignedTo!.Value, r.RfqId })
            .ToListAsync();
        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 1, Title = "Yêu cầu báo giá", Icon = "bi-clipboard-check", BadgeColor = "info",
            EmployeeSummaries = BuildSummaries(step1.Select(x => (x.UserId, "RFQ", x.RfqId)).ToList())
        });

        // ── 2. Quotations DRAFT ──
        var step2 = await _uow.Repository<Quotation>().Query()
            .Where(q => q.Status == "DRAFT")
            .Select(q => new { UserId = q.SalesPersonId, q.QuotationId })
            .ToListAsync();
        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 2, Title = "Báo giá chờ gửi", Icon = "bi-envelope", BadgeColor = "secondary",
            EmployeeSummaries = BuildSummaries(step2.Select(x => (x.UserId, "QUOTATION", x.QuotationId)).ToList())
        });

        // ── 3. Quotations SENT ──
        var step3 = await _uow.Repository<Quotation>().Query()
            .Where(q => q.Status == "SENT")
            .Select(q => new { UserId = q.SalesPersonId, q.QuotationId })
            .ToListAsync();
        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 3, Title = "Báo giá chờ phản hồi", Icon = "bi-hourglass-split", BadgeColor = "warning",
            EmployeeSummaries = BuildSummaries(step3.Select(x => (x.UserId, "QUOTATION", x.QuotationId)).ToList())
        });

        // ── 4. Quotations APPROVED ──
        //var step4 = await _uow.Repository<Quotation>().Query()
        //    .Where(q => q.Status == "APPROVED")
        //    .Select(q => new { UserId = q.SalesPersonId, q.QuotationId })
        //    .ToListAsync();
        //vm.Cards.Add(new WorkspaceCard
        //{
        //    StepNumber = 4, Title = "Báo giá đã duyệt", Icon = "bi-check-circle", BadgeColor = "success",
        //    EmployeeSummaries = BuildSummaries(step4.Select(x => (x.UserId, "QUOTATION", x.QuotationId)).ToList())
        //});

        // ── 4. SOs DRAFT ──
        var step4 = await _uow.Repository<SalesOrder>().Query()
            .Where(s => s.Status == "DRAFT" && s.CreatedBy != null)
            .Select(s => new { UserId = s.CreatedBy!.Value, s.SalesOrderId })
            .ToListAsync();
        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 4, Title = "Đơn hàng cần lập ĐNTU", Icon = "bi-file-earmark-excel", BadgeColor = "primary",
            EmployeeSummaries = BuildSummaries(step4.Select(x => (x.UserId, "SALES_ORDER", x.SalesOrderId)).ToList())
        });

        // ── 5. SOs WAIT ──
        var step5 = await _uow.Repository<SalesOrder>().Query()
            .Where(s => s.Status == "WAIT" && s.CreatedBy != null)
            .Select(s => new { UserId = s.CreatedBy!.Value, s.SalesOrderId })
            .ToListAsync();
        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 5, Title = "Đơn hàng chờ tạm ứng", Icon = "bi-cash-stack", BadgeColor = "warning",
            EmployeeSummaries = BuildSummaries(step5.Select(x => (x.UserId, "SALES_ORDER", x.SalesOrderId)).ToList())
        });

        // Pre-load stock transaction SO IDs
        var soIdsWithInbound = await _uow.Repository<StockTransaction>().Query()
            .Where(st => st.SalesOrderId != null && st.TransactionType == "INBOUND")
            .Select(st => st.SalesOrderId!.Value).Distinct().ToListAsync();

        var soIdsWithOutbound = await _uow.Repository<StockTransaction>().Query()
            .Where(st => st.SalesOrderId != null && st.TransactionType == "OUTBOUND")
            .Select(st => st.SalesOrderId!.Value).Distinct().ToListAsync();

        // ── 6. SOs BUYING, no INBOUND yet ──
        var step6 = await _uow.Repository<SalesOrder>().Query()
            .Where(s => s.Status == "BUYING" && s.CreatedBy != null && !soIdsWithInbound.Contains(s.SalesOrderId))
            .Select(s => new { UserId = s.CreatedBy!.Value, s.SalesOrderId })
            .ToListAsync();
        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 6, Title = "Đơn hàng đang mua", Icon = "bi-cart3", BadgeColor = "info",
            EmployeeSummaries = BuildSummaries(step6.Select(x => (x.UserId, "SALES_ORDER", x.SalesOrderId)).ToList())
        });

        // ── 7. SOs received (INBOUND, no OUTBOUND) ──
        var step7 = await _uow.Repository<SalesOrder>().Query()
            .Where(s => s.CreatedBy != null
                     && soIdsWithInbound.Contains(s.SalesOrderId)
                     && !soIdsWithOutbound.Contains(s.SalesOrderId)
                     && s.Status != "COMPLETED" && s.Status != "REPORTED" && s.Status != "CANCELLED")
            .Select(s => new { UserId = s.CreatedBy!.Value, s.SalesOrderId })
            .ToListAsync();
        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 7, Title = "Đơn hàng đã nhập kho", Icon = "bi-box-seam", BadgeColor = "primary",
            EmployeeSummaries = BuildSummaries(step7.Select(x => (x.UserId, "SALES_ORDER", x.SalesOrderId)).ToList())
        });

        // ── 8. SOs delivering (OUTBOUND, not DELIVERED) ──
        var step8 = await _uow.Repository<SalesOrder>().Query()
            .Where(s => s.CreatedBy != null
                     && soIdsWithOutbound.Contains(s.SalesOrderId)
                     && s.Status != "DELIVERED" && s.Status != "COMPLETED" && s.Status != "REPORTED" && s.Status != "CANCELLED")
            .Select(s => new { UserId = s.CreatedBy!.Value, s.SalesOrderId })
            .ToListAsync();
        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 8, Title = "Đơn hàng đã giao", Icon = "bi-truck", BadgeColor = "info",
            EmployeeSummaries = BuildSummaries(step8.Select(x => (x.UserId, "SALES_ORDER", x.SalesOrderId)).ToList())
        });

        // ── 9. SOs DELIVERED (pending settlement) ──
        var step9 = await _uow.Repository<SalesOrder>().Query()
            .Where(s => s.Status == "DELIVERED" && s.CreatedBy != null)
            .Select(s => new { UserId = s.CreatedBy!.Value, s.SalesOrderId })
            .ToListAsync();
        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 9, Title = "Đơn hàng chờ quyết toán", Icon = "bi-calculator", BadgeColor = "success",
            EmployeeSummaries = BuildSummaries(step9.Select(x => (x.UserId, "SALES_ORDER", x.SalesOrderId)).ToList())
        });

        // ── 10. SOs COMPLETED (pending KQKD report) ──
        var step10 = await _uow.Repository<SalesOrder>().Query()
            .Where(s => s.Status == "COMPLETED" && s.CreatedBy != null)
            .Select(s => new { UserId = s.CreatedBy!.Value, s.SalesOrderId })
            .ToListAsync();
        vm.Cards.Add(new WorkspaceCard
        {
            StepNumber = 10, Title = "Chờ báo cáo KQKD", Icon = "bi-file-earmark-bar-graph", BadgeColor = "primary",
            EmployeeSummaries = BuildSummaries(step10.Select(x => (x.UserId, "SALES_ORDER", x.SalesOrderId)).ToList())
        });
    }

    /// <summary>
    /// Gắn SLA severity (NORMAL/WARNING/DANGER) cho từng task item trên Workspace.
    /// Dùng batch query thay vì N+1.
    /// </summary>
    private async Task ApplySlaSeverityAsync(WorkspaceViewModel vm)
    {
        // Collect all active SLA trackings for current user
        var now = DateTime.Now;
        var activeTrackings = await _uow.Repository<SlaTracking>().Query()
            .Where(t => t.Status == "ACTIVE" || t.Status == "WARNING" || t.Status == "OVERDUE")
            .Select(t => new
            {
                t.EntityType,
                t.EntityId,
                Severity = now >= t.DeadlineAt ? "DANGER"
                         : now >= t.WarningAt ? "WARNING"
                         : "NORMAL",
                RemainingMinutes = EF.Functions.DateDiffMinute(now, t.DeadlineAt)
            })
            .ToListAsync();

        var severityMap = new Dictionary<(string, int), (string Severity, int RemainingMinutes)>();
        foreach (var t in activeTrackings)
        {
            var key = (t.EntityType, t.EntityId);
            if (!severityMap.ContainsKey(key) || SeverityRank(t.Severity) > SeverityRank(severityMap[key].Severity))
                severityMap[key] = (t.Severity, t.RemainingMinutes);
        }

        // Apply to task items
        foreach (var card in vm.Cards)
        {
            string? entityType = card.StepNumber switch
            {
                1 => "RFQ",
                2 or 3 => "QUOTATION",
                >= 4 and <= 11 => "SALES_ORDER",
                _ => null
            };

            if (entityType == null) continue;

            foreach (var task in card.Tasks)
            {
                if (severityMap.TryGetValue((task.EntityType!, task.EntityId), out var sla))
                {
                    task.SlaSeverity = sla.Severity;
                    if (sla.RemainingMinutes > 0)
                    {
                        var hours = sla.RemainingMinutes / 60.0;
                        task.SlaTimeRemaining = hours >= 1 ? $"{hours:F1}h" : $"{sla.RemainingMinutes}m";
                    }
                    else
                    {
                        var overdueHours = Math.Abs(sla.RemainingMinutes) / 60.0;
                        task.SlaTimeRemaining = overdueHours >= 1
                            ? $"-{overdueHours:F1}h"
                            : $"-{Math.Abs(sla.RemainingMinutes)}m";
                    }
                }
            }
        }
    }

    private static int ExtractEntityId(string url)
    {
        // URL format: /Controller/Detail/{id}
        var lastSlash = url.LastIndexOf('/');
        if (lastSlash >= 0 && int.TryParse(url[(lastSlash + 1)..], out var id))
            return id;
        return 0;
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "DANGER" => 2,
        "WARNING" => 1,
        _ => 0
    };
}
