using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Application.Models;

namespace ShiftTrack.Infrastructure.Services;

public class AcsEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<AcsEmailService> _logger;
    private const string InlineLogoContentId = "shifttrack-logo";

    public AcsEmailService(IOptions<EmailOptions> options, ILogger<AcsEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendResetEmailAsync(string email, string? displayName, string resetLink)
    {
        if (!IsConfigured()) return;

        try
        {
            var client = BuildClient();
            var from = _options.Email!;
            var subject = "Reset your ShiftTrack password";
            var template = ShiftTrackEmailTemplates.BuildResetEmail(displayName ?? email, resetLink);

            var message = BuildMessage(from, email, displayName, subject, template);
            await client.SendAsync(WaitUntil.Completed, message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to send reset email to {Email}. Sender {Sender}. Resource {Resource}.",
                email,
                _options.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reset email to {Email}", email);
        }
    }

    public async Task SendWelcomeEmailAsync(string email, string? displayName, string tempPassword, string resetLink)
    {
        if (!IsConfigured()) return;

        try
        {
            var client = BuildClient();
            var from = _options.Email!;
            var subject = "Welcome to ShiftTrack";
            var template = ShiftTrackEmailTemplates.BuildWelcomeEmail(displayName ?? email, tempPassword, resetLink);

            var message = BuildMessage(from, email, displayName, subject, template);
            await client.SendAsync(WaitUntil.Completed, message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to send welcome email to {Email}. Sender {Sender}. Resource {Resource}.",
                email,
                _options.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", email);
        }
    }

    public async Task SendPtoApprovalEmailAsync(IEnumerable<string> recipients, string employeeName, string employeeEmail, string requestType, int numberOfDays, string startDate, string endDate, string? comments, string reviewLink)
    {
        if (!IsConfigured()) return;

        var emails = recipients
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (emails.Length == 0) return;

        try
        {
            var client = BuildClient();
            var from = _options.Email!;
            var subject = "ShiftTrack PTO request pending approval";
            var template = ShiftTrackEmailTemplates.BuildPtoPendingEmail(employeeName, employeeEmail, requestType, numberOfDays, startDate, endDate, comments, reviewLink);
            var message = BuildBulkMessage(from, emails, subject, template);
            await client.SendAsync(WaitUntil.Completed, message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to send PTO approval email for {EmployeeEmail}. Sender {Sender}. Resource {Resource}.",
                employeeEmail,
                _options.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send PTO approval email for {EmployeeEmail}", employeeEmail);
        }
    }

    public async Task SendPtoApprovedEmailAsync(string recipientEmail, string employeeName, string requestType, int numberOfDays, string startDate, string endDate, string approvedByName, string? comments)
    {
        if (!IsConfigured() || string.IsNullOrWhiteSpace(recipientEmail)) return;

        try
        {
            var client = BuildClient();
            var from = _options.Email!;
            var subject = "Your ShiftTrack PTO request was approved";
            var template = ShiftTrackEmailTemplates.BuildPtoApprovedEmail(employeeName, requestType, numberOfDays, startDate, endDate, approvedByName, comments);

            var message = BuildMessage(from, recipientEmail.Trim(), employeeName, subject, template);
            await client.SendAsync(WaitUntil.Completed, message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to send PTO approved email to {Email}. Sender {Sender}. Resource {Resource}.",
                recipientEmail,
                _options.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send PTO approved email to {Email}", recipientEmail);
        }
    }

    public async Task SendPtoDeniedEmailAsync(string recipientEmail, string employeeName, string requestType, int numberOfDays, string startDate, string endDate, string deniedByName, string? comments)
    {
        if (!IsConfigured() || string.IsNullOrWhiteSpace(recipientEmail)) return;

        try
        {
            var client = BuildClient();
            var from = _options.Email!;
            var subject = "Your ShiftTrack PTO request was denied";
            var template = ShiftTrackEmailTemplates.BuildPtoDeniedEmail(employeeName, requestType, numberOfDays, startDate, endDate, deniedByName, comments);

            var message = BuildMessage(from, recipientEmail.Trim(), employeeName, subject, template);
            await client.SendAsync(WaitUntil.Completed, message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to send PTO denied email to {Email}. Sender {Sender}. Resource {Resource}.",
                recipientEmail,
                _options.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send PTO denied email to {Email}", recipientEmail);
        }
    }

    public async Task SendSwapApprovalEmailAsync(string recipientEmail, string recipientName, string requesterName, string requesterEmail, string requestType, IEnumerable<string> scheduleLines, string? comments, string reviewLink)
    {
        if (!IsConfigured() || string.IsNullOrWhiteSpace(recipientEmail)) return;

        try
        {
            var client = BuildClient();
            var from = _options.Email!;
            var subject = "ShiftTrack swap request pending your approval";
            var template = ShiftTrackEmailTemplates.BuildSwapPendingApprovalEmail(
                recipientName,
                requesterName,
                requesterEmail,
                requestType,
                scheduleLines,
                comments,
                reviewLink);

            var message = BuildMessage(from, recipientEmail.Trim(), recipientName, subject, template);
            await client.SendAsync(WaitUntil.Completed, message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to send swap approval email to {Email}. Sender {Sender}. Resource {Resource}.",
                recipientEmail,
                _options.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send swap approval email to {Email}", recipientEmail);
        }
    }

    public async Task SendSwapRequestSubmittedEmailAsync(string recipientEmail, string recipientName, string targetName, string targetEmail, string requestType, IEnumerable<string> scheduleLines, string? comments)
    {
        if (!IsConfigured() || string.IsNullOrWhiteSpace(recipientEmail)) return;

        try
        {
            var client = BuildClient();
            var from = _options.Email!;
            var subject = "Your ShiftTrack swap request is pending";
            var template = ShiftTrackEmailTemplates.BuildSwapPendingSubmittedEmail(
                recipientName,
                targetName,
                targetEmail,
                requestType,
                scheduleLines,
                comments);

            var message = BuildMessage(from, recipientEmail.Trim(), recipientName, subject, template);
            await client.SendAsync(WaitUntil.Completed, message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to send swap submitted email to {Email}. Sender {Sender}. Resource {Resource}.",
                recipientEmail,
                _options.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send swap submitted email to {Email}", recipientEmail);
        }
    }

    public async Task SendSwapApprovedEmailAsync(string recipientEmail, string recipientName, string approvedByName, string approvedByEmail, string requestType, IEnumerable<string> scheduleLines, string? comments)
    {
        if (!IsConfigured() || string.IsNullOrWhiteSpace(recipientEmail)) return;

        try
        {
            var client = BuildClient();
            var from = _options.Email!;
            var subject = "Your ShiftTrack swap request was approved";
            var template = ShiftTrackEmailTemplates.BuildSwapApprovedEmail(
                recipientName,
                approvedByName,
                approvedByEmail,
                requestType,
                scheduleLines,
                comments);

            var message = BuildMessage(from, recipientEmail.Trim(), recipientName, subject, template);
            await client.SendAsync(WaitUntil.Completed, message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to send swap approved email to {Email}. Sender {Sender}. Resource {Resource}.",
                recipientEmail,
                _options.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send swap approved email to {Email}", recipientEmail);
        }
    }

    public async Task SendSwapApprovedSummaryEmailAsync(IEnumerable<string> recipients, string requesterName, string requesterEmail, string targetName, string targetEmail, string requestType, IEnumerable<string> scheduleLines, string? comments)
    {
        if (!IsConfigured()) return;

        var emails = recipients
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (emails.Length == 0) return;

        try
        {
            var client = BuildClient();
            var from = _options.Email!;
            var subject = "ShiftTrack swap request approved";
            var template = ShiftTrackEmailTemplates.BuildSwapSummaryEmail(
                requesterName,
                requesterEmail,
                targetName,
                targetEmail,
                requestType,
                scheduleLines,
                comments);
            var message = BuildBulkMessage(from, emails, subject, template);
            await client.SendAsync(WaitUntil.Completed, message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to send swap approved summary emails. Sender {Sender}. Resource {Resource}.",
                _options.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send swap approved summary emails.");
        }
    }

    public async Task SendSwapDeniedEmailAsync(string recipientEmail, string requesterName, string requestType, IEnumerable<string> scheduleLines, string deniedByName, string? comments)
    {
        if (!IsConfigured() || string.IsNullOrWhiteSpace(recipientEmail)) return;

        try
        {
            var client = BuildClient();
            var from = _options.Email!;
            var subject = "Your ShiftTrack swap request was denied";
            var template = ShiftTrackEmailTemplates.BuildSwapDeniedEmail(
                requesterName,
                requestType,
                scheduleLines,
                deniedByName,
                comments);

            var message = BuildMessage(from, recipientEmail.Trim(), requesterName, subject, template);
            await client.SendAsync(WaitUntil.Completed, message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to send swap denied email to {Email}. Sender {Sender}. Resource {Resource}.",
                recipientEmail,
                _options.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send swap denied email to {Email}", recipientEmail);
        }
    }

    public async Task SendDailyScheduleChangedEmailAsync(string recipientEmail, string recipientName, string changedByName, string changedByEmail, string date, string startTime, string endTime, double durationHours, string comments)
    {
        if (!IsConfigured() || string.IsNullOrWhiteSpace(recipientEmail)) return;

        try
        {
            var client = BuildClient();
            var from = _options.Email!;
            var subject = "Your ShiftTrack schedule was updated";
            var template = ShiftTrackEmailTemplates.BuildDailyScheduleChangedEmail(
                recipientName,
                changedByName,
                changedByEmail,
                date,
                startTime,
                endTime,
                durationHours,
                comments);

            var message = BuildMessage(from, recipientEmail.Trim(), recipientName, subject, template);
            await client.SendAsync(WaitUntil.Started, message);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to enqueue daily schedule change email to {Email}. Sender {Sender}. Resource {Resource}.",
                recipientEmail,
                _options.Email,
                ResolveConfiguredResource());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue daily schedule change email to {Email}", recipientEmail);
        }
    }

    private bool IsConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Mode) || !_options.Mode.Equals("AccessKey", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Email mode not supported or not configured. Skipping email.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(_options.Email))
        {
            _logger.LogWarning("Email settings incomplete. Email sender missing. Skipping email.");
            return false;
        }

        var hasConnectionString = !string.IsNullOrWhiteSpace(_options.ConnectionString);
        var hasEndpointKey = !string.IsNullOrWhiteSpace(_options.Endpoint) && !string.IsNullOrWhiteSpace(_options.AccessKey);

        if (!hasConnectionString && !hasEndpointKey)
        {
            _logger.LogWarning("Email settings incomplete. Configure either ConnectionString or Endpoint/AccessKey plus Email. Skipping email.");
            return false;
        }

        return true;
    }

    private EmailClient BuildClient()
    {
        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            return new EmailClient(_options.ConnectionString);
        }

        return new EmailClient(new Uri(_options.Endpoint!), new AzureKeyCredential(_options.AccessKey!));
    }

    private EmailMessage BuildMessage(string from, string to, string? displayName, string subject, EmailTemplateContent template)
    {
        var content = new EmailContent(subject)
        {
            PlainText = template.PlainText,
            Html = template.Html
        };
        var recipients = new EmailRecipients(new[] { new EmailAddress(to, displayName) });
        var message = new EmailMessage(from, recipients, content);
        AttachInlineLogoIfAvailable(message);
        return message;
    }

    private EmailMessage BuildBulkMessage(string from, IEnumerable<string> recipients, string subject, EmailTemplateContent template)
    {
        var content = new EmailContent(subject)
        {
            PlainText = template.PlainText,
            Html = template.Html
        };
        var recipientList = recipients.Select(email => new EmailAddress(email)).ToArray();
        var message = new EmailMessage(from, new EmailRecipients(recipientList), content);
        AttachInlineLogoIfAvailable(message);
        return message;
    }

    private string ResolveConfiguredResource()
    {
        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            var endpointPart = _options.ConnectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(part => part.StartsWith("endpoint=", StringComparison.OrdinalIgnoreCase));

            return endpointPart ?? "[connection-string]";
        }

        return _options.Endpoint ?? "[unknown-endpoint]";
    }

    private void AttachInlineLogoIfAvailable(EmailMessage message)
    {
        var logoPath = ResolveLogoPath();
        if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
        {
            return;
        }

        var bytes = File.ReadAllBytes(logoPath);
        if (bytes.Length == 0)
        {
            return;
        }

        var attachment = new EmailAttachment(
            Path.GetFileName(logoPath),
            "image/png",
            BinaryData.FromBytes(bytes))
        {
            ContentId = InlineLogoContentId
        };

        message.Attachments.Add(attachment);
    }

    private string? ResolveLogoPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.LogoPath) && File.Exists(_options.LogoPath))
        {
            return _options.LogoPath;
        }

        var publishedLogoPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "logo-email.png");
        return File.Exists(publishedLogoPath) ? publishedLogoPath : null;
    }
}
