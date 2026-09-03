using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ShiftTrack.Api;

internal static class ResetFlowHelpers
{
    internal static string CreateResetCode(string email, string resetToken, TimeSpan ttl)
    {
        var code = Guid.NewGuid().ToString("N");
        var state = new ResetCodeState
        {
            Email = email.Trim().ToLowerInvariant(),
            ResetToken = resetToken,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl)
        };
        ResetFlowState.ResetCodeStore[code] = state;
        return code;
    }

    internal static bool TryConsumeResetCode(string code, out ResetCodeState state)
    {
        state = default!;
        if (!ResetFlowState.ResetCodeStore.TryRemove(code, out var stored))
        {
            return false;
        }

        if (stored.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        state = stored;
        return true;
    }

    internal static string CreateResetExchangeToken(string email, string resetToken, TimeSpan ttl)
    {
        var exchangeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var state = new ResetExchangeState
        {
            Email = email.Trim().ToLowerInvariant(),
            ResetToken = resetToken,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl)
        };
        ResetFlowState.ResetExchangeStore[exchangeToken] = state;
        return exchangeToken;
    }

    internal static bool TryConsumeResetExchangeToken(string email, string exchangeToken, out string resetToken)
    {
        resetToken = string.Empty;
        if (!ResetFlowState.ResetExchangeStore.TryRemove(exchangeToken, out var state))
        {
            return false;
        }

        if (state.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        if (!string.Equals(state.Email, email.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            return false;
        }

        resetToken = state.ResetToken;
        return true;
    }
}

internal readonly record struct EntraCodeExchangeResult(bool Success, string? IdToken, string? ErrorMessage);

internal sealed class ResetCodeState
{
    internal string Email { get; init; } = string.Empty;
    internal string ResetToken { get; init; } = string.Empty;
    internal DateTimeOffset ExpiresAtUtc { get; init; }
}

internal sealed class ResetExchangeState
{
    internal string Email { get; init; } = string.Empty;
    internal string ResetToken { get; init; } = string.Empty;
    internal DateTimeOffset ExpiresAtUtc { get; init; }
}

internal static class ResetFlowState
{
    internal static readonly ConcurrentDictionary<string, ResetCodeState> ResetCodeStore = new(StringComparer.Ordinal);
    internal static readonly ConcurrentDictionary<string, ResetExchangeState> ResetExchangeStore = new(StringComparer.Ordinal);
}
