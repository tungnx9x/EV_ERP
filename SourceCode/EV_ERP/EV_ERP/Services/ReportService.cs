using ClosedXML.Excel;
using EV_ERP.Models.Entities.Customers;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.ViewModels.Quotations;
using EV_ERP.Models.ViewModels.Reports;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;
    private static readonly string[] RevenueStatuses = ["DELIVERED", "COMPLETED", "REPORTED"];

    public ReportService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<SalesRevenueReportViewModel> GetSalesRevenueAsync(SalesRevenueFilterViewModel filter)
    {
        var query = BuildRevenueQuery(filter);

        var rows = await query
            .OrderByDescending(so => so.OrderDate)
            .Select(so => new SalesRevenueRowViewModel
            {
                SalesOrderId = so.SalesOrderId,
                SalesOrderNo = so.SalesOrderNo,
                CustomerName = so.Customer.CustomerName,
                SalesPersonName = so.SalesPerson.FullName,
                OrderDate = so.OrderDate,
                SubTotal = so.SubTotal,
                DiscountAmount = so.DiscountAmount,
                TaxAmount = so.TaxAmount,
                TotalAmount = so.TotalAmount,
                Status = so.Status
            })
            .ToListAsync();

        var totalRevenue = rows.Sum(r => r.TotalAmount);
        var totalOrders = rows.Count;
        var avgValue = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 0) : 0;

        // Chart: revenue grouped by month
        var chartGroups = rows
            .GroupBy(r => new { r.OrderDate.Year, r.OrderDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                Label = $"{g.Key.Month:D2}/{g.Key.Year}",
                Total = g.Sum(r => r.TotalAmount)
            })
            .ToList();

        return new SalesRevenueReportViewModel
        {
            Filter = filter,
            Customers = await GetCustomerOptionsAsync(),
            SalesPersons = await GetSalesPersonOptionsAsync(),
            TotalRevenue = totalRevenue,
            TotalOrders = totalOrders,
            AverageOrderValue = avgValue,
            ChartLabels = chartGroups.Select(c => c.Label).ToList(),
            ChartData = chartGroups.Select(c => c.Total).ToList(),
            Rows = rows
        };
    }

    public async Task<(byte[] FileBytes, string FileName)?> ExportSalesRevenueExcelAsync(SalesRevenueFilterViewModel filter)
    {
        var query = BuildRevenueQuery(filter);

        var rows = await query
            .OrderByDescending(so => so.OrderDate)
            .Select(so => new SalesRevenueRowViewModel
            {
                SalesOrderId = so.SalesOrderId,
                SalesOrderNo = so.SalesOrderNo,
                CustomerName = so.Customer.CustomerName,
                SalesPersonName = so.SalesPerson.FullName,
                OrderDate = so.OrderDate,
                SubTotal = so.SubTotal,
                DiscountAmount = so.DiscountAmount,
                TaxAmount = so.TaxAmount,
                TotalAmount = so.TotalAmount,
                Status = so.Status
            })
            .ToListAsync();

        if (rows.Count == 0) return null;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Doanh thu");

        // Header
        ws.Cell(1, 1).Value = "BÁO CÁO DOANH THU";
        ws.Range(1, 1, 1, 8).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Filter info
        var filterText = "Khoảng thời gian: ";
        if (filter.DateFrom.HasValue) filterText += filter.DateFrom.Value.ToString("dd/MM/yyyy");
        filterText += " - ";
        if (filter.DateTo.HasValue) filterText += filter.DateTo.Value.ToString("dd/MM/yyyy");
        ws.Cell(2, 1).Value = filterText;
        ws.Range(2, 1, 2, 8).Merge();
        ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Column headers
        int headerRow = 4;
        string[] headers = ["STT", "Mã đơn hàng", "Khách hàng", "Ngày đặt", "Doanh thu trước thuế", "Chiết khấu", "Thuế", "Tổng doanh thu"];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563eb");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Data rows
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            int row = headerRow + 1 + i;
            ws.Cell(row, 1).Value = i + 1;
            ws.Cell(row, 2).Value = r.SalesOrderNo;
            ws.Cell(row, 3).Value = r.CustomerName;
            ws.Cell(row, 4).Value = r.OrderDate.ToString("dd/MM/yyyy");
            ws.Cell(row, 5).Value = r.SubTotal;
            ws.Cell(row, 6).Value = r.DiscountAmount;
            ws.Cell(row, 7).Value = r.TaxAmount;
            ws.Cell(row, 8).Value = r.TotalAmount;

            // Number format
            for (int c = 5; c <= 8; c++)
            {
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            for (int c = 1; c <= 4; c++)
                ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Totals row
        int totalRow = headerRow + 1 + rows.Count;
        ws.Cell(totalRow, 1).Value = "";
        ws.Range(totalRow, 1, totalRow, 3).Merge();
        ws.Cell(totalRow, 1).Value = "TỔNG CỘNG";
        ws.Cell(totalRow, 1).Style.Font.Bold = true;
        ws.Cell(totalRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(totalRow, 5).Value = rows.Sum(r => r.SubTotal);
        ws.Cell(totalRow, 6).Value = rows.Sum(r => r.DiscountAmount);
        ws.Cell(totalRow, 7).Value = rows.Sum(r => r.TaxAmount);
        ws.Cell(totalRow, 8).Value = rows.Sum(r => r.TotalAmount);
        for (int c = 5; c <= 8; c++)
        {
            ws.Cell(totalRow, c).Style.NumberFormat.Format = "#,##0";
            ws.Cell(totalRow, c).Style.Font.Bold = true;
            ws.Cell(totalRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var fileName = $"DoanhThu_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return (ms.ToArray(), fileName);
    }

    // ── Private helpers ──────────────────────────────────

    private IQueryable<SalesOrder> BuildRevenueQuery(SalesRevenueFilterViewModel filter)
    {
        var query = _uow.Repository<SalesOrder>().Query()
            .Include(so => so.Customer)
            .Include(so => so.SalesPerson)
            .Where(so => RevenueStatuses.Contains(so.Status));

        if (filter.DateFrom.HasValue)
            query = query.Where(so => so.OrderDate >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(so => so.OrderDate <= filter.DateTo.Value);

        if (filter.CustomerId.HasValue && filter.CustomerId > 0)
            query = query.Where(so => so.CustomerId == filter.CustomerId.Value);

        if (filter.SalesPersonId.HasValue && filter.SalesPersonId > 0)
            query = query.Where(so => so.SalesPersonId == filter.SalesPersonId.Value);

        return query;
    }

    private async Task<List<CustomerOptionViewModel>> GetCustomerOptionsAsync()
    {
        return await _uow.Repository<Customer>().Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.CustomerName)
            .Select(c => new CustomerOptionViewModel
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName
            })
            .ToListAsync();
    }

    private async Task<List<SalesPersonOptionViewModel>> GetSalesPersonOptionsAsync()
    {
        return await _uow.Repository<User>().Query()
            .Where(u => u.IsActive && !u.IsLocked &&
                        (u.Role.RoleCode == "SALES" || u.Role.RoleCode == "MANAGER" || u.Role.RoleCode == "ADMIN"))
            .Include(u => u.Role)
            .OrderBy(u => u.FullName)
            .Select(u => new SalesPersonOptionViewModel
            {
                UserId = u.UserId,
                UserCode = u.UserCode,
                FullName = u.FullName
            })
            .ToListAsync();
    }
}
