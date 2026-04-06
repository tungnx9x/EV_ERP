using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Products;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    [RequireRole("ADMIN", "MANAGER")]
    public class ProductCategoryController : Controller
    {
        private readonly IProductCategoryService _categoryService;

        public ProductCategoryController(IProductCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        private int CurrentUserId =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

        // ── Index ────────────────────────────────────────
        public async Task<IActionResult> Index(string? keyword, string? status)
        {
            var vm = await _categoryService.GetListAsync(keyword, status);
            return View(vm);
        }

        // ── Create ───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = await _categoryService.GetFormAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductCategoryFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var fresh = await _categoryService.GetFormAsync();
                model.ParentOptions = fresh.ParentOptions;
                return View(model);
            }

            var (success, error) = await _categoryService.CreateAsync(model, CurrentUserId);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Tạo danh mục thất bại");
                var fresh = await _categoryService.GetFormAsync();
                model.ParentOptions = fresh.ParentOptions;
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã tạo danh mục '{model.CategoryName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Edit ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _categoryService.GetFormAsync(id);
            if (!vm.IsEditMode)
            {
                TempData["ErrorMessage"] = "Không tìm thấy danh mục";
                return RedirectToAction("Index");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductCategoryFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var fresh = await _categoryService.GetFormAsync(model.CategoryId);
                model.ParentOptions = fresh.ParentOptions;
                return View(model);
            }

            var (success, error) = await _categoryService.UpdateAsync(model, CurrentUserId);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Cập nhật thất bại");
                var fresh = await _categoryService.GetFormAsync(model.CategoryId);
                model.ParentOptions = fresh.ParentOptions;
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật danh mục '{model.CategoryName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Toggle Active (Ajax) ─────────────────────────
        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var (success, error) = await _categoryService.ToggleActiveAsync(id, CurrentUserId);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã cập nhật trạng thái" : error
            });
        }
    }
}
