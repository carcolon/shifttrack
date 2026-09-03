using System;
using ShiftTrack.Application.Models;

namespace ShiftTrack.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> LoginWithEntraAsync(Guid objectId, string email, string? displayName);
    Task<AuthResult> ResetPasswordAsync(string email, string newPassword);
    Task<AuthResult> ForceChangePasswordAsync(string email, string tokenOrPassword, string newPassword, bool isToken);
    Task<string?> GenerateResetTokenAsync(string email, TimeSpan ttl);
    Task<AuthResult> CreateUserAsync(string email, string displayName, int role, string tempPassword, string location, string company, IEnumerable<string>? companies, string operation, string shiftTime, string? scheduleBlocksJson);
    Task<bool> CanDeleteAsync(int callerRole, Guid targetUserId);
}
