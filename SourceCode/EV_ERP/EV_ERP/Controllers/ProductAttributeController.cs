using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Products;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    public class ProductAttributeController : Controller
    {
        private readonly IProductAttributeService _attrService;

        public ProductAttributeController(IProductAttributeService attrService)
        {
            _attrService = attrService;
        }

        // ── Attribute List ──────────────────────────────
        [RequireRole("ADMIN", "MANAGER")]
        public async Task<IActionResult> Index(string? keyword, int page = 1)
        {
            var vm = await _attrService.GetAttributeListAsync(keyword, page);
            return View(vm);
        }

        // ── Create Attribute ────────────────────────────
        [HttpGet]
        [RequireRole("ADMIN", "MANAGER")]
        public IActionResult Create()
        {
            return View(new ProductAttributeFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRole("ADMIN", "MANAGER")]
        public async Task<IActionResult> Create(ProductAttributeFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, error) = await _attrService.CreateAttributeAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Tạo thuộc tính thất bại");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã tạo thuộc tính '{model.AttributeName}'";
            return RedirectToAction("Index");
        }

        // ── Edit Attribute ──────────────────────────────
        [HttpGet]
        [RequireRole("ADMIN", "MANAGER")]
        public async Task<IActionResult> Edit(int id)
        {
            var vm = await _attrService.GetAttributeFormAsync(id);
            if (!vm.IsEditMode)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thuộc tính";
                return RedirectToAction("Index");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRole("ADMIN", "MANAGER")]
        public async Task<IActionResult> Edit(ProductAttributeFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Reload values
                var fresh = await _attrService.GetAttributeFormAsync(model.AttributeId);
                model.Values = fresh.Values;
                return View(model);
            }

            var (success, error) = await _attrService.UpdateAttributeAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Cập nhật thất bại");
                var fresh = await _attrService.GetAttributeFormAsync(model.AttributeId);
                model.Values = fresh.Values;
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật thuộc tính '{model.AttributeName}'";
            return RedirectToAction("Edit", new { id = model.AttributeId });
        }

        // ── Toggle Active (Ajax) ────────────────────────
        [HttpPost]
        [RequireRole("ADMIN", "MANAGER")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var (success, error) = await _attrService.ToggleAttributeActiveAsync(id);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã cập nhật trạng thái" : error
            });
        }

        // ═══════════════════════════════════════════════
        // ATTRIBUTE VALUES (Ajax)
        // ═══════════════════════════════════════════════

        [HttpPost]
        [RequireRole("ADMIN", "MANAGER")]
        public async Task<IActionResult> AddValue([FromBody] AttributeValueFormViewModel model)
        {
            try
            {
                var (success, error) = await _attrService.AddValueAsync(model);
                if (!success)
                    return Json(ApiResult<object>.Fail(error ?? "Thêm giá trị thất bại"));

                return Json(ApiResult<object>.Ok(new { }, "Đã thêm giá trị"));
            }
            catch (Exception)
            {
                return Json(ApiResult<object>.Fail("Có lỗi xảy ra"));
            }
        }

        [HttpPost]
        [RequireRole("ADMIN", "MANAGER")]
        public async Task<IActionResult> UpdateValue([FromBody] AttributeValueFormViewModel model)
        {
            try
            {
                var (success, error) = await _attrService.UpdateValueAsync(model);
                if (!success)
                    return Json(ApiResult<object>.Fail(error ?? "Cập nhật thất bại"));

                return Json(ApiResult<object>.Ok(new { }, "Đã cập nhật giá trị"));
            }
            catch (Exception)
            {
                return Json(ApiResult<object>.Fail("Có lỗi xảy ra"));
            }
        }

        [HttpPost]
        [RequireRole("ADMIN", "MANAGER")]
        public async Task<IActionResult> ToggleValueActive(int id)
        {
            try
            {
                var (success, error) = await _attrService.ToggleValueActiveAsync(id);
                return Json(new ApiResult<object>
                {
                    Success = success,
                    Message = success ? "Đã cập nhật trạng thái" : error
                });
            }
            catch (Exception)
            {
                return Json(ApiResult<object>.Fail("Có lỗi xảy ra"));
            }
        }

        // ═══════════════════════════════════════════════
        // SKU CONFIG PER CATEGORY
        // ═══════════════════════════════════════════════

        [HttpGet]
        [RequireRole("ADMIN", "MANAGER")]
        public async Task<IActionResult> SkuConfig(int id)
        {
            var vm = await _attrService.GetSkuConfigAsync(id);
            if (string.IsNullOrEmpty(vm.CategoryName))
            {
                TempData["ErrorMessage"] = "Không tìm thấy danh mục";
                return RedirectToAction("Index", "ProductCategory");
            }
            return View(vm);
        }

        [HttpPost]
        [RequireRole("ADMIN", "MANAGER")]
        public async Task<IActionResult> SaveSkuConfig([FromBody] SaveSkuConfigRequest request)
        {
            try
            {
                var (success, error) = await _attrService.SaveSkuConfigAsync(request.CategoryId, request.Configs);
                if (!success)
                    return Json(ApiResult<object>.Fail(error ?? "Lưu cấu hình thất bại"));

                return Json(ApiResult<object>.Ok(new { }, "Đã lưu cấu hình SKU"));
            }
            catch (Exception)
            {
                return Json(ApiResult<object>.Fail("Có lỗi xảy ra"));
            }
        }

        // ═══════════════════════════════════════════════
        // API: Get attributes for product form (Ajax)
        // ═══════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> GetAttributesByCategory(int categoryId, int? productId)
        {
            try
            {
                var attrs = await _attrService.GetAttributesByCategoryAsync(categoryId, productId);
                return Json(ApiResult<object>.Ok(attrs));
            }
            catch (Exception)
            {
                return Json(ApiResult<object>.Fail("Có lỗi xảy ra"));
            }
        }
    }

    public class SaveSkuConfigRequest
    {
        public int CategoryId { get; set; }
        public List<SkuConfigViewModel> Configs { get; set; } = [];
    }
}
