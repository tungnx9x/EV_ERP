namespace EV_ERP.Models.ViewModels.Shared;

/// <summary>
/// One product line (name + quantity) used to summarise the contents of a
/// quotation / sales order in list tables and workspace tooltips.
/// </summary>
public class ProductQtyLine
{
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }

    public static string FormatQty(decimal qty) => qty.ToString("#,##0.###");

    /// <summary>"Tên sản phẩm x SL; Tên sản phẩm x SL; ..."</summary>
    public static string Summarize(IEnumerable<ProductQtyLine>? lines) =>
        lines == null
            ? string.Empty
            : string.Join("; ", lines.Select(l => $"{l.ProductName} x {FormatQty(l.Quantity)}"));
}
