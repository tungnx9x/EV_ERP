using EV_ERP.Models.ViewModels.Reference;

namespace EV_ERP.Services.Interfaces
{
    public interface ICurrencyService
    {
        Task<CurrencyListViewModel> GetListAsync(string? keyword, string? status, int pageIndex = 1, int pageSize = 20);
        Task<CurrencyFormViewModel?> GetFormAsync(string code);
        Task<(bool Success, string? ErrorMessage)> CreateAsync(CurrencyFormViewModel model);
        Task<(bool Success, string? ErrorMessage)> UpdateAsync(CurrencyFormViewModel model);
        Task<(bool Success, string? ErrorMessage)> DeleteAsync(string code);
    }
}
