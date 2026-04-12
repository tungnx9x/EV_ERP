using Microsoft.AspNetCore.SignalR;
using EV_ERP.Helpers;

namespace EV_ERP.Hubs;

public class NotificationHub : Hub
{
    /// <summary>
    /// Khi client kết nối, tự động join group theo UserId từ session.
    /// Client sẽ nhận message qua group "user-{UserId}".
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var currentUser = httpContext?.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser);

        if (currentUser != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{currentUser.UserId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var httpContext = Context.GetHttpContext();
        var currentUser = httpContext?.Session.GetObject<CurrentUser>(SessionKeys.CurrentUser);

        if (currentUser != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{currentUser.UserId}");
        }

        await base.OnDisconnectedAsync(exception);
    }
}
