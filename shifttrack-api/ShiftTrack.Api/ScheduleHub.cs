using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ShiftTrack.Api;

public class ScheduleHub : Hub
{
    internal static string RequestExportsGroup(string email) =>
        $"request-exports:{email.Trim().ToLowerInvariant()}";

    // Keep join/leave lightweight so clients can scope subscriptions in the future.
    public Task JoinWeek(string weekStart)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"week:{weekStart}");

    public Task LeaveWeek(string weekStart)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"week:{weekStart}");

    public Task JoinRequestExports()
    {
        var email = Context.User?.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(email)
            ? Task.CompletedTask
            : Groups.AddToGroupAsync(Context.ConnectionId, RequestExportsGroup(email));
    }

    public Task LeaveRequestExports()
    {
        var email = Context.User?.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(email)
            ? Task.CompletedTask
            : Groups.RemoveFromGroupAsync(Context.ConnectionId, RequestExportsGroup(email));
    }
}

