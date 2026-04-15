using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.ViewModels.Products;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<ProductService> _logger;
        private readonly string _storageRoot;

        public ProductService(IUnitOfWork uow, ILogger<ProductService> logger, IConfiguration config, IWebHostEnvironment env)
        {
            _uow = uow;
            _logger = logger;
            _storageRoot = config["FileStorage:RootPath"] ?? Path.Combine(env.ContentRootPath, "ERP_Files");
        }

        // ── List ─────────────────────────────────────────
        public async Task<ProductListViewModel> GetListAsync(string? keyword, int? categoryId, string? status)
        {
            var query = _uow.Repository<Product>().Query()
                .Include(p => p.Category).ThenInclude(c => c!.ParentCategory)
                .Include(p => p.Unit)
                .Where(p => status == "inactive" ? !p.IsActive : p.IsActive);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(p =>
                    p.ProductName.ToLower().Contains(kw) ||
                    p.ProductCode.ToLower().Contains(kw) ||
                    (p.Barcode != null && p.Barcode.Contains(kw)));
            }

            if (categoryId.HasValue && categoryId > 0)
                query = query.Where(p => p.CategoryId == categoryId);

            var products = await query.OrderBy(p => p.ProductCode).ToListAsync();
            var categories = await GetCategoryOptionsAsync();

            return new ProductListViewModel
            {
                Products = products.Select(p => new ProductRowViewModel
                {
                    ProductId = p.ProductId,
                    ProductCode = p.ProductCode,
                    ProductName = p.ProductName,
                    CategoryName = p.Category == null ? null
                        : p.Category.ParentCategory != null
                            ? $"{p.Category.ParentCategory.CategoryName}/{p.Category.CategoryName}"
                            : p.Category.CategoryName,
                    ImageUrl = p.ImageUrl,
                    DefaultSalePrice = p.DefaultSalePrice,
                    DefaultPurchasePrice = p.DefaultPurchasePrice,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                }).ToList(),
                SearchKeyword = keyword,
                FilterCategoryId = categoryId,
                FilterStatus = status,
                TotalCount = products.Count,
                Categories = categories
            };
        }

        // ── Form ─────────────────────────────────────────
        public async Task<ProductFormViewModel> GetFormAsync(int? productId = null)
        {
            var categories = await GetCategoryOptionsAsync();
            var units = await GetUnitOptionsAsync();

            if (!productId.HasValue || productId <= 0)
                return new ProductFormViewModel { Categories = categories, Units = units };

            var product = await _uow.Repository<Product>().GetByIdAsync(productId.Value);
            if (product == null)
                return new ProductFormViewModel { Categories = categories, Units = units };

            var images = await _uow.Repository<ProductImage>().Query()
                .Where(i => i.ProductId == productId.Value)
                .OrderBy(i => i.DisplayOrder).ThenByDescending(i => i.IsPrimary)
                .Select(i => new ProductImageViewModel
                {
                    ImageId = i.ImageId,
                    ImageUrl = i.ImageUrl,
                    DisplayOrder = i.DisplayOrder,
                    IsPrimary = i.IsPrimary,
                    CreatedAt = i.CreatedAt
                }).ToListAsync();

            return new ProductFormViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Description = product.Description,
                CategoryId = product.CategoryId,
                UnitId = product.UnitId,
                Barcode = product.Barcode,
                BarcodeType = product.BarcodeType,
                DefaultSalePrice = product.DefaultSalePrice,
                DefaultPurchasePrice = product.DefaultPurchasePrice,
                MinStockLevel = product.MinStockLevel,
                Weight = product.Weight,
                WeightUnit = product.WeightUnit,
                IsActive = product.IsActive,
                Images = images,
                Categories = categories,
                Units = units
            };
        }

        // ── Create ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> CreateAsync(
            ProductFormViewModel model, int createdBy)
        {
            var repo = _uow.Repository<Product>();
            var code = await GenerateProductCodeAsync();

            var product = new Product
            {
                ProductCode = code,
                ProductName = model.ProductName.Trim(),
                Description = model.Description?.Trim(),
                CategoryId = model.CategoryId,
                UnitId = model.UnitId,
                Barcode = model.Barcode?.Trim(),
                BarcodeType = model.BarcodeType,
                DefaultSalePrice = model.DefaultSalePrice,
                DefaultPurchasePrice = model.DefaultPurchasePrice,
                MinStockLevel = model.MinStockLevel,
                Weight = model.Weight,
                WeightUnit = model.WeightUnit?.Trim(),
                IsActive = model.IsActive,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await repo.AddAsync(product);
            await _uow.SaveChangesAsync();

            // Save gallery images; the file at AvatarIndex becomes the avatar
            if (model.GalleryFiles != null && model.GalleryFiles.Count > 0)
            {
                var imgRepo = _uow.Repository<ProductImage>();
                int avatarIdx = Math.Clamp(model.AvatarIndex, 0, model.GalleryFiles.Count - 1);
                int order = 0;
                foreach (var file in model.GalleryFiles)
                {
                    var url = await SaveProductImageAsync(file, product.ProductId);
                    if (url == null) { order++; continue; }

                    bool isPrimary = order == avatarIdx;
                    await imgRepo.AddAsync(new ProductImage
                    {
                        ProductId = product.ProductId,
                        ImageUrl = url,
                        DisplayOrder = order++,
                        IsPrimary = isPrimary,
                        CreatedAt = DateTime.Now
                    });

                    if (isPrimary)
                        product.ImageUrl = url;
                }
                repo.Update(product);
                await _uow.SaveChangesAsync();
            }

            // Auto-generate barcode if not provided
            if (string.IsNullOrEmpty(product.Barcode))
            {
                product.Barcode = GenerateEAN13(product.ProductId);
                product.BarcodeType = "EAN13";
                repo.Update(product);
                await _uow.SaveChangesAsync();
            }

            _logger.LogInformation("Product created: {Code} by UserId={UserId}", code, createdBy);
            return (true, null);
        }

        // ── Update ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(
            ProductFormViewModel model, int updatedBy)
        {
            if (!model.ProductId.HasValue)
                return (false, "ProductId không hợp lệ");

            var repo = _uow.Repository<Product>();
            var product = await repo.GetByIdAsync(model.ProductId.Value);
            if (product == null)
                return (false, "Không tìm thấy sản phẩm");

            // Check barcode uniqueness if provided
            if (!string.IsNullOrWhiteSpace(model.Barcode))
            {
                var barcodeConflict = await repo.AnyAsync(
                    p => p.Barcode == model.Barcode.Trim() &&
                         p.ProductId != model.ProductId.Value &&
                         p.IsActive);
                if (barcodeConflict)
                    return (false, "Mã barcode này đã được sử dụng bởi sản phẩm khác");
            }

            product.ProductName = model.ProductName.Trim();
            product.Description = model.Description?.Trim();
            product.CategoryId = model.CategoryId;
            product.UnitId = model.UnitId;
            product.Barcode = model.Barcode?.Trim();
            product.BarcodeType = model.BarcodeType;
            product.DefaultSalePrice = model.DefaultSalePrice;
            product.DefaultPurchasePrice = model.DefaultPurchasePrice;
            product.MinStockLevel = model.MinStockLevel;
            product.Weight = model.Weight;
            product.WeightUnit = model.WeightUnit?.Trim();
            product.IsActive = model.IsActive;
            product.UpdatedBy = updatedBy;
            product.UpdatedAt = DateTime.Now;

            repo.Update(product);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Product updated: ProductId={Id} by UserId={UserId}",
                model.ProductId, updatedBy);
            return (true, null);
        }

        // ── Toggle Active ────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> ToggleActiveAsync(
            int productId, int updatedBy)
        {
            var repo = _uow.Repository<Product>();
            var product = await repo.GetByIdAsync(productId);
            if (product == null)
                return (false, "Không tìm thấy sản phẩm");

            product.IsActive = !product.IsActive;
            product.UpdatedBy = updatedBy;
            product.UpdatedAt = DateTime.Now;
            repo.Update(product);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Product toggled: ProductId={Id} IsActive={Active} by UserId={UserId}",
                productId, product.IsActive, updatedBy);
            return (true, null);
        }

        // ── Detail ───────────────────────────────────────
        public async Task<ProductDetailViewModel?> GetDetailAsync(int productId)
        {
            var product = await _uow.Repository<Product>().Query()
                .Include(p => p.Category)
                .Include(p => p.Unit)
                .Include(p => p.Images)
                .Include(p => p.CustomerPrices.Where(cp => cp.IsActive))
                    .ThenInclude(cp => cp.Customer)
                .Include(p => p.CustomerPrices.Where(cp => cp.IsActive))
                    .ThenInclude(cp => cp.CustomerGroup)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null) return null;

            return new ProductDetailViewModel
            {
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                Description = product.Description,
                CategoryName = product.Category?.CategoryName,
                CategoryCode = product.Category?.CategoryCode,
                UnitName = product.Unit.UnitName,
                UnitCode = product.Unit.UnitCode,
                Barcode = product.Barcode,
                BarcodeType = product.BarcodeType,
                ImageUrl = product.ImageUrl,
                DefaultSalePrice = product.DefaultSalePrice,
                DefaultPurchasePrice = product.DefaultPurchasePrice,
                MinStockLevel = product.MinStockLevel,
                Weight = product.Weight,
                WeightUnit = product.WeightUnit,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                Images = product.Images
                    .OrderBy(i => i.DisplayOrder).ThenByDescending(i => i.IsPrimary)
                    .Select(i => new ProductImageViewModel
                    {
                        ImageId = i.ImageId,
                        ImageUrl = i.ImageUrl,
                        DisplayOrder = i.DisplayOrder,
                        IsPrimary = i.IsPrimary,
                        CreatedAt = i.CreatedAt
                    }).ToList(),
                CustomerPrices = product.CustomerPrices
                    .OrderBy(cp => cp.SalePrice)
                    .Select(cp => new CustomerPriceViewModel
                    {
                        CustomerPriceId = cp.CustomerPriceId,
                        CustomerName = cp.Customer?.CustomerName,
                        CustomerCode = cp.Customer?.CustomerCode,
                        GroupName = cp.CustomerGroup?.GroupName,
                        SalePrice = cp.SalePrice,
                        Currency = cp.Currency,
                        MinQty = cp.MinQty,
                        EffectiveFrom = cp.EffectiveFrom,
                        EffectiveTo = cp.EffectiveTo,
                        IsActive = cp.IsActive
                    }).ToList()
            };
        }

        // ── Generate Barcode (single) ────────────────────
        public async Task<(bool Success, string? ErrorMessage, GenerateBarcodeResult? Result)> GenerateBarcodeAsync(
            int productId, int updatedBy)
        {
            var repo = _uow.Repository<Product>();
            var product = await repo.GetByIdAsync(productId);
            if (product == null)
                return (false, "Không tìm thấy sản phẩm", null);

            if (!string.IsNullOrEmpty(product.Barcode))
                return (false, "Sản phẩm đã có barcode", null);

            product.Barcode = GenerateEAN13(product.ProductId);
            product.BarcodeType = "EAN13";
            product.UpdatedBy = updatedBy;
            product.UpdatedAt = DateTime.Now;
            repo.Update(product);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Barcode generated: ProductId={Id} Barcode={Barcode}",
                productId, product.Barcode);

            return (true, null, new GenerateBarcodeResult
            {
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                Barcode = product.Barcode,
                BarcodeType = product.BarcodeType
            });
        }

        // ── Generate Barcodes for all products without barcode ──
        public async Task<(bool Success, string? ErrorMessage, int Count)> GenerateBarcodesForAllAsync(int updatedBy)
        {
            var repo = _uow.Repository<Product>();
            var products = await repo.Query()
                .Where(p => p.IsActive && (p.Barcode == null || p.Barcode == ""))
                .ToListAsync();

            if (!products.Any())
                return (true, "Tất cả sản phẩm đã có barcode", 0);

            foreach (var product in products)
            {
                product.Barcode = GenerateEAN13(product.ProductId);
                product.BarcodeType = "EAN13";
                product.UpdatedBy = updatedBy;
                product.UpdatedAt = DateTime.Now;
                repo.Update(product);
            }

            await _uow.SaveChangesAsync();

            _logger.LogInformation("Barcodes generated for {Count} products by UserId={UserId}",
                products.Count, updatedBy);
            return (true, null, products.Count);
        }

        // ── Private Helpers ──────────────────────────────

        /// <summary>
        /// Generate EAN-13 barcode: prefix 200 (internal use) + 9-digit padded ProductId + check digit
        /// </summary>
        private static string GenerateEAN13(int productId)
        {
            // 200 = GS1 prefix for internal/store use
            var body = $"200{productId:D9}";

            // Calculate EAN-13 check digit
            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int digit = body[i] - '0';
                sum += (i % 2 == 0) ? digit : digit * 3;
            }
            int checkDigit = (10 - (sum % 10)) % 10;

            return body + checkDigit;
        }

        private async Task<string> GenerateProductCodeAsync()
        {
            var last = await _uow.Repository<Product>().Query()
                .Where(p => p.ProductCode.StartsWith("SP-"))
                .OrderByDescending(p => p.ProductCode)
                .FirstOrDefaultAsync();

            int next = 1;
            if (last != null && last.ProductCode.Length > 3)
            {
                if (int.TryParse(last.ProductCode[3..], out int n))
                    next = n + 1;
            }

            return $"SP-{next:D5}";
        }

        private async Task<List<CategoryOptionViewModel>> GetCategoryOptionsAsync()
        {
            var categories = await _uow.Repository<ProductCategory>().Query()
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.CategoryName)
                .ToListAsync();
            var lookup = categories.ToLookup(c => c.ParentCategoryId);

            var result = new List<CategoryOptionViewModel>();

            void Flatten(int? parentId, int depth)
            {
                foreach (var c in lookup[parentId].OrderBy(c => c.DisplayOrder).ThenBy(c => c.CategoryName))
                {
                    var prefix = depth == 0 ? "" : new string('-', depth * 3) + "└ ";
                    result.Add(new CategoryOptionViewModel
                    {
                        CategoryId = c.CategoryId,
                        CategoryCode = c.CategoryCode,
                        CategoryName = prefix + c.CategoryName
                    });
                    Flatten(c.CategoryId, depth + 1);
                }
            }

            Flatten(null, 0);
            return result;
        }

        private async Task<List<UnitOptionViewModel>> GetUnitOptionsAsync()
        {
            return await _uow.Repository<Unit>().Query()
                .Where(u => u.IsActive)
                .OrderBy(u => u.UnitName)
                .Select(u => new UnitOptionViewModel
                {
                    UnitId = u.UnitId,
                    UnitCode = u.UnitCode,
                    UnitName = u.UnitName
                })
                .ToListAsync();
        }

        // ── Gallery: Add images ──────────────────────────
        public async Task<(bool Success, string? ErrorMessage, List<ProductImageViewModel> Added)> AddImagesAsync(
            int productId, IList<IFormFile> files)
        {
            var product = await _uow.Repository<Product>().GetByIdAsync(productId);
            if (product == null)
                return (false, "Không tìm thấy sản phẩm", []);

            var imgRepo = _uow.Repository<ProductImage>();
            var currentCount = await imgRepo.CountAsync(i => i.ProductId == productId);
            var added = new List<ProductImageViewModel>();

            foreach (var file in files)
            {
                var url = await SaveProductImageAsync(file, productId);
                if (url == null) continue;

                var img = new ProductImage
                {
                    ProductId = productId,
                    ImageUrl = url,
                    DisplayOrder = currentCount++,
                    IsPrimary = false,
                    CreatedAt = DateTime.Now
                };
                await imgRepo.AddAsync(img);
                await _uow.SaveChangesAsync();

                added.Add(new ProductImageViewModel
                {
                    ImageId = img.ImageId,
                    ImageUrl = img.ImageUrl,
                    DisplayOrder = img.DisplayOrder,
                    IsPrimary = img.IsPrimary,
                    CreatedAt = img.CreatedAt
                });
            }

            _logger.LogInformation("Added {Count} images to ProductId={Id}", added.Count, productId);
            return (true, null, added);
        }

        // ── Gallery: Delete image ────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> DeleteImageAsync(int imageId, int productId)
        {
            var imgRepo = _uow.Repository<ProductImage>();
            var img = await imgRepo.GetByIdAsync(imageId);
            if (img == null || img.ProductId != productId)
                return (false, "Không tìm thấy ảnh");

            DeleteProductImage(img.ImageUrl);
            imgRepo.Remove(img);
            await _uow.SaveChangesAsync();

            // If deleted image was used as avatar, clear it
            var product = await _uow.Repository<Product>().GetByIdAsync(productId);
            if (product != null && product.ImageUrl == img.ImageUrl)
            {
                // Auto-promote primary gallery image as new avatar, if any
                var next = await imgRepo.Query()
                    .Where(i => i.ProductId == productId)
                    .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder)
                    .FirstOrDefaultAsync();
                product.ImageUrl = next?.ImageUrl;
                product.UpdatedAt = DateTime.Now;
                _uow.Repository<Product>().Update(product);
                await _uow.SaveChangesAsync();
            }

            return (true, null);
        }

        // ── Gallery: Set image as avatar ─────────────────
        public async Task<(bool Success, string? ErrorMessage)> SetAvatarAsync(
            int imageId, int productId, int updatedBy)
        {
            var imgRepo = _uow.Repository<ProductImage>();
            var img = await imgRepo.GetByIdAsync(imageId);
            if (img == null || img.ProductId != productId)
                return (false, "Không tìm thấy ảnh");

            // Clear IsPrimary on all other images
            var others = await imgRepo.Query()
                .Where(i => i.ProductId == productId && i.IsPrimary && i.ImageId != imageId)
                .ToListAsync();
            foreach (var o in others) { o.IsPrimary = false; imgRepo.Update(o); }

            img.IsPrimary = true;
            imgRepo.Update(img);

            // Update product avatar
            var product = await _uow.Repository<Product>().GetByIdAsync(productId);
            if (product != null)
            {
                product.ImageUrl = img.ImageUrl;
                product.UpdatedBy = updatedBy;
                product.UpdatedAt = DateTime.Now;
                _uow.Repository<Product>().Update(product);
            }

            await _uow.SaveChangesAsync();
            return (true, null);
        }

        private async Task<string?> SaveProductImageAsync(IFormFile file, int productId)
        {
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return null;

            const long maxBytes = 5 * 1024 * 1024; // 5 MB
            if (file.Length > maxBytes)
                return null;

            var uploadsDir = Path.Combine(_storageRoot, "ProductImages");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(file.FileName).ToLower();
            var fileName = $"product-{productId}-{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/ProductImages/{fileName}";
        }

        private void DeleteProductImage(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;

            // URL format: /uploads/ProductImages/filename.jpg → strip "/uploads/" prefix to get relative path
            var relative = imageUrl.TrimStart('/');
            if (relative.StartsWith("uploads/"))
                relative = relative["uploads/".Length..];

            var filePath = Path.Combine(_storageRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
