using System.Text.Json;
using Hangfire;
using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal interface IRequestExportWorkflowService
{
    Task<IResult> StartAsync(HttpContext httpContext);
    Task<IResult> GetStatusAsync(HttpContext httpContext, Guid exportJobId);
    Task<IResult> DownloadAsync(HttpContext httpContext, Guid exportJobId);
}

internal interface IRequestExportJobQueue
{
    bool IsEnabled { get; }
    string? Enqueue(Guid exportJobId);
}

internal sealed class HangfireRequestExportJobQueue(IBackgroundJobClient backgroundJobs) : IRequestExportJobQueue
{
    public bool IsEnabled => true;

    public string? Enqueue(Guid exportJobId) =>
        backgroundJobs.Enqueue<IRequestExportJobRunner>(runner => runner.RunAsync(exportJobId));
}

internal sealed class DisabledRequestExportJobQueue : IRequestExportJobQueue
{
    public bool IsEnabled => false;

    public string? Enqueue(Guid exportJobId) => null;
}

internal sealed class RequestExportWorkflowService(
    IRequestExportRepository exports,
    IRequestExportJobQueue queue) : IRequestExportWorkflowService
{
    public async Task<IResult> StartAsync(HttpContext httpContext)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanReviewPto(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!queue.IsEnabled)
        {
            return Results.Problem("Request export background jobs are not configured.");
        }

        var callerUser = await ResolveCallerUserAsync(callerContext);
        if (callerUser is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var job = new RequestExportJob
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = callerUser.Id,
            RequestedByEmail = callerUser.Email,
            RequestedByName = callerUser.DisplayName ?? callerUser.Email,
            RequestedByRole = callerUser.Role,
            RequestedByIsSystemHidden = callerUser.IsSystemHidden,
            ScopeCompaniesJson = JsonSerializer.Serialize(CompanyScopeHelpers.ResolveCompanies(callerUser)),
            Status = "pending",
            CreatedAtUtc = DateTime.UtcNow
        };

        await exports.CreateAsync(job);
        var hangfireJobId = queue.Enqueue(job.Id);
        if (string.IsNullOrWhiteSpace(hangfireJobId))
        {
            await exports.MarkFailedAsync(job.Id, "Request export background jobs are not configured.");
            return Results.Problem("Request export background jobs are not configured.");
        }

        await exports.SetHangfireJobIdAsync(job.Id, hangfireJobId);
        return Results.Accepted($"/requests/exports/{job.Id:D}", new RequestExportJobResponse
        {
            Id = job.Id,
            Status = "queued",
            CreatedAtUtc = job.CreatedAtUtc.ToString("O")
        });
    }

    public async Task<IResult> GetStatusAsync(HttpContext httpContext, Guid exportJobId)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanReviewPto(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var job = await exports.GetAsync(exportJobId);
        if (job is null)
        {
            return Results.NotFound(new ErrorResponse("Request export job not found."));
        }

        if (!CanAccessJob(callerContext, job))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return Results.Ok(ToResponse(job));
    }

    public async Task<IResult> DownloadAsync(HttpContext httpContext, Guid exportJobId)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext) || !RoleHelpers.CanReviewPto(callerContext.Role))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var job = await exports.GetAsync(exportJobId);
        if (job is null)
        {
            return Results.NotFound(new ErrorResponse("Request export job not found."));
        }

        if (!CanAccessJob(callerContext, job))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!string.Equals(job.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
            job.FileContent is null ||
            job.FileContent.Length == 0)
        {
            return Results.BadRequest(new ErrorResponse("Request export file is not ready yet."));
        }

        if (job.ExpiresAtUtc.HasValue && job.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            return Results.BadRequest(new ErrorResponse("Request export file has expired. Generate a new export."));
        }

        return Results.File(
            job.FileContent,
            string.IsNullOrWhiteSpace(job.ContentType) ? RequestExportWorkbookBuilder.ContentType : job.ContentType,
            string.IsNullOrWhiteSpace(job.FileName) ? "shifttrack-requests-export.xlsx" : job.FileName);
    }

    private async Task<User?> ResolveCallerUserAsync(CallerContext callerContext)
    {
        if (callerContext.UserId.HasValue)
        {
            var byId = await GetUserByIdAsync(callerContext.UserId.Value);
            if (byId is not null && byId.IsActive)
            {
                return byId;
            }
        }

        return string.IsNullOrWhiteSpace(callerContext.Email)
            ? null
            : await GetUserByEmailAsync(callerContext.Email);
    }

    private async Task<User?> GetUserByIdAsync(Guid id) =>
        (await exports.GetUsersForExportAsync()).FirstOrDefault(user => user.Id == id);

    private async Task<User?> GetUserByEmailAsync(string email) =>
        (await exports.GetUsersForExportAsync()).FirstOrDefault(user =>
            string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));

    private static bool CanAccessJob(CallerContext callerContext, RequestExportJob job)
    {
        if (callerContext.UserId.HasValue && job.RequestedByUserId == callerContext.UserId.Value)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(callerContext.Email) &&
               string.Equals(job.RequestedByEmail, callerContext.Email, StringComparison.OrdinalIgnoreCase);
    }

    private static RequestExportJobResponse ToResponse(RequestExportJob job) => new()
    {
        Id = job.Id,
        Status = job.Status,
        FileName = job.FileName,
        ErrorMessage = job.ErrorMessage,
        CreatedAtUtc = job.CreatedAtUtc.ToString("O"),
        StartedAtUtc = job.StartedAtUtc?.ToString("O"),
        CompletedAtUtc = job.CompletedAtUtc?.ToString("O"),
        ExpiresAtUtc = job.ExpiresAtUtc?.ToString("O"),
        DownloadUrl = string.Equals(job.Status, "completed", StringComparison.OrdinalIgnoreCase)
            ? $"/requests/exports/{job.Id:D}/download"
            : null
    };
}
