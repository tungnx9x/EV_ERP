using EV_ERP.Models.ViewModels.Reports;

namespace EV_ERP.Services.Interfaces;

public interface IReportService
{
    Task<SalesRevenueReportViewModel> GetSalesRevenueAsync(SalesRevenueFilterViewModel filter);
    Task<(byte[] FileBytes, string FileName)?> ExportSalesRevenueExcelAsync(SalesRevenueFilterViewModel filter);

    Task<SalesResultReportViewModel> GetSalesResultAsync(SalesResultFilterViewModel filter, int userId);
    Task<(byte[] FileBytes, string FileName)?> ExportSalesResultExcelAsync(SalesResultFilterViewModel filter, int userId);
}
