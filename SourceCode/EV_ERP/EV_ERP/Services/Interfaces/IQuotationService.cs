using EV_ERP.Models.ViewModels.Quotations;

namespace EV_ERP.Services.Interfaces;

public interface IQuotationService
{
    // ── List (paginated) ─────────────────────────────
    Task<QuotationListViewModel> GetListAsync(
        string? keyword, string? status, int? customerId, int? salesPersonId, int? createdBy,
        int pageIndex = 1, int pageSize = 20);

    // ── Form ─────────────────────────────────────────
    Task<QuotationFormViewModel> GetFormAsync(int? quotationId = null, int? rfqId = null);

    // ── CRUD ─────────────────────────────────────────
    Task<(bool Success, string? ErrorMessage, int? QuotationId)> CreateAsync(
        QuotationFormViewModel model, int createdBy);

    Task<(bool Success, string? ErrorMessage)> UpdateAsync(
        QuotationFormViewModel model, int updatedBy);

    // ── Detail ───────────────────────────────────────
    Task<QuotationDetailViewModel?> GetDetailAsync(int quotationId);

    // ── Lightweight assignee lookup (for authz checks) ───
    Task<int?> GetSalesPersonIdAsync(int quotationId);

    // ── Status transitions ───────────────────────────
    Task<(bool Success, string? ErrorMessage)> SendAsync(int quotationId, int userId);
    Task<(bool Success, string? ErrorMessage)> ApproveAsync(int quotationId, int userId);
    Task<(bool Success, string? ErrorMessage)> RejectAsync(int quotationId, int userId, string? reason);
    Task<(bool Success, string? ErrorMessage, int? NewQuotationId)> AmendAsync(int quotationId, int userId);
    Task<(bool Success, string? ErrorMessage)> ExpireAsync(int quotationId, int userId);
    Task<(bool Success, string? ErrorMessage)> CancelAsync(int quotationId, int userId, string? reason);

    // ── Import from Excel ─────────────────────────────
    Task<(bool Success, string? ErrorMessage, int? QuotationId)> ImportFromExcelAsync(
        IFormFile file, int customerId, int salesPersonId, DateTime? deadline, int createdBy, int? rfqId = null);

    // ── Export Excel ───────────────────────────────────
    Task<(byte[] FileBytes, string FileName)?> ExportExcelAsync(QuotationExportRequest request);
    Task<(byte[] FileBytes, string FileName)?> ExportExcelByIdAsync(int quotationId);

    // ── Product search (Ajax) ────────────────────────
    Task<List<ProductSearchResult>> SearchProductsAsync(string keyword, int maxResults = 10);

    // ── Image upload (for ad-hoc items) ─────────────
    Task<string?> UploadItemImageAsync(IFormFile file);
}
