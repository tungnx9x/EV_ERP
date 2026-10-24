using EV_ERP.Filters;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Reference;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    [RequireRole("ADMIN")]
    public class CurrencyController : Controller
    {
        private readonly ICurrencyService _currencyService;
        private readonly ILogger<CurrencyController> _logger;

        public CurrencyController(ICurrencyService currencyService, ILogger<CurrencyController> logger)
        {
            _currencyService = currencyService;
            _logger = logger;
        }

        // ── Index ────────────────────────────────────────
        public async Task<IActionResult> Index(string? keyword, string? status, int page = 1)
        {
            var vm = await _currencyService.GetListAsync(keyword, status, page);
            return View(vm);
        }

        // ── Create ───────────────────────────────────────
        [HttpGet]
        public IActionResult Create() => View(new CurrencyFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CurrencyFormViewModel model)
        {
            model.IsEditMode = false;
            var rateError = model.ParseRateInput();
            if (rateError != null)
                ModelState.AddModelError(nameof(model.RateToVndInput), rateError);
            if (!ModelState.IsValid)
                return View(model);

            var (success, error) = await _currencyService.CreateAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Tạo tiền tệ thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã thêm tiền tệ {model.CurrencyCode.Trim().ToUpperInvariant()} thành công!";
            return RedirectToAction("Index");
        }

        // ── Edit ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var vm = await _currencyService.GetFormAsync(id);
            if (vm == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tiền tệ";
                return RedirectToAction("Index");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CurrencyFormViewModel model)
        {
            model.IsEditMode = true;
            var rateError = model.ParseRateInput();
            if (rateError != null)
                ModelState.AddModelError(nameof(model.RateToVndInput), rateError);
            if (!ModelState.IsValid)
                return View(model);

            var (success, error) = await _currencyService.UpdateAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Cập nhật thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật tiền tệ {model.CurrencyCode} thành công!";
            return RedirectToAction("Index");
        }

        // ── Delete (Ajax) ────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var (success, error) = await _currencyService.DeleteAsync(id);
                return Json(new ApiResult<object>
                {
                    Success = success,
                    Message = success ? $"Đã xóa tiền tệ {id}" : error
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete currency {Code} failed", id);
                return Json(ApiResult<object>.Fail("Lỗi hệ thống: " + (ex.InnerException?.Message ?? ex.Message)));
            }
        }
    }
}
