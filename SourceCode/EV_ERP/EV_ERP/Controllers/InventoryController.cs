using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.Stock;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers
{
    [RequireLogin]
    public class InventoryController : Controller
    {
        private readonly IInventoryService _inventoryService;

        public InventoryController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        private string CurrentRoleCode =>
            HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.RoleCode;

        // ── Index (Tồn kho) ──────────────────────────────
        public async Task<IActionResult> Index(string? keyword, int? warehouseId, string? status)
        {
            var vm = await _inventoryService.GetListAsync(keyword, warehouseId, status);
            return View(vm);
        }

        // ── Product Inventory Detail ─────────────────────
        public async Task<IActionResult> ProductDetail(int id)
        {
            var vm = await _inventoryService.GetProductDetailAsync(id);
            if (vm == null) return NotFound();
            return View(vm);
        }

        // ── Barcode Quick Lookup (Ajax) ──────────────────
        [HttpGet]
        public async Task<IActionResult> QuickLookup(string barcode)
        {
            var result = await _inventoryService.QuickLookupAsync(barcode);
            if (result == null)
                return Json(ApiResult<object>.Fail("Không tìm thấy sản phẩm"));

            return Json(ApiResult<BarcodeLookupResult>.Ok(result));
        }
    }
}
