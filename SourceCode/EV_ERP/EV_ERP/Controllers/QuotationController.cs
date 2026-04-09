using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Quotations;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers;

[RequireLogin]
public class QuotationController : Controller
{
    private readonly IQuotationService _quotationService;

    public QuotationController(IQuotationService quotationService)
    {
        _quotationService = quotationService;
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
        var vm = await _quotationService.GetListAsync(keyword, status, customerId, salesPersonId, page);
        ViewBag.CanEdit = CanEdit;
        return View(vm);
    }

    // ── Create ───────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Create(int? rfqId)
    {
        if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

        var vm = await _quotationService.GetFormAsync(rfqId: rfqId);
        // Default to current user as sales person
        if (vm.SalesPersonId == 0) vm.SalesPersonId = CurrentUserId;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] QuotationFormViewModel model)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

        var (success, error, quotationId) = await _quotationService.CreateAsync(model, CurrentUserId);
        if (!success)
            return Json(ApiResult<object>.Fail(error ?? "Tạo báo giá thất bại"));

        return Json(ApiResult<object>.Ok(new { QuotationId = quotationId }, "Đã tạo báo giá thành công"));
    }

    // ── Edit ─────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

        var vm = await _quotationService.GetFormAsync(id);
        if (!vm.IsEditMode)
        {
            TempData["ErrorMessage"] = "Không tìm thấy báo giá";
            return RedirectToAction("Index");
        }
        if (vm.CurrentStatus != "DRAFT")
        {
            TempData["ErrorMessage"] = "Chỉ có thể sửa báo giá ở trạng thái Nháp";
            return RedirectToAction("Detail", new { id });
        }
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromBody] QuotationFormViewModel model)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

        var (success, error) = await _quotationService.UpdateAsync(model, CurrentUserId);
        if (!success)
            return Json(ApiResult<object>.Fail(error ?? "Cập nhật thất bại"));

        return Json(ApiResult<object>.Ok(new { QuotationId = model.QuotationId }, "Đã cập nhật báo giá"));
    }

    // ── Detail ───────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var vm = await _quotationService.GetDetailAsync(id);
        if (vm == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy báo giá";
            return RedirectToAction("Index");
        }
        ViewBag.CanEdit = CanEdit;
        return View(vm);
    }

    // ── Status Actions (Ajax) ────────────────────────
    [HttpPost]
    public async Task<IActionResult> Send(int id)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _quotationService.SendAsync(id, CurrentUserId);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã gửi báo giá" : error });
    }

    [HttpPost]
    public async Task<IActionResult> Approve(int id)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _quotationService.ApproveAsync(id, CurrentUserId);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã duyệt báo giá" : error });
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, [FromBody] ReasonModel? model)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _quotationService.RejectAsync(id, CurrentUserId, model?.Reason);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã từ chối báo giá" : error });
    }

    [HttpPost]
    public async Task<IActionResult> Amend(int id)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error, newId) = await _quotationService.AmendAsync(id, CurrentUserId);
        if (!success)
            return Json(ApiResult<object>.Fail(error ?? "Lỗi tạo bản chỉnh sửa"));

        return Json(ApiResult<object>.Ok(new { QuotationId = newId }, "Đã tạo bản chỉnh sửa"));
    }

    [HttpPost]
    public async Task<IActionResult> Expire(int id)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _quotationService.ExpireAsync(id, CurrentUserId);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã đánh hết hạn" : error });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, [FromBody] ReasonModel? model)
    {
        if (!CanEdit)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var (success, error) = await _quotationService.CancelAsync(id, CurrentUserId, model?.Reason);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã hủy báo giá" : error });
    }

    // ── Export Excel ────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportExcel([FromBody] QuotationExportRequest request)
    {
        var result = await _quotationService.ExportExcelAsync(request);
        if (result == null)
            return BadRequest("Khong the xuat file");

        return File(result.Value.FileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.Value.FileName);
    }

    // ── Product Search (Ajax) ────────────────────────
    [HttpGet]
    public async Task<IActionResult> SearchProducts(string keyword)
    {
        var results = await _quotationService.SearchProductsAsync(keyword);
        return Json(results);
    }
}

// ── Shared request model ─────────────────────────────
public class ReasonModel
{
    public string? Reason { get; set; }
}
