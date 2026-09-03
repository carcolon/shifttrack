using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Application.Models;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Application.Services;

public class AuthService : IAuthService
{
    private const string DummyPasswordHash = "$2b$12$3OPFPnYw3gzGpjcVJkFdneU519/pgiJqjlcsHbIWFBian2QaA/Avu";
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IResetTokenStore _tokenStore;
    private readonly Guid _tenantId;
    private readonly string? _seedAdminEmail;
    private readonly Guid? _seedAdminObjectId;
    private readonly string[] _hiddenAdminEmails;
    private const string PasswordPolicy = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%*]).{8,}$";

    public AuthService(IUserRepository users, IPasswordHasher hasher, IResetTokenStore tokenStore, IConfiguration configuration)
    {
        _users = users;
        _hasher = hasher;
        _tokenStore = tokenStore;
        var tenantId = configuration["AzureAd:TenantId"];
        _tenantId = Guid.TryParse(tenantId, out var parsed) ? parsed : Guid.NewGuid();
        _seedAdminEmail = NormalizeEmail(configuration["SeedAdmin:Email"]);
        _seedAdminObjectId = Guid.TryParse(configuration["SeedAdmin:ObjectId"], out var seedObjectId)
            ? seedObjectId
            : null;
        _hiddenAdminEmails = ParseEmailList(configuration["HiddenAdmins:Emails"]);
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            _ = _hasher.Verify(password, DummyPasswordHash);
            return new AuthResult(false, "Credentials for the entered email are not valid. Please check and try again.");
        }

        var passwordHash = string.IsNullOrWhiteSpace(user.PasswordHash) ? DummyPasswordHash : user.PasswordHash;
        if (!_hasher.Verify(password, passwordHash) || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return new AuthResult(false, "Credentials for the entered email are not valid. Please check and try again.");
        }

        if (user.MustChangePassword)
        {
            return new AuthResult(false, "For security reasons, you are required to change your password before accessing your account.", user.Email, user.DisplayName, user.Role, true);
        }

        if (IsSystemHiddenAdmin(user.ObjectId, NormalizeEmail(user.Email) ?? string.Empty))
        {
            var role = await EnsureHiddenAdminAsync(user);
            return new AuthResult(true, null, user.Email, user.DisplayName, role);
        }

        return new AuthResult(true, null, user.Email, user.DisplayName, user.Role);
    }

    public async Task<AuthResult> LoginWithEntraAsync(Guid objectId, string email, string? displayName)
    {
        var userByOid = await _users.GetByObjectIdAsync(objectId);
        if (userByOid is not null)
        {
            if (!userByOid.IsActive)
            {
                return new AuthResult(false, "User not found or inactive.");
            }

            if (IsSystemHiddenAdmin(objectId, NormalizeEmail(userByOid.Email) ?? string.Empty))
            {
                var role = await EnsureHiddenAdminAsync(userByOid);
                return new AuthResult(true, null, userByOid.Email, userByOid.DisplayName, role);
            }

            return new AuthResult(true, null, userByOid.Email, userByOid.DisplayName, userByOid.Role);
        }

        var userByEmail = await _users.GetByEmailAsync(email);
        if (userByEmail is null || !userByEmail.IsActive)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (!string.IsNullOrWhiteSpace(normalizedEmail) && MatchesConfiguredHiddenAdmin(normalizedEmail))
            {
                return await CreateHiddenAdminFromEntraAsync(objectId, normalizedEmail, displayName);
            }

            return await TryCreateSeedAdminFromEntraAsync(objectId, email, displayName);
        }

        // One-time transition: if email matches but OID is not linked yet, bind it now.
        var rows = await _users.UpdateObjectIdAsync(userByEmail.Id, objectId);
        if (rows <= 0)
        {
            return new AuthResult(false, "Unable to link Microsoft account.");
        }

        if (IsSystemHiddenAdmin(objectId, NormalizeEmail(userByEmail.Email) ?? string.Empty))
        {
            var role = await EnsureHiddenAdminAsync(userByEmail);
            return new AuthResult(
                true,
                null,
                userByEmail.Email,
                string.IsNullOrWhiteSpace(userByEmail.DisplayName) ? displayName ?? userByEmail.Email : userByEmail.DisplayName,
                role);
        }

        return new AuthResult(
            true,
            null,
            userByEmail.Email,
            string.IsNullOrWhiteSpace(userByEmail.DisplayName) ? displayName ?? userByEmail.Email : userByEmail.DisplayName,
            userByEmail.Role);
    }

    public async Task<AuthResult> ResetPasswordAsync(string email, string newPassword)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            return new AuthResult(false, "User not found or inactive.");
        }

        if (!IsPasswordValid(newPassword))
        {
            return new AuthResult(false, "Password must be at least 8 characters and include uppercase, lowercase, number, and special character (!@#$%*).");
        }

        var hash = _hasher.Hash(newPassword);
        var rows = await _users.UpdatePasswordAsync(email, hash, false);
        return rows > 0
            ? new AuthResult(true, null, user.Email, user.DisplayName, user.Role)
            : new AuthResult(false, "Could not update password.");
    }

    public async Task<AuthResult> ForceChangePasswordAsync(string email, string tokenOrPassword, string newPassword, bool isToken)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            return new AuthResult(false, "User not found or inactive.");
        }

        if (!IsPasswordValid(newPassword))
        {
            return new AuthResult(false, "Password must be at least 8 characters and include uppercase, lowercase, number, and special character (!@#$%*).");
        }

        if (isToken)
        {
            var valid = _tokenStore.TryValidateAndConsume(email, tokenOrPassword);
            if (!valid) return new AuthResult(false, "Invalid or expired token.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(user.PasswordHash) || !_hasher.Verify(tokenOrPassword, user.PasswordHash))
            {
                return new AuthResult(false, "Current credentials are not valid.");
            }
        }

        var hash = _hasher.Hash(newPassword);
        var rows = await _users.UpdatePasswordAsync(email, hash, false);
        return rows > 0
            ? new AuthResult(true, null, user.Email, user.DisplayName, user.Role)
            : new AuthResult(false, "Could not update password.");
    }

    public async Task<string?> GenerateResetTokenAsync(string email, TimeSpan ttl)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user is null || !user.IsActive) return null;
        var token = _tokenStore.CreateToken(email, ttl);
        return token;
    }

    public async Task<AuthResult> CreateUserAsync(string email, string displayName, int role, string tempPassword, string location, string company, IEnumerable<string>? companies, string operation, string shiftTime, string? scheduleBlocksJson)
    {
        if (await _users.EmailExistsAsync(email))
        {
            return new AuthResult(false, "This email is already associated with an existing user.");
        }

        if (!IsPasswordValid(tempPassword))
        {
            return new AuthResult(false, "Password must be at least 8 characters and include uppercase, lowercase, number, and special character (!@#$%*).");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ObjectId = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            Role = role,
            IsActive = true,
            PasswordHash = _hasher.Hash(tempPassword),
            MustChangePassword = true,
            CreatedAtUtc = DateTime.UtcNow,
            Location = location,
            Company = company,
            CompanyScope = CompanyScopeHelpers.BuildCompanyScopeJson(companies, company),
            Operation = operation,
            ShiftTime = shiftTime,
            ScheduleBlocks = scheduleBlocksJson
        };

        var rows = await _users.CreateUserAsync(user);
        return rows > 0
            ? new AuthResult(true, null, email, displayName, role, true)
            : new AuthResult(false, "Could not create user.");
    }

    private async Task<AuthResult> TryCreateSeedAdminFromEntraAsync(Guid objectId, string email, string? displayName)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !MatchesSeedAdmin(objectId, normalizedEmail))
        {
            return new AuthResult(false, "This Microsoft account is not authorized to access ShiftTrack.");
        }

        var activeUsers = await _users.GetAllAsync();
        if (activeUsers.Any())
        {
            return new AuthResult(false, "This Microsoft account is not authorized to access ShiftTrack.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ObjectId = objectId,
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName.Trim(),
            Role = 2,
            IsActive = true,
            IsSystemHidden = true,
            PasswordHash = null,
            MustChangePassword = false,
            CreatedAtUtc = DateTime.UtcNow,
            Location = "COL",
            Company = "Solvo Global",
            CompanyScope = CompanyScopeHelpers.BuildCompanyScopeJson(new[] { "Solvo Global" }, "Solvo Global"),
            Operation = "Leaders",
            ShiftTime = "Morning",
            ScheduleBlocks = null
        };

        var rows = await _users.CreateUserAsync(user);
        return rows > 0
            ? new AuthResult(true, null, user.Email, user.DisplayName, user.Role)
            : new AuthResult(false, "Could not create seed admin.");
    }

    private async Task<AuthResult> CreateHiddenAdminFromEntraAsync(Guid objectId, string normalizedEmail, string? displayName)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ObjectId = objectId,
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName.Trim(),
            Role = RoleHelpers.Admin,
            IsActive = true,
            IsSystemHidden = true,
            PasswordHash = null,
            MustChangePassword = false,
            CreatedAtUtc = DateTime.UtcNow,
            Location = string.Empty,
            Company = string.Empty,
            CompanyScope = "[]",
            Operation = string.Empty,
            ShiftTime = string.Empty,
            ScheduleBlocks = null
        };

        var rows = await _users.CreateUserAsync(user);
        return rows > 0
            ? new AuthResult(true, null, user.Email, user.DisplayName, user.Role)
            : new AuthResult(false, "Could not create hidden admin.");
    }

    private async Task<int> EnsureHiddenAdminAsync(User user)
    {
        if (!user.IsSystemHidden)
        {
            await _users.SetSystemHiddenAsync(user.Id, true);
        }

        if (user.Role != RoleHelpers.Admin)
        {
            await _users.UpdateUserAsync(
                user.Id,
                string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName,
                RoleHelpers.Admin,
                user.Location,
                user.Company,
                user.CompanyScope,
                user.Operation,
                user.ShiftTime,
                user.ScheduleBlocks);
        }

        return RoleHelpers.Admin;
    }

    private bool MatchesSeedAdmin(Guid objectId, string normalizedEmail)
    {
        var matchesEmail = !string.IsNullOrWhiteSpace(_seedAdminEmail)
            && string.Equals(normalizedEmail, _seedAdminEmail, StringComparison.OrdinalIgnoreCase);
        var matchesObjectId = _seedAdminObjectId.HasValue && objectId == _seedAdminObjectId.Value;
        return matchesEmail || matchesObjectId;
    }

    private bool IsSystemHiddenAdmin(Guid objectId, string normalizedEmail) =>
        MatchesSeedAdmin(objectId, normalizedEmail) || MatchesConfiguredHiddenAdmin(normalizedEmail);

    private bool MatchesConfiguredHiddenAdmin(string normalizedEmail) =>
        _hiddenAdminEmails.Any(email => string.Equals(email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    private static string[] ParseEmailList(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<string>()
            : raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeEmail)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static bool IsPasswordValid(string password)
    {
        return !string.IsNullOrWhiteSpace(password) && Regex.IsMatch(password, PasswordPolicy);
    }

    public async Task<bool> CanDeleteAsync(int callerRole, Guid targetUserId)
    {
        if (!RoleHelpers.CanManageUsers(callerRole)) return false;
        var target = await _users.GetByIdAsync(targetUserId);
        if (target is null) return false;
        if (RoleHelpers.IsAdmin(callerRole)) return true;
        return RoleHelpers.CanManagerManageRole(target.Role);
    }
}
