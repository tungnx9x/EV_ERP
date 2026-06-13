using EV_ERP.Filters;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Customers;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    [RequireRole("ADMIN")]
    public class CustomerGroupController : Controller
    {
        private readonly ICustomerGroupService _groupService;

        public CustomerGroupController(ICustomerGroupService groupService)
        {
            _groupService = groupService;
        }

        // ── Index ────────────────────────────────────────
        public async Task<IActionResult> Index(string? keyword, string? status)
        {
            var vm = await _groupService.GetListAsync(keyword, status);
            return View(vm);
        }

        // ── Create ───────────────────────────────────────
        [HttpGet]
        public IActionResult Create() => View(new CustomerGroupFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerGroupFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, error) = await _groupService.CreateAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Tạo nhóm khách hàng thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã tạo nhóm '{model.GroupName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Edit ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _groupService.GetFormAsync(id);
            if (!vm.IsEditMode)
            {
                TempData["ErrorMessage"] = "Không tìm thấy nhóm khách hàng";
                return RedirectToAction("Index");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CustomerGroupFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, error) = await _groupService.UpdateAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Cập nhật thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật nhóm '{model.GroupName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Delete (Ajax) ────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var (success, error) = await _groupService.DeleteAsync(id);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã xóa nhóm khách hàng" : error
            });
        }
    }
}
