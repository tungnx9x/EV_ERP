using System.Text.Json;
using EV_ERP.Hubs;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.Sales;
using EV_ERP.Models.Entities.System;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services;

public class TaskCommentService : ITaskCommentService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;
    private readonly IHubContext<NotificationHub> _hubContext;

    public TaskCommentService(IUnitOfWork uow, INotificationService notificationService,
        IHubContext<NotificationHub> hubContext)
    {
        _uow = uow;
        _notificationService = notificationService;
        _hubContext = hubContext;
    }

    public async Task<TaskCommentDto> CreateAsync(CreateCommentRequest request, int currentUserId)
    {
        var comment = new TaskComment
        {
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            ParentCommentId = request.ParentCommentId,
            Content = request.Content,
            MentionedUserIds = request.MentionedUserIds.Count > 0
                ? JsonSerializer.Serialize(request.MentionedUserIds)
                : null,
            IsInternal = request.IsInternal,
            CreatedBy = currentUserId,
            CreatedAt = DateTime.Now
        };

        await _uow.Repository<TaskComment>().AddAsync(comment);
        await _uow.SaveChangesAsync();

        // Get creator info for response
        var creator = await _uow.Repository<User>().GetByIdAsync(currentUserId);

        // Build notification recipients
        var recipientIds = await GetRecipientIdsAsync(request, currentUserId);

        if (recipientIds.Count > 0)
        {
            var (entityNo, actionUrl) = await GetEntityInfoAsync(request.EntityType, request.EntityId);
            var senderName = creator?.FullName ?? "Người dùng";
            var title = $"Bình luận mới trên {request.EntityType} {entityNo}";
            var message = request.Content.Length > 100
                ? request.Content[..100] + "..."
                : request.Content;

            foreach (var userId in recipientIds)
            {
                await _notificationService.CreateAsync(
                    userId, title, $"{senderName}: {message}",
                    "TASK_COMMENT", "INFO",
                    "TASK_COMMENT", (int)comment.CommentId,
                    actionUrl);

                // SignalR push
                await _hubContext.Clients
                    .Group($"user-{userId}")
                    .SendAsync("ReceiveNotification", new
                    {
                        Title = title,
                        Message = $"{senderName}: {message}",
                        Severity = "INFO",
                        ActionUrl = actionUrl
                    });

                // Also push the new comment event for real-time UI update
                await _hubContext.Clients
                    .Group($"user-{userId}")
                    .SendAsync("NewComment", new
                    {
                        CommentId = comment.CommentId,
                        EntityType = request.EntityType,
                        EntityId = request.EntityId,
                        SenderName = senderName,
                        Content = request.Content,
                        CreatedAt = comment.CreatedAt
                    });
            }
        }

        return new TaskCommentDto
        {
            CommentId = comment.CommentId,
            EntityType = comment.EntityType,
            EntityId = comment.EntityId,
            ParentCommentId = comment.ParentCommentId,
            Content = comment.Content,
            IsInternal = comment.IsInternal,
            CreatedBy = currentUserId,
            CreatedByName = creator?.FullName ?? "",
            CreatedByAvatar = creator?.AvatarUrl,
            CreatedAt = comment.CreatedAt,
            MentionedUserIds = request.MentionedUserIds
        };
    }

    public async Task<List<TaskCommentDto>> GetByEntityAsync(string entityType, int entityId)
    {
        var comments = await _uow.Repository<TaskComment>().Query()
            .Where(c => c.EntityType == entityType && c.EntityId == entityId && c.IsActive)
            .Include(c => c.CreatedByUser)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        // Build tree: root comments with nested replies
        var rootComments = comments.Where(c => c.ParentCommentId == null).ToList();
        var replyLookup = comments.Where(c => c.ParentCommentId != null)
            .GroupBy(c => c.ParentCommentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.CreatedAt).ToList());

        return rootComments.Select(c => MapToDto(c, replyLookup)).ToList();
    }

    public async Task<bool> EditAsync(long commentId, string newContent, int currentUserId)
    {
        var comment = await _uow.Repository<TaskComment>().Query()
            .FirstOrDefaultAsync(c => c.CommentId == commentId && c.IsActive);

        if (comment == null || comment.CreatedBy != currentUserId)
            return false;

        comment.Content = newContent;
        comment.IsEdited = true;
        comment.EditedAt = DateTime.Now;
        _uow.Repository<TaskComment>().Update(comment);
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(long commentId, int currentUserId)
    {
        var comment = await _uow.Repository<TaskComment>().Query()
            .FirstOrDefaultAsync(c => c.CommentId == commentId && c.IsActive);

        if (comment == null || comment.CreatedBy != currentUserId)
            return false;

        comment.IsActive = false;
        _uow.Repository<TaskComment>().Update(comment);
        await _uow.SaveChangesAsync();
        return true;
    }

    private TaskCommentDto MapToDto(TaskComment c, Dictionary<long, List<TaskComment>> replyLookup)
    {
        var dto = new TaskCommentDto
        {
            CommentId = c.CommentId,
            EntityType = c.EntityType,
            EntityId = c.EntityId,
            ParentCommentId = c.ParentCommentId,
            Content = c.Content,
            IsInternal = c.IsInternal,
            IsEdited = c.IsEdited,
            EditedAt = c.EditedAt,
            CreatedBy = c.CreatedBy,
            CreatedByName = c.CreatedByUser?.FullName ?? "",
            CreatedByAvatar = c.CreatedByUser?.AvatarUrl,
            CreatedAt = c.CreatedAt,
            MentionedUserIds = ParseMentionedIds(c.MentionedUserIds)
        };

        if (replyLookup.TryGetValue(c.CommentId, out var replies))
        {
            dto.Replies = replies.Select(r => MapToDto(r, replyLookup)).ToList();
        }

        return dto;
    }

    private static List<int> ParseMentionedIds(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? []; }
        catch { return []; }
    }

    private async Task<HashSet<int>> GetRecipientIdsAsync(CreateCommentRequest request, int currentUserId)
    {
        var recipients = new HashSet<int>(request.MentionedUserIds);

        // Add entity owner/assignee
        switch (request.EntityType)
        {
            case "RFQ":
                var rfq = await _uow.Repository<RFQ>().GetByIdAsync(request.EntityId);
                if (rfq?.AssignedTo != null) recipients.Add(rfq.AssignedTo.Value);
                if (rfq != null) recipients.Add(rfq.CreatedBy);
                break;
            case "QUOTATION":
                var quot = await _uow.Repository<Quotation>().GetByIdAsync(request.EntityId);
                if (quot != null) recipients.Add(quot.SalesPersonId);
                if (quot?.CreatedBy != null) recipients.Add(quot.CreatedBy.Value);
                break;
            case "SALES_ORDER":
                var so = await _uow.Repository<SalesOrder>().GetByIdAsync(request.EntityId);
                if (so != null) recipients.Add(so.SalesPersonId);
                if (so?.CreatedBy != null) recipients.Add(so.CreatedBy.Value);
                break;
        }

        // If replying, also notify the parent comment author
        if (request.ParentCommentId.HasValue)
        {
            var parent = await _uow.Repository<TaskComment>().GetByIdAsync(request.ParentCommentId.Value);
            if (parent != null) recipients.Add(parent.CreatedBy);
        }

        // Remove sender
        recipients.Remove(currentUserId);
        return recipients;
    }

    private async Task<(string entityNo, string actionUrl)> GetEntityInfoAsync(string entityType, int entityId)
    {
        switch (entityType)
        {
            case "RFQ":
                var rfq = await _uow.Repository<RFQ>().GetByIdAsync(entityId);
                return (rfq?.RfqNo ?? $"#{entityId}", $"/Rfq/Detail/{entityId}#comments");
            case "QUOTATION":
                var quot = await _uow.Repository<Quotation>().GetByIdAsync(entityId);
                return (quot?.QuotationNo ?? $"#{entityId}", $"/Quotation/Detail/{entityId}#comments");
            case "SALES_ORDER":
                var so = await _uow.Repository<SalesOrder>().GetByIdAsync(entityId);
                return (so?.SalesOrderNo ?? $"#{entityId}", $"/SalesOrder/Detail/{entityId}#comments");
            default:
                return ($"#{entityId}", "#");
        }
    }
}
