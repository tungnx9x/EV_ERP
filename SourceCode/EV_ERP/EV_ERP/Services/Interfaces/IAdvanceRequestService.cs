using EV_ERP.Models.ViewModels.Finance;

namespace EV_ERP.Services.Interfaces;

public interface IAdvanceRequestService
{
    Task<SalesOrderAdvanceSummary> GetForSalesOrderAsync(int salesOrderId);

    /// <summary>Sum of AdvanceRequestItem.Amount per SOItemId for the given SO (only non-rejected requests).</summary>
    Task<Dictionary<int, decimal>> GetAdvancedByItemAsync(int salesOrderId);

    /// <summary>Sum of AdvanceRequestItem.Amount per SOItemId split into (Product, Shipping, CustomerShipping) by Purpose ("Vận chuyển" / "Vận chuyển KH").</summary>
    Task<Dictionary<int, (decimal Product, decimal Shipping, decimal CustomerShipping)>> GetAdvancedByItemSplitAsync(int salesOrderId);

    Task<(bool Success, string? ErrorMessage, int? AdvanceRequestId)> CreateAsync(
        int salesOrderId, AdvanceRequestCreateModel model, int userId);

    /// <summary>Load a single advance request shaped for the edit modal. Returns null if not found.</summary>
    Task<AdvanceRequestRow?> GetForEditAsync(int advanceRequestId);

    /// <summary>Update an existing advance request — only allowed while status is WAIT_ACCOUNTANT.</summary>
    Task<(bool Success, string? ErrorMessage)> UpdateAsync(
        int advanceRequestId, AdvanceRequestCreateModel model, int userId);

    Task<(bool Success, string? ErrorMessage)> DeleteAsync(int advanceRequestId, int userId);

    /// <summary>Count of non-REJECTED advance requests for the SO — used by SO submit-wait validation.</summary>
    Task<int> CountActiveAsync(int salesOrderId);

    // ── Quy trình duyệt 4 bước ──
    /// <summary>Bước 1→2: Kế toán duyệt (WAIT_ACCOUNTANT → WAIT_DIRECTOR).</summary>
    Task<(bool Success, string? ErrorMessage)> AccountantReviewAsync(int advanceRequestId, int userId);
    /// <summary>Bước 2→3: Giám đốc/Quản lý duyệt (WAIT_DIRECTOR → WAIT_DISBURSE).</summary>
    Task<(bool Success, string? ErrorMessage)> DirectorApproveAsync(int advanceRequestId, int userId);
    /// <summary>Bước 3→4: Kế toán xác nhận chi tiền (WAIT_DISBURSE → DISBURSED).</summary>
    Task<(bool Success, string? ErrorMessage)> DisburseAsync(int advanceRequestId, int userId);
    Task<(bool Success, string? ErrorMessage)> RejectAsync(int advanceRequestId, string? reason, int userId);

    /// <summary>
    /// Export DNTU file for a SINGLE advance request. All SO items appear as rows;
    /// items not allocated in this request have qty kept but all money cells = 0.
    /// </summary>
    Task<(byte[] FileBytes, string FileName)?> ExportDntuAsync(int advanceRequestId, int userId);
}
