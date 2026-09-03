using ShiftTrack.Application.Interfaces;

namespace ShiftTrack.Tests.Shared.Mocks;

public sealed class FakeEmailService : IEmailService
{
    public List<EmailMessageRecord> Sent { get; } = new();

    public Task SendResetEmailAsync(string email, string? displayName, string resetLink)
        => Record("reset", [email], displayName, new Dictionary<string, object?> { ["resetLink"] = resetLink });

    public Task SendWelcomeEmailAsync(string email, string? displayName, string tempPassword, string resetLink)
        => Record("welcome", [email], displayName, new Dictionary<string, object?> { ["tempPassword"] = tempPassword, ["resetLink"] = resetLink });

    public Task SendPtoApprovalEmailAsync(IEnumerable<string> recipients, string employeeName, string employeeEmail, string requestType, int numberOfDays, string startDate, string endDate, string? comments, string reviewLink)
        => Record("pto-approval", recipients, employeeName, new Dictionary<string, object?>
        {
            ["employeeEmail"] = employeeEmail,
            ["requestType"] = requestType,
            ["numberOfDays"] = numberOfDays,
            ["startDate"] = startDate,
            ["endDate"] = endDate,
            ["comments"] = comments,
            ["reviewLink"] = reviewLink
        });

    public Task SendPtoApprovedEmailAsync(string recipientEmail, string employeeName, string requestType, int numberOfDays, string startDate, string endDate, string approvedByName, string? comments)
        => Record("pto-approved", [recipientEmail], employeeName, new Dictionary<string, object?>
        {
            ["requestType"] = requestType,
            ["numberOfDays"] = numberOfDays,
            ["startDate"] = startDate,
            ["endDate"] = endDate,
            ["approvedByName"] = approvedByName,
            ["comments"] = comments
        });

    public Task SendPtoDeniedEmailAsync(string recipientEmail, string employeeName, string requestType, int numberOfDays, string startDate, string endDate, string deniedByName, string? comments)
        => Record("pto-denied", [recipientEmail], employeeName, new Dictionary<string, object?>
        {
            ["requestType"] = requestType,
            ["numberOfDays"] = numberOfDays,
            ["startDate"] = startDate,
            ["endDate"] = endDate,
            ["deniedByName"] = deniedByName,
            ["comments"] = comments
        });

    public Task SendSwapApprovalEmailAsync(string recipientEmail, string recipientName, string requesterName, string requesterEmail, string requestType, IEnumerable<string> scheduleLines, string? comments, string reviewLink)
        => Record("swap-approval", [recipientEmail], recipientName, new Dictionary<string, object?>
        {
            ["requesterName"] = requesterName,
            ["requesterEmail"] = requesterEmail,
            ["requestType"] = requestType,
            ["scheduleLines"] = scheduleLines.ToArray(),
            ["comments"] = comments,
            ["reviewLink"] = reviewLink
        });

    public Task SendSwapRequestSubmittedEmailAsync(string recipientEmail, string recipientName, string targetName, string targetEmail, string requestType, IEnumerable<string> scheduleLines, string? comments)
        => Record("swap-submitted", [recipientEmail], recipientName, new Dictionary<string, object?>
        {
            ["targetName"] = targetName,
            ["targetEmail"] = targetEmail,
            ["requestType"] = requestType,
            ["scheduleLines"] = scheduleLines.ToArray(),
            ["comments"] = comments
        });

    public Task SendSwapApprovedEmailAsync(string recipientEmail, string recipientName, string approvedByName, string approvedByEmail, string requestType, IEnumerable<string> scheduleLines, string? comments)
        => Record("swap-approved", [recipientEmail], recipientName, new Dictionary<string, object?>
        {
            ["approvedByName"] = approvedByName,
            ["approvedByEmail"] = approvedByEmail,
            ["requestType"] = requestType,
            ["scheduleLines"] = scheduleLines.ToArray(),
            ["comments"] = comments
        });

    public Task SendSwapApprovedSummaryEmailAsync(IEnumerable<string> recipients, string requesterName, string requesterEmail, string targetName, string targetEmail, string requestType, IEnumerable<string> scheduleLines, string? comments)
        => Record("swap-approved-summary", recipients, requesterName, new Dictionary<string, object?>
        {
            ["requesterEmail"] = requesterEmail,
            ["targetName"] = targetName,
            ["targetEmail"] = targetEmail,
            ["requestType"] = requestType,
            ["scheduleLines"] = scheduleLines.ToArray(),
            ["comments"] = comments
        });

    public Task SendSwapDeniedEmailAsync(string recipientEmail, string requesterName, string requestType, IEnumerable<string> scheduleLines, string deniedByName, string? comments)
        => Record("swap-denied", [recipientEmail], requesterName, new Dictionary<string, object?>
        {
            ["requestType"] = requestType,
            ["scheduleLines"] = scheduleLines.ToArray(),
            ["deniedByName"] = deniedByName,
            ["comments"] = comments
        });

    public Task SendDailyScheduleChangedEmailAsync(string recipientEmail, string recipientName, string changedByName, string changedByEmail, string date, string startTime, string endTime, double durationHours, string comments)
        => Record("daily-schedule-changed", [recipientEmail], recipientName, new Dictionary<string, object?>
        {
            ["changedByName"] = changedByName,
            ["changedByEmail"] = changedByEmail,
            ["date"] = date,
            ["startTime"] = startTime,
            ["endTime"] = endTime,
            ["durationHours"] = durationHours,
            ["comments"] = comments
        });

    public void Clear() => Sent.Clear();

    private Task Record(string kind, IEnumerable<string> recipients, string? displayName, IReadOnlyDictionary<string, object?> payload)
    {
        Sent.Add(new EmailMessageRecord(kind, recipients.ToArray(), displayName, payload));
        return Task.CompletedTask;
    }
}

public sealed record EmailMessageRecord(
    string Kind,
    IReadOnlyList<string> Recipients,
    string? DisplayName,
    IReadOnlyDictionary<string, object?> Payload);
