using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Stock;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    public class WarehouseController : Controller
    {
        private readonly IWarehouseService _warehouseService;

        public WarehouseController(IWarehouseService warehouseService)
        {
            _warehouseService = warehouseService;
        }

        private int CurrentUserId =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

        private string CurrentRoleCode =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.RoleCode;

        private bool CanEdit => CurrentRoleCode is "ADMIN" or "MANAGER" or "WAREHOUSE";
        private bool CanDelete => CurrentRoleCode is "ADMIN" or "MANAGER";

        // ── Index ────────────────────────────────────────
        public async Task<IActionResult> Index(string? keyword, string? status, int page = 1)
        {
            var vm = await _warehouseService.GetListAsync(keyword, status, page);
            ViewBag.CanEdit = CanEdit;
            ViewBag.CanDelete = CanDelete;
            return View(vm);
        }

        // ── Create ───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");
            var vm = await _warehouseService.GetFormAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WarehouseFormViewModel model)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            if (!ModelState.IsValid)
            {
                var fresh = await _warehouseService.GetFormAsync();
                model.Managers = fresh.Managers;
                return View(model);
            }

            var (success, error) = await _warehouseService.CreateAsync(model, CurrentUserId);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Tạo kho thất bại");
                var fresh = await _warehouseService.GetFormAsync();
                model.Managers = fresh.Managers;
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã tạo kho '{model.WarehouseName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Edit ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");
            var vm = await _warehouseService.GetFormAsync(id);
            if (!vm.IsEditMode) return NotFound();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(WarehouseFormViewModel model)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            if (!ModelState.IsValid)
            {
                var fresh = await _warehouseService.GetFormAsync(model.WarehouseId);
                model.Managers = fresh.Managers;
                return View(model);
            }

            var (success, error) = await _warehouseService.UpdateAsync(model, CurrentUserId);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Cập nhật kho thất bại");
                var fresh = await _warehouseService.GetFormAsync(model.WarehouseId);
                model.Managers = fresh.Managers;
                return View(model);
            }

            TempData["SuccessMessage"] = "Cập nhật kho thành công!";
            return RedirectToAction("Detail", new { id = model.WarehouseId });
        }

        // ── Detail ───────────────────────────────────────
        public async Task<IActionResult> Detail(int id)
        {
            var vm = await _warehouseService.GetDetailAsync(id);
            if (vm == null) return NotFound();
            ViewBag.CanEdit = CanEdit;
            ViewBag.CanDelete = CanDelete;
            return View(vm);
        }

        // ── Toggle Active (Ajax) ─────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            if (!CanDelete)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error) = await _warehouseService.ToggleActiveAsync(id);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Thành công" : error });
        }

        // ── Save Location (Ajax) ─────────────────────────
        [HttpPost]
        public async Task<IActionResult> SaveLocation([FromBody] LocationFormModel model)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error) = await _warehouseService.SaveLocationAsync(model);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Đã lưu vị trí" : error });
        }

        // ── Toggle Location Active (Ajax) ────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLocationActive(int id)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error) = await _warehouseService.ToggleLocationActiveAsync(id);
            return Json(new ApiResult<object> { Success = success, Message = success ? "Thành công" : error });
        }
    }
}
