namespace EV_ERP.Models.ViewModels.Workspace;

public class WorkspaceViewModel
{
    public List<WorkspaceCard> Cards { get; set; } = [];
}

public class WorkspaceCard
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string BadgeColor { get; set; } = "primary";
    public int StepNumber { get; set; }
    public List<WorkspaceTaskItem> Tasks { get; set; } = [];
}

public class WorkspaceTaskItem
{
    public string RfqNo { get; set; } = string.Empty;
    public string? SalesOrderNo { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public string? ExtraInfo { get; set; }
}
