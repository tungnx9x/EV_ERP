using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Customers;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        private int CurrentUserId =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

        private string CurrentRoleCode =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.RoleCode;

        private bool CanEdit => CurrentRoleCode is "ADMIN" or "MANAGER" or "SALES";
        private bool CanDelete => CurrentRoleCode is "ADMIN" or "MANAGER";

        // ── Index ────────────────────────────────────────
        public async Task<IActionResult> Index(string? keyword, int? groupId, string? status)
        {
            var vm = await _customerService.GetListAsync(keyword, groupId, status);
            ViewBag.CanEdit = CanEdit;
            ViewBag.CanDelete = CanDelete;
            return View(vm);
        }

        // ── Create ───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            var vm = await _customerService.GetFormAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerFormViewModel model)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            if (!ModelState.IsValid)
            {
                var fresh = await _customerService.GetFormAsync();
                model.Groups = fresh.Groups;
                model.SalesPersons = fresh.SalesPersons;
                return View(model);
            }

            var (success, error) = await _customerService.CreateAsync(model, CurrentUserId);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Tạo khách hàng thất bại");
                var fresh = await _customerService.GetFormAsync();
                model.Groups = fresh.Groups;
                model.SalesPersons = fresh.SalesPersons;
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã tạo khách hàng '{model.CustomerName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Edit ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            var vm = await _customerService.GetFormAsync(id);
            if (!vm.IsEditMode)
            {
                TempData["ErrorMessage"] = "Không tìm thấy khách hàng";
                return RedirectToAction("Index");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CustomerFormViewModel model)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            if (!ModelState.IsValid)
            {
                var fresh = await _customerService.GetFormAsync();
                model.Groups = fresh.Groups;
                model.SalesPersons = fresh.SalesPersons;
                return View(model);
            }

            var (success, error) = await _customerService.UpdateAsync(model, CurrentUserId);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Cập nhật thất bại");
                var fresh = await _customerService.GetFormAsync();
                model.Groups = fresh.Groups;
                model.SalesPersons = fresh.SalesPersons;
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật '{model.CustomerName}' thành công!";
            return RedirectToAction("Detail", new { id = model.CustomerId });
        }

        // ── Detail ───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var vm = await _customerService.GetDetailAsync(id);
            if (vm == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy khách hàng";
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

            var (success, error) = await _customerService.ToggleActiveAsync(id, CurrentUserId);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã cập nhật trạng thái" : error
            });
        }

        // ── Save Contact (Ajax) ──────────────────────────
        [HttpPost]
        public async Task<IActionResult> SaveContact([FromBody] ContactFormModel model)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return Json(ApiResult<object>.Fail("Dữ liệu không hợp lệ", errors));
            }

            var (success, error) = await _customerService.SaveContactAsync(model);
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

            var (success, error) = await _customerService.DeleteContactAsync(id);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã xóa liên hệ" : error
            });
        }

        // ── Add Note (Ajax) ──────────────────────────────
        [HttpPost]
        public async Task<IActionResult> AddNote(int customerId, string content)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error, note) = await _customerService.AddNoteAsync(
                customerId, content, CurrentUserId);

            if (!success)
                return Json(ApiResult<object>.Fail(error ?? "Lỗi thêm ghi chú"));

            return Json(ApiResult<object>.Ok(new
            {
                note!.NoteId,
                note.NoteContent,
                note.CreatedByName,
                CreatedAt = note.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            }));
        }
    }
}
