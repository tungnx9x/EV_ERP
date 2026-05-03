using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.RFQs;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers;

[RequireLogin]
public class RfqController : Controller
{
    private readonly IRfqService _rfqService;
    private readonly IQuotationService _quotationService;

    public RfqController(IRfqService rfqService, IQuotationService quotationService)
    {
        _rfqService = rfqService;
        _quotationService = quotationService;
    }

    private int CurrentUserId =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

    private string CurrentRoleCode =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.RoleCode;

    private bool CanCreate => CurrentRoleCode is "ADMIN" or "MANAGER" or "SALES";

    private bool IsOwner(int createdBy) => createdBy == CurrentUserId;

    // ── Index ────────────────────────────────────────
    public async Task<IActionResult> Index(
        string? keyword, string? priority,
        int? assignedTo, int? createdBy, int? customerId, int page = 1)
    {
        // First visit (no querystring) defaults to "filter by me as creator".
        // Once the user submits the form or paginates, the querystring is present
        // and the actual createdBy value (including null = "Tất cả") is respected.
        if (!Request.Query.Any())
            createdBy = CurrentUserId;

        var vm = await _rfqService.GetListAsync(keyword, priority, assignedTo, createdBy, customerId, page);
        ViewBag.CanCreate = CanCreate;
        ViewBag.CurrentUserId = CurrentUserId;
        return View(vm);
    }

    // ── Create ───────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!CanCreate) return RedirectToAction("AccessDenied", "Auth");

        var vm = await _rfqService.GetFormAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] RfqFormViewModel model)
    {
        if (!CanCreate)
            return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

        var (success, error, rfqId) = await _rfqService.CreateAsync(model, CurrentUserId);
        if (!success)
            return Json(ApiResult<object>.Fail(error ?? "Tạo yêu cầu báo giá thất bại"));

        return Json(ApiResult<object>.Ok(new { RfqId = rfqId }, "Đã tạo yêu cầu báo giá thành công"));
    }

    // ── Edit ─────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _rfqService.GetFormAsync(id);
        if (!vm.IsEditMode)
        {
            TempData["ErrorMessage"] = "Không tìm thấy yêu cầu báo giá";
            return RedirectToAction("Index");
        }
        if (!IsOwner(vm.CreatedBy ?? 0))
        {
            TempData["ErrorMessage"] = "Bạn chỉ có thể sửa RFQ do mình tạo";
            return RedirectToAction("Detail", new { id });
        }
        if (vm.CurrentStatus != "INPROGRESS")
        {
            TempData["ErrorMessage"] = "Chỉ có thể sửa RFQ đang xử lý";
            return RedirectToAction("Detail", new { id });
        }
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromBody] RfqFormViewModel model)
    {
        if (model.RfqId.HasValue)
        {
            var existing = await _rfqService.GetFormAsync(model.RfqId.Value);
            if (!IsOwner(existing.CreatedBy ?? 0))
                return Json(ApiResult<object>.Fail("Bạn chỉ có thể sửa RFQ do mình tạo"));
        }

        var (success, error) = await _rfqService.UpdateAsync(model, CurrentUserId);
        if (!success)
            return Json(ApiResult<object>.Fail(error ?? "Cập nhật thất bại"));

        return Json(ApiResult<object>.Ok(new { RfqId = model.RfqId }, "Đã cập nhật yêu cầu báo giá"));
    }

    // ── Detail ───────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var vm = await _rfqService.GetDetailAsync(id);
        if (vm == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy yêu cầu báo giá";
            return RedirectToAction("Index");
        }
        ViewBag.CanCreate = CanCreate;
        ViewBag.CurrentUserId = CurrentUserId;

        // SalesPersons for import quotation modal
        var formVm = await _quotationService.GetFormAsync();
        ViewBag.SalesPersons = formVm.SalesPersons;

        return View(vm);
    }

    // ── Image Upload (CKEditor) ──────────────────────
    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (!CanCreate)
            return Json(ApiResult<object>.Fail("Bạn không có quyền"));

        var url = await _rfqService.UploadImageAsync(file);
        if (url == null)
            return Json(ApiResult<object>.Fail("Upload thất bại — chỉ hỗ trợ JPG/PNG/GIF/WEBP, tối đa 5MB"));

        return Json(ApiResult<object>.Ok(new { url }));
    }

    // ── Cancel (Ajax) ────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Cancel(int id, [FromBody] ReasonRequest? model)
    {
        var existing = await _rfqService.GetDetailAsync(id);
        if (existing == null)
            return Json(ApiResult<object>.Fail("Không tìm thấy RFQ"));
        if (!IsOwner(existing.CreatedBy))
            return Json(ApiResult<object>.Fail("Bạn chỉ có thể hủy RFQ do mình tạo"));

        var (success, error) = await _rfqService.CancelAsync(id, CurrentUserId, model?.Reason);
        return Json(new ApiResult<object> { Success = success, Message = success ? "Đã hủy RFQ" : error });
    }
}

public class ReasonRequest
{
    public string? Reason { get; set; }
}
