using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Products;
using EV_ERP.Models.ViewModels.SalesOrders;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers;

[RequireLogin]
public class SalesOrderController : Controller
{
    private readonly ISalesOrderService _salesOrderService;
    private readonly IProductService _productService;
    private readonly ILogger<SalesOrderController> _logger;

    public SalesOrderController(ISalesOrderService salesOrderService, IProductService productService, ILogger<SalesOrderController> logger)
    {
        _salesOrderService = salesOrderService;
        _productService = productService;
        _logger = logger;
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
        if (CanEdit && vm.Status == "DRAFT" && vm.HasUnmappedProducts)
        {
            var productForm = await _productService.GetFormAsync();
            ViewBag.ProductForm = productForm;
        }
        return View(vm);
    }

    // ── Create Product & Map (from SO Detail modal) ─────
    [HttpPost]
    public async Task<IActionResult> CreateProduct(int id, ProductFormViewModel model, IList<IFormFile>? GalleryFiles, int AvatarIndex = 0)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            model.GalleryFiles = GalleryFiles?.ToList();
            model.AvatarIndex = AvatarIndex;

            var (success, error, productId) = await _productService.CreateAsync(model, CurrentUserId);
            if (!success)
                return Json(ApiResult<object>.Fail(error ?? "Tạo sản phẩm thất bại"));

            // Auto-map to SO item
            if (model.SoItemId.HasValue && productId.HasValue)
            {
                var (mapOk, mapErr) = await _salesOrderService.MapProductToSOItemAsync(
                    id, model.SoItemId.Value, productId.Value, CurrentUserId);
                if (!mapOk)
                    return Json(ApiResult<object>.Fail(mapErr ?? "Tạo SP thành công nhưng gắn vào đơn hàng thất bại"));
            }

            return Json(ApiResult<object>.Ok(new { productId }, "Đã tạo sản phẩm và gắn vào đơn hàng"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateProduct failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── Update Draft Info (PO KH + file) ──────────────
    [HttpPost]
    public async Task<IActionResult> UpdateDraftInfo(int id, string? customerPoNo, IFormFile? customerPoFile)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var (success, error) = await _salesOrderService.UpdateDraftInfoAsync(id, customerPoNo, customerPoFile, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã lưu thông tin PO khách hàng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateDraftInfo failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── Create Product & Map to SO Item ────────────────
    [HttpPost]
    public async Task<IActionResult> CreateAndMapProduct(int id, [FromBody] QuickProductModel model)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var (success, error) = await _salesOrderService.CreateProductAndMapAsync(id, model.SOItemId, model, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã tạo và gắn sản phẩm" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateAndMapProduct failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── Status: DRAFT → WAIT ─────────────────────────
    [HttpPost]
    public async Task<IActionResult> SubmitWait(int id, [FromBody] SalesOrderDraftModel model)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var (success, error) = await _salesOrderService.SubmitWaitAsync(id, model, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã gửi đề nghị tạm ứng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitWait failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── Status: WAIT → BUYING ────────────────────────
    [HttpPost]
    public async Task<IActionResult> StartBuying(int id, [FromBody] SalesOrderBuyingModel model)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var (success, error) = await _salesOrderService.StartBuyingAsync(id, model, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã bắt đầu mua hàng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartBuying failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── Status: BUYING → RECEIVED ────────────────────
    [HttpPost]
    public async Task<IActionResult> ConfirmReceived(int id)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var (success, error) = await _salesOrderService.ConfirmReceivedAsync(id, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã xác nhận nhận hàng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ConfirmReceived failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── Status: RECEIVED → DELIVERING ────────────────
    [HttpPost]
    public async Task<IActionResult> StartDelivering(int id)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var (success, error) = await _salesOrderService.StartDeliveringAsync(id, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã bàn giao cho vận chuyển" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartDelivering failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── Status: DELIVERING → DELIVERED ───────────────
    [HttpPost]
    public async Task<IActionResult> ConfirmDelivered(int id)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var (success, error) = await _salesOrderService.ConfirmDeliveredAsync(id, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Khách hàng đã nhận hàng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ConfirmDelivered failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── Status: DELIVERED → RETURNED ────────────────
    [HttpPost]
    public async Task<IActionResult> Return(int id, [FromBody] SalesOrderReturnModel model)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn kh��ng có quyền"));

            var (success, error) = await _salesOrderService.ReturnAsync(id, model, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã xác nhận trả hàng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Return failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── Status: DELIVERED|RETURNED → COMPLETED ────────────────
    [HttpPost]
    public async Task<IActionResult> Complete(int id, [FromBody] SalesOrderCompleteModel model)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var (success, error) = await _salesOrderService.CompleteAsync(id, model, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã hoàn tất đơn hàng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Complete failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── Status: COMPLETED → REPORTED ────────────────
    [HttpPost]
    public async Task<IActionResult> Report(int id)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var (success, error) = await _salesOrderService.ReportAsync(id, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã nộp báo cáo KQKD" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }

    // ── Cancel ───────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Cancel(int id, [FromBody] ReasonModel? model)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var (success, error) = await _salesOrderService.CancelAsync(id, CurrentUserId, model?.Reason);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã hủy đơn hàng" : error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancel failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
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

    // ── Export Product Template Excel ────────────────
    [HttpGet]
    public async Task<IActionResult> ExportProductTemplate(int id)
    {
        if (!CanEdit) return BadRequest("Bạn không có quyền");

        var result = await _salesOrderService.ExportProductTemplateAsync(id);
        if (result == null)
            return BadRequest("Không thể xuất template");

        return File(result.Value.FileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.Value.FileName);
    }

    // ── Import Products from Excel ──────────────────
    [HttpPost]
    public async Task<IActionResult> ImportProducts(int id, IFormFile file)
    {
        try
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            if (file == null || file.Length == 0)
                return Json(ApiResult<object>.Fail("Chưa chọn file"));

            var (success, message, created, mapped) =
                await _salesOrderService.ImportProductsAsync(id, file, CurrentUserId);

            return Json(new ApiResult<object>
            {
                Success = success,
                Message = message,
                Data = new { created, mapped }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportProducts failed for SO #{Id}", id);
            return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + ex.Message));
        }
    }
}
