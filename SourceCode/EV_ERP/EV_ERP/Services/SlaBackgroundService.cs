using EV_ERP.Hubs;
using EV_ERP.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace EV_ERP.Services;

public class SlaBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SlaBackgroundService> _logger;

    public SlaBackgroundService(IServiceProvider serviceProvider,
        IHubContext<NotificationHub> hubContext,
        ILogger<SlaBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SLA Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var slaService = scope.ServiceProvider.GetRequiredService<ISlaService>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var userIdsToNotify = await slaService.CheckAndNotifyAsync();

                // Push real-time update to affected users
                foreach (var userId in userIdsToNotify)
                {
                    var unreadCount = await notificationService.GetUnreadCountAsync(userId);
                    await _hubContext.Clients.Group($"user-{userId}")
                        .SendAsync("UpdateNotificationCount", unreadCount, stoppingToken);

                    var recent = await notificationService.GetRecentAsync(userId, 1);
                    if (recent.Count > 0)
                    {
                        await _hubContext.Clients.Group($"user-{userId}")
                            .SendAsync("ReceiveNotification", recent[0], stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SLA background check");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
