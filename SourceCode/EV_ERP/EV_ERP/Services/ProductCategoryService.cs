using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.ViewModels.Products;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class ProductCategoryService : IProductCategoryService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<ProductCategoryService> _logger;

        public ProductCategoryService(IUnitOfWork uow, ILogger<ProductCategoryService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // ── List ─────────────────────────────────────────
        public async Task<ProductCategoryListViewModel> GetListAsync(string? keyword, string? status)
        {
            // One query — load everything flat, build tree in memory
            var all = await _uow.Repository<ProductCategory>().Query()
                .Include(c => c.Products.Where(p => p.IsActive))
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.CategoryName)
                .ToListAsync();

            var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim().ToLower();
            bool? activeFilter = status == "active" ? true : status == "inactive" ? false : null;

            // ToLookup supports null keys (unlike ToDictionary)
            var lookup = all.ToLookup(c => c.ParentCategoryId);

            var rootNodes = BuildTree(lookup, parentId: null, depth: 0, kw, activeFilter);

            return new ProductCategoryListViewModel
            {
                RootNodes = rootNodes,
                SearchKeyword = keyword,
                FilterStatus = status,
                TotalCount = CountNodes(rootNodes)
            };
        }

        // ── Form ─────────────────────────────────────────
        public async Task<ProductCategoryFormViewModel> GetFormAsync(int? categoryId = null)
        {
            var parentOptions = await GetParentOptionsAsync(excludeId: categoryId);

            if (!categoryId.HasValue || categoryId <= 0)
                return new ProductCategoryFormViewModel { ParentOptions = parentOptions };

            var category = await _uow.Repository<ProductCategory>().GetByIdAsync(categoryId.Value);
            if (category == null)
                return new ProductCategoryFormViewModel { ParentOptions = parentOptions };

            return new ProductCategoryFormViewModel
            {
                CategoryId = category.CategoryId,
                CategoryCode = category.CategoryCode,
                CategoryName = category.CategoryName,
                ParentCategoryId = category.ParentCategoryId,
                Description = category.Description,
                DisplayOrder = category.DisplayOrder,
                IsActive = category.IsActive,
                ParentOptions = parentOptions
            };
        }

        // ── Create ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> CreateAsync(
            ProductCategoryFormViewModel model, int createdBy)
        {
            var repo = _uow.Repository<ProductCategory>();

            var duplicate = await repo.AnyAsync(c =>
                c.CategoryName.ToLower() == model.CategoryName.Trim().ToLower() &&
                c.ParentCategoryId == model.ParentCategoryId &&
                c.IsActive);
            if (duplicate)
                return (false, "Tên danh mục đã tồn tại trong cùng cấp");

            var code = await GenerateCategoryCodeAsync();

            var category = new ProductCategory
            {
                CategoryCode = code,
                CategoryName = model.CategoryName.Trim(),
                ParentCategoryId = model.ParentCategoryId,
                Description = model.Description?.Trim(),
                DisplayOrder = model.DisplayOrder,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await repo.AddAsync(category);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("ProductCategory created: {Code} by UserId={UserId}", code, createdBy);
            return (true, null);
        }

        // ── Update ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(
            ProductCategoryFormViewModel model, int updatedBy)
        {
            if (!model.CategoryId.HasValue)
                return (false, "CategoryId không hợp lệ");

            var repo = _uow.Repository<ProductCategory>();
            var category = await repo.GetByIdAsync(model.CategoryId.Value);
            if (category == null)
                return (false, "Không tìm thấy danh mục");

            if (model.ParentCategoryId == model.CategoryId)
                return (false, "Danh mục không thể là cha của chính nó");

            // Guard against circular reference: walk up the chosen parent chain
            if (model.ParentCategoryId.HasValue)
            {
                var all = await repo.Query().ToListAsync();
                if (IsDescendant(all, ancestorId: model.CategoryId.Value, nodeId: model.ParentCategoryId.Value))
                    return (false, "Không thể chọn danh mục con làm danh mục cha");
            }

            var duplicate = await repo.AnyAsync(c =>
                c.CategoryName.ToLower() == model.CategoryName.Trim().ToLower() &&
                c.ParentCategoryId == model.ParentCategoryId &&
                c.CategoryId != model.CategoryId.Value &&
                c.IsActive);
            if (duplicate)
                return (false, "Tên danh mục đã tồn tại trong cùng cấp");

            category.CategoryName = model.CategoryName.Trim();
            category.ParentCategoryId = model.ParentCategoryId;
            category.Description = model.Description?.Trim();
            category.DisplayOrder = model.DisplayOrder;
            category.IsActive = model.IsActive;
            category.UpdatedAt = DateTime.Now;

            repo.Update(category);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("ProductCategory updated: CategoryId={Id} by UserId={UserId}",
                model.CategoryId, updatedBy);
            return (true, null);
        }

        // ── Toggle Active ────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> ToggleActiveAsync(
            int categoryId, int updatedBy)
        {
            var repo = _uow.Repository<ProductCategory>();
            var category = await repo.GetByIdAsync(categoryId);
            if (category == null)
                return (false, "Không tìm thấy danh mục");

            category.IsActive = !category.IsActive;
            category.UpdatedAt = DateTime.Now;
            repo.Update(category);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("ProductCategory toggled: CategoryId={Id} IsActive={Active} by UserId={UserId}",
                categoryId, category.IsActive, updatedBy);
            return (true, null);
        }

        // ── Private: Tree building ────────────────────────

        /// <summary>
        /// Recursively builds the tree. Returns only nodes that match the filter,
        /// or that have descendants matching the filter.
        /// </summary>
        private List<ProductCategoryNodeViewModel> BuildTree(
            ILookup<int?, ProductCategory> lookup,
            int? parentId, int depth,
            string? keyword, bool? activeFilter)
        {
            var children = lookup[parentId].OrderBy(c => c.DisplayOrder).ThenBy(c => c.CategoryName);

            var result = new List<ProductCategoryNodeViewModel>();
            foreach (var c in children)
            {
                var childNodes = BuildTree(lookup, c.CategoryId, depth + 1, keyword, activeFilter);

                bool matchesStatus = !activeFilter.HasValue || c.IsActive == activeFilter.Value;
                bool matchesKeyword = keyword == null
                    || c.CategoryName.ToLower().Contains(keyword)
                    || c.CategoryCode.ToLower().Contains(keyword);

                // Include if self matches OR has any matching descendants
                if ((matchesStatus && matchesKeyword) || childNodes.Any())
                {
                    result.Add(new ProductCategoryNodeViewModel
                    {
                        CategoryId = c.CategoryId,
                        CategoryCode = c.CategoryCode,
                        CategoryName = c.CategoryName,
                        ParentCategoryId = c.ParentCategoryId,
                        DisplayOrder = c.DisplayOrder,
                        ProductCount = c.Products.Count,
                        IsActive = c.IsActive,
                        CreatedAt = c.CreatedAt,
                        Depth = depth,
                        Children = childNodes
                    });
                }
            }
            return result;
        }

        private static int CountNodes(List<ProductCategoryNodeViewModel> nodes) =>
            nodes.Count + nodes.Sum(n => CountNodes(n.Children));

        /// <summary>
        /// Checks whether nodeId is a descendant of ancestorId in the flat list.
        /// Used to prevent circular parent assignment.
        /// </summary>
        private static bool IsDescendant(List<ProductCategory> all, int ancestorId, int nodeId)
        {
            var parentMap = all.Where(c => c.ParentCategoryId.HasValue)
                               .ToDictionary(c => c.CategoryId, c => c.ParentCategoryId!.Value);

            var current = nodeId;
            while (parentMap.TryGetValue(current, out var parent))
            {
                if (parent == ancestorId) return true;
                current = parent;
            }
            return false;
        }

        // ── Private: Parent options ───────────────────────

        /// <summary>
        /// Returns all active categories as a flat ordered list with depth prefix for select display.
        /// Excludes the given ID and its entire subtree (to prevent circular references).
        /// </summary>
        private async Task<List<ParentCategoryOptionViewModel>> GetParentOptionsAsync(int? excludeId)
        {
            var all = await _uow.Repository<ProductCategory>().Query()
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.CategoryName)
                .ToListAsync();

            var excludedIds = excludeId.HasValue
                ? GetSubtreeIds(all, excludeId.Value)
                : new HashSet<int>();

            var lookup = all.ToLookup(c => c.ParentCategoryId);

            var result = new List<ParentCategoryOptionViewModel>();

            void Flatten(int? parentId, int depth)
            {
                foreach (var c in lookup[parentId].OrderBy(c => c.DisplayOrder).ThenBy(c => c.CategoryName))
                {
                    if (excludedIds.Contains(c.CategoryId)) continue;
                    var prefix = depth == 0 ? "" : new string(' ', depth * 3) + "└ ";
                    result.Add(new ParentCategoryOptionViewModel
                    {
                        CategoryId = c.CategoryId,
                        CategoryCode = c.CategoryCode,
                        DisplayName = prefix + c.CategoryName
                    });
                    Flatten(c.CategoryId, depth + 1);
                }
            }

            Flatten(null, 0);
            return result;
        }

        private static HashSet<int> GetSubtreeIds(List<ProductCategory> all, int rootId)
        {
            var result = new HashSet<int> { rootId };
            var lookup = all.ToLookup(c => c.ParentCategoryId);

            void Collect(int id)
            {
                foreach (var child in lookup[(int?)id])
                {
                    result.Add(child.CategoryId);
                    Collect(child.CategoryId);
                }
            }

            Collect(rootId);
            return result;
        }

        // ── Private: Code generation ─────────────────────
        private async Task<string> GenerateCategoryCodeAsync()
        {
            var last = await _uow.Repository<ProductCategory>().Query()
                .Where(c => c.CategoryCode.StartsWith("DM-"))
                .OrderByDescending(c => c.CategoryCode)
                .FirstOrDefaultAsync();

            int next = 1;
            if (last != null && last.CategoryCode.Length > 3)
            {
                if (int.TryParse(last.CategoryCode[3..], out int n))
                    next = n + 1;
            }

            return $"DM-{next:D5}";
        }
    }
}
