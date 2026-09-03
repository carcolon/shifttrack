namespace ShiftTrack.Application.Interfaces;

public record ResetTokenEntry(string Email, DateTimeOffset ExpiresAt);

public interface IResetTokenStore
{
    string CreateToken(string email, TimeSpan ttl);
    bool TryValidateAndConsume(string email, string token);
}

