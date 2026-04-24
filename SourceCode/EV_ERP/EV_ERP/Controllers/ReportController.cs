using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Reports;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers;

[RequireLogin]
public class ReportController : Controller
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportController> _logger;

    public ReportController(IReportService reportService, ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    private string CurrentRoleCode =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.RoleCode;

    private bool CanView => CurrentRoleCode is "ADMIN" or "MANAGER";

    // ── Sales Revenue page ──────────────────────────────
    [HttpGet]
    public async Task<IActionResult> SalesRevenue()
    {
        if (!CanView) return RedirectToAction("AccessDenied", "Auth");

        var filter = new SalesRevenueFilterViewModel
        {
            DateFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            DateTo = DateTime.Today
        };

        var vm = await _reportService.GetSalesRevenueAsync(filter);
        return View(vm);
    }

    // ── AJAX filter ─────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalesRevenue([FromBody] SalesRevenueFilterViewModel filter)
    {
        if (!CanView) return Forbid();

        try
        {
            var vm = await _reportService.GetSalesRevenueAsync(filter);
            return Json(new
            {
                Success = true,
                Data = new
                {
                    vm.TotalRevenue,
                    vm.TotalOrders,
                    vm.AverageOrderValue,
                    vm.ChartLabels,
                    vm.ChartData,
                    vm.Rows
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering sales revenue report");
            return Json(new { Success = false, Message = "Lỗi khi lọc báo cáo" });
        }
    }

    // ── Export Excel ─────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportSalesRevenue([FromBody] SalesRevenueFilterViewModel filter)
    {
        if (!CanView) return Forbid();

        try
        {
            var result = await _reportService.ExportSalesRevenueExcelAsync(filter);
            if (result == null)
                return Json(new { Success = false, Message = "Không có dữ liệu để xuất" });

            return File(result.Value.FileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                result.Value.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting sales revenue report");
            return Json(new { Success = false, Message = "Lỗi khi xuất báo cáo" });
        }
    }
}
