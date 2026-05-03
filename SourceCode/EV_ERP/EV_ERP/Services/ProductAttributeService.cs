using EV_ERP.Data;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.ViewModels.Products;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class ProductAttributeService : IProductAttributeService
    {
        private readonly IUnitOfWork _uow;
        private readonly AppDbContext _db;
        private readonly ILogger<ProductAttributeService> _logger;

        public ProductAttributeService(IUnitOfWork uow, AppDbContext db, ILogger<ProductAttributeService> logger)
        {
            _uow = uow;
            _db = db;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════
        // ATTRIBUTE CRUD
        // ═══════════════════════════════════════════════

        public async Task<ProductAttributeListViewModel> GetAttributeListAsync(string? keyword,
            int pageIndex = 1, int pageSize = 20)
        {
            var baseQuery = _uow.Repository<ProductAttribute>().Query()
                .Include(a => a.Values)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                baseQuery = baseQuery.Where(a =>
                    a.AttributeName.ToLower().Contains(kw) ||
                    a.AttrCode.ToLower().Contains(kw));
            }

            var totalCount = await baseQuery.CountAsync();

            var attributes = await baseQuery
                .OrderBy(a => a.DisplayOrder).ThenBy(a => a.AttributeName)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var rows = attributes.Select(a => new ProductAttributeRowViewModel
            {
                AttributeId = a.AttributeId,
                AttrCode = a.AttrCode,
                AttributeName = a.AttributeName,
                DataType = a.DataType,
                IncludeInSku = a.IncludeInSku,
                SkuPosition = a.SkuPosition,
                IsRequired = a.IsRequired,
                DisplayOrder = a.DisplayOrder,
                IsActive = a.IsActive,
                ValueCount = a.Values.Count(v => v.IsActive)
            }).ToList();

            return new ProductAttributeListViewModel
            {
                Paged = new Models.Common.PagedResult<ProductAttributeRowViewModel>
                {
                    Items = rows,
                    TotalCount = totalCount,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                },
                SearchKeyword = keyword
            };
        }

        public async Task<ProductAttributeFormViewModel> GetAttributeFormAsync(int? attributeId = null)
        {
            if (!attributeId.HasValue || attributeId <= 0)
                return new ProductAttributeFormViewModel();

            var attr = await _uow.Repository<ProductAttribute>().Query()
                .Include(a => a.Values.OrderBy(v => v.DisplayOrder))
                .FirstOrDefaultAsync(a => a.AttributeId == attributeId.Value);

            if (attr == null)
                return new ProductAttributeFormViewModel();

            return new ProductAttributeFormViewModel
            {
                AttributeId = attr.AttributeId,
                AttrCode = attr.AttrCode,
                AttributeName = attr.AttributeName,
                Description = attr.Description,
                DataType = attr.DataType,
                IncludeInSku = attr.IncludeInSku,
                SkuPosition = attr.SkuPosition,
                IsRequired = attr.IsRequired,
                DisplayOrder = attr.DisplayOrder,
                IsActive = attr.IsActive,
                Values = attr.Values.Select(v => new AttributeValueRowViewModel
                {
                    ValueId = v.ValueId,
                    SkuCode = v.SkuCode,
                    ValueName = v.ValueName,
                    Description = v.Description,
                    DisplayOrder = v.DisplayOrder,
                    IsActive = v.IsActive
                }).ToList()
            };
        }

        public async Task<(bool Success, string? ErrorMessage)> CreateAttributeAsync(
            ProductAttributeFormViewModel model)
        {
            var repo = _uow.Repository<ProductAttribute>();
            var code = model.AttrCode.Trim().ToUpper();

            var exists = await repo.AnyAsync(a => a.AttrCode == code);
            if (exists)
                return (false, $"Mã thuộc tính '{code}' đã tồn tại");

            var attr = new ProductAttribute
            {
                AttrCode = code,
                AttributeName = model.AttributeName.Trim(),
                Description = model.Description?.Trim(),
                DataType = model.DataType,
                IncludeInSku = model.IncludeInSku,
                SkuPosition = model.SkuPosition,
                IsRequired = model.IsRequired,
                DisplayOrder = model.DisplayOrder,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await repo.AddAsync(attr);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("ProductAttribute created: {Code}", code);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateAttributeAsync(
            ProductAttributeFormViewModel model)
        {
            if (!model.AttributeId.HasValue)
                return (false, "AttributeId không hợp lệ");

            var repo = _uow.Repository<ProductAttribute>();
            var attr = await repo.GetByIdAsync(model.AttributeId.Value);
            if (attr == null)
                return (false, "Không tìm thấy thuộc tính");

            attr.AttributeName = model.AttributeName.Trim();
            attr.Description = model.Description?.Trim();
            attr.DataType = model.DataType;
            attr.IncludeInSku = model.IncludeInSku;
            attr.SkuPosition = model.SkuPosition;
            attr.IsRequired = model.IsRequired;
            attr.DisplayOrder = model.DisplayOrder;
            attr.IsActive = model.IsActive;
            attr.UpdatedAt = DateTime.Now;

            repo.Update(attr);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("ProductAttribute updated: {Id}", model.AttributeId);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> ToggleAttributeActiveAsync(int attributeId)
        {
            var repo = _uow.Repository<ProductAttribute>();
            var attr = await repo.GetByIdAsync(attributeId);
            if (attr == null)
                return (false, "Không tìm thấy thuộc tính");

            attr.IsActive = !attr.IsActive;
            attr.UpdatedAt = DateTime.Now;
            repo.Update(attr);
            await _uow.SaveChangesAsync();
            return (true, null);
        }

        // ═══════════════════════════════════════════════
        // ATTRIBUTE VALUES CRUD
        // ═══════════════════════════════════════════════

        public async Task<(bool Success, string? ErrorMessage)> AddValueAsync(AttributeValueFormViewModel model)
        {
            var repo = _uow.Repository<ProductAttributeValue>();
            var code = model.SkuCode.Trim().ToUpper();

            var exists = await repo.AnyAsync(v =>
                v.AttributeId == model.AttributeId && v.SkuCode == code);
            if (exists)
                return (false, $"Mã SKU '{code}' đã tồn tại cho thuộc tính này");

            var value = new ProductAttributeValue
            {
                AttributeId = model.AttributeId,
                SkuCode = code,
                ValueName = model.ValueName.Trim(),
                Description = model.Description?.Trim(),
                DisplayOrder = model.DisplayOrder,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now
            };

            await repo.AddAsync(value);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("AttributeValue added: {Code} for AttrId={AttrId}", code, model.AttributeId);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateValueAsync(AttributeValueFormViewModel model)
        {
            if (!model.ValueId.HasValue)
                return (false, "ValueId không hợp lệ");

            var repo = _uow.Repository<ProductAttributeValue>();
            var value = await repo.GetByIdAsync(model.ValueId.Value);
            if (value == null)
                return (false, "Không tìm thấy giá trị");

            value.ValueName = model.ValueName.Trim();
            value.Description = model.Description?.Trim();
            value.DisplayOrder = model.DisplayOrder;
            value.IsActive = model.IsActive;
            repo.Update(value);
            await _uow.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> ToggleValueActiveAsync(int valueId)
        {
            var repo = _uow.Repository<ProductAttributeValue>();
            var value = await repo.GetByIdAsync(valueId);
            if (value == null)
                return (false, "Không tìm thấy giá trị");

            value.IsActive = !value.IsActive;
            repo.Update(value);
            await _uow.SaveChangesAsync();
            return (true, null);
        }

        // ═══════════════════════════════════════════════
        // SKU CONFIG PER CATEGORY
        // ═══════════════════════════════════════════════

        public async Task<SkuConfigFormViewModel> GetSkuConfigAsync(int categoryId)
        {
            var category = await _uow.Repository<ProductCategory>().GetByIdAsync(categoryId);
            if (category == null)
                return new SkuConfigFormViewModel();

            var configs = await _uow.Repository<SkuConfig>().Query()
                .Include(c => c.Attribute)
                .Where(c => c.CategoryId == categoryId)
                .OrderBy(c => c.SkuPosition)
                .ToListAsync();

            var allAttributes = await _uow.Repository<ProductAttribute>().Query()
                .Where(a => a.IsActive)
                .OrderBy(a => a.DisplayOrder)
                .ToListAsync();

            return new SkuConfigFormViewModel
            {
                CategoryId = categoryId,
                CategoryName = category.CategoryName,
                SkuPrefix = category.SkuPrefix,
                Configs = configs.Select(c => new SkuConfigViewModel
                {
                    SkuConfigId = c.SkuConfigId,
                    CategoryId = c.CategoryId,
                    AttributeId = c.AttributeId,
                    AttrCode = c.Attribute.AttrCode,
                    AttributeName = c.Attribute.AttributeName,
                    SkuPosition = c.SkuPosition,
                    IsRequired = c.IsRequired,
                    DefaultValueId = c.DefaultValueId,
                    IsActive = c.IsActive
                }).ToList(),
                AvailableAttributes = allAttributes.Select(a => new ProductAttributeOptionViewModel
                {
                    AttributeId = a.AttributeId,
                    AttrCode = a.AttrCode,
                    AttributeName = a.AttributeName
                }).ToList()
            };
        }

        public async Task<(bool Success, string? ErrorMessage)> SaveSkuConfigAsync(
            int categoryId, List<SkuConfigViewModel> configs)
        {
            var repo = _uow.Repository<SkuConfig>();

            // Remove existing configs for this category
            var existing = await repo.Query()
                .Where(c => c.CategoryId == categoryId)
                .ToListAsync();
            repo.RemoveRange(existing);

            // Add new configs
            foreach (var cfg in configs)
            {
                await repo.AddAsync(new SkuConfig
                {
                    CategoryId = categoryId,
                    AttributeId = cfg.AttributeId,
                    SkuPosition = cfg.SkuPosition,
                    IsRequired = cfg.IsRequired,
                    DefaultValueId = cfg.DefaultValueId,
                    IsActive = cfg.IsActive
                });
            }

            await _uow.SaveChangesAsync();

            _logger.LogInformation("SkuConfig saved for CategoryId={Id}, {Count} attributes",
                categoryId, configs.Count);
            return (true, null);
        }

        // ═══════════════════════════════════════════════
        // FOR PRODUCT FORM: ATTRIBUTES BY CATEGORY
        // ═══════════════════════════════════════════════

        public async Task<List<SkuAttributeFormItem>> GetAttributesByCategoryAsync(
            int categoryId, int? productId = null)
        {
            var skuConfigs = await _uow.Repository<SkuConfig>().Query()
                .Include(c => c.Attribute)
                    .ThenInclude(a => a.Values.Where(v => v.IsActive).OrderBy(v => v.DisplayOrder))
                .Where(c => c.CategoryId == categoryId && c.IsActive)
                .OrderBy(c => c.SkuPosition)
                .ToListAsync();

            // Load existing attribute maps if editing
            var existingMaps = productId.HasValue
                ? await _uow.Repository<ProductAttributeMap>().Query()
                    .Where(m => m.ProductId == productId.Value)
                    .ToListAsync()
                : new List<ProductAttributeMap>();

            return skuConfigs.Select(cfg =>
            {
                var existing = existingMaps.FirstOrDefault(m => m.AttributeId == cfg.AttributeId);
                return new SkuAttributeFormItem
                {
                    AttributeId = cfg.AttributeId,
                    AttrCode = cfg.Attribute.AttrCode,
                    AttributeName = cfg.Attribute.AttributeName,
                    DataType = cfg.Attribute.DataType,
                    IsRequired = cfg.IsRequired,
                    SkuPosition = cfg.SkuPosition,
                    SelectedValueId = existing?.ValueId ?? cfg.DefaultValueId,
                    TextValue = existing?.TextValue,
                    DefaultValueId = cfg.DefaultValueId,
                    Values = cfg.Attribute.Values.Select(v => new AttributeValueOptionViewModel
                    {
                        ValueId = v.ValueId,
                        SkuCode = v.SkuCode,
                        ValueName = v.ValueName
                    }).ToList()
                };
            }).ToList();
        }

        // ═══════════════════════════════════════════════
        // SKU GENERATION
        // ═══════════════════════════════════════════════

        public async Task<string> GenerateSkuAsync(int productId)
        {
            var product = await _uow.Repository<Product>().Query()
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
                throw new InvalidOperationException($"Product {productId} not found");

            var catPrefix = product.Category?.SkuPrefix ?? "XX";

            // Build attribute part from maps + sku configs
            var attrMaps = await _uow.Repository<ProductAttributeMap>().Query()
                .Include(m => m.Value)
                .Include(m => m.Attribute)
                .Where(m => m.ProductId == productId)
                .ToListAsync();

            var skuConfigs = product.CategoryId.HasValue
                ? await _uow.Repository<SkuConfig>().Query()
                    .Where(c => c.CategoryId == product.CategoryId.Value
                             && c.IsActive
                             && c.Attribute.IncludeInSku)
                    .OrderBy(c => c.SkuPosition)
                    .ToListAsync()
                : new List<SkuConfig>();

            var attrParts = new List<string>();
            foreach (var cfg in skuConfigs)
            {
                var map = attrMaps.FirstOrDefault(m => m.AttributeId == cfg.AttributeId);
                var code = map?.Value?.SkuCode ?? "NA";
                attrParts.Add(code);
            }

            var skuBody = string.Join("-", attrParts);
            var skuPrefix = catPrefix + (skuBody.Length > 0 ? "-" + skuBody : "");

            // Thread-safe sequence generation (wrapped for SqlServerRetryingExecutionStrategy)
            var strategy = _db.Database.CreateExecutionStrategy();
            var sku = await strategy.ExecuteAsync(async () =>
            {
                await _uow.BeginTransactionAsync();
                try
                {
                    var seqRepo = _uow.Repository<SkuSequence>();
                    var seq = await seqRepo.Query()
                        .FirstOrDefaultAsync(s => s.SkuPrefix == skuPrefix);

                    int nextNum;
                    if (seq != null)
                    {
                        seq.LastNumber++;
                        seq.UpdatedAt = DateTime.Now;
                        seqRepo.Update(seq);
                        nextNum = seq.LastNumber;
                    }
                    else
                    {
                        seq = new SkuSequence
                        {
                            SkuPrefix = skuPrefix,
                            LastNumber = 1,
                            UpdatedAt = DateTime.Now
                        };
                        await seqRepo.AddAsync(seq);
                        nextNum = 1;
                    }

                    var generatedSku = $"{skuPrefix}-{nextNum:D4}";

                    product.SKU = generatedSku;
                    product.UpdatedAt = DateTime.Now;
                    _uow.Repository<Product>().Update(product);

                    await _uow.SaveChangesAsync();
                    await _uow.CommitAsync();

                    return generatedSku;
                }
                catch
                {
                    await _uow.RollbackAsync();
                    throw;
                }
            });

            _logger.LogInformation("SKU generated: {SKU} for ProductId={Id}", sku, productId);
            return sku;
        }
    }
}
