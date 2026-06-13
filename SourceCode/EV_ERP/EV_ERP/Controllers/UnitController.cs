using EV_ERP.Filters;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Products;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    [RequireRole("ADMIN")]
    public class UnitController : Controller
    {
        private readonly IUnitService _unitService;

        public UnitController(IUnitService unitService)
        {
            _unitService = unitService;
        }

        // ── Index ────────────────────────────────────────
        public async Task<IActionResult> Index(string? keyword, string? status)
        {
            var vm = await _unitService.GetListAsync(keyword, status);
            return View(vm);
        }

        // ── Create ───────────────────────────────────────
        [HttpGet]
        public IActionResult Create() => View(new UnitFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UnitFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, error) = await _unitService.CreateAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Tạo đơn vị tính thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã tạo đơn vị '{model.UnitName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Edit ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _unitService.GetFormAsync(id);
            if (!vm.IsEditMode)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn vị tính";
                return RedirectToAction("Index");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UnitFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, error) = await _unitService.UpdateAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Cập nhật thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật đơn vị '{model.UnitName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Delete (Ajax) ────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var (success, error) = await _unitService.DeleteAsync(id);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã xóa đơn vị tính" : error
            });
        }
    }
}
