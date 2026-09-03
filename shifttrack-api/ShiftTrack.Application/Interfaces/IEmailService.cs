using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShiftTrack.Application.Interfaces;

public interface IEmailService
{
    Task SendResetEmailAsync(string email, string? displayName, string resetLink);
    Task SendWelcomeEmailAsync(string email, string? displayName, string tempPassword, string resetLink);
    Task SendPtoApprovalEmailAsync(IEnumerable<string> recipients, string employeeName, string employeeEmail, string requestType, int numberOfDays, string startDate, string endDate, string? comments, string reviewLink);
    Task SendPtoApprovedEmailAsync(string recipientEmail, string employeeName, string requestType, int numberOfDays, string startDate, string endDate, string approvedByName, string? comments);
    Task SendPtoDeniedEmailAsync(string recipientEmail, string employeeName, string requestType, int numberOfDays, string startDate, string endDate, string deniedByName, string? comments);
    Task SendSwapApprovalEmailAsync(string recipientEmail, string recipientName, string requesterName, string requesterEmail, string requestType, IEnumerable<string> scheduleLines, string? comments, string reviewLink);
    Task SendSwapRequestSubmittedEmailAsync(string recipientEmail, string recipientName, string targetName, string targetEmail, string requestType, IEnumerable<string> scheduleLines, string? comments);
    Task SendSwapApprovedEmailAsync(string recipientEmail, string recipientName, string approvedByName, string approvedByEmail, string requestType, IEnumerable<string> scheduleLines, string? comments);
    Task SendSwapApprovedSummaryEmailAsync(IEnumerable<string> recipients, string requesterName, string requesterEmail, string targetName, string targetEmail, string requestType, IEnumerable<string> scheduleLines, string? comments);
    Task SendSwapDeniedEmailAsync(string recipientEmail, string requesterName, string requestType, IEnumerable<string> scheduleLines, string deniedByName, string? comments);
    Task SendDailyScheduleChangedEmailAsync(string recipientEmail, string recipientName, string changedByName, string changedByEmail, string date, string startTime, string endTime, double durationHours, string comments);
}
