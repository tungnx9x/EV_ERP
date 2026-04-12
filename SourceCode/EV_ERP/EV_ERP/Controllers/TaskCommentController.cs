using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Models.Entities.Auth;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Controllers;

[RequireLogin]
public class TaskCommentController : Controller
{
    private readonly ITaskCommentService _commentService;
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;

    public TaskCommentController(ITaskCommentService commentService, IUnitOfWork uow,
        INotificationService notificationService)
    {
        _commentService = commentService;
        _uow = uow;
        _notificationService = notificationService;
    }

    private int CurrentUserId =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

    /// <summary>
    /// GET /TaskComment/GetComments?entityType=SALES_ORDER&entityId=3
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetComments(string entityType, int entityId)
    {
        var comments = await _commentService.GetByEntityAsync(entityType, entityId);

        // Mark related notifications as read
        var userId = CurrentUserId;
        var notifications = await _uow.Repository<Models.Entities.System.Notification>().Query()
            .Where(n => n.UserId == userId && !n.IsRead
                && n.NotificationType == "TASK_COMMENT"
                && n.ReferenceType == "TASK_COMMENT"
                && n.ActionUrl != null && n.ActionUrl.Contains($"/{entityType.Replace("_", "")}/Detail/{entityId}"))
            .ToListAsync();

        if (notifications.Count > 0)
        {
            var now = DateTime.Now;
            foreach (var n in notifications)
            {
                n.IsRead = true;
                n.ReadAt = now;
                _uow.Repository<Models.Entities.System.Notification>().Update(n);
            }
            await _uow.SaveChangesAsync();
        }

        return Json(new { success = true, data = comments, totalCount = comments.Count });
    }

    /// <summary>
    /// POST /TaskComment/Create
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return Json(new { success = false, message = "Nội dung không được để trống" });

        var comment = await _commentService.CreateAsync(request, CurrentUserId);
        return Json(new { success = true, data = comment });
    }

    /// <summary>
    /// POST /TaskComment/Edit
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Edit([FromBody] EditCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return Json(new { success = false, message = "Nội dung không được để trống" });

        var result = await _commentService.EditAsync(request.CommentId, request.Content, CurrentUserId);
        return Json(new { success = result, message = result ? null : "Không thể chỉnh sửa" });
    }

    /// <summary>
    /// POST /TaskComment/Delete/{id}
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _commentService.DeleteAsync(id, CurrentUserId);
        return Json(new { success = result, message = result ? null : "Không thể xóa" });
    }

    /// <summary>
    /// GET /TaskComment/SearchUsers?q=Nguyen
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchUsers(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 1)
            return Json(new { data = Array.Empty<object>() });

        var users = await _uow.Repository<User>().Query()
            .Where(u => u.IsActive && (u.FullName.Contains(q) || u.UserCode.Contains(q)))
            .OrderBy(u => u.FullName)
            .Take(10)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.UserCode,
                u.AvatarUrl
            })
            .ToListAsync();

        return Json(new { data = users });
    }
}

public class EditCommentRequest
{
    public long CommentId { get; set; }
    public string Content { get; set; } = string.Empty;
}
