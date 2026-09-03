using System.Net;
using System.Text;

namespace ShiftTrack.Infrastructure.Services;

internal sealed record EmailTemplateContent(string PlainText, string Html);

internal readonly record struct EmailFact(string Label, string Value);
internal readonly record struct EmailTone(
    string Name,
    string BadgeLabel,
    string BadgeBackground,
    string BadgeForeground,
    string CalloutBackground,
    string CalloutBorder,
    string CalloutTitleColor,
    string CalloutBodyColor);

internal static class ShiftTrackEmailTemplates
{
    private const string FooterText = "This is an automated ShiftTrack message for your scheduling workflow.";
    private static readonly EmailTone PendingTone = new(
        "pending",
        "Pending",
        "#e9f4ff",
        "#1456a3",
        "#f3f9ff",
        "#cfe3ff",
        "#5f82b0",
        "#24476f");
    private static readonly EmailTone ApprovedTone = new(
        "approved",
        "Approved",
        "#e8fff4",
        "#15724d",
        "#effff7",
        "#ccefdc",
        "#4f8d71",
        "#215a45");
    private static readonly EmailTone DeniedTone = new(
        "denied",
        "Denied",
        "#fff0f0",
        "#b33d54",
        "#fff6f6",
        "#f3d2d7",
        "#b06a78",
        "#6b3340");
    private static readonly EmailTone InfoTone = new(
        "info",
        "Info",
        "#eef2ff",
        "#4b53b5",
        "#f6f8ff",
        "#dbe1ff",
        "#6f7bc4",
        "#38448d");

    public static EmailTemplateContent BuildWelcomeEmail(string recipientName, string tempPassword, string resetLink) =>
        BuildTemplate(
            accentStart: "#2ea7ff",
            accentEnd: "#1a3c78",
            eyebrow: "Welcome aboard",
            title: "Your ShiftTrack account is ready",
            greeting: $"Hello {recipientName},",
            intro: "You have been invited to ShiftTrack. Use the temporary password below to access your account and complete your first-time password reset.",
            facts:
            [
                new("Temporary password", tempPassword),
                new("Next step", "Open the secure activation link below"),
                new("Security note", "You will be forced to create a new password after login")
            ],
            tone: InfoTone,
            scheduleLines: null,
            calloutTitle: "First-time access",
            calloutBody: "Use the secure button below to activate your account. If you were not expecting this invitation, ignore this email and contact an administrator.",
            ctaLabel: "Activate account",
            ctaUrl: resetLink);

    public static EmailTemplateContent BuildResetEmail(string recipientName, string resetLink) =>
        BuildTemplate(
            accentStart: "#ffb347",
            accentEnd: "#ff7a18",
            eyebrow: "Password reset",
            title: "Reset your ShiftTrack password",
            greeting: $"Hello {recipientName},",
            intro: "We received a request to reset your password. For security, this link should be used within 30 minutes.",
            facts:
            [
                new("Action", "Password reset requested"),
                new("Link validity", "30 minutes"),
                new("If this was not you", "Ignore this email and your password will remain unchanged")
            ],
            tone: InfoTone,
            scheduleLines: null,
            calloutTitle: "Secure access",
            calloutBody: "Use the button below to choose a new password. Avoid forwarding this message or sharing the link.",
            ctaLabel: "Reset password",
            ctaUrl: resetLink);

    public static EmailTemplateContent BuildPtoPendingEmail(
        string employeeName,
        string employeeEmail,
        string requestType,
        int numberOfDays,
        string startDate,
        string endDate,
        string? comments,
        string reviewLink) =>
        BuildTemplate(
            accentStart: "#4f8cff",
            accentEnd: "#1f4fcb",
            eyebrow: "PTO pending review",
            title: "A PTO request needs your approval",
            greeting: "Hello,",
            intro: "A new PTO request was submitted in ShiftTrack and is waiting for review.",
            facts:
            [
                new("Employee", employeeName),
                new("Email", employeeEmail),
                new("Request type", requestType),
                new("Days requested", numberOfDays.ToString()),
                new("Start date", startDate),
                new("End date", endDate),
                new("Comments", string.IsNullOrWhiteSpace(comments) ? "N/A" : comments.Trim())
            ],
            tone: PendingTone,
            scheduleLines: null,
            calloutTitle: "Review required",
            calloutBody: "Open the review page to approve or deny the request.",
            ctaLabel: "Review PTO request",
            ctaUrl: reviewLink);

