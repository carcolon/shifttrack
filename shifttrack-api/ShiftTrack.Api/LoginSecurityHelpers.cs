using System.Collections.Concurrent;

namespace ShiftTrack.Api;

internal static class LoginSecurityHelpers
{
    internal static string BuildLoginLockoutKey(HttpContext httpContext, string email)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"{email.Trim().ToLowerInvariant()}|{ip}";
    }

    internal static void RegisterFailedLoginAttempt(string key)
    {
        var now = DateTime.UtcNow;
        LoginSecurityState.FailedLoginAttempts.AddOrUpdate(
            key,
            _ => new FailedLoginState { Count = 1, LockedUntilUtc = null, LastFailedUtc = now },
            (_, state) =>
            {
                if (state.LockedUntilUtc.HasValue && state.LockedUntilUtc.Value <= now)
                {
                    state.Count = 0;
                    state.LockedUntilUtc = null;
                }

                state.Count += 1;
                state.LastFailedUtc = now;
                if (state.Count >= LoginSecurityState.LoginMaxAttempts)
                {
                    state.LockedUntilUtc = now.Add(LoginSecurityState.LoginLockoutWindow);
                    state.Count = 0;
                }
                return state;
            });
    }

    internal static bool TryGetLoginLockoutRemaining(string key, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (!LoginSecurityState.FailedLoginAttempts.TryGetValue(key, out var state) || !state.LockedUntilUtc.HasValue)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (state.LockedUntilUtc.Value <= now)
        {
            LoginSecurityState.FailedLoginAttempts.TryRemove(key, out _);
            return false;
        }

        remaining = state.LockedUntilUtc.Value - now;
        return true;
    }

    internal static void ClearLoginFailedAttempts(string key)
    {
        LoginSecurityState.FailedLoginAttempts.TryRemove(key, out _);
    }
}

internal sealed class FailedLoginState
{
    internal int Count { get; set; }
    internal DateTime? LockedUntilUtc { get; set; }
    internal DateTime LastFailedUtc { get; set; }
}

internal static class LoginSecurityState
{
    internal static readonly ConcurrentDictionary<string, FailedLoginState> FailedLoginAttempts = new();
    internal static readonly TimeSpan LoginLockoutWindow = TimeSpan.FromMinutes(10);
    internal const int LoginMaxAttempts = 5;
}
