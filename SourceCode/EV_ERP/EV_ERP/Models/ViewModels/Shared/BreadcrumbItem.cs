namespace EV_ERP.Models.ViewModels.Shared;

public class BreadcrumbItem
{
    public string Text { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsActive => string.IsNullOrEmpty(Url);

    public BreadcrumbItem() { }

    public BreadcrumbItem(string text, string? url = null)
    {
        Text = text;
        Url = url;
    }
}
