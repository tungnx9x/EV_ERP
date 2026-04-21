using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.Common;
using EV_ERP.Models.ViewModels.RFQs;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services;

public class RfqService : IRfqService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RfqService> _logger;
    private readonly string _storageRoot;
    private readonly ISlaService _slaService;

    public RfqService(IUnitOfWork uow, ILogger<RfqService> logger, IConfiguration config,
        IWebHostEnvironment env, ISlaService slaService)
    {
        _uow = uow;
        _logger = logger;
        _storageRoot = config["FileStorage:RootPath"] ?? Path.Combine(env.ContentRootPath, "ERP_Files");
        _slaService = slaService;
    }

    // ══════════════════════════════════════════════════
    // LIST
    // ══════════════════════════════════════════════════
    public async Task<RfqListViewModel> GetListAsync(
        string? keyword, string? status, string? priority,
        int? assignedTo, int? customerId,
        int pageIndex = 1, int pageSize = 20)
    {
        var query = _uow.Repository<RFQ>().Query()
            .Include(r => r.Customer)
            .Include(r => r.AssignedToUser)
            .Include(r => r.CreatedByUser)
            .Include(r => r.Quotations)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(r =>
                r.RfqNo.ToLower().Contains(kw) ||
                r.Customer.CustomerName.ToLower().Contains(kw) ||
                r.Customer.CustomerCode.ToLower().Contains(kw) ||
                (r.Description != null && r.Description.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(r => r.Priority == priority);

        if (assignedTo.HasValue && assignedTo > 0)
            query = query.Where(r => r.AssignedTo == assignedTo);

        if (customerId.HasValue && customerId > 0)
            query = query.Where(r => r.CustomerId == customerId);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RfqRowViewModel
            {
                RfqId = r.RfqId,
                RfqNo = r.RfqNo,
                CustomerName = r.Customer.CustomerName,
                CustomerCode = r.Customer.CustomerCode,
                RequestDate = r.RequestDate,
                Deadline = r.Deadline,
                Status = r.Status,
                Priority = r.Priority,
                AssignedToName = r.AssignedToUser != null ? r.AssignedToUser.FullName : null,
                Description = r.Description,
                QuotationCount = r.Quotations.Count,
                CreatedByName = r.CreatedByUser.FullName,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return new RfqListViewModel
        {
            Paged = new PagedResult<RfqRowViewModel>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            },
            SearchKeyword = keyword,
            FilterStatus = status,
            FilterPriority = priority,
            FilterAssignedTo = assignedTo,
            FilterCustomerId = customerId,
            Customers = await GetCustomerOptionsAsync(),
            Users = await GetUserOptionsAsync()
        };
    }

    // ══════════════════════════════════════════════════
    // FORM
    // ══════════════════════════════════════════════════
    public async Task<RfqFormViewModel> GetFormAsync(int? rfqId = null)
    {
        var customers = await GetCustomerOptionsAsync();
        var users = await GetUserOptionsAsync();

        if (!rfqId.HasValue || rfqId <= 0)
        {
            var rfqNo = await GenerateRfqNoAsync();
            return new RfqFormViewModel
            {
                Customers = customers,
                Users = users,
                RfqNo = rfqNo
            };
        }

        var r = await _uow.Repository<RFQ>().Query()
            .FirstOrDefaultAsync(x => x.RfqId == rfqId.Value);

        if (r == null)
            return new RfqFormViewModel { Customers = customers, Users = users };

        return new RfqFormViewModel
        {
            RfqId = r.RfqId,
            RfqNo = r.RfqNo,
            CustomerId = r.CustomerId,
            ContactId = r.ContactId,
            RequestDate = r.RequestDate,
            Deadline = r.Deadline,
            Description = r.Description,
            Priority = r.Priority,
            AssignedTo = r.AssignedTo,
            Notes = r.Notes,
            CurrentStatus = r.Status,
            Customers = customers,
            Users = users
        };
    }

    // ══════════════════════════════════════════════════
    // CREATE
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage, int? RfqId)> CreateAsync(
        RfqFormViewModel model, int createdBy)
    {
        try
        {
            if (!model.Deadline.HasValue)
                return (false, "Hạn xử lý là bắt buộc", null);

            var rfqNo = await GenerateRfqNoAsync();

            var rfq = new RFQ
            {
                RfqNo = rfqNo,
                CustomerId = model.CustomerId,
                ContactId = model.ContactId,
                RequestDate = model.RequestDate,
                Deadline = model.Deadline.Value,
                Description = model.Description,
                Priority = model.Priority,
                AssignedTo = model.AssignedTo,
                Notes = model.Notes,
                Status = "INPROGRESS",
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _uow.Repository<RFQ>().AddAsync(rfq);
            await _uow.SaveChangesAsync();

            // SLA: start tracking INPROGRESS
            await _slaService.StartTrackingAsync("RFQ", rfq.RfqId, "INPROGRESS", rfq.AssignedTo);

            _logger.LogInformation("RFQ created: {No} by UserId={UserId}", rfqNo, createdBy);
            return (true, null, rfq.RfqId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating RFQ");
            return (false, "Lỗi tạo yêu cầu báo giá: " + ex.Message, null);
        }
    }

    // ══════════════════════════════════════════════════
    // UPDATE
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(
        RfqFormViewModel model, int updatedBy)
    {
        try
        {
            var rfq = await _uow.Repository<RFQ>().Query()
                .FirstOrDefaultAsync(r => r.RfqId == model.RfqId);

            if (rfq == null) return (false, "Không tìm thấy RFQ");
            if (rfq.Status != "INPROGRESS") return (false, "Chỉ có thể sửa RFQ đang xử lý");
            if (!model.Deadline.HasValue) return (false, "Hạn xử lý là bắt buộc");

            rfq.CustomerId = model.CustomerId;
            rfq.ContactId = model.ContactId;
            rfq.RequestDate = model.RequestDate;
            rfq.Deadline = model.Deadline.Value;
            rfq.Description = model.Description;
            rfq.Priority = model.Priority;
            rfq.AssignedTo = model.AssignedTo;
            rfq.Notes = model.Notes;
            rfq.UpdatedAt = DateTime.Now;

            await _uow.SaveChangesAsync();

            _logger.LogInformation("RFQ updated: {No} by UserId={UserId}", rfq.RfqNo, updatedBy);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating RFQ");
            return (false, "Lỗi cập nhật RFQ: " + ex.Message);
        }
    }

    // ══════════════════════════════════════════════════
    // DETAIL
    // ══════════════════════════════════════════════════
    public async Task<RfqDetailViewModel?> GetDetailAsync(int rfqId)
    {
        var r = await _uow.Repository<RFQ>().Query()
            .Include(x => x.Customer)
            .Include(x => x.Contact)
            .Include(x => x.AssignedToUser)
            .Include(x => x.CreatedByUser)
            .Include(x => x.Quotations)
            .FirstOrDefaultAsync(x => x.RfqId == rfqId);

        if (r == null) return null;

        return new RfqDetailViewModel
        {
            RfqId = r.RfqId,
            RfqNo = r.RfqNo,
            CustomerId = r.CustomerId,
            CustomerName = r.Customer.CustomerName,
            CustomerCode = r.Customer.CustomerCode,
            ContactName = r.Contact?.ContactName,
            ContactPhone = r.Contact?.Phone,
            RequestDate = r.RequestDate,
            Deadline = r.Deadline,
            Description = r.Description,
            Status = r.Status,
            Priority = r.Priority,
            AssignedToName = r.AssignedToUser?.FullName,
            Notes = r.Notes,
            CompletedAt = r.CompletedAt,
            CancelledAt = r.CancelledAt,
            CreatedByName = r.CreatedByUser.FullName,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            Quotations = r.Quotations.Select(q => new RfqQuotationRow
            {
                QuotationId = q.QuotationId,
                QuotationNo = q.QuotationNo,
                Status = q.Status,
                TotalAmount = q.TotalAmount,
                QuotationDate = q.QuotationDate
            }).OrderByDescending(q => q.QuotationDate).ToList()
        };
    }

    // ══════════════════════════════════════════════════
    // CANCEL
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage)> CancelAsync(int rfqId, int userId, string? reason)
    {
        var rfq = await _uow.Repository<RFQ>().Query()
            .FirstOrDefaultAsync(r => r.RfqId == rfqId);

        if (rfq == null) return (false, "Không tìm thấy RFQ");
        if (rfq.Status != "INPROGRESS") return (false, "Chỉ có thể hủy RFQ đang xử lý");

        rfq.Status = "CANCELLED";
        rfq.CancelledAt = DateTime.Now;
        rfq.Notes = !string.IsNullOrEmpty(reason)
            ? $"{rfq.Notes}\n[Hủy] {reason}".Trim()
            : rfq.Notes;
        rfq.UpdatedAt = DateTime.Now;

        await _uow.SaveChangesAsync();

        // SLA: skip all active tracking
        await _slaService.SkipTrackingAsync("RFQ", rfqId);

        _logger.LogInformation("RFQ cancelled: {No} by UserId={UserId}", rfq.RfqNo, userId);
        return (true, null);
    }

    // ══════════════════════════════════════════════════
    // IMAGE UPLOAD
    // ══════════════════════════════════════════════════
    public async Task<string?> UploadImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0) return null;

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext)) return null;

        const long maxBytes = 5 * 1024 * 1024;
        if (file.Length > maxBytes) return null;

        var dir = Path.Combine(_storageRoot, "RFQ", "Images");
        Directory.CreateDirectory(dir);

        var fileName = $"rfq-img-{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(dir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/RFQ/Images/{fileName}";
    }

    // ══════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════
    private async Task<string> GenerateRfqNoAsync()
    {
        var seq = await _uow.NextSequenceValueAsync("RfqSequence");
        return $"RFQ-{DateTime.Now:yyyyMMdd}-{seq:D3}";
    }

    private async Task<List<CustomerOption>> GetCustomerOptionsAsync()
    {
        return await _uow.Repository<Customer>().Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.CustomerName)
            .Select(c => new CustomerOption
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName
            }).ToListAsync();
    }

    private async Task<List<UserOption>> GetUserOptionsAsync()
    {
        return await _uow.Repository<User>().Query()
            .Include(u => u.Role)
            .Where(u => u.IsActive && u.Role.RoleCode == "SALES")
            .OrderBy(u => u.FullName)
            .Select(u => new UserOption
            {
                UserId = u.UserId,
                UserCode = u.UserCode,
                FullName = u.FullName
            }).ToListAsync();
    }
}
