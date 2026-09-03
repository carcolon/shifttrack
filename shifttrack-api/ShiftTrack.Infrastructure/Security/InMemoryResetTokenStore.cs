using System.Collections.Concurrent;
using ShiftTrack.Application.Interfaces;

namespace ShiftTrack.Infrastructure.Security;

public class InMemoryResetTokenStore : IResetTokenStore
{
    private readonly ConcurrentDictionary<string, (string Token, ResetTokenEntry Entry)> _tokens = new();
    private readonly Random _rng = new();

    public string CreateToken(string email, TimeSpan ttl)
    {
        var token = Guid.NewGuid().ToString("N") + _rng.NextInt64().ToString("x");
        var expires = DateTimeOffset.UtcNow.Add(ttl);
        _tokens[email.ToLowerInvariant()] = (token, new ResetTokenEntry(email, expires));
        return token;
    }

    public bool TryValidateAndConsume(string email, string token)
    {
        var key = email.ToLowerInvariant();
        if (_tokens.TryGetValue(key, out var stored))
        {
            var valid = stored.Token == token && stored.Entry.ExpiresAt > DateTimeOffset.UtcNow;
            _tokens.TryRemove(key, out _);
            return valid;
        }
        return false;
    }
}
