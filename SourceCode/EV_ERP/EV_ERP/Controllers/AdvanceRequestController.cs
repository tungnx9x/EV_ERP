using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Finance;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers;

[RequireLogin]
public class AdvanceRequestController : Controller
{
    private readonly IAdvanceRequestService _advanceService;
    private readonly ISalesOrderService _salesOrderService;
    private readonly ILogger<AdvanceRequestController> _logger;

    public AdvanceRequestController(
        IAdvanceRequestService advanceService,
        ISalesOrderService salesOrderService,
        ILogger<AdvanceRequestController> logger)
    {
        _advanceService = advanceService;
        _salesOrderService = salesOrderService;
        _logger = logger;
    }

    private int CurrentUserId =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

    private string CurrentRoleCode =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.RoleCode;

    private bool IsManager => CurrentRoleCode is "ADMIN" or "MANAGER";

    private async Task<bool> CanManageAsync(int salesOrderId)
    {
        if (IsManager) return true;
        var assigneeId = await _salesOrderService.GetSalesPersonIdAsync(salesOrderId);
        return assigneeId.HasValue && assigneeId.Value == CurrentUserId;
    }

    // ── GET: advance summary for a SO (used for AJAX refresh) ──
    [HttpGet]
    public async Task<IActionResult> GetForSalesOrder(int salesOrderId)
    {
        try
        {
            var data = await _advanceService.GetForSalesOrderAsync(salesOrderId);
            return Json(ApiResult<SalesOrderAdvanceSummary>.Ok(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdvanceRequest.GetForSalesOrder failed for SO #{Id}", salesOrderId);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── POST: create new advance request for a SO ──────
    [HttpPost]
    public async Task<IActionResult> Create(int salesOrderId, [FromBody] AdvanceRequestCreateModel model)
    {
        try
        {
            if (!await CanManageAsync(salesOrderId))
                return Json(ApiResult<object>.Fail("Chỉ người phụ trách hoặc quản lý mới có quyền thực hiện"));

            var (success, error, id) = await _advanceService.CreateAsync(salesOrderId, model, CurrentUserId);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã tạo đề nghị tạm ứng" : error,
                Data = success ? new { advanceRequestId = id } : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdvanceRequest.Create failed for SO #{Id}", salesOrderId);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── POST: delete an advance request ────────────────
    [HttpPost]
    public async Task<IActionResult> Delete(int id, int salesOrderId)
    {
        try
        {
            if (!await CanManageAsync(salesOrderId))
                return Json(ApiResult<object>.Fail("Chỉ người phụ trách hoặc quản lý mới có quyền thực hiện"));

            var (success, error) = await _advanceService.DeleteAsync(id, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã xóa đề nghị tạm ứng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdvanceRequest.Delete failed for #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }
}
