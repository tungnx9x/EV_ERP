using EV_ERP.Models.ViewModels.Reports;

namespace EV_ERP.Services.Interfaces;

public interface IReportService
{
    Task<SalesRevenueReportViewModel> GetSalesRevenueAsync(SalesRevenueFilterViewModel filter);
    Task<(byte[] FileBytes, string FileName)?> ExportSalesRevenueExcelAsync(SalesRevenueFilterViewModel filter);
}
