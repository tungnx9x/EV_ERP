using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Users;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    [RequireRole("ADMIN")]
    public class UserController : Controller
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;

        public UserController(IUserService userService, IAuthService authService)
        {
            _userService = userService;
            _authService = authService;
        }

        // ── List ─────────────────────────────────────────
        public async Task<IActionResult> Index(string? keyword, int? roleId, string? status, int page = 1)
        {
            var vm = await _userService.GetListAsync(keyword, roleId, status, page);
            return View(vm);
        }

        // ── Create ───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = await _userService.GetFormAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError("Password", "Mật khẩu là bắt buộc khi tạo tài khoản mới");

            if (!ModelState.IsValid)
            {
                model.Roles = (await _userService.GetFormAsync()).Roles;
                return View(model);
            }

            var adminId = HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;
            var (success, error) = await _userService.CreateAsync(model, adminId);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Tạo tài khoản thất bại");
                model.Roles = (await _userService.GetFormAsync()).Roles;
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã tạo tài khoản '{model.FullName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Edit ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _userService.GetFormAsync(id);
            if (!vm.IsEditMode)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản";
                return RedirectToAction("Index");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserFormViewModel model)
        {
            // Remove password validation on edit
            ModelState.Remove("Password");
            ModelState.Remove("ConfirmPassword");

            if (!ModelState.IsValid)
            {
                model.Roles = (await _userService.GetFormAsync()).Roles;
                return View(model);
            }

            var adminId = HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;
            var (success, error) = await _userService.UpdateAsync(model, adminId);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Cập nhật thất bại");
                model.Roles = (await _userService.GetFormAsync()).Roles;
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật tài khoản '{model.FullName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Toggle Lock (Ajax) ───────────────────────────
        [HttpPost]
        public async Task<IActionResult> ToggleLock(int id)
        {
            var adminId = HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;
            var (success, error) = await _userService.ToggleLockAsync(id, adminId);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Cập nhật trạng thái khóa thành công" : error
            });
        }

        // ── Reset Password (Ajax) ────────────────────────
        [HttpPost]
        public async Task<IActionResult> ResetPassword(int id, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return Json(ApiResult<object>.Fail("Mật khẩu phải có ít nhất 6 ký tự"));

            var adminId = HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;
            var (success, error) = await _authService.ResetPasswordAsync(id, newPassword, adminId);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đặt lại mật khẩu thành công" : error
            });
        }

        // ── Deactivate ───────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var adminId = HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;
            var (success, error) = await _userService.DeleteAsync(id, adminId);

            if (success)
                TempData["SuccessMessage"] = "Đã vô hiệu hóa tài khoản";
            else
                TempData["ErrorMessage"] = error;

            return RedirectToAction("Index");
        }
    }
}
