using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using EV_ERP.Models.Common;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Products;
using EV_ERP.Models.Entities.Reference;
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
        string? keyword, string? status, int? customerId, int? salesPersonId, int? createdBy,
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

        if (createdBy.HasValue && createdBy > 0)
            query = query.Where(q => q.CreatedBy == createdBy);

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
                ItemCount = q.Items.Count,
                CreatedBy = q.CreatedBy
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
            FilterCreatedBy = createdBy,
            Customers = await GetCustomerOptionsAsync(),
            SalesPersons = await GetSalesPersonOptionsAsync(),
            Users = await GetFilterUserOptionsAsync()
        };
    }

    // ══════════════════════════════════════════════════
    // FORM
    // ══════════════════════════════════════════════════
    public async Task<QuotationFormViewModel> GetFormAsync(int? quotationId = null, int? rfqId = null)
    {
        var customers = await GetCustomerOptionsAsync();
        var salesPersons = await GetSalesPersonOptionsAsync();
        var currencies = await GetCurrencyOptionsAsync();
        var units = await GetUnitOptionsAsync();

        if (!quotationId.HasValue || quotationId <= 0)
        {
            var code = await GenerateQuotationNoAsync();
            var vm = new QuotationFormViewModel
            {
                RfqId = rfqId,
                Customers = customers,
                SalesPersons = salesPersons,
                Currencies = currencies,
                Units = units,
                QuotationNo = code
            };

            if (rfqId.HasValue && rfqId > 0)
            {
                var rfq = await _uow.Repository<RFQ>().Query()
                    .FirstOrDefaultAsync(r => r.RfqId == rfqId.Value);
                if (rfq != null)
                {
                    vm.CustomerId = rfq.CustomerId;
                    vm.Deadline = rfq.Deadline;
                }
            }

            return vm;
        }

        var q = await _uow.Repository<Quotation>().Query()
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.QuotationId == quotationId.Value);

        if (q == null)
            return new QuotationFormViewModel { Customers = customers, SalesPersons = salesPersons, Currencies = currencies, Units = units };

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
            CreatedBy = q.CreatedBy,
            Items = q.Items.Select(i => new QuotationItemFormModel
            {
                QuotationItemId = i.QuotationItemId,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Proposal = i.ProductDescription,
                ImageUrl = i.ImageUrl ?? i.Product?.ImageUrl,
                RequiredImageUrl = i.RequiredImageUrl,
                RequiredDescription = i.RequiredDescription,
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
                IsProductMapped = i.IsProductMapped,
                PurchaseCurrency = i.PurchaseCurrency ?? "VND",
                ExchangeRate = i.PurchaseExchangeRate ?? 1,
                PurchaseMode = string.IsNullOrEmpty(i.PurchaseMode) ? "OFFICIAL" : i.PurchaseMode,
                PurchaseQuantity = i.PurchaseQuantity,
                BasePrice = i.BasePrice,
                PurchaseTax = i.PurchaseTax,
                InspectionFee = i.InspectionFee,
                BankingFee = i.BankingFee,
                OtherCosts = i.OtherCosts,
                OfficialShipping = i.OfficialShipping,
                UnofficialDomesticShipping = i.UnofficialDomesticShipping,
                UnofficialWeightKg = i.UnofficialWeightKg,
                UnofficialCostPerKg = i.UnofficialCostPerKg,
                UnofficialHandCarryFee = i.UnofficialHandCarryFee,
                UnofficialW2WShipping = i.UnofficialW2WShipping,
                UnofficialLength = i.UnofficialLength,
                UnofficialWidth = i.UnofficialWidth,
                UnofficialHeight = i.UnofficialHeight,
                UnofficialCostPerCbm = i.UnofficialCostPerCbm,
                PurchaseVatPercent = i.PurchaseVatPercent
            }).ToList(),
            Customers = customers,
            SalesPersons = salesPersons,
            Currencies = currencies,
            Units = units
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
                RequiredImageUrl = item.RequiredImageUrl,
                RequiredDescription = item.RequiredDescription?.Trim(),
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
                IsProductMapped = hasProduct,
                PurchaseCurrency = string.IsNullOrWhiteSpace(item.PurchaseCurrency) ? "VND" : item.PurchaseCurrency.Trim().ToUpperInvariant(),
                PurchaseExchangeRate = item.ExchangeRate > 0 ? item.ExchangeRate : 1,
                PurchaseMode = NormalizePurchaseMode(item.PurchaseMode),
                PurchaseQuantity = item.PurchaseQuantity,
                BasePrice = item.BasePrice,
                PurchaseTax = item.PurchaseTax,
                InspectionFee = item.InspectionFee,
                BankingFee = item.BankingFee,
                OtherCosts = item.OtherCosts,
                OfficialShipping = item.OfficialShipping,
                UnofficialDomesticShipping = item.UnofficialDomesticShipping,
                UnofficialWeightKg = item.UnofficialWeightKg,
                UnofficialCostPerKg = item.UnofficialCostPerKg,
                UnofficialHandCarryFee = item.UnofficialHandCarryFee,
                UnofficialW2WShipping = item.UnofficialW2WShipping,
                UnofficialLength = item.UnofficialLength,
                UnofficialWidth = item.UnofficialWidth,
                UnofficialHeight = item.UnofficialHeight,
                UnofficialCostPerCbm = item.UnofficialCostPerCbm,
                PurchaseVatPercent = item.PurchaseVatPercent
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
                RequiredImageUrl = item.RequiredImageUrl,
                RequiredDescription = item.RequiredDescription?.Trim(),
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
                IsProductMapped = hasProduct,
                PurchaseCurrency = string.IsNullOrWhiteSpace(item.PurchaseCurrency) ? "VND" : item.PurchaseCurrency.Trim().ToUpperInvariant(),
                PurchaseExchangeRate = item.ExchangeRate > 0 ? item.ExchangeRate : 1,
                PurchaseMode = NormalizePurchaseMode(item.PurchaseMode),
                PurchaseQuantity = item.PurchaseQuantity,
                BasePrice = item.BasePrice,
                PurchaseTax = item.PurchaseTax,
                InspectionFee = item.InspectionFee,
                BankingFee = item.BankingFee,
                OtherCosts = item.OtherCosts,
                OfficialShipping = item.OfficialShipping,
                UnofficialDomesticShipping = item.UnofficialDomesticShipping,
                UnofficialWeightKg = item.UnofficialWeightKg,
                UnofficialCostPerKg = item.UnofficialCostPerKg,
                UnofficialHandCarryFee = item.UnofficialHandCarryFee,
                UnofficialW2WShipping = item.UnofficialW2WShipping,
                UnofficialLength = item.UnofficialLength,
                UnofficialWidth = item.UnofficialWidth,
                UnofficialHeight = item.UnofficialHeight,
                UnofficialCostPerCbm = item.UnofficialCostPerCbm,
                PurchaseVatPercent = item.PurchaseVatPercent
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
            SalesPersonId = q.SalesPersonId,
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
            CreatedBy = q.CreatedBy,
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
                IsProductMapped = i.IsProductMapped,
                PurchaseCurrency = i.PurchaseCurrency ?? "VND",
                PurchaseMode = string.IsNullOrEmpty(i.PurchaseMode) ? "OFFICIAL" : i.PurchaseMode,
                PurchaseQuantity = i.PurchaseQuantity,
                BasePrice = i.BasePrice,
                PurchaseTax = i.PurchaseTax,
                InspectionFee = i.InspectionFee,
                BankingFee = i.BankingFee,
                OtherCosts = i.OtherCosts,
                OfficialShipping = i.OfficialShipping,
                UnofficialDomesticShipping = i.UnofficialDomesticShipping,
                UnofficialWeightKg = i.UnofficialWeightKg,
                UnofficialCostPerKg = i.UnofficialCostPerKg,
                UnofficialHandCarryFee = i.UnofficialHandCarryFee,
                UnofficialW2WShipping = i.UnofficialW2WShipping,
                UnofficialLength = i.UnofficialLength,
                UnofficialWidth = i.UnofficialWidth,
                UnofficialHeight = i.UnofficialHeight,
                UnofficialCostPerCbm = i.UnofficialCostPerCbm,
                PurchaseVatPercent = i.PurchaseVatPercent
            }).ToList()
        };
    }

    public async Task<int?> GetSalesPersonIdAsync(int quotationId)
    {
        return await _uow.Repository<Quotation>().Query()
            .Where(q => q.QuotationId == quotationId)
            .Select(q => (int?)q.SalesPersonId)
            .FirstOrDefaultAsync();
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
                RequiredImageUrl = item.RequiredImageUrl,
                RequiredDescription = item.RequiredDescription,
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
                IsProductMapped = item.IsProductMapped,
                PurchaseCurrency = item.PurchaseCurrency,
                PurchaseExchangeRate = item.PurchaseExchangeRate,
                PurchaseMode = string.IsNullOrEmpty(item.PurchaseMode) ? "OFFICIAL" : item.PurchaseMode,
                PurchaseQuantity = item.PurchaseQuantity,
                BasePrice = item.BasePrice,
                PurchaseTax = item.PurchaseTax,
                InspectionFee = item.InspectionFee,
                BankingFee = item.BankingFee,
                OtherCosts = item.OtherCosts,
                OfficialShipping = item.OfficialShipping,
                UnofficialDomesticShipping = item.UnofficialDomesticShipping,
                UnofficialWeightKg = item.UnofficialWeightKg,
                UnofficialCostPerKg = item.UnofficialCostPerKg,
                UnofficialHandCarryFee = item.UnofficialHandCarryFee,
                UnofficialW2WShipping = item.UnofficialW2WShipping,
                UnofficialLength = item.UnofficialLength,
                UnofficialWidth = item.UnofficialWidth,
                UnofficialHeight = item.UnofficialHeight,
                UnofficialCostPerCbm = item.UnofficialCostPerCbm,
                PurchaseVatPercent = item.PurchaseVatPercent
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
                Proposal = i.ProductDescription,
                RequiredDescription = i.RequiredDescription,
                ImageUrl = i.ImageUrl,
                RequiredImageUrl = i.RequiredImageUrl,
                UnitName = i.UnitName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                AmountExclVat = i.LineTotal,
                VatRate = i.TaxRate ?? q.TaxRate,
                AmountInclVat = i.LineTotalWithTax ?? Math.Round(i.LineTotal * (1 + q.TaxRate / 100m), 0),
                Supplier = i.SourceName,
                SourceUrl = i.SourceUrl,
                ImportPrice = i.PurchasePrice ?? 0,
                Shipping = i.ShippingFee ?? 0,
                Coefficient = i.Coefficient ?? 1,
                Notes = i.Notes,
                // Purchase/CBM breakdown — needed for export columns O (Giá RMB), P (Giá VND),
                // Q (Weight), R (Volume CBM) and the S shipping formula.
                PurchaseCurrency = i.PurchaseCurrency ?? "VND",
                ExchangeRate = i.PurchaseExchangeRate ?? 1,
                PurchaseMode = string.IsNullOrEmpty(i.PurchaseMode) ? "OFFICIAL" : i.PurchaseMode,
                BasePrice = i.BasePrice,
                OfficialShipping = i.OfficialShipping,
                UnofficialDomesticShipping = i.UnofficialDomesticShipping,
                UnofficialWeightKg = i.UnofficialWeightKg,
                UnofficialCostPerKg = i.UnofficialCostPerKg,
                UnofficialHandCarryFee = i.UnofficialHandCarryFee,
                UnofficialW2WShipping = i.UnofficialW2WShipping,
                UnofficialLength = i.UnofficialLength,
                UnofficialWidth = i.UnofficialWidth,
                UnofficialHeight = i.UnofficialHeight,
                UnofficialCostPerCbm = i.UnofficialCostPerCbm
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

        // ── Customer info (rows 7-11, merged across the header band) ──
        ws.Cell("A7").Value = $"Khách hàng: {(customer.CustomerName ?? string.Empty).ToUpper()}";
        ws.Cell("A8").Value = $"Địa chỉ: {customer.Address ?? string.Empty}";
        ws.Cell("A9").Value = "Người liên hệ: ";
        ws.Cell("A10").Value = $"SĐT: {customer.Phone ?? string.Empty}";
        ws.Cell("A11").Value = $"Email: {customer.Email ?? string.Empty}";

        // Request column header (B13:C13 merged)
        ws.Cell("B13").Value = $"{customer.CustomerName}'s REQUEST";

        const int dataStartRow = 14;
        const int lastCol = 25; // Column Y — covers the GIÁ THỊ TRƯỜNG section
        int itemCount = request.Items.Count;

        // Capture the template data-row height BEFORE duplicating. Range.CopyTo /
        // InsertRowsBelow copy cell content & styles but NOT the row height, so the
        // inserted rows collapse to the default height on save — which makes every
        // embedded image (anchored with a fixed top offset sized for the tall row)
        // overflow its now-short row and appear misaligned from the 2nd item onward.
        double dataRowHeight = ws.Row(dataStartRow).Height;

        // Duplicate template row 14 (with formulas, styles, merges) for additional items.
        if (itemCount > 1)
        {
            ws.Row(dataStartRow).InsertRowsBelow(itemCount - 1);
            var templateRange = ws.Range(dataStartRow, 1, dataStartRow, lastCol);
            for (int i = 1; i < itemCount; i++)
            {
                templateRange.CopyTo(ws.Cell(dataStartRow + i, 1));
            }
        }

        // Re-apply the template row height to every data row so the inserted rows keep
        // the tall layout the embedded images are positioned for.
        for (int i = 0; i < itemCount; i++)
            ws.Row(dataStartRow + i).Height = dataRowHeight;

        // Fill placeholders per row — leave formula cells (I, K, P, R, S, T, W, Y)
        // and market-price input cells (O, Q, U, V, X) untouched.
        for (int i = 0; i < itemCount; i++)
        {
            var item = request.Items[i];
            int row = dataStartRow + i;

            // A — STT
            ws.Cell(row, 1).Value = i + 1;

            // B — Customer required image (clear placeholder text, embed picture)
            ws.Cell(row, 2).Value = string.Empty;
            EmbedItemPicture(ws, item.RequiredImageUrl, row, 2);

            // C — Customer required description
            ws.Cell(row, 3).Value = item.RequiredDescription?.Trim() ?? string.Empty;
            ws.Cell(row, 3).Style.Alignment.WrapText = true;
            ws.Cell(row, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // D — EVH Proposal / ProductDescription
            ws.Cell(row, 4).Value = item.Proposal ?? string.Empty;
            ws.Cell(row, 4).Style.Alignment.WrapText = true;
            ws.Cell(row, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // E — Product image (clear placeholder text, embed picture)
            ws.Cell(row, 5).Value = string.Empty;
            EmbedItemPicture(ws, item.ImageUrl, row, 5);

            // F — Unit
            ws.Cell(row, 6).Value = item.UnitName;

            // G — Quantity
            ws.Cell(row, 7).Value = item.Quantity;
            ws.Cell(row, 7).Style.NumberFormat.Format = "#";

            // H — Unit price
            ws.Cell(row, 8).Value = item.UnitPrice;
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";

            // I — formula =H*G (untouched)

            // J — VAT rate; write as decimal so K = I+I*J computes correctly
            ws.Cell(row, 10).Value = item.VatRate / 100m;
            ws.Cell(row, 10).Style.NumberFormat.Format = "0%";

            // K — formula =I+I*J (untouched)

            // L — Notes
            ws.Cell(row, 12).Value = item.Notes ?? string.Empty;
     
            // N — Source URL / NCC link
            ws.Cell(row, 14).Value = item.SourceUrl ?? string.Empty;
            // O — Gia RMB
            if (item.PurchaseCurrency != "VND")
            {
                ws.Cell(row, 15).Value = item.BasePrice;
                ws.Cell(row, 15).Style.NumberFormat.Format = "#,##0";
            }
            else
            {
                ws.Cell(row, 15).Value = 0;
            }
            // P Gia VND = O × Tỷ giá
            ws.Cell(row, 16).FormulaA1 = $"O{row}*{item.ExchangeRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            // Q Can nang
            ws.Cell(row, 17).Value = item.UnofficialWeightKg;
            ws.Cell(row, 17).Style.NumberFormat.Format = "#,##0";
            // R Volume (CBM) = Dài × Rộng × Cao (write as a formula so the cell shows L*W*H)
            var unofficialLength = (item.UnofficialLength ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var unofficialWidth = (item.UnofficialWidth ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var unofficialHeight = (item.UnofficialHeight ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            ws.Cell(row, 18).FormulaA1 = $"{unofficialLength}*{unofficialWidth}*{unofficialHeight}";
            ws.Cell(row, 18).Style.NumberFormat.Format = "#,##0";
            // S Vận chuyển tới EV = tổng chi phí vận chuyển mua hàng, kèm công thức thành phần.
            // VD: Nội địa 5 + (Phí/kg 5 × 1 kg) → giá trị 10, công thức "=5 + 5 * 1".
            var shipFormula = BuildShippingToEvFormula(item);
            if (shipFormula != null)
                ws.Cell(row, 19).FormulaA1 = shipFormula;
            else
                ws.Cell(row, 19).Value = 0;
            ws.Cell(row, 19).Style.NumberFormat.Format = "#,##0";
            //T Gia von final
            ws.Cell(row, 20).Value = item.ImportPrice;
            ws.Cell(row, 20).Style.NumberFormat.Format = "#,##0";
            // U Ship
            ws.Cell(row, 21).Value = item.Shipping/item.Quantity;
            ws.Cell(row, 21).Style.NumberFormat.Format = "#,##0";
            // V He so — keep the original (possibly fractional) value, e.g. 0.5
            ws.Cell(row, 22).Value = item.Coefficient;
            ws.Cell(row, 22).Style.NumberFormat.Format = "#,##0";
            // X — VAT (market-price section); same fraction + "0%" format as the J column
            ws.Cell(row, 24).Value = item.VatRate / 100m;
            ws.Cell(row, 24).Style.NumberFormat.Format = "0%";
        }

        // Totals row sits immediately after the data block (was row 15 in the template,
        // now row 14 + itemCount after row insertion).
        int totalRow = dataStartRow + itemCount;
        int lastDataRow = dataStartRow + itemCount - 1;
        ws.Cell(totalRow, 9).FormulaA1 = $"SUM(I{dataStartRow}:I{lastDataRow})";
        ws.Cell(totalRow, 11).FormulaA1 = $"SUM(K{dataStartRow}:K{lastDataRow})";
        ws.Cell(totalRow, 9).Style.NumberFormat.Format = "#,##0";
        ws.Cell(totalRow, 11).Style.NumberFormat.Format = "#,##0";

        // Generate filename: YYYY.MM.DD Quotation EVH-{customer}-{product}
        var now = DateTime.Now;
        var firstProductName = request.Items.First().ProductName;
        if (firstProductName.Length > 30) firstProductName = firstProductName[..30];
        var safeCustomer = SanitizeFileName(customer.CustomerName);
        var safeProduct = SanitizeFileName(firstProductName);
        var fileName = $"{now:yyyy.MM.dd} Quotation EVH-{safeCustomer}-{safeProduct}.xlsx";

        // Re-apply print area and page break preview — ClosedXML does not reliably
        // round-trip Print_Area (worksheet-scoped defined name) after InsertRowsBelow,
        // and always resets the sheetView back to "normal" on save.
        ws.PageSetup.PrintAreas.Clear();
        ws.PageSetup.PrintAreas.Add($"A1:{ws.Cell(totalRow+18, lastCol-13).Address}");
        ws.SheetView.View = XLSheetViewOptions.PageBreakPreview;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), fileName);
    }

    private void EmbedItemPicture(IXLWorksheet ws, string? imageUrl, int row, int col)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;
        var imgPath = ResolveImagePath(imageUrl);
        if (imgPath == null || !File.Exists(imgPath)) return;

        try
        {
            var pic = ws.AddPicture(imgPath);
            const int maxSize = 200; // larger than old 80px — fits the new template's taller rows / wider image columns
            double scaleW = (double)maxSize / pic.Width;
            double scaleH = (double)maxSize / pic.Height;
            double scale = Math.Min(scaleW, scaleH);
            int picW = (int)(pic.Width * scale);
            int picH = (int)(pic.Height * scale);

            // Compute centering offsets BEFORE anchoring.
            // Column width (Calibri 11) ≈ chars * 7 px + 5 px padding; row height in pts → px at 96 dpi.
            double cellWidthPx = ws.Column(col).Width * 7.0 + 5.0;
            double cellHeightPx = ws.Row(row).Height * 96.0 / 72.0;
            int xOffset = Math.Max(0, (int)((cellWidthPx - picW) / 2.0));
            int yOffset = Math.Max(0, (int)((cellHeightPx - picH) / 2.0));

            // Anchor first, then resize — ClosedXML drops the picture if WithSize runs before any MoveTo.
            pic.MoveTo(ws.Cell(row, col), xOffset, yOffset);
            pic.WithSize(picW, picH);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to embed image {Url} in Excel export", imageUrl);
        }
    }
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("", name.Where(c => !invalid.Contains(c))).Trim();
    }

    /// <summary>
    /// Tổng chi phí vận chuyển mua hàng (Vận chuyển tới EV) cho cột S, trả về dưới dạng
    /// công thức Excel để vừa hiển thị giá trị vừa cho thấy cách tính.
    /// Khớp với phần shippingVnd trong popup máy tính Giá nhập:
    ///   - UNOFFICIAL: Nội địa + (Phí/kg × Kg) + (Phí/CBM × CBM) + Xách tay + Kho→Kho
    ///   - OFFICIAL / DOMESTIC: Phí vận chuyển chính thức
    /// VD: Nội địa 5 + Phí/kg 5 × 1 kg → "=5 + 5 * 1" (giá trị 10).
    /// Trả về null khi không có chi phí nào (cột để 0).
    /// </summary>
    private static string? BuildShippingToEvFormula(QuotationItemFormModel item)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        static bool NonZero(decimal v) => v != 0m;
        string N(decimal v) => v.ToString(ci);

        if (item.PurchaseMode is "OFFICIAL" or "DOMESTIC")
        {
            var off = item.OfficialShipping ?? 0m;
            return NonZero(off) ? "=" + N(off) : null;
        }

        // UNOFFICIAL — gộp các thành phần khác 0 theo thứ tự của popup.
        var terms = new List<string>();

        var dom = item.UnofficialDomesticShipping ?? 0m;
        if (NonZero(dom)) terms.Add(N(dom));

        var costPerKg = item.UnofficialCostPerKg ?? 0m;
        var weight = item.UnofficialWeightKg ?? 0m;
        if (NonZero(costPerKg) && NonZero(weight)) terms.Add($"{N(costPerKg)} * {N(weight)}");

        var costPerCbm = item.UnofficialCostPerCbm ?? 0m;
        var cbm = (item.UnofficialLength ?? 0m) * (item.UnofficialWidth ?? 0m) * (item.UnofficialHeight ?? 0m);
        if (NonZero(costPerCbm) && NonZero(cbm)) terms.Add($"{N(costPerCbm)} * {N(cbm)}");

        var handCarry = item.UnofficialHandCarryFee ?? 0m;
        if (NonZero(handCarry)) terms.Add(N(handCarry));

        var w2w = item.UnofficialW2WShipping ?? 0m;
        if (NonZero(w2w)) terms.Add(N(w2w));

        return terms.Count > 0 ? "=" + string.Join(" + ", terms) : null;
    }

    /// <summary>
    /// Resolves an ImageUrl (e.g. /uploads/Quotation/Images/file.jpg) to a physical file path.
    /// </summary>
    private string? ResolveImagePath(string imageUrl)
    {
        // ImageUrl format: /uploads/Module/SubFolder/file.ext → stored at _storageRoot/Module/SubFolder/file.ext
        if (imageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = imageUrl["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_storageRoot, relativePath);
            if (File.Exists(fullPath)) return fullPath;
        }

        // Fallback: try as wwwroot-relative path
        var wwwrootPath = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(wwwrootPath)) return wwwrootPath;

        return null;
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
                DefaultPurchasePrice = p.DefaultPurchasePrice,
                DefaultPurchaseCurrency = p.DefaultPurchaseCurrency,
                SourceUrl = p.SourceUrl
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

    /// <summary>
    /// MANAGER + SALES users for the list page's creator/assignee filter dropdowns.
    /// Excludes ADMIN deliberately — admins don't normally create or own quotations.
    /// </summary>
    private async Task<List<SalesPersonOptionViewModel>> GetFilterUserOptionsAsync()
    {
        return await _uow.Repository<User>().Query()
            .Include(u => u.Role)
            .Where(u => u.IsActive && !u.IsLocked &&
                        (u.Role.RoleCode == "MANAGER" || u.Role.RoleCode == "SALES"))
            .OrderBy(u => u.FullName)
            .Select(u => new SalesPersonOptionViewModel
            {
                UserId = u.UserId,
                UserCode = u.UserCode,
                FullName = u.FullName
            })
            .ToListAsync();
    }

    private async Task<List<CurrencyOptionViewModel>> GetCurrencyOptionsAsync()
    {
        return await _uow.Repository<Currency>().Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.CurrencyCode)
            .Select(c => new CurrencyOptionViewModel
            {
                CurrencyCode = c.CurrencyCode,
                CurrencyName = c.CurrencyName,
                Symbol = c.Symbol,
                DecimalPlaces = c.DecimalPlaces
            })
            .ToListAsync();
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

    public async Task<decimal> GetCurrencyRateToVndAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || string.Equals(code, "VND", StringComparison.OrdinalIgnoreCase))
            return 1m;

        var rate = await _uow.Repository<ExchangeRate>().Query()
            .Where(x => x.FromCurrency == code && x.ToCurrency == "VND")
            .OrderByDescending(x => x.EffectiveDate)
            .Select(x => (decimal?)x.Rate)
            .FirstOrDefaultAsync();

        return rate ?? 1m;
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

    // ── Import cell parsing ───────────────────────────
    // Distinguishes empty / valid number / non-numeric text / formula error so the
    // import can point the user at the exact cell instead of silently using 0.
    private enum ImportCellState { Empty, Ok, Invalid, FormulaError }

    private const decimal ImportMaxNumber = 999_999_999_999m;

    private static ImportCellState TryReadImportNumber(IXLCell cell, out decimal value, out string raw)
    {
        value = 0m;
        raw = string.Empty;

        XLCellValue v;
        try { v = cell.Value; }
        catch
        {
            raw = "không đọc được giá trị";
            return ImportCellState.Invalid;
        }

        if (v.IsBlank) return ImportCellState.Empty;
        if (v.IsError)
        {
            raw = v.ToString();
            return ImportCellState.FormulaError;
        }
        if (v.IsNumber)
        {
            var d = v.GetNumber();
            if (double.IsNaN(d) || double.IsInfinity(d) || Math.Abs(d) > (double)ImportMaxNumber)
            {
                raw = cell.GetString();
                return ImportCellState.Invalid;
            }
            value = (decimal)d;
            return ImportCellState.Ok;
        }

        raw = cell.GetString().Trim();
        if (raw.Length == 0) return ImportCellState.Empty;

        var s = raw.Replace(" ", "").Replace("%", "");
        // Vietnamese thousand separators typed as text: "1.000.000" → 1000000
        s = System.Text.RegularExpressions.Regex.IsMatch(s, @"^-?\d{1,3}(\.\d{3})+$")
            ? s.Replace(".", "")
            : s.Replace(",", "");
        return decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                   System.Globalization.CultureInfo.InvariantCulture, out value)
               && Math.Abs(value) <= ImportMaxNumber
            ? ImportCellState.Ok
            : ImportCellState.Invalid;
    }

    private static readonly Dictionary<int, string> ImportColumnNames = new()
    {
        [1] = "STT", [2] = "Ảnh yêu cầu", [3] = "Tên SP / Yêu cầu KH", [4] = "EVH's Proposal",
        [5] = "Hình ảnh", [6] = "ĐVT", [7] = "SL", [8] = "Đơn giá", [10] = "VAT",
        [12] = "Ghi chú", [14] = "Link/NCC", [15] = "Giá RMB", [17] = "Cân nặng (KG)",
        [20] = "Giá vốn final", [21] = "Vận chuyển khách", [22] = "Hệ số"
    };

    private static string ImportColumnLabel(int col)
    {
        var letter = XLHelper.GetColumnLetterFromNumber(col);
        return ImportColumnNames.TryGetValue(col, out var name) ? $"{letter} ({name})" : letter;
    }

    private static string ShortenForMessage(string s) =>
        s.Length > 40 ? s[..40] + "…" : s;

    private static string NormalizePurchaseMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return "OFFICIAL";
        var u = mode.Trim().ToUpperInvariant();
        if (u == "UNOFFICIAL") return "UNOFFICIAL";
        if (u == "DOMESTIC") return "DOMESTIC";
        return "OFFICIAL";
    }

    // ══════════════════════════════════════════════════
    // IMPORT FROM EXCEL
    // ══════════════════════════════════════════════════
    // All-or-nothing: every row is validated first; if any cell is wrong no quotation
    // is created and the full list of "Dòng X, cột Y: ..." errors is returned so the
    // user can fix the file and import again.
    private const int ImportMaxErrors = 200;

    public async Task<(bool Success, string? Message, int? QuotationId, List<string>? Errors)> ImportFromExcelAsync(
        IFormFile file, int customerId, int salesPersonId, DateTime? deadline, int createdBy, int? rfqId = null)
    {
        if (file == null || file.Length == 0)
            return (false, "File không hợp lệ hoặc rỗng", null, null);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".xlsx" or ".xlsm"))
            return (false, $"Định dạng file \"{ext}\" không được hỗ trợ. Vui lòng lưu file dưới dạng Excel .xlsx (File → Save As → Excel Workbook) rồi import lại.", null, null);

        // Validate customer
        var customer = await _uow.Repository<Customer>().GetByIdAsync(customerId);
        if (customer == null)
            return (false, "Khách hàng không tồn tại", null, null);

        if (!deadline.HasValue)
            return (false, "Hạn xử lý nội bộ là bắt buộc", null, null);

        using var stream = file.OpenReadStream();
        XLWorkbook loaded;
        try
        {
            loaded = new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quotation import: cannot open file {File}", file.FileName);
            return (false, "Không đọc được file Excel. File có thể bị hỏng, đang đặt mật khẩu hoặc không đúng định dạng .xlsx. Vui lòng mở file bằng Excel, lưu lại rồi import lại.", null, null);
        }
        using var wb = loaded;

        var ws = wb.Worksheets.FirstOrDefault();
        if (ws == null)
            return (false, "File không có sheet nào", null, null);

        // Detect data rows: scan from row 2 downward looking for the first row
        // that has a numeric STT in column A (skip header/customer info rows)
        int dataStartRow = 0;
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= lastRow; r++)
        {
            var cellA = ws.Cell(r, 1).GetString().Trim();
            // Header row detection: "STT" / "STT/ No." / "No" → data starts next row
            if (cellA.StartsWith("STT", StringComparison.OrdinalIgnoreCase) ||
                cellA.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                cellA.Equals("No.", StringComparison.OrdinalIgnoreCase))
            {
                dataStartRow = r + 1;
                break;
            }
        }

        if (dataStartRow == 0)
        {
            // Fallback: look for first row with numeric col A
            for (int r = 2; r <= lastRow; r++)
            {
                if (int.TryParse(ws.Cell(r, 1).GetString().Trim(), out _))
                {
                    dataStartRow = r;
                    break;
                }
            }
        }

        if (dataStartRow == 0)
            return (false, $"Không tìm thấy bảng sản phẩm trong sheet \"{ws.Name}\". " +
                "Cột A phải có dòng tiêu đề \"STT\" và các dòng sản phẩm bên dưới (giống file mẫu). " +
                "Vui lòng dùng nút \"Tải file mẫu\" để lấy đúng định dạng.", null, null);

        // Map embedded images to their anchor row, split by column band so the two
        // image columns of the new template are kept apart:
        //   col B (2) = customer's required image  → RequiredImageUrl
        //   col E (5) = EVH product image          → ImageUrl
        // First picture per row per band wins. Pictures anchored in cols 1-3 are
        // treated as the request image; cols 4+ as the product image.
        var requiredPicByRow = new Dictionary<int, IXLPicture>();
        var productPicByRow = new Dictionary<int, IXLPicture>();
        foreach (var pic in ws.Pictures)
        {
            var topLeft = pic.TopLeftCell;
            if (topLeft == null) continue;
            var anchorRow = topLeft.Address.RowNumber;
            if (anchorRow < dataStartRow) continue;
            var anchorCol = topLeft.Address.ColumnNumber;
            if (anchorCol <= 3)
            {
                if (!requiredPicByRow.ContainsKey(anchorRow))
                    requiredPicByRow[anchorRow] = pic;
            }
            else
            {
                if (!productPicByRow.ContainsKey(anchorRow))
                    productPicByRow[anchorRow] = pic;
            }
        }

        // Parse + validate items (pictures are saved only after the whole file is valid,
        // so a rejected import leaves no orphan image files behind)
        var items = new List<(QuotationItem Item, int Row)>();
        var errors = new List<string>();
        int sortOrder = 0;

        void AddError(int row, int col, string msg) =>
            errors.Add($"Dòng {row}, cột {ImportColumnLabel(col)}: {msg}");

        void CheckLength(int row, int col, string? value, int max, string what)
        {
            if (value != null && value.Length > max)
                AddError(row, col, $"{what} dài {value.Length} ký tự, vượt quá giới hạn {max} ký tự");
        }

        // Reads a non-negative number; records an error and returns null when the cell is wrong.
        decimal? ReadNumber(int row, int col, bool required)
        {
            var state = TryReadImportNumber(ws.Cell(row, col), out var value, out var raw);
            switch (state)
            {
                case ImportCellState.Empty:
                    if (required) AddError(row, col, "không được để trống");
                    return null;
                case ImportCellState.FormulaError:
                    AddError(row, col, $"công thức đang bị lỗi ({raw}), kiểm tra lại các ô mà công thức tham chiếu tới");
                    return null;
                case ImportCellState.Invalid:
                    AddError(row, col, $"giá trị \"{ShortenForMessage(raw)}\" không phải là số hợp lệ");
                    return null;
            }
            if (value < 0)
            {
                AddError(row, col, $"không được là số âm ({value:0.##})");
                return null;
            }
            return value;
        }

        for (int r = dataStartRow; r <= lastRow; r++)
        {
            var sttText = ws.Cell(r, 1).GetString().Trim();
            // C = product name (+ optional required description after a blank line)
            var nameBlock = ws.Cell(r, 3).GetString().Replace("\r\n", "\n").Trim();

            // Stop at total row or empty
            if (string.IsNullOrEmpty(nameBlock) && string.IsNullOrEmpty(sttText))
                continue;
            // Normalize Vietnamese diacritics for TỔNG detection
            var sttNorm = sttText.Normalize(System.Text.NormalizationForm.FormD);
            var nameNorm = nameBlock.Normalize(System.Text.NormalizationForm.FormD);
            if (sttNorm.Contains("TONG", StringComparison.OrdinalIgnoreCase) ||
                nameNorm.Contains("TONG", StringComparison.OrdinalIgnoreCase) ||
                sttText.Contains("TỔNG", StringComparison.OrdinalIgnoreCase) ||
                nameBlock.Contains("TỔNG", StringComparison.OrdinalIgnoreCase))
                break;
            if (!int.TryParse(sttText, out _) && string.IsNullOrEmpty(nameBlock))
                continue;

            if (errors.Count >= ImportMaxErrors) break;

            int errorsBefore = errors.Count;

            if (string.IsNullOrEmpty(nameBlock))
                AddError(r, 3, "tên sản phẩm không được để trống");

            // C column mirrors the export's block: "ProductName\n\nRequiredDescription".
            // Split on the first blank line; everything after is the required description.
            string productName;
            string? requiredDescription = null;
            var sepIdx = nameBlock.IndexOf("\n\n", StringComparison.Ordinal);
            if (sepIdx >= 0)
            {
                productName = nameBlock[..sepIdx].Trim();
                var rest = nameBlock[(sepIdx + 2)..].Trim();
                requiredDescription = string.IsNullOrEmpty(rest) ? null : rest;
            }
            else
            {
                productName = nameBlock;
            }
            CheckLength(r, 3, productName, 300, "Tên sản phẩm (phần trước dòng trống)");
            CheckLength(r, 3, requiredDescription, 2000, "Mô tả yêu cầu (phần sau dòng trống)");

            // D — EVH proposal / product description
            var proposal = ws.Cell(r, 4).GetString().Trim();

            // F — unit
            var unitName = ws.Cell(r, 6).GetString().Trim();
            if (string.IsNullOrEmpty(unitName)) unitName = "Cái";
            CheckLength(r, 6, unitName, 50, "Đơn vị tính");

            // G — quantity (required, > 0)
            var qty = ReadNumber(r, 7, required: true);
            if (qty == 0)
            {
                AddError(r, 7, "số lượng phải lớn hơn 0");
                qty = null;
            }

            // H — unit price (empty = 0)
            var unitPrice = ReadNumber(r, 8, required: false) ?? 0;

            // J — VAT. Exported as a fraction (0.1) with "0%" format; users may also
            // type "8", "8%" or "10". Treat values in (0,1) as fractions. Empty = 10%.
            var vatRead = ReadNumber(r, 10, required: false);
            decimal vatRate = 10;
            if (vatRead.HasValue)
            {
                vatRate = vatRead.Value > 0 && vatRead.Value < 1 ? vatRead.Value * 100 : vatRead.Value;
                if (vatRate > 100)
                    AddError(r, 10, $"thuế VAT {vatRate:0.##}% không hợp lệ (chỉ từ 0% đến 100%)");
            }

            // L — notes
            var notes = ws.Cell(r, 12).GetString().Trim();
            CheckLength(r, 12, notes, 500, "Ghi chú");

            // N — Link/NCC: a URL goes to SourceUrl, otherwise a supplier name to SourceName
            var supplierRaw = ws.Cell(r, 14).GetString().Trim();
            string? supplier = null;
            string? sourceUrl = null;
            if (supplierRaw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                supplierRaw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                sourceUrl = supplierRaw;
                CheckLength(r, 14, sourceUrl, 1000, "Link nguồn");
            }
            else if (!string.IsNullOrEmpty(supplierRaw))
            {
                supplier = supplierRaw;
                CheckLength(r, 14, supplier, 200, "Tên nhà cung cấp");
            }

            // O — Giá RMB (base purchase price, in the purchase currency)
            var basePrice = ReadNumber(r, 15, required: false) ?? 0;
            // Q — Cân nặng (KG)
            var weightKg = ReadNumber(r, 17, required: false) ?? 0;
            // T — Giá vốn final (final cost price in VND)
            var importPrice = ReadNumber(r, 20, required: false) ?? 0;
            // U — Vận chuyển khách (customer shipping fee)
            var shipping = ReadNumber(r, 21, required: false) ?? 0;
            // V — Hệ số (coefficient); empty = 1, explicit 0 would divide by zero
            var coefficientRead = ReadNumber(r, 22, required: false);
            decimal coefficient = 1;
            if (coefficientRead.HasValue)
            {
                if (coefficientRead.Value == 0)
                    AddError(r, 22, "hệ số phải lớn hơn 0 (để trống nếu muốn dùng hệ số 1)");
                else if (coefficientRead.Value > 999_999)
                    AddError(r, 22, $"hệ số {coefficientRead.Value:0.##} quá lớn");
                else
                    coefficient = coefficientRead.Value;
            }

            // Row has errors → keep validating the rest of the file, but don't build the item
            if (errors.Count > errorsBefore || qty == null)
                continue;

            // Calculate line totals
            var lineTotal = qty.Value * unitPrice;
            var taxAmount = Math.Round(lineTotal * vatRate / 100m, 0);
            var lineTotalWithTax = lineTotal + taxAmount;

            items.Add((new QuotationItem
            {
                ProductName = productName,
                ProductDescription = string.IsNullOrEmpty(proposal) ? null : proposal,
                RequiredDescription = requiredDescription,
                UnitName = unitName,
                Quantity = qty.Value,
                UnitPrice = unitPrice,
                BasePrice = basePrice > 0 ? basePrice : null,
                UnofficialWeightKg = weightKg > 0 ? weightKg : null,
                PurchasePrice = importPrice > 0 ? importPrice : null,
                ShippingFee = shipping > 0 ? shipping : null,
                Coefficient = coefficient,
                LineTotal = lineTotal,
                TaxRate = vatRate,
                TaxAmount = taxAmount,
                LineTotalWithTax = lineTotalWithTax,
                SourceName = supplier,
                SourceUrl = sourceUrl,
                Notes = string.IsNullOrEmpty(notes) ? null : notes,
                SortOrder = sortOrder++,
                IsProductMapped = false
            }, r));
        }

        if (errors.Count > 0)
        {
            var msg = errors.Count >= ImportMaxErrors
                ? $"File có quá nhiều lỗi (hiển thị {ImportMaxErrors} lỗi đầu tiên)."
                : $"File có {errors.Count} lỗi.";
            msg += " Chưa có báo giá nào được tạo — vui lòng sửa file theo danh sách dưới đây rồi import lại.";
            return (false, msg, null, errors);
        }

        if (items.Count == 0)
            return (false, $"Không tìm thấy dòng sản phẩm nào bên dưới dòng tiêu đề (từ dòng {dataStartRow} trở đi). " +
                "Mỗi sản phẩm cần có STT ở cột A và tên sản phẩm ở cột C.", null, null);

        // File is valid → extract embedded images for each row (B = required image, E = product image)
        foreach (var (item, row) in items)
        {
            if (requiredPicByRow.TryGetValue(row, out var reqPic))
                item.RequiredImageUrl = await SaveImportedItemPictureAsync(reqPic);
            if (productPicByRow.TryGetValue(row, out var prodPic))
                item.ImageUrl = await SaveImportedItemPictureAsync(prodPic);
        }

        // Create quotation
        var code = await GenerateQuotationNoAsync();
        decimal subTotal = items.Sum(i => i.Item.LineTotal);
        decimal totalTax = items.Sum(i => i.Item.TaxAmount ?? 0);

        var quotation = new Quotation
        {
            QuotationNo = code,
            RfqId = rfqId,
            CustomerId = customerId,
            QuotationDate = DateTime.Today,
            Deadline = deadline.Value,
            Status = "DRAFT",
            SalesPersonId = salesPersonId,
            TaxRate = items[0].Item.TaxRate ?? 10,
            SubTotal = subTotal,
            TaxAmount = totalTax,
            TotalAmount = subTotal + totalTax,
            CreatedBy = createdBy,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        foreach (var (item, _) in items)
            quotation.Items.Add(item);

        await _uow.Repository<Quotation>().AddAsync(quotation);
        await _uow.SaveChangesAsync();

        // SLA: start tracking
        await _slaService.StartTrackingAsync("QUOTATION", quotation.QuotationId, "DRAFT", quotation.SalesPersonId);

        _logger.LogInformation("Quotation imported from Excel: {No}, {Count} items, by UserId={UserId}",
            code, items.Count, createdBy);

        return (true, $"Đã tạo báo giá {code} với {items.Count} sản phẩm.", quotation.QuotationId, null);
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

    // Saves an embedded image extracted from an imported quotation Excel file.
    private async Task<string?> SaveImportedItemPictureAsync(IXLPicture pic)
    {
        try
        {
            var ext = pic.Format switch
            {
                XLPictureFormat.Png => ".png",
                XLPictureFormat.Jpeg => ".jpg",
                XLPictureFormat.Gif => ".gif",
                XLPictureFormat.Bmp => ".bmp",
                XLPictureFormat.Tiff => ".tif",
                XLPictureFormat.Webp => ".webp",
                _ => ".png"
            };

            var dir = Path.Combine(_storageRoot, "Quotation", "Images");
            Directory.CreateDirectory(dir);

            var fileName = $"quot-imp-{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(dir, fileName);

            var src = pic.ImageStream;
            src.Position = 0;
            await using var fs = new FileStream(filePath, FileMode.Create);
            await src.CopyToAsync(fs);

            return $"/uploads/Quotation/Images/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract embedded image from imported quotation");
            return null;
        }
    }
}
