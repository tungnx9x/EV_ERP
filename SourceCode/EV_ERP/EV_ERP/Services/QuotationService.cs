using ClosedXML.Excel;
using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.ViewModels.Quotations;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services;

public class QuotationService : IQuotationService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<QuotationService> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly ISalesOrderService _salesOrderService;
    private readonly ISlaService _slaService;
    private readonly string _storageRoot;

    public QuotationService(IUnitOfWork uow, ILogger<QuotationService> logger, IWebHostEnvironment env,
        ISalesOrderService salesOrderService, ISlaService slaService, IConfiguration config)
    {
        _uow = uow;
        _logger = logger;
        _env = env;
        _salesOrderService = salesOrderService;
        _slaService = slaService;
        _storageRoot = config["FileStorage:RootPath"] ?? Path.Combine(env.ContentRootPath, "ERP_Files");
    }

    // ══════════════════════════════════════════════════
    // LIST (paginated)
    // ══════════════════════════════════════════════════
    public async Task<QuotationListViewModel> GetListAsync(
        string? keyword, string? status, int? customerId, int? salesPersonId,
        int pageIndex = 1, int pageSize = 20)
    {
        var query = _uow.Repository<Quotation>().Query()
            .Include(q => q.Customer)
            .Include(q => q.SalesPerson)
            .Include(q => q.Items)
            .AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(q => q.Status == status);

        if (customerId.HasValue && customerId > 0)
            query = query.Where(q => q.CustomerId == customerId);

        if (salesPersonId.HasValue && salesPersonId > 0)
            query = query.Where(q => q.SalesPersonId == salesPersonId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(q =>
                q.QuotationNo.ToLower().Contains(kw) ||
                q.Customer.CustomerName.ToLower().Contains(kw) ||
                q.Customer.CustomerCode.ToLower().Contains(kw));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(q => q.QuotationDate)
            .ThenByDescending(q => q.QuotationId)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new QuotationRowViewModel
            {
                QuotationId = q.QuotationId,
                QuotationNo = q.QuotationNo,
                CustomerName = q.Customer.CustomerName,
                CustomerCode = q.Customer.CustomerCode,
                SalesPersonName = q.SalesPerson.FullName,
                QuotationDate = q.QuotationDate,
                ExpiryDate = q.ExpiryDate,
                Deadline = q.Deadline,
                Status = q.Status,
                TotalAmount = q.TotalAmount,
                Currency = q.Currency,
                ItemCount = q.Items.Count
            })
            .ToListAsync();

        return new QuotationListViewModel
        {
            Paged = new PagedResult<QuotationRowViewModel>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            },
            SearchKeyword = keyword,
            FilterStatus = status,
            FilterCustomerId = customerId,
            FilterSalesPersonId = salesPersonId,
            Customers = await GetCustomerOptionsAsync(),
            SalesPersons = await GetSalesPersonOptionsAsync()
        };
    }

    // ══════════════════════════════════════════════════
    // FORM
    // ══════════════════════════════════════════════════
    public async Task<QuotationFormViewModel> GetFormAsync(int? quotationId = null, int? rfqId = null)
    {
        var customers = await GetCustomerOptionsAsync();
        var salesPersons = await GetSalesPersonOptionsAsync();

        if (!quotationId.HasValue || quotationId <= 0)
        {
            var code = await GenerateQuotationNoAsync();
            var vm = new QuotationFormViewModel
            {
                RfqId = rfqId,
                Customers = customers,
                SalesPersons = salesPersons,
                QuotationNo = code
            };

            if (rfqId.HasValue && rfqId > 0)
            {
                var rfq = await _uow.Repository<RFQ>().Query()
                    .FirstOrDefaultAsync(r => r.RfqId == rfqId.Value);
                if (rfq != null)
                    vm.CustomerId = rfq.CustomerId;
            }

            return vm;
        }

        var q = await _uow.Repository<Quotation>().Query()
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.QuotationId == quotationId.Value);

        if (q == null)
            return new QuotationFormViewModel { Customers = customers, SalesPersons = salesPersons };

        return new QuotationFormViewModel
        {
            QuotationId = q.QuotationId,
            QuotationNo = q.QuotationNo,
            RfqId = q.RfqId,
            CustomerId = q.CustomerId,
            ContactId = q.ContactId,
            QuotationDate = q.QuotationDate,
            ExpiryDate = q.ExpiryDate,
            Deadline = q.Deadline,
            SalesPersonId = q.SalesPersonId,
            PaymentTerms = q.PaymentTerms,
            TaxRate = q.TaxRate,
            DiscountType = q.DiscountType,
            DiscountValue = q.DiscountValue,
            Notes = q.Notes,
            InternalNotes = q.InternalNotes,
            TemplateId = q.TemplateId,
            CurrentStatus = q.Status,
            Items = q.Items.Select(i => new QuotationItemFormModel
            {
                QuotationItemId = i.QuotationItemId,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Proposal = i.ProductDescription,
                ImageUrl = i.ImageUrl ?? i.Product?.ImageUrl,
                UnitName = i.UnitName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                ImportPrice = i.PurchasePrice ?? 0,
                Shipping = i.ShippingFee ?? 0,
                Coefficient = i.Coefficient ?? 1,
                VatRate = i.TaxRate ?? 10,
                AmountExclVat = i.LineTotal,
                AmountInclVat = i.LineTotalWithTax ?? i.LineTotal,
                DiscountType = i.DiscountType,
                DiscountValue = i.DiscountValue,
                DiscountAmount = i.DiscountAmount,
                LineTotal = i.LineTotal,
                Supplier = i.SourceName,
                SourceUrl = i.SourceUrl,
                SortOrder = i.SortOrder,
                Notes = i.Notes,
                IsProductMapped = i.IsProductMapped
            }).ToList(),
            Customers = customers,
            SalesPersons = salesPersons
        };
    }

    // ══════════════════════════════════════════════════
    // CREATE
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage, int? QuotationId)> CreateAsync(
        QuotationFormViewModel model, int createdBy)
    {
        if (model.Items.Count == 0)
            return (false, "Báo giá phải có ít nhất 1 sản phẩm", null);

        if (!model.Deadline.HasValue)
            return (false, "Hạn xử lý nội bộ là bắt buộc", null);

        var code = await GenerateQuotationNoAsync();

        var quotation = new Quotation
        {
            QuotationNo = code,
            RfqId = model.RfqId,
            CustomerId = model.CustomerId,
            ContactId = model.ContactId,
            QuotationDate = model.QuotationDate,
            ExpiryDate = model.ExpiryDate,
            Deadline = model.Deadline.Value,
            Status = "DRAFT",
            SalesPersonId = model.SalesPersonId > 0 ? model.SalesPersonId : createdBy,
            PaymentTerms = model.PaymentTerms?.Trim(),
            TaxRate = model.TaxRate,
            DiscountType = model.DiscountType,
            DiscountValue = model.DiscountValue,
            Notes = model.Notes?.Trim(),
            InternalNotes = model.InternalNotes?.Trim(),
            TemplateId = model.TemplateId,
            CreatedBy = createdBy,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        // Calculate items
        decimal subTotal = 0;
        int sortOrder = 0;
        foreach (var item in model.Items)
        {
            var discountAmt = CalculateLineDiscount(item.Quantity, item.UnitPrice, item.DiscountType, item.DiscountValue);
            var lineTotal = item.Quantity * item.UnitPrice - discountAmt;
            var lineTaxAmount = Math.Round(lineTotal * item.VatRate / 100m, 0);
            var lineTotalWithTax = lineTotal + lineTaxAmount;
            var hasProduct = item.ProductId.HasValue && item.ProductId > 0;

            quotation.Items.Add(new QuotationItem
            {
                ProductId = hasProduct ? item.ProductId : null,
                ProductName = item.ProductName.Trim(),
                ProductDescription = item.Proposal?.Trim(),
                ImageUrl = item.ImageUrl,
                UnitName = item.UnitName.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                PurchasePrice = item.ImportPrice,
                ShippingFee = item.Shipping,
                Coefficient = item.Coefficient,
                DiscountType = item.DiscountType,
                DiscountValue = item.DiscountValue,
                DiscountAmount = discountAmt,
                LineTotal = lineTotal,
                TaxRate = item.VatRate,
                TaxAmount = lineTaxAmount,
                LineTotalWithTax = lineTotalWithTax,
                SourceUrl = item.SourceUrl?.Trim(),
                SourceName = item.Supplier?.Trim(),
                SortOrder = sortOrder++,
                Notes = item.Notes?.Trim(),
                IsProductMapped = hasProduct
            });

            subTotal += lineTotal;
        }

        // Calculate totals
        var orderDiscountAmt = CalculateOrderDiscount(subTotal, model.DiscountType, model.DiscountValue);
        var afterDiscount = subTotal - orderDiscountAmt;
        var taxAmount = afterDiscount * model.TaxRate / 100m;

        quotation.SubTotal = subTotal;
        quotation.DiscountAmount = orderDiscountAmt;
        quotation.TaxAmount = taxAmount;
        quotation.TotalAmount = afterDiscount + taxAmount;

        await _uow.Repository<Quotation>().AddAsync(quotation);
        await _uow.SaveChangesAsync();

        // SLA: start tracking DRAFT
        await _slaService.StartTrackingAsync("QUOTATION", quotation.QuotationId, "DRAFT", quotation.SalesPersonId);

        _logger.LogInformation("Quotation created: {No} by UserId={UserId}", code, createdBy);
        return (true, null, quotation.QuotationId);
    }

    // ══════════════════════════════════════════════════
    // UPDATE
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage)> UpdateAsync(
        QuotationFormViewModel model, int updatedBy)
    {
        if (!model.QuotationId.HasValue)
            return (false, "QuotationId không hợp lệ");

        if (model.Items.Count == 0)
            return (false, "Báo giá phải có ít nhất 1 sản phẩm");

        var repo = _uow.Repository<Quotation>();
        var quotation = await repo.Query()
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.QuotationId == model.QuotationId.Value);

        if (quotation == null)
            return (false, "Không tìm thấy báo giá");

        if (quotation.Status != "DRAFT")
            return (false, "Chỉ có thể sửa báo giá ở trạng thái Nháp");
        if (!model.Deadline.HasValue)
            return (false, "Hạn xử lý nội bộ là bắt buộc");

        // Update header
        quotation.CustomerId = model.CustomerId;
        quotation.ContactId = model.ContactId;
        quotation.QuotationDate = model.QuotationDate;
        quotation.ExpiryDate = model.ExpiryDate;
        quotation.Deadline = model.Deadline.Value;
        quotation.SalesPersonId = model.SalesPersonId;
        quotation.PaymentTerms = model.PaymentTerms?.Trim();
        quotation.TaxRate = model.TaxRate;
        quotation.DiscountType = model.DiscountType;
        quotation.DiscountValue = model.DiscountValue;
        quotation.Notes = model.Notes?.Trim();
        quotation.InternalNotes = model.InternalNotes?.Trim();
        quotation.TemplateId = model.TemplateId;
        quotation.UpdatedBy = updatedBy;
        quotation.UpdatedAt = DateTime.Now;

        // Remove old items
        var itemRepo = _uow.Repository<QuotationItem>();
        itemRepo.RemoveRange(quotation.Items);

        // Add new items
        decimal subTotal = 0;
        int sortOrder = 0;
        foreach (var item in model.Items)
        {
            var discountAmt = CalculateLineDiscount(item.Quantity, item.UnitPrice, item.DiscountType, item.DiscountValue);
            var lineTotal = item.Quantity * item.UnitPrice - discountAmt;
            var lineTaxAmount = Math.Round(lineTotal * item.VatRate / 100m, 0);
            var lineTotalWithTax = lineTotal + lineTaxAmount;
            var hasProduct = item.ProductId.HasValue && item.ProductId > 0;

            quotation.Items.Add(new QuotationItem
            {
                ProductId = hasProduct ? item.ProductId : null,
                ProductName = item.ProductName.Trim(),
                ProductDescription = item.Proposal?.Trim(),
                ImageUrl = item.ImageUrl,
                UnitName = item.UnitName.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                PurchasePrice = item.ImportPrice,
                ShippingFee = item.Shipping,
                Coefficient = item.Coefficient,
                DiscountType = item.DiscountType,
                DiscountValue = item.DiscountValue,
                DiscountAmount = discountAmt,
                LineTotal = lineTotal,
                TaxRate = item.VatRate,
                TaxAmount = lineTaxAmount,
                LineTotalWithTax = lineTotalWithTax,
                SourceUrl = item.SourceUrl?.Trim(),
                SourceName = item.Supplier?.Trim(),
                SortOrder = sortOrder++,
                Notes = item.Notes?.Trim(),
                IsProductMapped = hasProduct
            });

            subTotal += lineTotal;
        }

        var orderDiscountAmt = CalculateOrderDiscount(subTotal, model.DiscountType, model.DiscountValue);
        var afterDiscount = subTotal - orderDiscountAmt;
        var taxAmount = afterDiscount * model.TaxRate / 100m;

        quotation.SubTotal = subTotal;
        quotation.DiscountAmount = orderDiscountAmt;
        quotation.TaxAmount = taxAmount;
        quotation.TotalAmount = afterDiscount + taxAmount;

        repo.Update(quotation);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Quotation updated: {No} by UserId={UserId}", quotation.QuotationNo, updatedBy);
        return (true, null);
    }

    // ══════════════════════════════════════════════════
    // DETAIL
    // ══════════════════════════════════════════════════
    public async Task<QuotationDetailViewModel?> GetDetailAsync(int quotationId)
    {
        var q = await _uow.Repository<Quotation>().Query()
            .Include(x => x.Rfq)
            .Include(x => x.Customer)
            .Include(x => x.Contact)
            .Include(x => x.SalesPerson)
            .Include(x => x.AmendFrom)
            .Include(x => x.SalesOrder)
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
                .ThenInclude(i => i.Product)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.QuotationId == quotationId);

        if (q == null) return null;

        string? createdByName = null;
        if (q.CreatedBy.HasValue)
        {
            var user = await _uow.Repository<User>().GetByIdAsync(q.CreatedBy.Value);
            createdByName = user?.FullName;
        }

        return new QuotationDetailViewModel
        {
            QuotationId = q.QuotationId,
            QuotationNo = q.QuotationNo,
            RfqId = q.RfqId,
            RfqNo = q.Rfq?.RfqNo,
            CustomerId = q.CustomerId,
            CustomerName = q.Customer.CustomerName,
            CustomerCode = q.Customer.CustomerCode,
            ContactName = q.Contact?.ContactName,
            ContactPhone = q.Contact?.Phone,
            QuotationDate = q.QuotationDate,
            ExpiryDate = q.ExpiryDate,
            Deadline = q.Deadline,
            SentAt = q.SentAt,
            ApprovedAt = q.ApprovedAt,
            RejectedAt = q.RejectedAt,
            RejectReason = q.RejectReason,
            ExpiredAt = q.ExpiredAt,
            CancelledAt = q.CancelledAt,
            CancelReason = q.CancelReason,
            Status = q.Status,
            AmendFromId = q.AmendFromId,
            AmendFromNo = q.AmendFrom?.QuotationNo,
            SalesPersonName = q.SalesPerson.FullName,
            SubTotal = q.SubTotal,
            DiscountType = q.DiscountType,
            DiscountValue = q.DiscountValue,
            DiscountAmount = q.DiscountAmount,
            TaxRate = q.TaxRate,
            TaxAmount = q.TaxAmount,
            TotalAmount = q.TotalAmount,
            Currency = q.Currency,
            PaymentTerms = q.PaymentTerms,
            Notes = q.Notes,
            InternalNotes = q.InternalNotes,
            CreatedAt = q.CreatedAt,
            UpdatedAt = q.UpdatedAt,
            CreatedByName = createdByName,
            SalesOrderId = q.SalesOrder?.SalesOrderId,
            SalesOrderNo = q.SalesOrder?.SalesOrderNo,
            Items = q.Items.Select(i => new QuotationItemDetailViewModel
            {
                QuotationItemId = i.QuotationItemId,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Proposal = i.ProductDescription,
                ImageUrl = i.ImageUrl ?? i.Product?.ImageUrl,
                UnitName = i.UnitName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                ImportPrice = i.PurchasePrice ?? 0,
                Shipping = i.ShippingFee ?? 0,
                Coefficient = i.Coefficient ?? 1,
                VatRate = i.TaxRate ?? 10,
                AmountExclVat = i.LineTotal,
                AmountInclVat = i.LineTotalWithTax ?? i.LineTotal,
                DiscountType = i.DiscountType,
                DiscountValue = i.DiscountValue,
                DiscountAmount = i.DiscountAmount,
                LineTotal = i.LineTotal,
                Supplier = i.SourceName,
                SourceUrl = i.SourceUrl,
                Notes = i.Notes,
                IsProductMapped = i.IsProductMapped
            }).ToList()
        };
    }

    // ══════════════════════════════════════════════════
    // STATUS TRANSITIONS
    // ══════════════════════════════════════════════════
    public async Task<(bool Success, string? ErrorMessage)> SendAsync(int quotationId, int userId)
    {
        var q = await _uow.Repository<Quotation>().GetByIdAsync(quotationId);
        if (q == null) return (false, "Không tìm thấy báo giá");
        if (q.Status != "DRAFT") return (false, "Chỉ có thể gửi báo giá ở trạng thái Nháp");

        q.Status = "SENT";
        q.SentAt = DateTime.Now;
        q.UpdatedBy = userId;
        q.UpdatedAt = DateTime.Now;
        _uow.Repository<Quotation>().Update(q);
        await _uow.SaveChangesAsync();

        // SLA: complete DRAFT, start SENT
        await _slaService.CompleteTrackingAsync("QUOTATION", quotationId, "DRAFT");
        await _slaService.StartTrackingAsync("QUOTATION", quotationId, "SENT", q.SalesPersonId);

        _logger.LogInformation("Quotation sent: {No} by UserId={UserId}", q.QuotationNo, userId);
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> ApproveAsync(int quotationId, int userId)
    {
        var q = await _uow.Repository<Quotation>().GetByIdAsync(quotationId);
        if (q == null) return (false, "Không tìm thấy báo giá");
        if (q.Status != "SENT") return (false, "Chỉ có thể duyệt báo giá đã gửi");

        q.Status = "APPROVED";
        q.ApprovedAt = DateTime.Now;
        q.UpdatedBy = userId;
        q.UpdatedAt = DateTime.Now;
        _uow.Repository<Quotation>().Update(q);
        await _uow.SaveChangesAsync();

        // SLA: complete SENT tracking
        await _slaService.CompleteTrackingAsync("QUOTATION", quotationId, "SENT");

        _logger.LogInformation("Quotation approved: {No} by UserId={UserId}", q.QuotationNo, userId);

        // Auto-create Sales Order
        var (soSuccess, soError, soId) = await _salesOrderService.CreateFromQuotationAsync(quotationId, userId);
        if (!soSuccess)
            _logger.LogWarning("Failed to auto-create SO from Quotation {No}: {Error}", q.QuotationNo, soError);

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> RejectAsync(int quotationId, int userId, string? reason)
    {
        var q = await _uow.Repository<Quotation>().GetByIdAsync(quotationId);
        if (q == null) return (false, "Không tìm thấy báo giá");
        if (q.Status != "SENT") return (false, "Chỉ có thể từ chối báo giá đã gửi");

        q.Status = "REJECTED";
        q.RejectedAt = DateTime.Now;
        q.RejectReason = reason?.Trim();
        q.UpdatedBy = userId;
        q.UpdatedAt = DateTime.Now;
        _uow.Repository<Quotation>().Update(q);
        await _uow.SaveChangesAsync();

        // SLA: complete SENT tracking
        await _slaService.CompleteTrackingAsync("QUOTATION", quotationId, "SENT");

        _logger.LogInformation("Quotation rejected: {No} by UserId={UserId}", q.QuotationNo, userId);
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage, int? NewQuotationId)> AmendAsync(int quotationId, int userId)
    {
        var original = await _uow.Repository<Quotation>().Query()
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId);

        if (original == null) return (false, "Không tìm thấy báo giá", null);
        if (original.Status is not ("SENT" or "REJECTED"))
            return (false, "Chỉ có thể chỉnh sửa báo giá đã gửi hoặc bị từ chối", null);

        // Mark original as AMEND
        original.Status = "AMEND";
        original.UpdatedBy = userId;
        original.UpdatedAt = DateTime.Now;
        _uow.Repository<Quotation>().Update(original);

        // Create copy
        var newCode = await GenerateQuotationNoAsync();
        var copy = new Quotation
        {
            QuotationNo = newCode,
            RfqId = original.RfqId,
            AmendFromId = original.QuotationId,
            CustomerId = original.CustomerId,
            ContactId = original.ContactId,
            QuotationDate = DateTime.Today,
            ExpiryDate = original.ExpiryDate,
            Deadline = DateTime.Today.AddDays(3),
            Status = "DRAFT",
            SalesPersonId = original.SalesPersonId,
            PaymentTerms = original.PaymentTerms,
            TaxRate = original.TaxRate,
            DiscountType = original.DiscountType,
            DiscountValue = original.DiscountValue,
            SubTotal = original.SubTotal,
            DiscountAmount = original.DiscountAmount,
            TaxAmount = original.TaxAmount,
            TotalAmount = original.TotalAmount,
            Currency = original.Currency,
            Notes = original.Notes,
            InternalNotes = original.InternalNotes,
            TemplateId = original.TemplateId,
            CreatedBy = userId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        foreach (var item in original.Items)
        {
            copy.Items.Add(new QuotationItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                ProductDescription = item.ProductDescription,
                ImageUrl = item.ImageUrl,
                UnitName = item.UnitName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                PurchasePrice = item.PurchasePrice,
                ShippingFee = item.ShippingFee,
                Coefficient = item.Coefficient,
                DiscountType = item.DiscountType,
                DiscountValue = item.DiscountValue,
                DiscountAmount = item.DiscountAmount,
                LineTotal = item.LineTotal,
                TaxRate = item.TaxRate,
                TaxAmount = item.TaxAmount,
                LineTotalWithTax = item.LineTotalWithTax,
                SourceUrl = item.SourceUrl,
                SourceName = item.SourceName,
                SortOrder = item.SortOrder,
                Notes = item.Notes,
                IsProductMapped = item.IsProductMapped
            });
        }

        await _uow.Repository<Quotation>().AddAsync(copy);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Quotation amended: {OldNo} → {NewNo} by UserId={UserId}",
            original.QuotationNo, newCode, userId);
        return (true, null, copy.QuotationId);
    }

    public async Task<(bool Success, string? ErrorMessage)> ExpireAsync(int quotationId, int userId)
    {
        var q = await _uow.Repository<Quotation>().GetByIdAsync(quotationId);
        if (q == null) return (false, "Không tìm thấy báo giá");
        if (q.Status != "SENT") return (false, "Chỉ có thể đánh hết hạn báo giá đã gửi");

        q.Status = "EXPIRED";
        q.ExpiredAt = DateTime.Now;
        q.UpdatedBy = userId;
        q.UpdatedAt = DateTime.Now;
        _uow.Repository<Quotation>().Update(q);
        await _uow.SaveChangesAsync();

        // SLA: complete SENT tracking (expired = done waiting)
        await _slaService.CompleteTrackingAsync("QUOTATION", quotationId, "SENT");

        _logger.LogInformation("Quotation expired: {No} by UserId={UserId}", q.QuotationNo, userId);
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> CancelAsync(int quotationId, int userId, string? reason)
    {
        var q = await _uow.Repository<Quotation>().GetByIdAsync(quotationId);
        if (q == null) return (false, "Không tìm thấy báo giá");
        if (q.Status is "APPROVED" or "EXPIRED" or "AMEND")
            return (false, "Không thể hủy báo giá ở trạng thái này");

        q.Status = "CANCELLED";
        q.CancelledAt = DateTime.Now;
        q.CancelReason = reason?.Trim();
        q.UpdatedBy = userId;
        q.UpdatedAt = DateTime.Now;
        _uow.Repository<Quotation>().Update(q);
        await _uow.SaveChangesAsync();

        // SLA: skip all active tracking
        await _slaService.SkipTrackingAsync("QUOTATION", quotationId);

        _logger.LogInformation("Quotation cancelled: {No} by UserId={UserId}", q.QuotationNo, userId);
        return (true, null);
    }

    // ══════════════════════════════════════════════════
    // EXPORT EXCEL
    // ══════════════════════════════════════════════════
    public async Task<(byte[] FileBytes, string FileName)?> ExportExcelByIdAsync(int quotationId)
    {
        var q = await _uow.Repository<Quotation>().Query()
            .Include(x => x.Customer)
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(x => x.QuotationId == quotationId);

        if (q == null || q.Items.Count == 0) return null;

        var request = new QuotationExportRequest
        {
            CustomerId = q.CustomerId,
            Notes = q.Notes,
            Items = q.Items.Select(i => new QuotationItemFormModel
            {
                ProductName = i.ProductName,
                UnitName = i.UnitName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                AmountExclVat = i.LineTotal,
                VatRate = q.TaxRate,
                AmountInclVat = Math.Round(i.LineTotal * (1 + q.TaxRate / 100m), 0),
                Supplier = i.SourceName,
                SourceUrl = i.SourceUrl,
                Notes = i.Notes
            }).ToList()
        };

        return await ExportExcelAsync(request);
    }

    public async Task<(byte[] FileBytes, string FileName)?> ExportExcelAsync(QuotationExportRequest request)
    {
        if (request.Items.Count == 0) return null;

        var customer = await _uow.Repository<Customer>().GetByIdAsync(request.CustomerId);
        if (customer == null) return null;

        var templatePath = Path.Combine(_env.WebRootPath, "templates", "quotation-template.xlsx");
        if (!File.Exists(templatePath)) return null;

        using var wb = new XLWorkbook(templatePath);
        var ws = wb.Worksheet(1);

        // Fill customer info (Row 7-11, merged A:L)
        ws.Cell("A7").Value = $"Khach hang: {customer.CustomerName}";
        ws.Cell("A8").Value = $"Dia chi: {customer.Address ?? ""}";
        ws.Cell("A9").Value = "Nguoi lien he: ";
        ws.Cell("A10").Value = $"SDT: {customer.Phone ?? ""}";
        ws.Cell("A11").Value = $"Email: {customer.Email ?? ""}";

        // Update request column header with customer name
        ws.Cell("B13").Value = $"{customer.CustomerName}'s REQUEST";

        // Data starts at row 14 (template row), need to insert rows for items > 1
        int dataStartRow = 14;
        int itemCount = request.Items.Count;

        if (itemCount > 1)
        {
            // Insert extra rows by copying row 14's format
            ws.Row(dataStartRow).InsertRowsBelow(itemCount - 1);
        }

        // Fill item data
        decimal totalExclVat = 0;
        decimal totalInclVat = 0;

        for (int i = 0; i < itemCount; i++)
        {
            var item = request.Items[i];
            int row = dataStartRow + i;

            // Merge B:C for product name (like template)
            ws.Range(row, 2, row, 3).Merge();

            ws.Cell(row, 1).Value = i + 1;                                     // STT
            ws.Cell(row, 2).Value = item.ProductName;                           // Request
            ws.Cell(row, 4).Value = item.Proposal ?? "";                        // Proposal
            // Column 5 = Image (skip for Excel export)
            ws.Cell(row, 6).Value = item.UnitName;                              // Unit
            ws.Cell(row, 7).Value = item.Quantity;                              // Quantity
            ws.Cell(row, 8).Value = item.UnitPrice;                             // Unit Price
            ws.Cell(row, 9).Value = item.AmountExclVat;                         // Amount excl VAT
            ws.Cell(row, 10).Value = $"{item.VatRate}%";                        // VAT
            ws.Cell(row, 11).Value = item.AmountInclVat;                        // Amount incl VAT
            ws.Cell(row, 12).Value = item.Notes ?? "";                          // Note
            ws.Cell(row, 14).Value = item.Supplier ?? "";                       // NCC
            ws.Cell(row, 15).Value = item.ImportPrice;                          // Import Price
            ws.Cell(row, 16).Value = item.Shipping;                             // Shipping
            ws.Cell(row, 17).Value = item.Coefficient;                          // Coefficient

            // Format number cells
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.###";
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 15).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 16).Style.NumberFormat.Format = "#,##0";

            // Copy borders from template row style
            ws.Range(row, 1, row, 17).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 1, row, 17).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            totalExclVat += item.AmountExclVat;
            totalInclVat += item.AmountInclVat;
        }

        // Totals row (after data rows)
        int totalRow = dataStartRow + itemCount;
        ws.Range(totalRow, 1, totalRow, 8).Merge();
        ws.Cell(totalRow, 1).Value = "TONG GIA TRI (VND)";
        ws.Cell(totalRow, 1).Style.Font.Bold = true;
        ws.Cell(totalRow, 9).Value = totalExclVat;
        ws.Cell(totalRow, 9).Style.NumberFormat.Format = "#,##0";
        ws.Cell(totalRow, 9).Style.Font.Bold = true;
        ws.Cell(totalRow, 11).Value = totalInclVat;
        ws.Cell(totalRow, 11).Style.NumberFormat.Format = "#,##0";
        ws.Cell(totalRow, 11).Style.Font.Bold = true;

        // Update date in signature area
        var now = DateTime.Now;
        int dateRow = totalRow + 8; // approximate position
        // Find the date row dynamically
        for (int r = totalRow + 1; r <= totalRow + 15; r++)
        {
            var cellVal = ws.Cell(r, 7).GetFormattedString();
            if (cellVal.Contains("Ngay") || cellVal.Contains("ngày") || cellVal.Contains("Ngày"))
            {
                ws.Cell(r, 7).Value = $"Ha noi, Ngay {now:dd} Thang {now:MM} Nam {now:yyyy}";
                break;
            }
        }

        // Generate filename: YYYY.MM.DD Quotation EVH-{customer}-{product}
        var firstProductName = request.Items.First().ProductName;
        if (firstProductName.Length > 30) firstProductName = firstProductName[..30];
        var safeCustomer = SanitizeFileName(customer.CustomerName);
        var safeProduct = SanitizeFileName(firstProductName);
        var fileName = $"{now:yyyy.MM.dd} Quotation EVH-{safeCustomer}-{safeProduct}.xlsx";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("", name.Where(c => !invalid.Contains(c))).Trim();
    }

    // ══════════════════════════════════════════════════
    // PRODUCT SEARCH (Ajax)
    // ══════════════════════════════════════════════════
    public async Task<List<ProductSearchResult>> SearchProductsAsync(string keyword, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return [];

        var kw = keyword.Trim().ToLower();

        return await _uow.Repository<Product>().Query()
            .Include(p => p.Unit)
            .Where(p => p.IsActive &&
                (p.ProductName.ToLower().Contains(kw) ||
                 p.ProductCode.ToLower().Contains(kw) ||
                 (p.Barcode != null && p.Barcode.Contains(kw))))
            .OrderBy(p => p.ProductName)
            .Take(maxResults)
            .Select(p => new ProductSearchResult
            {
                ProductId = p.ProductId,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                Description = p.Description,
                ImageUrl = p.ImageUrl,
                UnitName = p.Unit.UnitName,
                DefaultSalePrice = p.DefaultSalePrice,
                DefaultPurchasePrice = p.DefaultPurchasePrice
            })
            .ToListAsync();
    }

    // ══════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════
    private async Task<string> GenerateQuotationNoAsync()
    {
        var seq = await _uow.NextSequenceValueAsync("QuotationSequence");
        return $"BG-{DateTime.Now:yyyyMMdd}-{seq:D3}";
    }

    private async Task<List<CustomerOptionViewModel>> GetCustomerOptionsAsync()
    {
        return await _uow.Repository<Customer>().Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.CustomerName)
            .Select(c => new CustomerOptionViewModel
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName
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
                UserCode = u.UserCode,
                FullName = u.FullName
            })
            .ToListAsync();
    }

    private static decimal CalculateLineDiscount(decimal qty, decimal unitPrice, string? discountType, decimal? discountValue)
    {
        if (discountType == null || !discountValue.HasValue || discountValue <= 0) return 0;
        var gross = qty * unitPrice;
        return discountType == "PERCENT"
            ? Math.Round(gross * discountValue.Value / 100m, 0)
            : Math.Min(discountValue.Value, gross);
    }

    private static decimal CalculateOrderDiscount(decimal subTotal, string? discountType, decimal? discountValue)
    {
        if (discountType == null || !discountValue.HasValue || discountValue <= 0) return 0;
        return discountType == "PERCENT"
            ? Math.Round(subTotal * discountValue.Value / 100m, 0)
            : Math.Min(discountValue.Value, subTotal);
    }

    // ══════════════════════════════════════════════════
    // IMAGE UPLOAD (for ad-hoc quotation items)
    // ══════════════════════════════════════════════════
    public async Task<string?> UploadItemImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0) return null;

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext)) return null;

        const long maxBytes = 5 * 1024 * 1024;
        if (file.Length > maxBytes) return null;

        var dir = Path.Combine(_storageRoot, "Quotation", "Images");
        Directory.CreateDirectory(dir);

        var fileName = $"quot-item-{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(dir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/Quotation/Images/{fileName}";
    }
}
