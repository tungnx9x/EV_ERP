using EV_ERP.Models.Entities.System;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;

    public NotificationService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task CreateAsync(int userId, string title, string message, string type,
        string severity = "INFO", string? referenceType = null, int? referenceId = null, string? actionUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            NotificationType = type,
            Severity = severity,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.Now
        };

        await _uow.Repository<Notification>().AddAsync(notification);
        await _uow.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _uow.Repository<Notification>().Query()
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<List<NotificationDto>> GetRecentAsync(int userId, int take = 20)
    {
        return await _uow.Repository<Notification>().Query()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                Title = n.Title,
                Message = n.Message,
                Severity = n.Severity,
                ActionUrl = n.ActionUrl,
                ReferenceType = n.ReferenceType,
                ReferenceId = n.ReferenceId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(long notificationId, int userId)
    {
        var n = await _uow.Repository<Notification>().Query()
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId);

        if (n != null && !n.IsRead)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.Now;
            _uow.Repository<Notification>().Update(n);
            await _uow.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var unread = await _uow.Repository<Notification>().Query()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        var now = DateTime.Now;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
            _uow.Repository<Notification>().Update(n);
        }

        if (unread.Count > 0)
            await _uow.SaveChangesAsync();
    }
}
