using ShiftTrack.Application.Interfaces;

namespace ShiftTrack.Infrastructure.Security;

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plain)
    {
        return BCrypt.Net.BCrypt.HashPassword(plain, workFactor: 12);
    }

    public bool Verify(string plain, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(plain, hash);
    }
}
