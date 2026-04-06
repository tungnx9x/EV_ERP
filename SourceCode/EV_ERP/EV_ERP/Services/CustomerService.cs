using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.ViewModels.Customers;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(IUnitOfWork uow, ILogger<CustomerService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // ── List ─────────────────────────────────────────
        public async Task<CustomerListViewModel> GetListAsync(string? keyword, int? groupId, string? status)
        {
            var query = _uow.Repository<Customer>().Query()
                .Include(c => c.CustomerGroup)
                .Include(c => c.SalesPerson)
                .Where(c => status == "inactive" ? !c.IsActive : c.IsActive);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(c =>
                    c.CustomerName.ToLower().Contains(kw) ||
                    c.CustomerCode.ToLower().Contains(kw) ||
                    (c.Phone != null && c.Phone.Contains(kw)) ||
                    (c.Email != null && c.Email.ToLower().Contains(kw)));
            }

            if (groupId.HasValue && groupId > 0)
                query = query.Where(c => c.CustomerGroupId == groupId);

            var customers = await query.OrderBy(c => c.CustomerCode).ToListAsync();

            var groups = await GetGroupOptionsAsync();

            return new CustomerListViewModel
            {
                Customers = customers.Select(c => new CustomerRowViewModel
                {
                    CustomerId = c.CustomerId,
                    CustomerCode = c.CustomerCode,
                    CustomerName = c.CustomerName,
                    Phone = c.Phone,
                    Email = c.Email,
                    City = c.City,
                    GroupName = c.CustomerGroup?.GroupName,
                    GroupCode = c.CustomerGroup?.GroupCode,
                    SalesPersonName = c.SalesPerson?.FullName,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt
                }).ToList(),
                SearchKeyword = keyword,
                FilterGroupId = groupId,
                FilterStatus = status,
                TotalCount = customers.Count,
                Groups = groups
            };
        }

        // ── Form ─────────────────────────────────────────
        public async Task<CustomerFormViewModel> GetFormAsync(int? customerId = null)
        {
            var groups = await GetGroupOptionsAsync();
            var salesPersons = await GetSalesPersonOptionsAsync();

            if (!customerId.HasValue || customerId <= 0)
                return new CustomerFormViewModel { Groups = groups, SalesPersons = salesPersons };

            var customer = await _uow.Repository<Customer>().GetByIdAsync(customerId.Value);
            if (customer == null)
                return new CustomerFormViewModel { Groups = groups, SalesPersons = salesPersons };

            return new CustomerFormViewModel
            {
                CustomerId = customer.CustomerId,
                CustomerName = customer.CustomerName,
                TaxCode = customer.TaxCode,
                Address = customer.Address,
                City = customer.City,
                District = customer.District,
                Ward = customer.Ward,
                Phone = customer.Phone,
                Email = customer.Email,
                Website = customer.Website,
                CustomerGroupId = customer.CustomerGroupId,
                SalesPersonId = customer.SalesPersonId,
                PaymentTermDays = customer.PaymentTermDays,
                CreditLimit = customer.CreditLimit,
                InternalNotes = customer.InternalNotes,
                IsActive = customer.IsActive,
                Groups = groups,
                SalesPersons = salesPersons
            };
        }

        // ── Create ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> CreateAsync(
            CustomerFormViewModel model, int createdBy)
        {
            var repo = _uow.Repository<Customer>();

            if (!string.IsNullOrWhiteSpace(model.TaxCode))
            {
                var taxConflict = await repo.AnyAsync(
                    c => c.TaxCode == model.TaxCode.Trim() && c.IsActive);
                if (taxConflict)
                    return (false, "Mã số thuế này đã được sử dụng");
            }

            var code = await GenerateCustomerCodeAsync();

            var customer = new Customer
            {
                CustomerCode = code,
                CustomerName = model.CustomerName.Trim(),
                TaxCode = model.TaxCode?.Trim(),
                Address = model.Address?.Trim(),
                City = model.City?.Trim(),
                District = model.District?.Trim(),
                Ward = model.Ward?.Trim(),
                Phone = model.Phone?.Trim(),
                Email = model.Email?.Trim().ToLower(),
                Website = model.Website?.Trim(),
                CustomerGroupId = model.CustomerGroupId,
                SalesPersonId = model.SalesPersonId,
                PaymentTermDays = model.PaymentTermDays,
                CreditLimit = model.CreditLimit,
                InternalNotes = model.InternalNotes?.Trim(),
                IsActive = model.IsActive,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await repo.AddAsync(customer);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Customer created: {Code} by UserId={UserId}", code, createdBy);
            return (true, null);
        }

        // ── Update ───────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(
            CustomerFormViewModel model, int updatedBy)
        {
            if (!model.CustomerId.HasValue)
                return (false, "CustomerId không hợp lệ");

            var repo = _uow.Repository<Customer>();
            var customer = await repo.GetByIdAsync(model.CustomerId.Value);
            if (customer == null)
                return (false, "Không tìm thấy khách hàng");

            if (!string.IsNullOrWhiteSpace(model.TaxCode))
            {
                var taxConflict = await repo.AnyAsync(
                    c => c.TaxCode == model.TaxCode.Trim() &&
                         c.CustomerId != model.CustomerId.Value &&
                         c.IsActive);
                if (taxConflict)
                    return (false, "Mã số thuế này đã được sử dụng bởi khách hàng khác");
            }

            customer.CustomerName = model.CustomerName.Trim();
            customer.TaxCode = model.TaxCode?.Trim();
            customer.Address = model.Address?.Trim();
            customer.City = model.City?.Trim();
            customer.District = model.District?.Trim();
            customer.Ward = model.Ward?.Trim();
            customer.Phone = model.Phone?.Trim();
            customer.Email = model.Email?.Trim().ToLower();
            customer.Website = model.Website?.Trim();
            customer.CustomerGroupId = model.CustomerGroupId;
            customer.SalesPersonId = model.SalesPersonId;
            customer.PaymentTermDays = model.PaymentTermDays;
            customer.CreditLimit = model.CreditLimit;
            customer.InternalNotes = model.InternalNotes?.Trim();
            customer.IsActive = model.IsActive;
            customer.UpdatedBy = updatedBy;
            customer.UpdatedAt = DateTime.Now;

            repo.Update(customer);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Customer updated: CustomerId={Id} by UserId={UserId}",
                model.CustomerId, updatedBy);
            return (true, null);
        }

        // ── Toggle Active ────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> ToggleActiveAsync(
            int customerId, int updatedBy)
        {
            var repo = _uow.Repository<Customer>();
            var customer = await repo.GetByIdAsync(customerId);
            if (customer == null)
                return (false, "Không tìm thấy khách hàng");

            customer.IsActive = !customer.IsActive;
            customer.UpdatedBy = updatedBy;
            customer.UpdatedAt = DateTime.Now;
            repo.Update(customer);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Customer toggled: CustomerId={Id} IsActive={Active} by UserId={UserId}",
                customerId, customer.IsActive, updatedBy);
            return (true, null);
        }

        // ── Detail ───────────────────────────────────────
        public async Task<CustomerDetailViewModel?> GetDetailAsync(int customerId)
        {
            var customer = await _uow.Repository<Customer>().Query()
                .Include(c => c.CustomerGroup)
                .Include(c => c.SalesPerson)
                .Include(c => c.Contacts.Where(ct => ct.IsActive))
                .Include(c => c.Notes)
                    .ThenInclude(n => n.CreatedByUser)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (customer == null) return null;

            return new CustomerDetailViewModel
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                TaxCode = customer.TaxCode,
                Address = customer.Address,
                City = customer.City,
                District = customer.District,
                Ward = customer.Ward,
                Phone = customer.Phone,
                Email = customer.Email,
                Website = customer.Website,
                GroupName = customer.CustomerGroup?.GroupName,
                GroupCode = customer.CustomerGroup?.GroupCode,
                SalesPersonName = customer.SalesPerson?.FullName,
                PaymentTermDays = customer.PaymentTermDays,
                CreditLimit = customer.CreditLimit,
                InternalNotes = customer.InternalNotes,
                IsActive = customer.IsActive,
                CreatedAt = customer.CreatedAt,
                UpdatedAt = customer.UpdatedAt,
                Contacts = customer.Contacts
                    .OrderByDescending(c => c.IsPrimary)
                    .ThenBy(c => c.ContactName)
                    .Select(c => new ContactViewModel
                    {
                        ContactId = c.ContactId,
                        ContactName = c.ContactName,
                        JobTitle = c.JobTitle,
                        Phone = c.Phone,
                        Email = c.Email,
                        IsPrimary = c.IsPrimary,
                        Notes = c.Notes,
                        IsActive = c.IsActive
                    }).ToList(),
                Notes = customer.Notes
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new NoteViewModel
                    {
                        NoteId = n.NoteId,
                        NoteContent = n.NoteContent,
                        CreatedByName = n.CreatedByUser?.FullName ?? "—",
                        CreatedAt = n.CreatedAt
                    }).ToList()
            };
        }

        // ── Contacts ─────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage)> SaveContactAsync(ContactFormModel model)
        {
            var contactRepo = _uow.Repository<CustomerContact>();

            // If marking as primary, unset other primary contacts
            if (model.IsPrimary)
            {
                var existing = await contactRepo.Query()
                    .Where(c => c.CustomerId == model.CustomerId && c.IsPrimary &&
                                (model.ContactId == null || c.ContactId != model.ContactId))
                    .ToListAsync();
                foreach (var c in existing)
                {
                    c.IsPrimary = false;
                    contactRepo.Update(c);
                }
            }

            if (model.ContactId.HasValue && model.ContactId > 0)
            {
                var contact = await contactRepo.GetByIdAsync(model.ContactId.Value);
                if (contact == null || contact.CustomerId != model.CustomerId)
                    return (false, "Không tìm thấy liên hệ");

                contact.ContactName = model.ContactName.Trim();
                contact.JobTitle = model.JobTitle?.Trim();
                contact.Phone = model.Phone?.Trim();
                contact.Email = model.Email?.Trim().ToLower();
                contact.IsPrimary = model.IsPrimary;
                contact.Notes = model.Notes?.Trim();
                contact.UpdatedAt = DateTime.Now;
                contactRepo.Update(contact);
            }
            else
            {
                var contact = new CustomerContact
                {
                    CustomerId = model.CustomerId,
                    ContactName = model.ContactName.Trim(),
                    JobTitle = model.JobTitle?.Trim(),
                    Phone = model.Phone?.Trim(),
                    Email = model.Email?.Trim().ToLower(),
                    IsPrimary = model.IsPrimary,
                    Notes = model.Notes?.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                await contactRepo.AddAsync(contact);
            }

            await _uow.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteContactAsync(int contactId)
        {
            var contact = await _uow.Repository<CustomerContact>().GetByIdAsync(contactId);
            if (contact == null)
                return (false, "Không tìm thấy liên hệ");

            contact.IsActive = false;
            contact.UpdatedAt = DateTime.Now;
            _uow.Repository<CustomerContact>().Update(contact);
            await _uow.SaveChangesAsync();
            return (true, null);
        }

        // ── Notes ────────────────────────────────────────
        public async Task<(bool Success, string? ErrorMessage, NoteViewModel? Note)> AddNoteAsync(
            int customerId, string content, int userId)
        {
            if (string.IsNullOrWhiteSpace(content))
                return (false, "Nội dung ghi chú không được để trống", null);

            var note = new CustomerNote
            {
                CustomerId = customerId,
                NoteContent = content.Trim(),
                CreatedBy = userId,
                CreatedAt = DateTime.Now
            };

            await _uow.Repository<CustomerNote>().AddAsync(note);
            await _uow.SaveChangesAsync();

            var user = await _uow.Repository<User>().GetByIdAsync(userId);

            return (true, null, new NoteViewModel
            {
                NoteId = note.NoteId,
                NoteContent = note.NoteContent,
                CreatedByName = user?.FullName ?? "—",
                CreatedAt = note.CreatedAt
            });
        }

        // ── Private Helpers ──────────────────────────────
        private async Task<string> GenerateCustomerCodeAsync()
        {
            var last = await _uow.Repository<Customer>().Query()
                .Where(c => c.CustomerCode.StartsWith("KH-"))
                .OrderByDescending(c => c.CustomerCode)
                .FirstOrDefaultAsync();

            int next = 1;
            if (last != null && last.CustomerCode.Length > 3)
            {
                if (int.TryParse(last.CustomerCode[3..], out int n))
                    next = n + 1;
            }

            return $"KH-{next:D4}";
        }

        private async Task<List<CustomerGroupOptionViewModel>> GetGroupOptionsAsync()
        {
            return await _uow.Repository<CustomerGroup>().Query()
                .Where(g => g.IsActive)
                .OrderBy(g => g.PriorityLevel).ThenBy(g => g.GroupName)
                .Select(g => new CustomerGroupOptionViewModel
                {
                    CustomerGroupId = g.CustomerGroupId,
                    GroupCode = g.GroupCode,
                    GroupName = g.GroupName
                })
                .ToListAsync();
        }

        private async Task<List<SalesPersonOptionViewModel>> GetSalesPersonOptionsAsync()
        {
            return await _uow.Repository<User>().Query()
                .Where(u => u.IsActive && !u.IsLocked &&
                            (u.Role.RoleCode == "SALES" || u.Role.RoleCode == "MANAGER" || u.Role.RoleCode == "ADMIN"))
                .Include(u => u.Role)
                .OrderBy(u => u.FullName)
                .Select(u => new SalesPersonOptionViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    UserCode = u.UserCode
                })
                .ToListAsync();
        }
    }
}
