using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Products;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    public class ProductController : Controller
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        private int CurrentUserId =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

        private string CurrentRoleCode =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.RoleCode;

        // Any authenticated user can create/edit products (incl. gallery, barcode).
        // Deactivation (ToggleActive) stays restricted to ADMIN/MANAGER.
        private bool CanEdit => true;
        private bool CanDelete => CurrentRoleCode is "ADMIN" or "MANAGER";

        // ── Index ────────────────────────────────────────
        public async Task<IActionResult> Index(string? keyword, int? categoryId, string? status, int page = 1)
        {
            var vm = await _productService.GetListAsync(keyword, categoryId, status, page);
            ViewBag.CanEdit = CanEdit;
            ViewBag.CanDelete = CanDelete;
            return View(vm);
        }

        // ── Create ───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            var vm = await _productService.GetFormAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductFormViewModel model)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            var fresh = await _productService.GetFormAsync();
            ValidateNonRootCategory(model, fresh);

            if (!ModelState.IsValid)
            {
                model.Categories = fresh.Categories;
                model.Units = fresh.Units;
                return View(model);
            }

            var (success, error, _) = await _productService.CreateAsync(model, CurrentUserId);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Tạo sản phẩm thất bại");
                model.Categories = fresh.Categories;
                model.Units = fresh.Units;
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã tạo sản phẩm '{model.ProductName}' thành công!";
            return RedirectToAction("Index");
        }

        // ── Edit ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            var vm = await _productService.GetFormAsync(id);
            if (!vm.IsEditMode)
            {
                TempData["ErrorMessage"] = "Không tìm thấy sản phẩm";
                return RedirectToAction("Index");
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductFormViewModel model)
        {
            if (!CanEdit) return RedirectToAction("AccessDenied", "Auth");

            var fresh = await _productService.GetFormAsync();
            ValidateNonRootCategory(model, fresh);

            if (!ModelState.IsValid)
            {
                model.Categories = fresh.Categories;
                model.Units = fresh.Units;
                return View(model);
            }

            var (success, error) = await _productService.UpdateAsync(model, CurrentUserId);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Cập nhật thất bại");
                model.Categories = fresh.Categories;
                model.Units = fresh.Units;
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật '{model.ProductName}' thành công!";
            return RedirectToAction("Detail", new { id = model.ProductId });
        }

        private void ValidateNonRootCategory(ProductFormViewModel model, ProductFormViewModel fresh)
        {
            if (!model.CategoryId.HasValue) return;
            var cat = fresh.Categories.FirstOrDefault(c => c.CategoryId == model.CategoryId.Value);
            if (cat == null)
            {
                ModelState.AddModelError(nameof(model.CategoryId), "Danh mục không tồn tại.");
            }
            else if (cat.ParentCategoryId == null)
            {
                ModelState.AddModelError(nameof(model.CategoryId), "Vui lòng chọn danh mục con (không thể chọn danh mục gốc).");
            }
        }

        // ── Detail ───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var vm = await _productService.GetDetailAsync(id);
            if (vm == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy sản phẩm";
                return RedirectToAction("Index");
            }
            ViewBag.CanEdit = CanEdit;
            ViewBag.CanDelete = CanDelete;
            return View(vm);
        }

        // ── Check Duplicate (Ajax) ───────────────────────
        [HttpPost]
        public async Task<IActionResult> CheckDuplicate([FromBody] CheckDuplicateRequest request)
        {
            try
            {
                if (!request.CategoryId.HasValue || request.AttributeValues == null || request.AttributeValues.Count == 0)
                    return Json(ApiResult<object>.Ok(new { hasDuplicate = false }));

                var (hasDuplicate, productCode, productName, productId) =
                    await _productService.CheckDuplicateAsync(
                        request.CategoryId.Value,
                        request.AttributeValues,
                        request.ExcludeProductId);

                return Json(ApiResult<object>.Ok(new
                {
                    hasDuplicate,
                    productCode,
                    productName,
                    productId
                }));
            }
            catch (Exception)
            {
                return Json(ApiResult<object>.Ok(new { hasDuplicate = false }));
            }
        }

        public class CheckDuplicateRequest
        {
            public int? CategoryId { get; set; }
            public Dictionary<int, int?> AttributeValues { get; set; } = new();
            public int? ExcludeProductId { get; set; }
        }

        // ── Toggle Active (Ajax) ─────────────────────────
        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id)
        {
            if (!CanDelete)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error) = await _productService.ToggleActiveAsync(id, CurrentUserId);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã cập nhật trạng thái" : error
            });
        }

        // ── Generate Barcode (Ajax) ──────────────────────
        [HttpPost]
        public async Task<IActionResult> GenerateBarcode(int id)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error, result) = await _productService.GenerateBarcodeAsync(id, CurrentUserId);
            if (!success)
                return Json(ApiResult<object>.Fail(error ?? "Lỗi tạo barcode"));

            return Json(ApiResult<object>.Ok(new
            {
                result!.ProductId,
                result.ProductCode,
                result.Barcode,
                result.BarcodeType
            }, "Đã tạo barcode thành công"));
        }

        // ── Add Gallery Images (Ajax) ─────────────────────
        [HttpPost]
        public async Task<IActionResult> AddImages(int id, IList<IFormFile> files)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            if (files == null || files.Count == 0)
                return Json(ApiResult<object>.Fail("Chưa chọn file nào"));

            var (success, error, added) = await _productService.AddImagesAsync(id, files);
            if (!success)
                return Json(ApiResult<object>.Fail(error ?? "Lỗi upload ảnh"));

            return Json(ApiResult<object>.Ok(
                added.Select(i => new { i.ImageId, i.ImageUrl, i.IsPrimary }).ToList(),
                $"Đã thêm {added.Count} ảnh"));
        }

        // ── Delete Gallery Image (Ajax) ───────────────────
        [HttpPost]
        public async Task<IActionResult> DeleteImage(int id, int imageId)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error) = await _productService.DeleteImageAsync(imageId, id);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã xóa ảnh" : error
            });
        }

        // ── Set Image as Avatar (Ajax) ────────────────────
        [HttpPost]
        public async Task<IActionResult> SetAvatar(int id, int imageId)
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error) = await _productService.SetAvatarAsync(imageId, id, CurrentUserId);
            return Json(new ApiResult<object>
            {
                Success = success,
                Message = success ? "Đã đặt làm ảnh đại diện" : error
            });
        }

        // ── Generate Barcodes for All (Ajax) ─────────────
        [HttpPost]
        public async Task<IActionResult> GenerateBarcodesForAll()
        {
            if (!CanEdit)
                return Json(ApiResult<object>.Fail("Bạn không có quyền thực hiện thao tác này"));

            var (success, error, count) = await _productService.GenerateBarcodesForAllAsync(CurrentUserId);
            if (count == 0)
                return Json(ApiResult<object>.Ok(new { Count = 0 }, error ?? "Không có sản phẩm nào cần tạo barcode"));

            return Json(ApiResult<object>.Ok(new { Count = count },
                $"Đã tạo barcode cho {count} sản phẩm"));
        }
    }
}
