using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Stock;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    public class StockController : Controller
    {
        private readonly IStockService _stockService;
        private readonly IWarehouseService _warehouseService;
        private readonly ISalesOrderService _salesOrderService;
        private readonly IAttachmentService _attachmentService;

        public StockController(IStockService stockService, IWarehouseService warehouseService,
            ISalesOrderService salesOrderService, IAttachmentService attachmentService)
        {
            _stockService = stockService;
            _warehouseService = warehouseService;
            _salesOrderService = salesOrderService;
            _attachmentService = attachmentService;
        }

        private int CurrentUserId =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

        private string CurrentRoleCode =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.RoleCode;

        private bool CanEdit => CurrentRoleCode is "ADMIN" or "MANAGER" or "WAREHOUSE";
        private bool CanConfirm => CurrentRoleCode is "ADMIN" or "MANAGER" or "WAREHOUSE";

        // ── Index ────────────────────────────────────────
        public async Task<IActionResult> Index(string? keyword, string? type, string? status, int? warehouseId)
        {
            var vm = await _stockService.GetListAsync(keyword, type, status, warehouseId);
            ViewBag.CanEdit = CanEdit;
            return View(vm);
        }

        // ── Detail ───────────────────────────────────────
        public async Task<IActionResult> Detail(long id)
        {
            var vm = await _stockService.GetDetailAsync(id);
            if (vm == null) return NotFound();
            ViewBag.CanEdit = CanEdit;
            ViewBag.CanConfirm = CanConfirm;
            return View(vm);
        }

        // ── Create ────────��──────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Create(string? type, int? salesOrderId)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");
            var vm = await _stockService.GetFormAsync(null, type ?? "INBOUND", salesOrderId);
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] StockTransactionFormViewModel model)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error, transactionId) = await _stockService.SaveAsync(model, CurrentUserId);
            if (!success)
                return Json(ApiResult<object>.Fail(error ?? "Tạo phiếu kho th��t bại"));

            return Json(ApiResult<object>.Ok(new { TransactionId = transactionId }, "Đã tạo phiếu kho thành công"));
        }

        // ── Edit ─���───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");
            var vm = await _stockService.GetFormAsync(id);
            if (!vm.IsEditMode) return NotFound();
            return View("Create", vm); // Reuse Create view
        }

        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] StockTransactionFormViewModel model)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error, transactionId) = await _stockService.SaveAsync(model, CurrentUserId);
            if (!success)
                return Json(ApiResult<object>.Fail(error ?? "Cập nhật phiếu kho thất bại"));

            return Json(ApiResult<object>.Ok(new { TransactionId = transactionId }, "Đã cập nhật phiếu kho"));
        }

        // ── Confirm Inbound (Ajax) ───────────────────────
        [HttpPost]
        public async Task<IActionResult> ConfirmInbound(long id)
        {
            if (!CanConfirm)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error) = await _stockService.ConfirmInboundAsync(id, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã xác nhận nhập kho" : error });
        }

        // ── Start Delivery (Ajax) ──────���─────────────────
        [HttpPost]
        public async Task<IActionResult> StartDelivery(long id)
        {
            if (!CanConfirm)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error) = await _stockService.StartDeliveryAsync(id, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã bắt đầu giao hàng" : error });
        }

        // ── Confirm Delivered (Ajax) ─────────────────────
        [HttpPost]
        public async Task<IActionResult> ConfirmDelivered(long id, [FromBody] DeliveryConfirmModel model)
        {
            if (!CanConfirm)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error) = await _stockService.ConfirmDeliveredAsync(id, model, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã xác nhận giao hàng thành công" : error });
        }

        // ── Cancel (Ajax) ──────���─────────────────────────
        [HttpPost]
        public async Task<IActionResult> Cancel(long id)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quy���n thực hiện thao tác này"));

            var (success, error) = await _stockService.CancelAsync(id, CurrentUserId);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đ�� hủy phiếu" : error });
        }

        // ── Barcode Lookup (Ajax) ────────────────────────
        [HttpGet]
        public async Task<IActionResult> LookupBarcode(string barcode, int? warehouseId)
        {
            var result = await _stockService.LookupBarcodeAsync(barcode, warehouseId);
            if (result == null)
                return Json(ApiResult<object>.Fail("Không tìm thấy sản phẩm với mã barcode/mã SP này"));

            return Json(ApiResult<BarcodeLookupResult>.Ok(result));
        }

        // ── Get Locations for Warehouse (Ajax) ───────────
        [HttpGet]
        public async Task<IActionResult> GetLocations(int warehouseId)
        {
            var locations = await _stockService.GetLocationOptionsAsync(warehouseId);
            return Json(ApiResult<List<LocationOptionViewModel>>.Ok(locations));
        }

        // ── Lookup Sales Order by code (Ajax) ───────────
        [HttpGet]
        public async Task<IActionResult> LookupSalesOrder(string soCode)
        {
            var result = await _stockService.LookupSalesOrderAsync(soCode?.Trim());
            if (result == null)
                return Json(ApiResult<object>.Fail("Không tìm thấy đơn hàng với mã này"));

            return Json(ApiResult<object>.Ok(result));
        }

        // ── Upload Image (Ajax) ─────────────────────────
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file, long transactionId, string? description)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var result = await _attachmentService.UploadImageAsync(
                file, "STOCK_TRANSACTION", (int)transactionId,
                "STOCK_PHOTO", description, CurrentUserId);

            if (result == null)
                return Json(ApiResult<object>.Fail("Upload thất bại — chỉ hỗ trợ JPG/PNG/GIF/WEBP, tối đa 5MB"));

            return Json(ApiResult<object>.Ok(result));
        }

        // ── Upload Image (temp, before transaction saved) ─
        [HttpPost]
        public async Task<IActionResult> UploadTempImage(IFormFile file, string? description)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            // Use 0 as placeholder referenceId — will be updated after save
            var result = await _attachmentService.UploadImageAsync(
                file, "STOCK_TRANSACTION", 0,
                "STOCK_PHOTO", description, CurrentUserId);

            if (result == null)
                return Json(ApiResult<object>.Fail("Upload thất bại — chỉ hỗ trợ JPG/PNG/GIF/WEBP, tối đa 5MB"));

            return Json(ApiResult<object>.Ok(result));
        }

        // ── Delete Image (Ajax) ─────────────────────────
        [HttpPost]
        public async Task<IActionResult> DeleteImage(int attachmentId)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền"));

            var ok = await _attachmentService.DeleteAsync(attachmentId, CurrentUserId);
            return Json(new ApiResult<object> { Success = ok, Message = ok ? "Đã xóa" : "Không tìm thấy ảnh" });
        }

        // ── Get Images (Ajax) ───────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetImages(long transactionId)
        {
            var images = await _attachmentService.GetListAsync("STOCK_TRANSACTION", (int)transactionId);
            return Json(ApiResult<object>.Ok(images));
        }
    }
}
