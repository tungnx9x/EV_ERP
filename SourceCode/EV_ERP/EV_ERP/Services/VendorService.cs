using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Vendors;
using EV_ERP.Models.ViewModels.Vendors;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class VendorService : IVendorService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<VendorService> _logger;

        public VendorService(IUnitOfWork uow, ILogger<VendorService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // ── List ─────────────────────────────────────────
        public async Task<VendorListViewModel> GetListAsync(string? keyword, string? status)
        {
            var query = _uow.Repository<Vendor>().Query()
                .Where(v => status == "inactive" ? !v.IsActive : v.IsActive);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(v =>
                    v.VendorName.ToLower().Contains(kw) ||
                    v.VendorCode.ToLower().Contains(kw) ||
                    (v.Phone != null && v.Phone.Contains(kw)) ||
                    (v.Email != null && v.Email.ToLower().Contains(kw)) ||
                    (v.TaxCode != null && v.TaxCode.Contains(kw)));
            }

            var vendors = await query.OrderBy(v => v.VendorCode).ToListAsync();

            return new VendorListViewModel
            {
                Vendors = vendors.Select(v => new VendorRowViewModel
                {
                    VendorId = v.VendorId,
                    VendorCode = v.VendorCode,
                    VendorName = v.VendorName,
                    Phone = v.Phone,
                    Email = v.Email,
                    City = v.City,
                    ContactPerson = v.ContactPerson,
                    PaymentTermDays = v.PaymentTermDays,
                    IsActive = v.IsActive,
                    CreatedAt = v.CreatedAt
                }).ToList(),
                SearchKeyword = keyword,
                FilterStatus = status,
                TotalCount = vendors.Count
            };
        }

        // ── Form ─────────────────────────────────────────
        public async Task<VendorFormViewModel> GetFormAsync(int? vendorId = null)
        {
            if (!vendorId.HasValue || vendorId <= 0)
                return new VendorFormViewModel();

            var vendor = await _uow.Repository<Vendor>().GetByIdAsync(vendorId.Value);
            if (vendor == null)
                return new VendorFormViewModel();

            return new VendorFormViewModel
            {
                VendorId = vendor.VendorId,
                VendorName = vendor.VendorName,
                TaxCode = vendor.TaxCode,
                Address = vendor.Address,
                City = vendor.City,
                District = vendor.District,
                Phone = vendor.Phone,
                Email = vendor.Email,
                Website = vendor.Website,
                ContactPerson = vendor.ContactPerson,
                ContactPhone = vendor.ContactPhone,
                ContactEmail = vendor.ContactEmail,
                BankAccountNo = vendor.BankAccountNo,
                BankName = vendor.BankName,
                BankBranch = vendor.BankBranch,
                PaymentTermDays = vendor.PaymentTermDays,
                InternalNotes = vendor.InternalNotes,
                IsActive = vendor.IsActive
            };
        }

        // ── Create ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> CreateAsync(
            VendorFormViewModel model, int createdBy)
        {
            var repo = _uow.Repository<Vendor>();

            if (!string.IsNullOrWhiteSpace(model.TaxCode))
            {
                var conflict = await repo.AnyAsync(v => v.TaxCode == model.TaxCode.Trim() && v.IsActive);
                if (conflict)
                    return (false, "Mã số thuế này đã được sử dụng");
            }

            var code = await GenerateVendorCodeAsync();

            var vendor = new Vendor
            {
                VendorCode = code,
                VendorName = model.VendorName.Trim(),
                TaxCode = model.TaxCode?.Trim(),
                Address = model.Address?.Trim(),
                City = model.City?.Trim(),
                District = model.District?.Trim(),
                Phone = model.Phone?.Trim(),
                Email = model.Email?.Trim().ToLower(),
                Website = model.Website?.Trim(),
                ContactPerson = model.ContactPerson?.Trim(),
                ContactPhone = model.ContactPhone?.Trim(),
                ContactEmail = model.ContactEmail?.Trim().ToLower(),
                BankAccountNo = model.BankAccountNo?.Trim(),
                BankName = model.BankName?.Trim(),
                BankBranch = model.BankBranch?.Trim(),
                PaymentTermDays = model.PaymentTermDays,
                InternalNotes = model.InternalNotes?.Trim(),
                IsActive = model.IsActive,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await repo.AddAsync(vendor);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Vendor created: {Code} by UserId={UserId}", code, createdBy);
            return (true, null);
        }

        // ── Update ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(
            VendorFormViewModel model, int updatedBy)
        {
            if (!model.VendorId.HasValue)
                return (false, "VendorId không hợp lệ");

            var repo = _uow.Repository<Vendor>();
            var vendor = await repo.GetByIdAsync(model.VendorId.Value);
            if (vendor == null)
                return (false, "Không tìm thấy nhà cung cấp");

            if (!string.IsNullOrWhiteSpace(model.TaxCode))
            {
                var conflict = await repo.AnyAsync(v =>
                    v.TaxCode == model.TaxCode.Trim() &&
                    v.VendorId != model.VendorId.Value &&
                    v.IsActive);
                if (conflict)
                    return (false, "Mã số thuế này đã được sử dụng bởi nhà cung cấp khác");
            }

            vendor.VendorName = model.VendorName.Trim();
            vendor.TaxCode = model.TaxCode?.Trim();
            vendor.Address = model.Address?.Trim();
            vendor.City = model.City?.Trim();
            vendor.District = model.District?.Trim();
            vendor.Phone = model.Phone?.Trim();
            vendor.Email = model.Email?.Trim().ToLower();
            vendor.Website = model.Website?.Trim();
            vendor.ContactPerson = model.ContactPerson?.Trim();
            vendor.ContactPhone = model.ContactPhone?.Trim();
            vendor.ContactEmail = model.ContactEmail?.Trim().ToLower();
            vendor.BankAccountNo = model.BankAccountNo?.Trim();
            vendor.BankName = model.BankName?.Trim();
            vendor.BankBranch = model.BankBranch?.Trim();
            vendor.PaymentTermDays = model.PaymentTermDays;
            vendor.InternalNotes = model.InternalNotes?.Trim();
            vendor.IsActive = model.IsActive;
            vendor.UpdatedBy = updatedBy;
            vendor.UpdatedAt = DateTime.Now;

            repo.Update(vendor);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Vendor updated: VendorId={Id} by UserId={UserId}",
                model.VendorId, updatedBy);
            return (true, null);
        }

        // ── Toggle Active ────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> ToggleActiveAsync(
            int vendorId, int updatedBy)
        {
            var repo = _uow.Repository<Vendor>();
            var vendor = await repo.GetByIdAsync(vendorId);
            if (vendor == null)
                return (false, "Không tìm thấy nhà cung cấp");

            vendor.IsActive = !vendor.IsActive;
            vendor.UpdatedBy = updatedBy;
            vendor.UpdatedAt = DateTime.Now;
            repo.Update(vendor);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Vendor toggled: VendorId={Id} IsActive={Active} by UserId={UserId}",
                vendorId, vendor.IsActive, updatedBy);
            return (true, null);
        }

        // ── Detail ───────────────────────────────────────
        public async Task<VendorDetailViewModel?> GetDetailAsync(int vendorId)
        {
            var vendor = await _uow.Repository<Vendor>().Query()
                .Include(v => v.Contacts.Where(c => c.IsActive))
                .FirstOrDefaultAsync(v => v.VendorId == vendorId);

            if (vendor == null) return null;

            var products = await _uow.Repository<VendorPrice>().Query()
                .Where(vp => vp.VendorId == vendorId && vp.IsActive)
                .Include(vp => vp.Product).ThenInclude(p => p.Category)
                .Include(vp => vp.Product).ThenInclude(p => p.Unit)
                .OrderBy(vp => vp.Product.ProductCode)
                .ToListAsync();

            return new VendorDetailViewModel
            {
                VendorId = vendor.VendorId,
                VendorCode = vendor.VendorCode,
                VendorName = vendor.VendorName,
                TaxCode = vendor.TaxCode,
                Address = vendor.Address,
                City = vendor.City,
                District = vendor.District,
                Phone = vendor.Phone,
                Email = vendor.Email,
                Website = vendor.Website,
                ContactPerson = vendor.ContactPerson,
                ContactPhone = vendor.ContactPhone,
                ContactEmail = vendor.ContactEmail,
                BankAccountNo = vendor.BankAccountNo,
                BankName = vendor.BankName,
                BankBranch = vendor.BankBranch,
                PaymentTermDays = vendor.PaymentTermDays,
                AvgDeliveryDays = vendor.AvgDeliveryDays,
                QualityRating = vendor.QualityRating,
                OnTimeRate = vendor.OnTimeRate,
                InternalNotes = vendor.InternalNotes,
                IsActive = vendor.IsActive,
                CreatedAt = vendor.CreatedAt,
                UpdatedAt = vendor.UpdatedAt,
                Contacts = vendor.Contacts
                    .OrderByDescending(c => c.IsPrimary).ThenBy(c => c.ContactName)
                    .Select(c => new VendorContactViewModel
                    {
                        ContactId = c.ContactId,
                        ContactName = c.ContactName,
                        JobTitle = c.JobTitle,
                        Phone = c.Phone,
                        Email = c.Email,
                        IsPrimary = c.IsPrimary,
                        IsActive = c.IsActive
                    }).ToList(),
                Products = products.Select(vp => new VendorProductViewModel
                {
                    VendorPriceId = vp.VendorPriceId,
                    ProductId = vp.ProductId,
                    ProductCode = vp.Product.ProductCode,
                    ProductName = vp.Product.ProductName,
                    CategoryName = vp.Product.Category?.CategoryName,
                    UnitName = vp.Product.Unit.UnitName,
                    PurchasePrice = vp.PurchasePrice,
                    Currency = vp.Currency,
                    MinOrderQty = vp.MinOrderQty,
                    LeadTimeDays = vp.LeadTimeDays,
                    EffectiveFrom = vp.EffectiveFrom,
                    EffectiveTo = vp.EffectiveTo
                }).ToList()
            };
        }

        // ── Contacts ─────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> SaveContactAsync(
            VendorContactFormModel model)
        {
            var repo = _uow.Repository<VendorContact>();

            if (model.IsPrimary)
            {
                var others = await repo.Query()
                    .Where(c => c.VendorId == model.VendorId && c.IsPrimary &&
                                (model.ContactId == null || c.ContactId != model.ContactId))
                    .ToListAsync();
                foreach (var c in others) { c.IsPrimary = false; repo.Update(c); }
            }

            if (model.ContactId.HasValue && model.ContactId > 0)
            {
                var contact = await repo.GetByIdAsync(model.ContactId.Value);
                if (contact == null || contact.VendorId != model.VendorId)
                    return (false, "Không tìm thấy liên hệ");

                contact.ContactName = model.ContactName.Trim();
                contact.JobTitle = model.JobTitle?.Trim();
                contact.Phone = model.Phone?.Trim();
                contact.Email = model.Email?.Trim().ToLower();
                contact.IsPrimary = model.IsPrimary;
                repo.Update(contact);
            }
            else
            {
                var contact = new VendorContact
                {
                    VendorId = model.VendorId,
                    ContactName = model.ContactName.Trim(),
                    JobTitle = model.JobTitle?.Trim(),
                    Phone = model.Phone?.Trim(),
                    Email = model.Email?.Trim().ToLower(),
                    IsPrimary = model.IsPrimary,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                await repo.AddAsync(contact);
            }

            await _uow.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteContactAsync(int contactId)
        {
            var contact = await _uow.Repository<VendorContact>().GetByIdAsync(contactId);
            if (contact == null)
                return (false, "Không tìm thấy liên hệ");

            contact.IsActive = false;
            _uow.Repository<VendorContact>().Update(contact);
            await _uow.SaveChangesAsync();
            return (true, null);
        }

        // ── Private Helpers ──────────────────────────────
        private async Task<string> GenerateVendorCodeAsync()
        {
            var last = await _uow.Repository<Vendor>().Query()
                .Where(v => v.VendorCode.StartsWith("NCC-"))
                .OrderByDescending(v => v.VendorCode)
                .FirstOrDefaultAsync();

            int next = 1;
            if (last != null && last.VendorCode.Length > 4)
            {
                if (int.TryParse(last.VendorCode[4..], out int n))
                    next = n + 1;
            }

            return $"NCC-{next:D4}";
        }
    }
}