    public static EmailTemplateContent BuildPtoApprovedEmail(
        string employeeName,
        string requestType,
        int numberOfDays,
        string startDate,
        string endDate,
        string approvedByName,
        string? comments) =>
        BuildTemplate(
            accentStart: "#24c58b",
            accentEnd: "#157f63",
            eyebrow: "PTO approved",
            title: "Your PTO request was approved",
            greeting: $"Hello {employeeName},",
            intro: "Good news. Your PTO request was approved and the schedule has been updated accordingly.",
            facts:
            [
                new("Request type", requestType),
                new("Days approved", numberOfDays.ToString()),
                new("Start date", startDate),
                new("End date", endDate),
                new("Approved by", approvedByName),
                new("Comments", string.IsNullOrWhiteSpace(comments) ? "N/A" : comments.Trim())
            ],
            tone: ApprovedTone,
            scheduleLines: null,
            calloutTitle: "What happens now",
            calloutBody: "Your calendar should already reflect the approved PTO range.",
            ctaLabel: null,
            ctaUrl: null);

    public static EmailTemplateContent BuildPtoDeniedEmail(
        string employeeName,
        string requestType,
        int numberOfDays,
        string startDate,
        string endDate,
        string deniedByName,
        string? comments) =>
        BuildTemplate(
            accentStart: "#ff7d7d",
            accentEnd: "#d9485f",
            eyebrow: "PTO denied",
            title: "Your PTO request was denied",
            greeting: $"Hello {employeeName},",
            intro: "Your PTO request was reviewed and denied.",
            facts:
            [
                new("Request type", requestType),
                new("Days requested", numberOfDays.ToString()),
                new("Start date", startDate),
                new("End date", endDate),
                new("Reviewed by", deniedByName),
                new("Comments", string.IsNullOrWhiteSpace(comments) ? "N/A" : comments.Trim())
            ],
            tone: DeniedTone,
            scheduleLines: null,
            calloutTitle: "Next step",
            calloutBody: "If you need more context, contact your manager or administrator in ShiftTrack.",
            ctaLabel: null,
            ctaUrl: null);

    public static EmailTemplateContent BuildSwapPendingApprovalEmail(
        string recipientName,
        string requesterName,
        string requesterEmail,
        string requestType,
        IEnumerable<string> scheduleLines,
        string? comments,
        string reviewLink) =>
        BuildTemplate(
            accentStart: "#29b6f6",
            accentEnd: "#1565c0",
            eyebrow: "Day off request pending",
            title: "A schedule swap needs your approval",
            greeting: $"Hello {recipientName},",
            intro: "A coworker submitted a day off request that requires your review in ShiftTrack.",
            facts:
            [
                new("Requested by", requesterName),
                new("Requester email", requesterEmail),
                new("Request type", requestType),
                new("Status", "Pending your approval")
            ],
            tone: PendingTone,
            scheduleLines: scheduleLines,
            calloutTitle: "Observations",
            calloutBody: string.IsNullOrWhiteSpace(comments) ? "No additional observations were included." : comments.Trim(),
            ctaLabel: "Review request",
            ctaUrl: reviewLink);

    public static EmailTemplateContent BuildSwapPendingSubmittedEmail(
        string recipientName,
        string targetName,
        string targetEmail,
        string requestType,
        IEnumerable<string> scheduleLines,
        string? comments) =>
        BuildTemplate(
            accentStart: "#7c4dff",
            accentEnd: "#4527a0",
            eyebrow: "Request submitted",
            title: "Your day off request is pending",
            greeting: $"Hello {recipientName},",
            intro: "Your request was created successfully. ShiftTrack is now waiting for the selected coworker to review it.",
            facts:
            [
                new("Swap with", $"{targetName} ({targetEmail})"),
                new("Request type", requestType),
                new("Status", "Pending")
            ],
            tone: PendingTone,
            scheduleLines: scheduleLines,
            calloutTitle: "Observations",
            calloutBody: string.IsNullOrWhiteSpace(comments) ? "No additional observations were included." : comments.Trim(),
            ctaLabel: null,
            ctaUrl: null);

