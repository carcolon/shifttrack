using Microsoft.OpenApi.Models;

namespace ShiftTrack.Api;

internal static class ScheduleEndpoints
{
    internal static WebApplication MapScheduleEndpoints(this WebApplication app, string frontendBaseUrl)
    {
        app.MapPost("/calendar/pto", (HttpContext httpContext, UpsertPtoRequest request, IScheduleWorkflowService workflow) =>
            workflow.UpsertCalendarPtoAsync(httpContext, request))
        .WithName("UpsertCalendarPto")
        .WithOpenApi(op =>
        {
            op.Parameters.Add(new OpenApiParameter { Name = "X-Role", In = ParameterLocation.Header, Required = true, Description = "Caller role: 0=Employee, 1=Manager, 2=Admin, 3=Team Leader" });
            op.Parameters.Add(new OpenApiParameter { Name = "X-User-Email", In = ParameterLocation.Header, Required = false, Description = "Caller email (required for employee or team leader self-service)." });
            op.Parameters.Add(new OpenApiParameter { Name = "X-User-Name", In = ParameterLocation.Header, Required = false, Description = "Caller display name." });
            return op;
        })
        .RequireAuthorization("EmployeeOrAbove");

        app.MapPost("/calendar/day-schedule", (HttpContext httpContext, UpsertDailyScheduleRequest request, IScheduleWorkflowService workflow) =>
            workflow.UpsertDailyScheduleAsync(httpContext, request))
        .WithName("UpsertDailySchedule")
        .WithOpenApi()
        .RequireAuthorization("ManagerOrAbove");

        app.MapPost("/calendar/pto/coverage-preview", (HttpContext httpContext, UpsertPtoRequest request, IScheduleWorkflowService workflow) =>
            workflow.PreviewCalendarPtoCoverageAsync(httpContext, request))
        .WithName("PreviewCalendarPtoCoverage")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapGet("/pto/requests/{requestId:guid}", (HttpContext httpContext, Guid requestId, IScheduleWorkflowService workflow) =>
            workflow.GetPtoRequestAsync(httpContext, requestId))
        .WithName("GetPtoRequest")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapGet("/pto/requests/{requestId:guid}/coverage-preview", (HttpContext httpContext, Guid requestId, IScheduleWorkflowService workflow) =>
            workflow.GetPtoCoveragePreviewAsync(httpContext, requestId))
        .WithName("GetPtoCoveragePreview")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapGet("/pto/requests", (HttpContext httpContext, HttpRequest request, IScheduleWorkflowService workflow) =>
            workflow.GetPtoRequestsAsync(httpContext, request))
        .WithName("GetPtoRequests")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapPost("/pto/requests/{requestId:guid}/approve", (HttpContext httpContext, Guid requestId, ReviewRequest review, IScheduleWorkflowService workflow) =>
            workflow.ApprovePtoRequestAsync(httpContext, requestId, review))
        .WithName("ApprovePtoRequest")
        .WithOpenApi()
        .RequireAuthorization("ManagerOrAbove");

        app.MapPost("/pto/requests/{requestId:guid}/deny", (HttpContext httpContext, Guid requestId, ReviewRequest review, IScheduleWorkflowService workflow) =>
            workflow.DenyPtoRequestAsync(httpContext, requestId, review))
        .WithName("DenyPtoRequest")
        .WithOpenApi()
        .RequireAuthorization("ManagerOrAbove");

        app.MapPost("/pto/requests/{requestId:guid}/cancel", (HttpContext httpContext, Guid requestId, IScheduleWorkflowService workflow) =>
            workflow.CancelPtoRequestAsync(httpContext, requestId))
        .WithName("CancelPtoRequest")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapGet("/swap/candidates", (HttpContext httpContext, HttpRequest request, IScheduleWorkflowService workflow) =>
            workflow.GetSwapCandidatesAsync(httpContext, request))
        .WithName("GetSwapCandidates")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapPost("/swap/requests", (HttpContext httpContext, CreateSwapRequest request, IScheduleWorkflowService workflow) =>
            workflow.CreateSwapRequestAsync(httpContext, request))
        .WithName("CreateSwapRequest")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapGet("/swap/requests/{requestId:guid}", (HttpContext httpContext, Guid requestId, IScheduleWorkflowService workflow) =>
            workflow.GetSwapRequestAsync(httpContext, requestId))
        .WithName("GetSwapRequest")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapGet("/swap/requests", (HttpContext httpContext, HttpRequest request, IScheduleWorkflowService workflow) =>
            workflow.GetSwapRequestsAsync(httpContext, request))
        .WithName("GetSwapRequests")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapPost("/swap/requests/{requestId:guid}/approve", (HttpContext httpContext, Guid requestId, ReviewRequest review, IScheduleWorkflowService workflow) =>
            workflow.ApproveSwapRequestAsync(httpContext, requestId, review))
        .WithName("ApproveSwapRequest")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapPost("/swap/requests/{requestId:guid}/deny", (HttpContext httpContext, Guid requestId, ReviewRequest review, IScheduleWorkflowService workflow) =>
            workflow.DenySwapRequestAsync(httpContext, requestId, review))
        .WithName("DenySwapRequest")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapPost("/swap/requests/{requestId:guid}/cancel", (HttpContext httpContext, Guid requestId, IScheduleWorkflowService workflow) =>
            workflow.CancelSwapRequestAsync(httpContext, requestId))
        .WithName("CancelSwapRequest")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapGet("/calendar", (HttpContext httpContext, HttpRequest request, IScheduleWorkflowService workflow) =>
            workflow.GetCalendarAsync(httpContext, request))
        .WithName("GetCalendar")
        .WithOpenApi(op =>
        {
            op.Parameters.Add(new OpenApiParameter { Name = "weekStart", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string", Format = "date" } });
            op.Parameters.Add(new OpenApiParameter { Name = "employee", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string" } });
            op.Parameters.Add(new OpenApiParameter { Name = "role", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "integer" }, Description = "Role filter: 0=Employee, 1=Manager, 2=Admin, 3=Team Leader" });
            op.Parameters.Add(new OpenApiParameter { Name = "shift", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string" } });
            op.Parameters.Add(new OpenApiParameter { Name = "operation", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string" } });
            op.Parameters.Add(new OpenApiParameter { Name = "company", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string" } });
            op.Parameters.Add(new OpenApiParameter { Name = "X-Role", In = ParameterLocation.Header, Required = false, Description = "Optional role header for consistency" });
            return op;
        })
        .RequireAuthorization("EmployeeOrAbove");

        app.MapGet("/calendar/export", (HttpContext httpContext, HttpRequest request, IScheduleWorkflowService workflow) =>
            workflow.ExportCalendarAsync(httpContext, request))
        .WithName("ExportCalendar")
        .WithOpenApi(op =>
        {
            op.Parameters.Add(new OpenApiParameter { Name = "weekStart", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string", Format = "date" } });
            op.Parameters.Add(new OpenApiParameter { Name = "employee", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string" } });
            op.Parameters.Add(new OpenApiParameter { Name = "role", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "integer" }, Description = "Role filter: 0=Employee, 1=Manager, 2=Admin, 3=Team Leader" });
            op.Parameters.Add(new OpenApiParameter { Name = "shift", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string" } });
            op.Parameters.Add(new OpenApiParameter { Name = "operation", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string" } });
            op.Parameters.Add(new OpenApiParameter { Name = "company", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string" } });
            return op;
        })
        .RequireAuthorization("ManagerOrAbove");

        app.MapGet("/schedule/events", (HttpContext httpContext, int? take, IScheduleWorkflowService workflow) =>
            workflow.GetScheduleEventsAsync(httpContext, take))
        .WithName("GetScheduleEvents")
        .WithOpenApi(op =>
        {
            op.Parameters.Add(new OpenApiParameter { Name = "take", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "integer", Minimum = 1, Maximum = 100 } });
            op.Parameters.Add(new OpenApiParameter { Name = "X-Role", In = ParameterLocation.Header, Required = true, Description = "Caller role: 0=Employee, 1=Manager, 2=Admin, 3=Team Leader" });
            return op;
        })
        .RequireAuthorization("ManagerOrAbove");

        app.MapHub<ScheduleHub>("/hubs/schedule").RequireAuthorization("EmployeeOrAbove");
        return app;
    }
}
