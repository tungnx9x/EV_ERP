using EV_ERP.Models.ViewModels.Customers;

namespace EV_ERP.Services.Interfaces
{
    public interface ICustomerGroupService
    {
        Task<CustomerGroupListViewModel> GetListAsync(string? keyword, string? status);
        Task<CustomerGroupFormViewModel> GetFormAsync(int? customerGroupId = null);
        Task<(bool Success, string? ErrorMessage)> CreateAsync(CustomerGroupFormViewModel model);
        Task<(bool Success, string? ErrorMessage)> UpdateAsync(CustomerGroupFormViewModel model);
        Task<(bool Success, string? ErrorMessage)> DeleteAsync(int customerGroupId);
    }
}
