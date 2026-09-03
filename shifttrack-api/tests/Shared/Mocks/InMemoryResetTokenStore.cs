using ShiftTrack.Application.Interfaces;

namespace ShiftTrack.Tests.Shared.Mocks;

public sealed class InMemoryResetTokenStore : IResetTokenStore
{
    private readonly Dictionary<string, ResetTokenEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);

    public string CreateToken(string email, TimeSpan ttl)
    {
        var token = Guid.NewGuid().ToString("N");
        _entries[email] = new ResetTokenEntry(email, DateTimeOffset.UtcNow.Add(ttl));
        _tokens[email] = token;
        return token;
    }

    public bool TryValidateAndConsume(string email, string token)
    {
        if (!_entries.TryGetValue(email, out var entry))
        {
            return false;
        }

        if (!_tokens.TryGetValue(email, out var storedToken))
        {
            return false;
        }

        if (!string.Equals(storedToken, token, StringComparison.Ordinal))
        {
            return false;
        }

        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return false;
        }

        _entries.Remove(email);
        _tokens.Remove(email);
        return true;
    }
}
