namespace EV_ERP.Models.ViewModels.Workspace;

public class WorkspaceViewModel
{
    public List<WorkspaceCard> Cards { get; set; } = [];

    // Manager view-as feature
    public bool CanViewOthers { get; set; }
    public bool IsManagerOverview { get; set; }          // true = team overview mode
    public int ViewingUserId { get; set; }
    public string ViewingUserName { get; set; } = string.Empty;
    public List<UserOption> Users { get; set; } = [];
}

public class UserOption
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string UserCode { get; set; } = string.Empty;
}

public class WorkspaceCard
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string BadgeColor { get; set; } = "primary";
    public int StepNumber { get; set; }
    public List<WorkspaceTaskItem> Tasks { get; set; } = [];

    // Manager overview: employee breakdown per card
    public List<EmployeeCardSummary> EmployeeSummaries { get; set; } = [];
    public int TotalTasks => EmployeeSummaries.Sum(e => e.TaskCount);
    public int TotalSlaViolations => EmployeeSummaries.Sum(e => e.SlaViolationCount);
}

public class EmployeeCardSummary
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public int SlaViolationCount { get; set; }    // WARNING + DANGER
}

public class WorkspaceTaskItem
{
    public string RfqNo { get; set; } = string.Empty;
    public string? SalesOrderNo { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public string? ExtraInfo { get; set; }

    // SLA
    public string? SlaSeverity { get; set; }          // NORMAL, WARNING, DANGER
    public string? SlaTimeRemaining { get; set; }     // VD: "2.5h", "Quá hạn 1.2h"

    // Elapsed timer (steps 1 & 2: show time since creation instead of SLA)
    public DateTime? CreatedAt { get; set; }

    // Quick note
    public string? Notes { get; set; }

    // Entity info for SLA lookup
    public string? EntityType { get; set; }
    public int EntityId { get; set; }
}
