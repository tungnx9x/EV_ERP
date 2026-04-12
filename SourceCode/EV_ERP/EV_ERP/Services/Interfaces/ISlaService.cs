namespace EV_ERP.Services.Interfaces;

public class SlaAlertDto
{
    public long SlaTrackingId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string TrackedStatus { get; set; } = string.Empty;
    public string ConfigName { get; set; } = string.Empty;
    public DateTime DeadlineAt { get; set; }
    public string SlaStatus { get; set; } = string.Empty;        // ACTIVE, WARNING, OVERDUE
    public string DisplaySeverity { get; set; } = "NORMAL";      // NORMAL, WARNING, DANGER
    public decimal? RemainingHours { get; set; }
}

public interface ISlaService
{
    /// <summary>Tạo SlaTracking khi entity chuyển sang status mới</summary>
    Task StartTrackingAsync(string entityType, int entityId, string fromStatus, int? assigneeId);

    /// <summary>Đánh dấu COMPLETED khi entity chuyển sang status tiếp theo</summary>
    Task CompleteTrackingAsync(string entityType, int entityId, string currentStatus);

    /// <summary>Đánh dấu SKIPPED (khi cancel)</summary>
    Task SkipTrackingAsync(string entityType, int entityId);

    /// <summary>Lấy danh sách SLA alerts cho 1 user (Workspace)</summary>
    Task<List<SlaAlertDto>> GetActiveAlertsForUserAsync(int userId);

    /// <summary>Lấy SLA severity cho danh sách entity (batch query cho Workspace)</summary>
    Task<Dictionary<(string EntityType, int EntityId), string>> GetSeverityMapAsync(
        List<(string EntityType, int EntityId)> entities);

    /// <summary>Background job: kiểm tra và gửi cảnh báo, trả về danh sách userId cần notify</summary>
    Task<List<int>> CheckAndNotifyAsync();
}
