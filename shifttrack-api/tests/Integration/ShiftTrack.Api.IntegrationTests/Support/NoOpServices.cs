using ShiftTrack.Application.Interfaces;
using ShiftTrack.Application;
using ShiftTrack.Application.Models;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api.IntegrationTests.Support;

internal sealed class NoOpEmailService : IEmailService
{
    public Task SendResetEmailAsync(string email, string? displayName, string resetLink) => Task.CompletedTask;
    public Task SendWelcomeEmailAsync(string email, string? displayName, string tempPassword, string resetLink) => Task.CompletedTask;
    public Task SendPtoApprovalEmailAsync(IEnumerable<string> recipients, string employeeName, string employeeEmail, string requestType, int numberOfDays, string startDate, string endDate, string? comments, string reviewLink) => Task.CompletedTask;
    public Task SendPtoApprovedEmailAsync(string recipientEmail, string employeeName, string requestType, int numberOfDays, string startDate, string endDate, string approvedByName, string? comments) => Task.CompletedTask;
    public Task SendPtoDeniedEmailAsync(string recipientEmail, string employeeName, string requestType, int numberOfDays, string startDate, string endDate, string deniedByName, string? comments) => Task.CompletedTask;
    public Task SendSwapApprovalEmailAsync(string email, string? displayName, string requesterName, string requesterEmail, string requestType, IEnumerable<string> scheduleLines, string? comments, string reviewLink) => Task.CompletedTask;
    public Task SendSwapRequestSubmittedEmailAsync(string recipientEmail, string recipientName, string targetName, string targetEmail, string requestType, IEnumerable<string> scheduleLines, string? comments) => Task.CompletedTask;
    public Task SendSwapApprovedEmailAsync(string recipientEmail, string recipientName, string approvedByName, string approvedByEmail, string requestType, IEnumerable<string> scheduleLines, string? comments) => Task.CompletedTask;
    public Task SendSwapApprovedSummaryEmailAsync(IEnumerable<string> recipients, string requesterName, string requesterEmail, string targetName, string targetEmail, string requestType, IEnumerable<string> scheduleLines, string? comments) => Task.CompletedTask;
    public Task SendSwapDeniedEmailAsync(string recipientEmail, string requesterName, string requestType, IEnumerable<string> scheduleLines, string deniedByName, string? comments) => Task.CompletedTask;
    public Task SendDailyScheduleChangedEmailAsync(string recipientEmail, string recipientName, string changedByName, string changedByEmail, string date, string startTime, string endTime, double durationHours, string comments) => Task.CompletedTask;
}

internal sealed class FakeAuthService : IAuthService
{
    private readonly InMemoryUserRepository _users;

    public FakeAuthService(InMemoryUserRepository users)
    {
        _users = users;
    }

    public Task<AuthResult> LoginAsync(string email, string password) =>
        Task.FromResult(new AuthResult(false, "Not implemented in tests."));

    public Task<AuthResult> LoginWithEntraAsync(Guid objectId, string email, string? displayName) =>
        Task.FromResult(new AuthResult(false, "Not implemented in tests."));

    public Task<AuthResult> ResetPasswordAsync(string email, string newPassword) =>
        Task.FromResult(new AuthResult(false, "Not implemented in tests."));

    public Task<AuthResult> ForceChangePasswordAsync(string email, string tokenOrPassword, string newPassword, bool isToken) =>
        Task.FromResult(new AuthResult(false, "Not implemented in tests."));

    public Task<string?> GenerateResetTokenAsync(string email, TimeSpan ttl) =>
        Task.FromResult<string?>("test-reset-token");

    public async Task<AuthResult> CreateUserAsync(string email, string displayName, int role, string tempPassword, string location, string company, IEnumerable<string>? companies, string operation, string shiftTime, string? scheduleBlocksJson)
    {
        if (await _users.EmailExistsAsync(email))
        {
            return new AuthResult(false, "This email is already associated with an existing user.");
        }

        await _users.CreateUserAsync(new User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ObjectId = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            Role = role,
            IsActive = true,
            PasswordHash = "hash",
            MustChangePassword = true,
            CreatedAtUtc = DateTime.UtcNow,
            Location = location,
            Company = company,
            CompanyScope = CompanyScopeHelpers.BuildCompanyScopeJson(companies, company),
            Operation = operation,
            ShiftTime = shiftTime,
            ScheduleBlocks = scheduleBlocksJson
        });

        return new AuthResult(true, null, email, displayName, role, true);
    }

    public async Task<bool> CanDeleteAsync(int callerRole, Guid targetUserId)
    {
        var target = await _users.GetByIdAsync(targetUserId);
        if (target is null || !RoleHelpers.CanManageUsers(callerRole)) return false;
        if (RoleHelpers.IsAdmin(callerRole)) return true;
        return RoleHelpers.CanManagerManageRole(target.Role);
    }
}
