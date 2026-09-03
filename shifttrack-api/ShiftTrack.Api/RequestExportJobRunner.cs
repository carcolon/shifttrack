using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal interface IRequestExportJobRunner
{
    Task RunAsync(Guid exportJobId);
}

internal sealed class RequestExportJobRunner(
    IRequestExportRepository exports,
    IHubContext<ScheduleHub> hub,
    ILogger<RequestExportJobRunner> logger) : IRequestExportJobRunner
{
    private static readonly HashSet<string> ExportableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending",
        "approved",
        "denied",
        "canceled",
        "cancelled"
    };

    public async Task RunAsync(Guid exportJobId)
    {
        await exports.MarkProcessingAsync(exportJobId);

        try
        {
            var job = await exports.GetAsync(exportJobId);
            if (job is null)
            {
                logger.LogWarning("Request export job {ExportJobId} was not found.", exportJobId);
                return;
            }

            var scopeCompanies = DeserializeScopeCompanies(job.ScopeCompaniesJson);
            var users = (await exports.GetUsersForExportAsync()).ToArray();
            var scopedUserIds = users
                .Where(user => IsUserInScope(job, user, scopeCompanies))
                .Select(user => user.Id)
                .ToHashSet();

            var ptoRequests = (await exports.GetPtoRequestsForExportAsync())
                .Where(request => ExportableStatuses.Contains(request.Status))
                .Where(request => scopedUserIds.Contains(request.UserId))
                .ToArray();

            var ptoSheetRequests = ptoRequests
                .Where(request => !string.Equals(request.RequestType, "day_off", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var dayOffSheetRequests = ptoRequests
                .Where(request => string.Equals(request.RequestType, "day_off", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var swapRequests = (await exports.GetSwapRequestsForExportAsync())
                .Where(request => ExportableStatuses.Contains(request.Status))
                .Where(request => scopedUserIds.Contains(request.RequestedByUserId) && scopedUserIds.Contains(request.TargetUserId))
                .ToArray();

            var content = RequestExportWorkbookBuilder.Build(ptoSheetRequests, swapRequests, dayOffSheetRequests);
            var fileName = $"shifttrack-requests-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
            await exports.MarkCompletedAsync(
                exportJobId,
                fileName,
                RequestExportWorkbookBuilder.ContentType,
                content,
                DateTime.UtcNow.AddHours(48));
            await NotifyAsync(job.RequestedByEmail, new RequestExportJobResponse
            {
                Id = exportJobId,
                Status = "completed",
                FileName = fileName,
                CreatedAtUtc = job.CreatedAtUtc.ToString("O"),
                StartedAtUtc = job.StartedAtUtc?.ToString("O"),
                CompletedAtUtc = DateTime.UtcNow.ToString("O"),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(48).ToString("O"),
                DownloadUrl = $"/requests/exports/{exportJobId:D}/download"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Request export job {ExportJobId} failed.", exportJobId);
            await exports.MarkFailedAsync(exportJobId, ex.Message);
            var failedJob = await exports.GetAsync(exportJobId);
            if (failedJob is not null)
            {
                await NotifyAsync(failedJob.RequestedByEmail, new RequestExportJobResponse
                {
                    Id = exportJobId,
                    Status = "failed",
                    ErrorMessage = ex.Message,
                    CreatedAtUtc = failedJob.CreatedAtUtc.ToString("O"),
                    StartedAtUtc = failedJob.StartedAtUtc?.ToString("O"),
                    CompletedAtUtc = DateTime.UtcNow.ToString("O")
                });
            }
        }
    }

    private Task NotifyAsync(string email, RequestExportJobResponse response)
    {
        return string.IsNullOrWhiteSpace(email)
            ? Task.CompletedTask
            : hub.Clients.Group(ScheduleHub.RequestExportsGroup(email)).SendAsync("requests.export.status", response);
    }

    private static bool IsUserInScope(RequestExportJob job, User user, IReadOnlyCollection<string> scopeCompanies)
    {
        if (job.RequestedByIsSystemHidden)
        {
            return true;
        }

        if (scopeCompanies.Count == 0)
        {
            return false;
        }

        var userCompanies = CompanyScopeHelpers.ResolveCompanies(user);
        return userCompanies.Any(company => scopeCompanies.Contains(company, StringComparer.OrdinalIgnoreCase));
    }

    private static string[] DeserializeScopeCompanies(string? scopeCompaniesJson)
    {
        if (string.IsNullOrWhiteSpace(scopeCompaniesJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(scopeCompaniesJson) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
