using EV_ERP.Models.Entities.Auth;
using EV_ERP.Models.Entities.System;
using EV_ERP.Repositories.Interfaces;
using EV_ERP.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EV_ERP.Services;

public class SlaService : ISlaService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SlaService> _logger;
    private List<int>? _managerUserIds;

    public SlaService(IUnitOfWork uow, ILogger<SlaService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    private async Task<List<int>> GetManagerUserIdsAsync()
    {
        _managerUserIds ??= await _uow.Repository<User>().Query()
            .Include(u => u.Role)
            .Where(u => u.IsActive && (u.Role.RoleCode == "MANAGER" || u.Role.RoleCode == "ADMIN"))
            .Select(u => u.UserId)
            .ToListAsync();
        return _managerUserIds;
    }

    public async Task StartTrackingAsync(string entityType, int entityId, string fromStatus, int? assigneeId)
    {
        var config = await _uow.Repository<SlaConfig>().Query()
            .FirstOrDefaultAsync(c => c.EntityType == entityType
                                   && c.FromStatus == fromStatus
                                   && c.IsActive);
        if (config == null) return; // Không có cấu hình SLA cho bước này

        var now = DateTime.Now;
        var durationMinutes = config.DurationHours * 60;
        var warningMinutes = durationMinutes * config.WarningPercent / 100;

        var tracking = new SlaTracking
        {
            SlaConfigId = config.SlaConfigId,
            EntityType = entityType,
            EntityId = entityId,
            TrackedStatus = fromStatus,
            AssigneeId = assigneeId,
            StartedAt = now,
            WarningAt = now.AddMinutes((double)warningMinutes),
            DeadlineAt = now.AddMinutes((double)durationMinutes),
            Status = "ACTIVE",
            CreatedAt = now
        };

        await _uow.Repository<SlaTracking>().AddAsync(tracking);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("SLA tracking started: {EntityType} #{EntityId} status={Status}, deadline={Deadline}",
            entityType, entityId, fromStatus, tracking.DeadlineAt);
    }

    public async Task CompleteTrackingAsync(string entityType, int entityId, string currentStatus)
    {
        var activeTrackings = await _uow.Repository<SlaTracking>().Query()
            .Where(t => t.EntityType == entityType
                      && t.EntityId == entityId
                      && t.TrackedStatus == currentStatus
                      && t.Status != "COMPLETED" && t.Status != "SKIPPED")
            .ToListAsync();

        var now = DateTime.Now;
        foreach (var t in activeTrackings)
        {
            t.Status = "COMPLETED";
            t.CompletedAt = now;
            _uow.Repository<SlaTracking>().Update(t);
        }

        if (activeTrackings.Count > 0)
            await _uow.SaveChangesAsync();
    }

    public async Task SkipTrackingAsync(string entityType, int entityId)
    {
        var activeTrackings = await _uow.Repository<SlaTracking>().Query()
            .Where(t => t.EntityType == entityType
                      && t.EntityId == entityId
                      && t.Status != "COMPLETED" && t.Status != "SKIPPED")
            .ToListAsync();

        foreach (var t in activeTrackings)
        {
            t.Status = "SKIPPED";
            t.CompletedAt = DateTime.Now;
            _uow.Repository<SlaTracking>().Update(t);
        }

        if (activeTrackings.Count > 0)
            await _uow.SaveChangesAsync();
    }

    public async Task<List<SlaAlertDto>> GetActiveAlertsForUserAsync(int userId)
    {
        var now = DateTime.Now;

        return await _uow.Repository<SlaTracking>().Query()
            .Include(t => t.SlaConfig)
            .Where(t => t.AssigneeId == userId
                      && (t.Status == "ACTIVE" || t.Status == "WARNING" || t.Status == "OVERDUE"))
            .OrderBy(t => t.DeadlineAt)
            .Select(t => new SlaAlertDto
            {
                SlaTrackingId = t.SlaTrackingId,
                EntityType = t.EntityType,
                EntityId = t.EntityId,
                TrackedStatus = t.TrackedStatus,
                ConfigName = t.SlaConfig.ConfigName,
                DeadlineAt = t.DeadlineAt,
                SlaStatus = t.Status,
                DisplaySeverity = now >= t.DeadlineAt ? "DANGER"
                                : now >= t.WarningAt ? "WARNING"
                                : "NORMAL",
                RemainingHours = (decimal)EF.Functions.DateDiffMinute(now, t.DeadlineAt) / 60m
            })
            .ToListAsync();
    }

    public async Task<Dictionary<(string EntityType, int EntityId), string>> GetSeverityMapAsync(
        List<(string EntityType, int EntityId)> entities)
    {
        if (entities.Count == 0)
            return new Dictionary<(string, int), string>();

        var entityTypes = entities.Select(e => e.EntityType).Distinct().ToList();
        var entityIds = entities.Select(e => e.EntityId).Distinct().ToList();
        var now = DateTime.Now;

        var trackings = await _uow.Repository<SlaTracking>().Query()
            .Where(t => entityTypes.Contains(t.EntityType)
                      && entityIds.Contains(t.EntityId)
                      && (t.Status == "ACTIVE" || t.Status == "WARNING" || t.Status == "OVERDUE"))
            .Select(t => new
            {
                t.EntityType,
                t.EntityId,
                Severity = now >= t.DeadlineAt ? "DANGER"
                         : now >= t.WarningAt ? "WARNING"
                         : "NORMAL"
            })
            .ToListAsync();

        var result = new Dictionary<(string, int), string>();
        foreach (var t in trackings)
        {
            var key = (t.EntityType, t.EntityId);
            // Giữ severity cao nhất
            if (!result.TryGetValue(key, out var existing) || SeverityRank(t.Severity) > SeverityRank(existing))
                result[key] = t.Severity;
        }
        return result;
    }

    public async Task<List<int>> CheckAndNotifyAsync()
    {
        var now = DateTime.Now;
        var userIdsToNotify = new HashSet<int>();

        // Tìm các tracking ACTIVE đã đến mốc WARNING nhưng chưa gửi
        var warningItems = await _uow.Repository<SlaTracking>().Query()
            .Include(t => t.SlaConfig)
            .Where(t => t.Status == "ACTIVE"
                      && now >= t.WarningAt
                      && t.WarningNotifiedAt == null)
            .ToListAsync();

        foreach (var t in warningItems)
        {
            t.Status = "WARNING";
            t.WarningNotifiedAt = now;
            _uow.Repository<SlaTracking>().Update(t);

            // Notify assignee
            if (t.AssigneeId.HasValue && t.SlaConfig.NotifyAssignee)
            {
                await CreateSlaNotificationAsync(t, "WARNING", t.AssigneeId.Value);
                userIdsToNotify.Add(t.AssigneeId.Value);
            }

            // Notify managers
            if (t.SlaConfig.NotifyManager)
            {
                var managerIds = await GetManagerUserIdsAsync();
                foreach (var mId in managerIds)
                {
                    if (mId == t.AssigneeId) continue; // avoid duplicate
                    await CreateSlaNotificationAsync(t, "WARNING", mId);
                    userIdsToNotify.Add(mId);
                }
            }
        }

        // Tìm các tracking ACTIVE/WARNING đã quá hạn nhưng chưa gửi
        var overdueItems = await _uow.Repository<SlaTracking>().Query()
            .Include(t => t.SlaConfig)
            .Where(t => (t.Status == "ACTIVE" || t.Status == "WARNING")
                      && now >= t.DeadlineAt
                      && t.OverdueNotifiedAt == null)
            .ToListAsync();

        foreach (var t in overdueItems)
        {
            t.Status = "OVERDUE";
            t.OverdueNotifiedAt = now;
            _uow.Repository<SlaTracking>().Update(t);

            // Notify assignee
            if (t.AssigneeId.HasValue && t.SlaConfig.NotifyAssignee)
            {
                await CreateSlaNotificationAsync(t, "DANGER", t.AssigneeId.Value);
                userIdsToNotify.Add(t.AssigneeId.Value);
            }

            // Escalate to managers on overdue
            if (t.SlaConfig.EscalateOnOverdue || t.SlaConfig.NotifyManager)
            {
                var managerIds = await GetManagerUserIdsAsync();
                foreach (var mId in managerIds)
                {
                    if (mId == t.AssigneeId) continue;
                    await CreateSlaNotificationAsync(t, "DANGER", mId);
                    userIdsToNotify.Add(mId);
                }
            }
        }

        if (warningItems.Count > 0 || overdueItems.Count > 0)
            await _uow.SaveChangesAsync();

        _logger.LogDebug("SLA check: {WarningCount} warnings, {OverdueCount} overdue",
            warningItems.Count, overdueItems.Count);

        return userIdsToNotify.ToList();
    }

    private async Task CreateSlaNotificationAsync(SlaTracking tracking, string severity, int userId)
    {
        var entityLabel = tracking.EntityType switch
        {
            "RFQ" => "RFQ",
            "QUOTATION" => "Báo giá",
            "SALES_ORDER" => "Đơn hàng",
            _ => tracking.EntityType
        };

        var actionUrl = tracking.EntityType switch
        {
            "RFQ" => $"/Rfq/Detail/{tracking.EntityId}",
            "QUOTATION" => $"/Quotation/Detail/{tracking.EntityId}",
            "SALES_ORDER" => $"/SalesOrder/Detail/{tracking.EntityId}",
            _ => null
        };

        var isWarning = severity == "WARNING";
        var title = isWarning
            ? $"SLA sắp hết hạn: {tracking.SlaConfig.ConfigName}"
            : $"SLA quá hạn: {tracking.SlaConfig.ConfigName}";

        var message = isWarning
            ? $"{entityLabel} đang ở bước \"{tracking.TrackedStatus}\" sắp hết thời gian cho phép."
            : $"{entityLabel} đã vượt quá thời gian cho phép ở bước \"{tracking.TrackedStatus}\".";

        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            NotificationType = "SLA",
            Severity = severity,
            ReferenceType = tracking.EntityType,
            ReferenceId = tracking.EntityId,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.Now
        };

        await _uow.Repository<Notification>().AddAsync(notification);
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "DANGER" => 2,
        "WARNING" => 1,
        _ => 0
    };
}