    public static EmailTemplateContent BuildSwapApprovedEmail(
        string recipientName,
        string approvedByName,
        string approvedByEmail,
        string requestType,
        IEnumerable<string> scheduleLines,
        string? comments) =>
        BuildTemplate(
            accentStart: "#24c58b",
            accentEnd: "#157f63",
            eyebrow: "Request approved",
            title: "Your day off request was approved",
            greeting: $"Hello {recipientName},",
            intro: "The request was approved and the affected schedules were updated in ShiftTrack.",
            facts:
            [
                new("Approved by", $"{approvedByName} ({approvedByEmail})"),
                new("Request type", requestType),
                new("Status", "Approved")
            ],
            tone: ApprovedTone,
            scheduleLines: scheduleLines,
            calloutTitle: "Observations",
            calloutBody: string.IsNullOrWhiteSpace(comments) ? "No additional observations were included." : comments.Trim(),
            ctaLabel: null,
            ctaUrl: null);

    public static EmailTemplateContent BuildSwapDeniedEmail(
        string requesterName,
        string requestType,
        IEnumerable<string> scheduleLines,
        string deniedByName,
        string? comments) =>
        BuildTemplate(
            accentStart: "#ff7d7d",
            accentEnd: "#d9485f",
            eyebrow: "Request denied",
            title: "Your day off request was denied",
            greeting: $"Hello {requesterName},",
            intro: "The selected coworker reviewed your request and denied it.",
            facts:
            [
                new("Reviewed by", deniedByName),
                new("Request type", requestType),
                new("Status", "Denied")
            ],
            tone: DeniedTone,
            scheduleLines: scheduleLines,
            calloutTitle: "Observations",
            calloutBody: string.IsNullOrWhiteSpace(comments) ? "No additional observations were included." : comments.Trim(),
            ctaLabel: null,
            ctaUrl: null);

    public static EmailTemplateContent BuildSwapSummaryEmail(
        string requesterName,
        string requesterEmail,
        string targetName,
        string targetEmail,
        string requestType,
        IEnumerable<string> scheduleLines,
        string? comments) =>
        BuildTemplate(
            accentStart: "#00bcd4",
            accentEnd: "#006c84",
            eyebrow: "Approved summary",
            title: "A ShiftTrack day off request was approved",
            greeting: "Hello,",
            intro: "This is the final approved summary for a day off request that changed the schedule.",
            facts:
            [
                new("Requester", $"{requesterName} ({requesterEmail})"),
                new("Approved by", $"{targetName} ({targetEmail})"),
                new("Request type", requestType),
                new("Status", "Approved")
            ],
            tone: ApprovedTone,
            scheduleLines: scheduleLines,
            calloutTitle: "Observations",
            calloutBody: string.IsNullOrWhiteSpace(comments) ? "No additional observations were included." : comments.Trim(),
            ctaLabel: null,
            ctaUrl: null);

    public static EmailTemplateContent BuildDailyScheduleChangedEmail(
        string recipientName,
        string changedByName,
        string changedByEmail,
        string date,
        string startTime,
        string endTime,
        double durationHours,
        string comments) =>
        BuildTemplate(
            accentStart: "#29b6f6",
            accentEnd: "#1565c0",
            eyebrow: "Schedule updated",
            title: "Your schedule was changed for one day",
            greeting: $"Hello {recipientName},",
            intro: "A manager or administrator updated your schedule for a specific day in ShiftTrack.",
            facts:
            [
                new("Date", date),
                new("New schedule", $"{startTime} - {endTime}"),
                new("Duration", $"{durationHours:0.##} hours"),
                new("Changed by", $"{changedByName} ({changedByEmail})")
            ],
            tone: InfoTone,
            scheduleLines: null,
            calloutTitle: "Comments",
            calloutBody: string.IsNullOrWhiteSpace(comments) ? "No comments were included." : comments.Trim(),
            ctaLabel: null,
            ctaUrl: null);

