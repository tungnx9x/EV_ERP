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
    private readonly IWebHostEnvironment _env;
    private static readonly string[] RevenueStatuses = ["DELIVERED", "COMPLETED", "REPORTED"];
    // BCKQKD = "Báo cáo kết quả kinh doanh" — counts SOs that finished the sales cycle.
    private static readonly string[] SalesResultStatuses = ["COMPLETED", "REPORTED"];

    public ReportService(IUnitOfWork uow, IWebHostEnvironment env)
    {
        _uow = uow;
        _env = env;
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

    // ══════════════════════════════════════════════════════
    // SALES RESULT (BCKQKD) — per current sales user
    // ══════════════════════════════════════════════════════

    public async Task<SalesResultReportViewModel> GetSalesResultAsync(
        SalesResultFilterViewModel filter, int userId)
    {
        var rows = await BuildSalesResultRowsAsync(filter, userId);
        var user = await _uow.Repository<User>().GetByIdAsync(userId);

        return new SalesResultReportViewModel
        {
            Filter = filter,
            UserFullName = user?.FullName ?? "",
            Rows = rows,
            TotalSubTotal = rows.Sum(r => r.SubTotal),
            TotalTaxAmount = rows.Sum(r => r.TaxAmount),
            TotalAmount = rows.Sum(r => r.TotalAmount),
            TotalPurchaseCost = rows.Sum(r => r.PurchaseCost),
            TotalShippingFee = rows.Sum(r => r.ShippingFee),
            TotalUnofficialW2WShipping = rows.Sum(r => r.UnofficialW2WShipping)
        };
    }

    public async Task<(byte[] FileBytes, string FileName)?> ExportSalesResultExcelAsync(
        SalesResultFilterViewModel filter, int userId)
    {
        var rows = await BuildSalesResultRowsAsync(filter, userId);
        if (rows.Count == 0) return null;

        var user = await _uow.Repository<User>().GetByIdAsync(userId);
        var userName = user?.FullName ?? "";

        var templatePath = Path.Combine(_env.WebRootPath, "templates", "BCKQKD-template.xlsx");
        if (!File.Exists(templatePath)) return null;

        // Reporting period — prefer DateTo's month, else DateFrom's, else current.
        var refDate = filter.DateTo ?? filter.DateFrom ?? DateTime.Today;
        var monthLabel = refDate.ToString("MM/yyyy");
        var monthNumber = refDate.ToString("MM");

        using var wb = new XLWorkbook(templatePath);
        var ws = wb.Worksheet(1);

        // ── Header text replacements (rows 5, 8, 9 in template) ──
        // Row 5 = title "BÁO CÁO KẾT QUẢ KINH DOANH THÁNG {MM/yyyy}" (merged A5:N6)
        // Row 8 = "Nhân viên: {Users.FullName}"
        // Row 9 = "Phòng Dự án xin báo cáo BLĐ kết quả kinh doanh tháng {MM} như sau:"
        ReplaceInRow(ws, 5, "{MM/yyyy}", monthLabel);
        ReplaceInRow(ws, 8, "{Users.FullName}", userName);
        ReplaceInRow(ws, 9, "{MM}", monthNumber);

        // ── Data rows ──
        const int dataStartRow = 12;
        int itemCount = rows.Count;
        if (itemCount > 1)
        {
            ws.Row(dataStartRow).InsertRowsBelow(itemCount - 1);
        }

        for (int i = 0; i < itemCount; i++)
        {
            var r = rows[i];
            int row = dataStartRow + i;

            ws.Cell(row, 1).Value = i + 1;                          // A: TT
            ws.Cell(row, 2).Value = r.CustomerName;                  // B: Tên Dự án/KH
            ws.Cell(row, 3).Value = r.CustomerPoNo ?? "";            // C: Số PO/HĐ
            ws.Cell(row, 4).Value = r.SalesOrderNo;                  // D: Số DNTT/DNHU
            ws.Cell(row, 5).Value = "";                              // E: Số Hóa Đơn
            ws.Cell(row, 6).Value = "";                              // F: Ngày Hóa Đơn
            ws.Cell(row, 7).Value = r.ProductName;                   // G: Gói hàng hóa
            ws.Cell(row, 8).Value = r.TotalAmount;                   // H: Thành tiền BÁN
            ws.Cell(row, 9).Value = r.PurchaseCost;                  // I: Thành tiền MUA
            ws.Cell(row, 10).Value = r.SubTotal;                     // J: Tổng tiền BÁN chưa VAT
            // K: Tổng tiền BÁN ko VAT — leave empty per template
            ws.Cell(row, 12).Value = r.TaxAmount;                    // L: Tổng VAT đầu vào
            ws.Cell(row, 13).Value = r.UnofficialW2WShipping;        // M: Phí v/c về VP
            ws.Cell(row, 14).Value = r.ShippingFee;                  // N: Phí v/c đến KH

            // Number formatting for currency columns
            foreach (var col in new[] { 8, 9, 10, 11, 12, 13, 14 })
            {
                ws.Cell(row, col).Style.NumberFormat.Format = "#,##0";
            }
        }

        // ── Sum row (was row 13, now shifted) ──
        int lastDataRow = dataStartRow + itemCount - 1;
        int sumRow = lastDataRow + 1;
        // Rebuild SUM formulas so they cover all data rows (H..N).
        var sumCols = new (int Idx, string Letter)[]
        {
            (8, "H"), (9, "I"), (10, "J"), (11, "K"),
            (12, "L"), (13, "M"), (14, "N")
        };
        foreach (var (idx, letter) in sumCols)
        {
            ws.Cell(sumRow, idx).FormulaA1 = $"SUM({letter}{dataStartRow}:{letter}{lastDataRow})";
            ws.Cell(sumRow, idx).Style.NumberFormat.Format = "#,##0";
        }

        var safeUser = SanitizeFileName(userName);
        var fileName = $"BCKQKD_{refDate:yyyy.MM}_{safeUser}.xlsx";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), fileName);
    }

    private async Task<List<SalesResultRowViewModel>> BuildSalesResultRowsAsync(
        SalesResultFilterViewModel filter, int userId)
    {
        var query = _uow.Repository<SalesOrder>().Query()
            .Include(so => so.Customer)
            .Include(so => so.Items)
            .Where(so => SalesResultStatuses.Contains(so.Status)
                         && so.SalesPersonId == userId);

        // Date filter: prefer CompletedAt — that's when the sales cycle finished.
        if (filter.DateFrom.HasValue)
        {
            var from = filter.DateFrom.Value.Date;
            query = query.Where(so => so.CompletedAt != null && so.CompletedAt >= from);
        }
        if (filter.DateTo.HasValue)
        {
            var toExclusive = filter.DateTo.Value.Date.AddDays(1);
            query = query.Where(so => so.CompletedAt != null && so.CompletedAt < toExclusive);
        }

        var sos = await query.OrderBy(so => so.CompletedAt).ToListAsync();
        if (sos.Count == 0) return [];

        // Pull QuotationItems for shipping figures — one query keyed by QuotationId.
        var quotationIds = sos.Where(so => so.QuotationId.HasValue)
                              .Select(so => so.QuotationId!.Value).Distinct().ToList();

        var qItemsByQuotation = new Dictionary<int, List<QuotationItem>>();
        if (quotationIds.Count > 0)
        {
            var qItems = await _uow.Repository<QuotationItem>().Query()
                .Where(qi => quotationIds.Contains(qi.QuotationId))
                .ToListAsync();
            qItemsByQuotation = qItems.GroupBy(qi => qi.QuotationId)
                                      .ToDictionary(g => g.Key, g => g.ToList());
        }

        var rows = new List<SalesResultRowViewModel>(sos.Count);
        foreach (var so in sos)
        {
            decimal shipping = 0, w2w = 0;
            if (so.QuotationId.HasValue
                && qItemsByQuotation.TryGetValue(so.QuotationId.Value, out var qItems))
            {
                shipping = qItems.Sum(qi => qi.ShippingFee ?? 0);
                w2w = qItems.Sum(qi => qi.UnofficialW2WShipping ?? 0);
            }

            var productNames = string.Join(", ", so.Items
                .OrderBy(i => i.SortOrder)
                .Select(i => i.ProductName));

            rows.Add(new SalesResultRowViewModel
            {
                SalesOrderId = so.SalesOrderId,
                SalesOrderNo = so.SalesOrderNo,
                CustomerName = so.Customer.CustomerName,
                CustomerPoNo = so.CustomerPoNo,
                ProductName = productNames,
                OrderDate = so.OrderDate,
                CompletedAt = so.CompletedAt,
                SubTotal = so.SubTotal,
                TaxAmount = so.TaxAmount,
                TotalAmount = so.TotalAmount,
                PurchaseCost = so.PurchaseCost ?? so.ActualCost ?? 0,
                ShippingFee = shipping,
                UnofficialW2WShipping = w2w
            });
        }
        return rows;
    }

    private static void ReplaceInRow(IXLWorksheet ws, int rowNumber, string token, string replacement)
    {
        foreach (var cell in ws.Row(rowNumber).CellsUsed())
        {
            if (cell.DataType == XLDataType.Text)
            {
                var text = cell.GetString();
                if (text.Contains(token))
                {
                    cell.Value = text.Replace(token, replacement);
                }
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("", name.Where(c => !invalid.Contains(c))).Trim();
    }
}
