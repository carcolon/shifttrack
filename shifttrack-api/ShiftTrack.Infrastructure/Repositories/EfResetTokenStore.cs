using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Infrastructure.Persistence;

namespace ShiftTrack.Infrastructure.Repositories;

public sealed class EfResetTokenStore(ShiftTrackDbContext dbContext) : IResetTokenStore
{
    public string CreateToken(string email, TimeSpan ttl)
    {
        var token = GenerateToken();
        var hash = HashToken(token);

        dbContext.ResetTokens.Add(new ResetToken
        {
            Id = Guid.NewGuid(),
            Email = email,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
            CreatedAtUtc = DateTime.UtcNow
        });
        dbContext.SaveChanges();

        return token;
    }

    public bool TryValidateAndConsume(string email, string token)
    {
        var hash = HashToken(token);
        using var tx = dbContext.Database.BeginTransaction();

        var entry = dbContext.ResetTokens
            .Where(resetToken => resetToken.Email == email &&
                                 resetToken.UsedAtUtc == null &&
                                 resetToken.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(resetToken => resetToken.CreatedAtUtc)
            .FirstOrDefault();

        if (entry is null || !string.Equals(entry.TokenHash, hash, StringComparison.Ordinal))
        {
            tx.Commit();
            return false;
        }

        dbContext.ResetTokens
            .Where(resetToken => resetToken.Id == entry.Id)
            .ExecuteUpdate(setters => setters.SetProperty(resetToken => resetToken.UsedAtUtc, DateTime.UtcNow));

        tx.Commit();
        return true;
    }

    private static string GenerateToken()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer);
    }

    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
