namespace ShiftTrack.Api;

internal static class RequestExportEndpoints
{
    internal static WebApplication MapRequestExportEndpoints(this WebApplication app)
    {
        app.MapPost("/requests/exports", (HttpContext httpContext, IRequestExportWorkflowService workflow) =>
            workflow.StartAsync(httpContext))
        .WithName("StartRequestsExport")
        .WithOpenApi()
        .RequireAuthorization("ManagerOrAbove");

        app.MapGet("/requests/exports/{exportJobId:guid}", (HttpContext httpContext, Guid exportJobId, IRequestExportWorkflowService workflow) =>
            workflow.GetStatusAsync(httpContext, exportJobId))
        .WithName("GetRequestsExportStatus")
        .WithOpenApi()
        .RequireAuthorization("ManagerOrAbove");

        app.MapGet("/requests/exports/{exportJobId:guid}/download", (HttpContext httpContext, Guid exportJobId, IRequestExportWorkflowService workflow) =>
            workflow.DownloadAsync(httpContext, exportJobId))
        .WithName("DownloadRequestsExport")
        .WithOpenApi()
        .RequireAuthorization("ManagerOrAbove");

        return app;
    }
}
