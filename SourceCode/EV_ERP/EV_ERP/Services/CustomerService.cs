using ClosedXML.Excel;
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

            if (!string.IsNullOrWhiteSpace(model.Phone))
            {
                var phone = model.Phone.Trim();
                var phoneConflict = await repo.AnyAsync(c => c.Phone == phone && c.IsActive);
                if (phoneConflict)
                    return (false, "Số điện thoại này đã được sử dụng");
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

            if (!string.IsNullOrWhiteSpace(model.Phone))
            {
                var phone = model.Phone.Trim();
                var phoneConflict = await repo.AnyAsync(
                    c => c.Phone == phone &&
                         c.CustomerId != model.CustomerId.Value &&
                         c.IsActive);
                if (phoneConflict)
                    return (false, "Số điện thoại này đã được sử dụng bởi khách hàng khác");
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

        // ── Import from Excel ────────────────────────────
        // Expected columns (header row auto-detected & skipped):
        //   A = Tên khách hàng (bắt buộc) | B = Địa chỉ | C = Số điện thoại
        //   D = Email | E = Website
        // Customer group is forced to "NEW" (Khách hàng mới).
        public async Task<CustomerImportResult> ImportFromExcelAsync(IFormFile file, int createdBy)
        {
            var result = new CustomerImportResult();

            if (file == null || file.Length == 0)
            {
                result.ErrorMessage = "File không hợp lệ";
                return result;
            }

            // Resolve default "New Customer" group
            var newGroupId = await _uow.Repository<CustomerGroup>().Query()
                .Where(g => g.GroupCode == "NEW")
                .Select(g => (int?)g.CustomerGroupId)
                .FirstOrDefaultAsync();

            IXLWorksheet ws;
            try
            {
                using var stream = file.OpenReadStream();
                using var wb = new XLWorkbook(stream);
                ws = wb.Worksheet(1);
                if (ws == null)
                {
                    result.ErrorMessage = "Không đọc được sheet đầu tiên";
                    return result;
                }

                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                if (lastRow == 0)
                {
                    result.ErrorMessage = "File không có dữ liệu";
                    return result;
                }

                // Detect header: if row 1 col A is non-numeric text containing "tên"/"name"
                // or any header keyword, treat it as a header and start at row 2.
                int startRow = 1;
                var firstA = ws.Cell(1, 1).GetString().Trim().ToLower();
                if (firstA.Contains("tên") || firstA.Contains("name") ||
                    firstA.Contains("khách") || firstA == "stt" || firstA == "#")
                    startRow = 2;

                var repo = _uow.Repository<Customer>();

                // Seed the running code counter once for the whole batch
                int nextCode = await GetNextCustomerCodeNumberAsync();

                // Phone-based duplicate detection: load existing active phones once,
                // and track phones added during this import to catch intra-file dupes.
                var existingPhones = await repo.Query()
                    .Where(c => c.IsActive && c.Phone != null && c.Phone != "")
                    .Select(c => c.Phone!)
                    .ToListAsync();
                var seenPhones = new HashSet<string>(existingPhones, StringComparer.OrdinalIgnoreCase);

                for (int r = startRow; r <= lastRow; r++)
                {
                    var name = ws.Cell(r, 1).GetString().Trim();
                    var address = ws.Cell(r, 2).GetString().Trim();
                    var phone = ws.Cell(r, 3).GetString().Trim();
                    var email = ws.Cell(r, 4).GetString().Trim();
                    var website = ws.Cell(r, 5).GetString().Trim();

                    // Skip fully-empty rows silently
                    if (string.IsNullOrWhiteSpace(name) &&
                        string.IsNullOrWhiteSpace(address) &&
                        string.IsNullOrWhiteSpace(phone) &&
                        string.IsNullOrWhiteSpace(email) &&
                        string.IsNullOrWhiteSpace(website))
                        continue;

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        result.RowErrors.Add($"Dòng {r}: thiếu tên khách hàng → bỏ qua");
                        result.SkippedCount++;
                        continue;
                    }

                    // Skip duplicates based on phone number (existing DB or earlier in file)
                    if (!string.IsNullOrWhiteSpace(phone) && !seenPhones.Add(phone))
                    {
                        result.RowErrors.Add($"Dòng {r}: số điện thoại '{phone}' đã tồn tại → bỏ qua");
                        result.SkippedCount++;
                        continue;
                    }

                    var customer = new Customer
                    {
                        CustomerCode = $"KH-{nextCode:D4}",
                        CustomerName = name,
                        Address = string.IsNullOrWhiteSpace(address) ? null : address,
                        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                        Email = string.IsNullOrWhiteSpace(email) ? null : email.ToLower(),
                        Website = string.IsNullOrWhiteSpace(website) ? null : website,
                        CustomerGroupId = newGroupId,
                        PaymentTermDays = 30,
                        IsActive = true,
                        CreatedBy = createdBy,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    await repo.AddAsync(customer);
                    nextCode++;
                    result.ImportedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Customer Excel import failed");
                result.ErrorMessage = "Không đọc được file Excel. Vui lòng kiểm tra định dạng.";
                return result;
            }

            if (result.ImportedCount == 0)
            {
                result.ErrorMessage = result.SkippedCount > 0
                    ? $"Không có khách hàng nào được thêm. Đã bỏ qua {result.SkippedCount} dòng (trùng hoặc thiếu dữ liệu)."
                    : "Không có dòng dữ liệu hợp lệ để import";
                return result;
            }

            await _uow.SaveChangesAsync();
            result.Success = true;
            _logger.LogInformation("Customer import: {Count} created by UserId={UserId}",
                result.ImportedCount, createdBy);
            return result;
        }

        // ── Import template ──────────────────────────────
        public byte[] BuildImportTemplate()
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Khách hàng");

            string[] headers = { "Tên khách hàng (*)", "Địa chỉ", "Số điện thoại", "Email", "Website" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E0F2FE");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Sample row to illustrate the expected format
            ws.Cell(2, 1).Value = "Khách sạn ABC";
            ws.Cell(2, 2).Value = "123 Lê Lợi, Quận 1, TP.HCM";
            ws.Cell(2, 3).Value = "0901234567";
            ws.Cell(2, 4).Value = "lienhe@abc.com";
            ws.Cell(2, 5).Value = "https://abc.com";

            ws.Columns(1, 5).AdjustToContents();
            ws.SheetView.FreezeRows(1);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
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
            => $"KH-{await GetNextCustomerCodeNumberAsync():D4}";

        private async Task<int> GetNextCustomerCodeNumberAsync()
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

            return next;
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
