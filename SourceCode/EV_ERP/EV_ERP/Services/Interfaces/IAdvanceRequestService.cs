using EV_ERP.Models.ViewModels.Finance;

namespace EV_ERP.Services.Interfaces;

public interface IAdvanceRequestService
{
    Task<SalesOrderAdvanceSummary> GetForSalesOrderAsync(int salesOrderId);

    /// <summary>Sum of AdvanceRequestItem.Amount per SOItemId for the given SO (only non-rejected requests).</summary>
    Task<Dictionary<int, decimal>> GetAdvancedByItemAsync(int salesOrderId);

    Task<(bool Success, string? ErrorMessage, int? AdvanceRequestId)> CreateAsync(
        int salesOrderId, AdvanceRequestCreateModel model, int userId);

    Task<(bool Success, string? ErrorMessage)> DeleteAsync(int advanceRequestId, int userId);
}
