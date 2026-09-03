using Microsoft.AspNetCore.SignalR;

namespace ShiftTrack.Tests.Shared.Mocks;

public sealed class SpyHubClientProxy : IClientProxy
{
    public List<HubCallRecord> Calls { get; } = new();

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        Calls.Add(new HubCallRecord(method, args));
        return Task.CompletedTask;
    }
}

public sealed record HubCallRecord(string Method, object?[] Args);
