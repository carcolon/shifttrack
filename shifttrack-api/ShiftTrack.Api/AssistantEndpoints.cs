using Microsoft.OpenApi.Models;

namespace ShiftTrack.Api;

internal static class AssistantEndpoints
{
    internal static WebApplication MapAssistantEndpoints(this WebApplication app)
    {
        app.MapPost("/assistant/query", (HttpContext httpContext, AssistantQueryRequest request, IAssistantWorkflowService workflow) =>
            workflow.QueryAsync(httpContext, request))
        .WithName("AssistantQuery")
        .WithOpenApi(op =>
        {
            op.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Role",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Caller role inferred from the authenticated user."
            });
            return op;
        })
        .RequireAuthorization("EmployeeOrAbove");

        return app;
    }
}
