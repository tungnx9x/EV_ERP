using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Vendors;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    public class VendorController : Controller
    {
        private readonly IVendorService _vendorService;

        public VendorController(IVendorService vendorService)
        {
            _vendorService = vendorService;
        }

        private int CurrentUserId =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

        private string CurrentRoleCode =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.RoleCode;

        private bool CanEdit => CurrentRoleCode is "ADMIN" or "MANAGER" or "ACCOUNTANT";
        private bool CanDelete => CurrentRoleCode is "ADMIN" or "MANAGER";

        // ── Index ────────────────────────────────────────
        public async Task<IActionResult> Index(string? keyword, string? status)
        {
            var vm = await _vendorService.GetListAsync(keyword, status);
            ViewBag.CanEdit = CanEdit;
            ViewBag.CanDelete = CanDelete;
            return View(vm);
        }

        // ── Create ───────────────────────────────────────
        [HttpGet]
        public IActionResult Create()
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");
            return View(new VendorFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VendorFormViewModel model)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            if (!ModelState.IsValid)
                return View(model);

            var (success, error) = await _vendorService.CreateAsync(model, CurrentUserId);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Tạo nhà cung cấp thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã tạo nhà cung cấp '{model.VendorName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Edit ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            var vm = await _vendorService.GetFormAsync(id);
            if (!vm.IsEditMode)
            {
                TempData["ErrorMessage"] = "Không tìm thấy nhà cung cấp";
                return RedirectToAction("Index");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(VendorFormViewModel model)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            if (!ModelState.IsValid)
                return View(model);

            var (success, error) = await _vendorService.UpdateAsync(model, CurrentUserId);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Cập nhật thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật '{model.VendorName}' thành công!";
            return RedirectToAction("Detail", new { id = model.VendorId });
        }

        // ── Detail ───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var vm = await _vendorService.GetDetailAsync(id);
            if (vm == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy nhà cung cấp";
                return RedirectToAction("Index");
            }
            ViewBag.CanEdit = CanEdit;
            ViewBag.CanDelete = CanDelete;
            return View(vm);
        }

        // ── Toggle Active (Ajax) ─────────────────────────
        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id)
        {
            if (!CanDelete)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error) = await _vendorService.ToggleActiveAsync(id, CurrentUserId);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã cập nhật trạng thái" : error
            });
        }

        // ── Save Contact (Ajax) ──────────────────────────
        [HttpPost]
        public async Task<IActionResult> SaveContact([FromBody] VendorContactFormModel model)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage).ToList();
                return Json(ApiResult<object>.Fail("Dữ liệu không hợp lệ", errors));
            }

            var (success, error) = await _vendorService.SaveContactAsync(model);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã lưu liên hệ" : error
            });
        }

        // ── Delete Contact (Ajax) ────────────────────────
        [HttpPost]
        public async Task<IActionResult> DeleteContact(int id)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error) = await _vendorService.DeleteContactAsync(id);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã xóa liên hệ" : error
            });
        }
    }
}