    private static EmailTemplateContent BuildTemplate(
        string accentStart,
        string accentEnd,
        string eyebrow,
        string title,
        string greeting,
        string intro,
        IEnumerable<EmailFact>? facts,
        EmailTone tone,
        IEnumerable<string>? scheduleLines,
        string calloutTitle,
        string calloutBody,
        string? ctaLabel,
        string? ctaUrl)
    {
        var factList = facts?.Where(item => !string.IsNullOrWhiteSpace(item.Value)).ToArray() ?? Array.Empty<EmailFact>();
        var lines = scheduleLines?.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToArray() ?? Array.Empty<string>();
        return new EmailTemplateContent(
            BuildPlainText(greeting, title, intro, factList, tone, lines, calloutTitle, calloutBody, ctaLabel, ctaUrl),
            BuildHtml(accentStart, accentEnd, eyebrow, title, greeting, intro, factList, tone, lines, calloutTitle, calloutBody, ctaLabel, ctaUrl));
    }

    private static string BuildPlainText(
        string greeting,
        string title,
        string intro,
        IReadOnlyList<EmailFact> facts,
        EmailTone tone,
        IReadOnlyList<string> lines,
        string calloutTitle,
        string calloutBody,
        string? ctaLabel,
        string? ctaUrl)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"ShiftTrack | {title}");
        builder.AppendLine();
        builder.AppendLine($"Status: {tone.BadgeLabel}");
        builder.AppendLine();
        builder.AppendLine(greeting);
        builder.AppendLine();
        builder.AppendLine(intro);
        builder.AppendLine();
        foreach (var fact in facts) builder.AppendLine($"{fact.Label}: {fact.Value}");
        if (lines.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Schedule details:");
            foreach (var line in lines) builder.AppendLine($"- {line}");
        }
        builder.AppendLine();
        builder.AppendLine($"{calloutTitle}: {calloutBody}");
        if (!string.IsNullOrWhiteSpace(ctaLabel) && !string.IsNullOrWhiteSpace(ctaUrl))
        {
            builder.AppendLine();
            builder.AppendLine($"{ctaLabel}: {ctaUrl}");
        }
        builder.AppendLine();
        builder.AppendLine(FooterText);
        return builder.ToString().Trim();
    }

    private static string BuildHtml(
        string accentStart,
        string accentEnd,
        string eyebrow,
        string title,
        string greeting,
        string intro,
        IReadOnlyList<EmailFact> facts,
        EmailTone tone,
        IReadOnlyList<string> lines,
        string calloutTitle,
        string calloutBody,
        string? ctaLabel,
        string? ctaUrl)
    {
        var html = new StringBuilder();
        html.Append("""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <meta name="color-scheme" content="light" />
  <meta name="supported-color-schemes" content="light" />
  <title>ShiftTrack Notification</title>
</head>
<body style="margin:0;padding:0;background-color:#eef4ff;font-family:Segoe UI,Arial,sans-serif;color:#153257;">
  <div style="display:none;max-height:0;overflow:hidden;opacity:0;">
""");
        html.Append(HtmlEncode($"{title}. {intro}"));
        html.Append("""
  </div>
  <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:#eef4ff;margin:0;padding:32px 12px;">
    <tr>
      <td align="center">
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="max-width:680px;background:#ffffff;border-radius:28px;overflow:hidden;box-shadow:0 20px 60px rgba(18,53,99,0.18);">
          <tr>
            <td style="padding:0;">
""");
        html.Append($"""
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" bgcolor="#eef5ff" style="background-color:#eef5ff;border-top:6px solid {accentStart};">
                <tr>
                  <td style="padding:28px 32px;">
                    <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%">
                      <tr>
                        <td valign="middle" style="width:92px;">
                          <img src="cid:shifttrack-logo" alt="ShiftTrack logo" width="72" height="72" style="display:block;width:72px;height:72px;border:0;outline:none;text-decoration:none;" />
                        </td>
                        <td valign="middle">
                          <div style="display:inline-block;padding:8px 14px;border-radius:999px;background:#dceaff;font-size:12px;line-height:1;font-weight:700;letter-spacing:0.18em;text-transform:uppercase;color:#5b7fae;">{HtmlEncode(eyebrow)}</div>
                          <div style="margin-top:16px;font-size:15px;line-height:1.2;font-weight:800;letter-spacing:0.08em;text-transform:uppercase;color:#3f70b7;">ShiftTrack</div>
                          <div style="margin-top:16px;">
                            <span style="display:inline-block;padding:8px 14px;border-radius:999px;background:{tone.BadgeBackground};color:{tone.BadgeForeground};font-size:13px;line-height:1;font-weight:800;letter-spacing:0.08em;text-transform:uppercase;">
                              {HtmlEncode(tone.BadgeLabel)}
                            </span>
                          </div>
                          <div style="margin-top:16px;font-size:36px;line-height:1.1;font-weight:800;color:#173860;">{HtmlEncode(title)}</div>
                          <div style="margin-top:12px;font-size:16px;line-height:1.7;color:#4a6a93;">{HtmlEncode(intro)}</div>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
          <tr>
            <td style="padding:32px;">
              <div style="font-size:20px;line-height:1.5;font-weight:700;color:#143256;">{HtmlEncode(greeting)}</div>
""");

        if (facts.Count > 0)
        {
            html.Append("""
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="margin-top:24px;border-collapse:separate;border-spacing:0 12px;">
""");
            foreach (var fact in facts)
            {
                html.Append($"""
                <tr>
                  <td style="padding:18px 20px;border:1px solid #d7e6ff;border-radius:18px;background:#f8fbff;">
                    <div style="font-size:11px;line-height:1.2;font-weight:800;letter-spacing:0.14em;text-transform:uppercase;color:#6d84ad;">{HtmlEncode(fact.Label)}</div>
                    <div style="margin-top:8px;font-size:18px;line-height:1.45;font-weight:700;color:#173860;">{HtmlEncode(fact.Value)}</div>
                  </td>
                </tr>
""");
            }
            html.Append("</table>");
        }

        if (lines.Count > 0)
        {
            html.Append("""
              <div style="margin-top:24px;padding:22px 24px;border-radius:22px;background:#102a55;">
                <div style="font-size:11px;line-height:1.2;font-weight:800;letter-spacing:0.14em;text-transform:uppercase;color:#8dc9ff;">Schedule details</div>
""");
            foreach (var line in lines)
            {
                html.Append($"""
                <div style="margin-top:12px;padding:14px 16px;border-radius:16px;background:rgba(255,255,255,0.08);font-size:15px;line-height:1.6;color:#f2f7ff;">{HtmlEncode(line)}</div>
""");
            }
            html.Append("</div>");
        }

        html.Append($"""
              <div style="margin-top:24px;padding:22px 24px;border-radius:22px;background:{tone.CalloutBackground};border:1px solid {tone.CalloutBorder};">
                <div style="font-size:11px;line-height:1.2;font-weight:800;letter-spacing:0.14em;text-transform:uppercase;color:{tone.CalloutTitleColor};">{HtmlEncode(calloutTitle)}</div>
                <div style="margin-top:10px;font-size:16px;line-height:1.7;color:{tone.CalloutBodyColor};">{HtmlEncode(calloutBody)}</div>
              </div>
""");

        if (!string.IsNullOrWhiteSpace(ctaLabel) && !string.IsNullOrWhiteSpace(ctaUrl))
        {
            html.Append($"""
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin-top:28px;">
                <tr>
                  <td align="center" bgcolor="{HtmlEncode(accentStart)}" style="border-radius:16px;">
                    <a href="{HtmlEncode(ctaUrl)}" style="display:inline-block;padding:16px 28px;font-size:16px;line-height:1.2;font-weight:800;color:#ffffff;text-decoration:none;">{HtmlEncode(ctaLabel)}</a>
                  </td>
                </tr>
              </table>
              <div style="margin-top:12px;font-size:12px;line-height:1.6;color:#6d84ad;">If the button does not open, copy and paste this link into your browser:<br /><a href="{HtmlEncode(ctaUrl)}" style="color:#1f70d8;text-decoration:none;">{HtmlEncode(ctaUrl)}</a></div>
""");
        }

        html.Append($"""
              <div style="margin-top:28px;padding-top:20px;border-top:1px solid #e2ecff;font-size:12px;line-height:1.7;color:#7389af;">{HtmlEncode(FooterText)}</div>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>
""");

        return html.ToString();
    }

    private static string HtmlEncode(string value) => WebUtility.HtmlEncode(value);
}
