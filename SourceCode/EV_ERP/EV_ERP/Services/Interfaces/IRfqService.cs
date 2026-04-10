using EV_ERP.Models.ViewModels.RFQs;

namespace EV_ERP.Services.Interfaces;

public interface IRfqService
{
    Task<RfqListViewModel> GetListAsync(
        string? keyword, string? status, string? priority,
        int? assignedTo, int? customerId,
        int pageIndex = 1, int pageSize = 20);

    Task<RfqFormViewModel> GetFormAsync(int? rfqId = null);

    Task<(bool Success, string? ErrorMessage, int? RfqId)> CreateAsync(
        RfqFormViewModel model, int createdBy);

    Task<(bool Success, string? ErrorMessage)> UpdateAsync(
        RfqFormViewModel model, int updatedBy);

    Task<RfqDetailViewModel?> GetDetailAsync(int rfqId);

    Task<(bool Success, string? ErrorMessage)> CancelAsync(int rfqId, int userId, string? reason);

    Task<string?> UploadImageAsync(IFormFile file);
}
