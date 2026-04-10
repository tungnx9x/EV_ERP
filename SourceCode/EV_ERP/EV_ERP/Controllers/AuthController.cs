using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.ViewModels.Auth;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // ── Login ────────────────────────────────────────
        [HttpGet]
        public IActionResult Login(string? returnUrl)
        {
            if (HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser) != null)
                return RedirectToAction("Index", "Workspace");

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            var (success, error, user) = await _authService.LoginAsync(model, ipAddress, userAgent);

            if (!success || user == null)
            {
                ModelState.AddModelError(string.Empty, error ?? "Đăng nhập thất bại");
                return View(model);
            }

            HttpContext.Session.SetObject(SessionKeys.CurrentUser, user);

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Workspace");
        }

        // ── Logout ───────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireLogin]
        public async Task<IActionResult> Logout()
        {
            var user = HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser);
            if (user != null)
                await _authService.LogoutAsync(user.UserId);

            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // ── Change Password ──────────────────────────────
        [HttpGet]
        [RequireLogin]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireLogin]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var currentUser = HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!;
            var (success, error) = await _authService.ChangePasswordAsync(currentUser.UserId, model);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Đổi mật khẩu thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("ChangePassword");
        }

        // ── Access Denied ────────────────────────────────
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
