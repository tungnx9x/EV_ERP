using EV_ERP.Models.ViewModels.Customers;

namespace EV_ERP.Services.Interfaces
{
    public interface ICustomerService
    {
        Task<CustomerListViewModel> GetListAsync(string? keyword, int? groupId, string? status);
        Task<CustomerFormViewModel> GetFormAsync(int? customerId = null);
        Task<(bool Success, string? ErrorMessage)> CreateAsync(CustomerFormViewModel model, int createdBy);
        Task<(bool Success, string? ErrorMessage)> UpdateAsync(CustomerFormViewModel model, int updatedBy);
        Task<(bool Success, string? ErrorMessage)> ToggleActiveAsync(int customerId, int updatedBy);
        Task<CustomerDetailViewModel?> GetDetailAsync(int customerId);

        // Import
        Task<CustomerImportResult> ImportFromExcelAsync(Microsoft.AspNetCore.Http.IFormFile file, int createdBy);
        byte[] BuildImportTemplate();

        // Contacts
        Task<(bool Success, string? ErrorMessage)> SaveContactAsync(ContactFormModel model);
        Task<(bool Success, string? ErrorMessage)> DeleteContactAsync(int contactId);

        // Notes
        Task<(bool Success, string? ErrorMessage, NoteViewModel? Note)> AddNoteAsync(
            int customerId, string content, int userId);
    }
}
