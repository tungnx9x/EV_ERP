using EV_ERP.Models.ViewModels.Vendors;

namespace EV_ERP.Services.Interfaces
{
    public interface IVendorService
    {
        Task<VendorListViewModel> GetListAsync(string? keyword, string? status);
        Task<VendorFormViewModel> GetFormAsync(int? vendorId = null);
        Task<(bool Success, string? ErrorMessage)> CreateAsync(VendorFormViewModel model, int createdBy);
        Task<(bool Success, string? ErrorMessage)> UpdateAsync(VendorFormViewModel model, int updatedBy);
        Task<(bool Success, string? ErrorMessage)> ToggleActiveAsync(int vendorId, int updatedBy);
        Task<VendorDetailViewModel?> GetDetailAsync(int vendorId);

        // Contacts
        Task<(bool Success, string? ErrorMessage)> SaveContactAsync(VendorContactFormModel model);
        Task<(bool Success, string? ErrorMessage)> DeleteContactAsync(int contactId);
    }
}
