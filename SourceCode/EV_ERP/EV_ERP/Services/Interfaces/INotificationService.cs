namespace EV_ERP.Services.Interfaces;

public class NotificationDto
{
    public long NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "INFO";
    public string? ActionUrl { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TimeAgo => FormatTimeAgo(CreatedAt);

    private static string FormatTimeAgo(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.TotalMinutes < 1) return "vừa xong";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} ngày trước";
        return dt.ToString("dd/MM/yyyy");
    }
}

public interface INotificationService
{
    Task CreateAsync(int userId, string title, string message, string type,
        string severity = "INFO", string? referenceType = null, int? referenceId = null, string? actionUrl = null);
    Task<int> GetUnreadCountAsync(int userId);
    Task<List<NotificationDto>> GetRecentAsync(int userId, int take = 20);
    Task MarkAsReadAsync(long notificationId, int userId);
    Task MarkAllAsReadAsync(int userId);
}
