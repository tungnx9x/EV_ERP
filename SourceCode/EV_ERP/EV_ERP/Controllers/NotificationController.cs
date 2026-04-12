using EV_ERP.Filters;
using EV_ERP.Helpers;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EV_ERP.Controllers;

[RequireLogin]
public class NotificationController : Controller
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private int CurrentUserId =>
        HttpContext.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser)!.UserId;

    [HttpGet]
    public async Task<IActionResult> GetRecent()
    {
        var userId = CurrentUserId;
        var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
        var items = await _notificationService.GetRecentAsync(userId, 20);

        return Json(new { unreadCount, items });
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(long id)
    {
        await _notificationService.MarkAsReadAsync(id, CurrentUserId);
        var unreadCount = await _notificationService.GetUnreadCountAsync(CurrentUserId);
        return Json(new { success = true, unreadCount });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notificationService.MarkAllAsReadAsync(CurrentUserId);
        return Json(new { success = true, unreadCount = 0 });
    }
}
