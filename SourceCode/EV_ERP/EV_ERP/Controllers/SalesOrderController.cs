using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.SalesOrders;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers;

[RequireLogin]
public class SalesOrderController : Controller
{
    private readonly ISalesOrderService _salesOrderService;

    public SalesOrderController(ISalesOrderService salesOrderService)
    {
        _salesOrderService = salesOrderService;
    }

    private int CurrentUserId =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

    private string CurrentRoleCode =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.RoleCode;

    private bool CanEdit => CurrentRoleCode is "ADMIN" or "MANAGER" or "SALES";

    // ── Index ────────────────────────────────────────
    public async Task<IActionResult> Index(
        string? keyword, string? status, int? customerId, int? salesPersonId, int page = 1)
    {
        var vm = await _salesOrderService.GetListAsync(keyword, status, customerId, salesPersonId, page);
        ViewBag.CanEdit = CanEdit;
        return View(vm);
    }

    // ── Detail ───────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var vm = await _salesOrderService.GetDetailAsync(id);
        if (vm == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng";
            return RedirectToAction("Index");
        }
        ViewBag.CanEdit = CanEdit;
        return View(vm);
    }

    // ── Update Draft Info (PO KH + file) ──────────────
    [HttpPost]
    public async Task<IActionResult> UpdateDraftInfo(int id, string? customerPoNo, IFormFile? customerPoFile)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _salesOrderService.UpdateDraftInfoAsync(id, customerPoNo, customerPoFile, CurrentUserId);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã lưu thông tin PO khách hàng" : error });
    }

    // ── Status: DRAFT → WAIT ─────────────────────────
    [HttpPost]
    public async Task<IActionResult> SubmitWait(int id, [FromBody] SalesOrderDraftModel model)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _salesOrderService.SubmitWaitAsync(id, model, CurrentUserId);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã gửi đề nghị tạm ứng" : error });
    }

    // ── Status: WAIT → BUYING ────────────────────────
    [HttpPost]
    public async Task<IActionResult> StartBuying(int id, [FromBody] SalesOrderBuyingModel model)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _salesOrderService.StartBuyingAsync(id, model, CurrentUserId);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã bắt đầu mua hàng" : error });
    }

    // ── Status: BUYING → RECEIVED ────────────────────
    [HttpPost]
    public async Task<IActionResult> ConfirmReceived(int id)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _salesOrderService.ConfirmReceivedAsync(id, CurrentUserId);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã xác nhận nhận hàng" : error });
    }

    // ── Status: RECEIVED → DELIVERING ────────────────
    [HttpPost]
    public async Task<IActionResult> StartDelivering(int id)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _salesOrderService.StartDeliveringAsync(id, CurrentUserId);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã bàn giao cho vận chuyển" : error });
    }

    // ── Status: DELIVERING → DELIVERED ───────────────
    [HttpPost]
    public async Task<IActionResult> ConfirmDelivered(int id)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _salesOrderService.ConfirmDeliveredAsync(id, CurrentUserId);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Khách hàng đã nhận hàng" : error });
    }

    // ── Status: DELIVERED → COMPLETED ────────────────
    [HttpPost]
    public async Task<IActionResult> Complete(int id, [FromBody] SalesOrderCompleteModel model)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _salesOrderService.CompleteAsync(id, model, CurrentUserId);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã hoàn tất đơn hàng" : error });
    }

    // ── Cancel ───────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Cancel(int id, [FromBody] ReasonModel? model)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _salesOrderService.CancelAsync(id, CurrentUserId, model?.Reason);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã hủy đơn hàng" : error });
    }

    // ── Export ĐNTU Excel ────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ExportDntu(int id)
    {
        var result = await _salesOrderService.ExportDntuAsync(id, CurrentUserId);
        if (result == null)
            return BadRequest("Không thể xuất file");

        return File(result.Value.FileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.Value.FileName);
    }
}
