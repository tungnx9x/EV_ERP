using EV_ERP.Models.ViewModels.Finance;

namespace EV_ERP.Services.Interfaces;

public interface IAdvanceRequestService
{
    Task<SalesOrderAdvanceSummary> GetForSalesOrderAsync(int salesOrderId);

    /// <summary>Sum of AdvanceRequestItem.Amount per SOItemId for the given SO (only non-rejected requests).</summary>
    Task<Dictionary<int, decimal>> GetAdvancedByItemAsync(int salesOrderId);

    Task<(bool Success, string? ErrorMessage, int? AdvanceRequestId)> CreateAsync(
        int salesOrderId, AdvanceRequestCreateModel model, int userId);

    /// <summary>Load a single advance request shaped for the edit modal. Returns null if not found.</summary>
    Task<AdvanceRequestRow?> GetForEditAsync(int advanceRequestId);

    /// <summary>Update an existing advance request — only allowed while status is PENDING.</summary>
    Task<(bool Success, string? ErrorMessage)> UpdateAsync(
        int advanceRequestId, AdvanceRequestCreateModel model, int userId);

    Task<(bool Success, string? ErrorMessage)> DeleteAsync(int advanceRequestId, int userId);

    /// <summary>Count of non-REJECTED advance requests for the SO — used by SO submit-wait validation.</summary>
    Task<int> CountActiveAsync(int salesOrderId);

    Task<(bool Success, string? ErrorMessage)> ApproveAsync(int advanceRequestId, int userId);
    Task<(bool Success, string? ErrorMessage)> MarkReceivedAsync(int advanceRequestId, int userId);
    Task<(bool Success, string? ErrorMessage)> RejectAsync(int advanceRequestId, string? reason, int userId);

    /// <summary>
    /// Export DNTU file for a SINGLE advance request. All SO items appear as rows;
    /// items not allocated in this request have qty kept but all money cells = 0.
    /// </summary>
    Task<(byte[] FileBytes, string FileName)?> ExportDntuAsync(int advanceRequestId, int userId);
}
