namespace EV_ERP.Services.Interfaces;

public class TaskCommentDto
{
    public long CommentId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public long? ParentCommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public int CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? CreatedByAvatar { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TimeAgo => FormatTimeAgo(CreatedAt);
    public List<int> MentionedUserIds { get; set; } = [];
    public List<TaskCommentDto> Replies { get; set; } = [];

    private static string FormatTimeAgo(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.TotalMinutes < 1) return "vừa xong";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} ngày trước";
        return dt.ToString("dd/MM/yyyy HH:mm");
    }
}

public class CreateCommentRequest
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public long? ParentCommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<int> MentionedUserIds { get; set; } = [];
    public bool IsInternal { get; set; }
}

public interface ITaskCommentService
{
    Task<TaskCommentDto> CreateAsync(CreateCommentRequest request, int currentUserId);
    Task<List<TaskCommentDto>> GetByEntityAsync(string entityType, int entityId);
    Task<bool> EditAsync(long commentId, string newContent, int currentUserId);
    Task<bool> DeleteAsync(long commentId, int currentUserId);
}
