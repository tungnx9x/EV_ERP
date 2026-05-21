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

    // Sales-side: assignee or manager (used for create/delete)
    private async Task<bool> CanManageAsync(int salesOrderId)
    {
        if (IsManager) return true;
        var assigneeId = await _salesOrderService.GetSalesPersonIdAsync(salesOrderId);
        return assigneeId.HasValue && assigneeId.Value == CurrentUserId;
    }

    // Accountant-side: status transitions (approve/receive/reject)
    private bool CanApproveAdvance => CurrentRoleCode is "ADMIN" or "MANAGER" or "ACCOUNTANT";

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

    // ── GET: advance request shaped for edit modal ────
    [HttpGet]
    public async Task<IActionResult> GetForEdit(int id)
    {
        try
        {
            var row = await _advanceService.GetForEditAsync(id);
            if (row == null) return Json(ApiResult<object>.Fail("Không tìm thấy đề nghị tạm ứng"));
            return Json(ApiResult<AdvanceRequestRow>.Ok(row));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdvanceRequest.GetForEdit failed for #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── POST: update an existing advance request (PENDING only) ──
    [HttpPost]
    public async Task<IActionResult> Update(int id, int salesOrderId, [FromBody] AdvanceRequestCreateModel model)
    {
        try
        {
            if (!await CanManageAsync(salesOrderId))
                return Json(ApiResult<object>.Fail("Chỉ người phụ trách hoặc quản lý mới có quyền thực hiện"));

            var (success, error) = await _advanceService.UpdateAsync(id, model, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã cập nhật đề nghị tạm ứng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdvanceRequest.Update failed for #{Id}", id);
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

    // ── POST: accountant approves a pending advance request ──
    [HttpPost]
    public async Task<IActionResult> Approve(int id)
    {
        try
        {
            if (!CanApproveAdvance)
                return Json(ApiResult<object>.Fail("Chỉ Kế toán hoặc Quản lý mới có quyền duyệt"));

            var (success, error) = await _advanceService.ApproveAsync(id, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã duyệt đề nghị tạm ứng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdvanceRequest.Approve failed for #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── POST: accountant confirms the money has been transferred ──
    [HttpPost]
    public async Task<IActionResult> MarkReceived(int id)
    {
        try
        {
            if (!CanApproveAdvance)
                return Json(ApiResult<object>.Fail("Chỉ Kế toán hoặc Quản lý mới có quyền xác nhận chuyển tiền"));

            var (success, error) = await _advanceService.MarkReceivedAsync(id, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã xác nhận chuyển tiền" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdvanceRequest.MarkReceived failed for #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── POST: accountant rejects an advance request ──
    public class RejectModel { public string? Reason { get; set; } }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectModel? model)
    {
        try
        {
            if (!CanApproveAdvance)
                return Json(ApiResult<object>.Fail("Chỉ Kế toán hoặc Quản lý mới có quyền từ chối"));

            var (success, error) = await _advanceService.RejectAsync(id, model?.Reason, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã từ chối đề nghị tạm ứng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdvanceRequest.Reject failed for #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── GET: export DNTU Excel for a specific advance request ──
    [HttpGet]
    public async Task<IActionResult> ExportDntu(int id)
    {
        var result = await _advanceService.ExportDntuAsync(id, CurrentUserId);
        if (result == null) return BadRequest("Không thể xuất file DNTU");

        return File(result.Value.FileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.Value.FileName);
    }
}
