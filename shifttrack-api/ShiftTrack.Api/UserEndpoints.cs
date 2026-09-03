using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace ShiftTrack.Api;

internal static class UserEndpoints
{
    internal static WebApplication MapUserEndpoints(this WebApplication app, string frontendBaseUrl)
    {
        app.MapPost("/users", (HttpContext httpContext, CreateUserRequest request, IUserWorkflowService workflow) =>
            workflow.CreateUserAsync(httpContext, request))
        .WithName("CreateUser")
        .WithOpenApi()
        .RequireAuthorization("ManagerOrAbove");

        app.MapPost("/users/bulk-upload", (HttpContext httpContext, IFormFile file, IUserWorkflowService workflow) =>
            workflow.BulkUploadUsersAsync(httpContext, file))
        .WithName("BulkUploadUsers")
        .DisableAntiforgery()
        .WithOpenApi(op =>
        {
            AddRoleHeader(op, "Caller role: 1=Manager, 2=Admin");
            return op;
        })
        .RequireAuthorization("ManagerOrAbove");

        app.MapGet("/users", (HttpContext httpContext, IUserWorkflowService workflow) =>
            workflow.ListUsersAsync(httpContext, inactiveOnly: false))
        .WithName("ListUsers")
        .WithOpenApi(op =>
        {
            AddRoleHeader(op, "Caller role: 0=Employee, 1=Manager, 2=Admin, 3=Team Leader");
            return op;
        })
        .RequireAuthorization("ManagerOrAbove");

        app.MapGet("/users/inactive", (HttpContext httpContext, IUserWorkflowService workflow) =>
            workflow.ListUsersAsync(httpContext, inactiveOnly: true))
        .WithName("ListInactiveUsers")
        .WithOpenApi(op =>
        {
            AddRoleHeader(op, "Caller role: 0=Employee, 1=Manager, 2=Admin, 3=Team Leader");
            return op;
        })
        .RequireAuthorization("ManagerOrAbove");

        app.MapGet("/users/system-hidden", (HttpContext httpContext, IUserWorkflowService workflow) =>
            workflow.ListSystemHiddenUsersAsync(httpContext))
        .WithName("ListSystemHiddenUsers")
        .WithOpenApi(op =>
        {
            AddRoleHeader(op, "Caller role: 2=Admin with IsSystemHidden=true");
            return op;
        })
        .RequireAuthorization("AdminOnly");

        app.MapPut("/users/{id:guid}", (HttpContext httpContext, Guid id, UpdateUserRequest request, IUserWorkflowService workflow) =>
            workflow.UpdateUserAsync(httpContext, id, request))
        .WithName("UpdateUser")
        .WithOpenApi(op =>
        {
            AddRoleHeader(op, "Caller role: 1=Manager, 2=Admin");
            return op;
        })
        .RequireAuthorization("ManagerOrAbove");

        app.MapDelete("/users/{id:guid}", (HttpContext httpContext, Guid id, IUserWorkflowService workflow) =>
            workflow.DeleteUserAsync(httpContext, id))
        .WithName("DeleteUser")
        .WithOpenApi(op =>
        {
            AddRoleHeader(op, "Caller role: 1=Manager, 2=Admin");
            return op;
        })
        .RequireAuthorization("ManagerOrAbove");

        app.MapPut("/users/{id:guid}/reactivate", (HttpContext httpContext, Guid id, IUserWorkflowService workflow) =>
            workflow.ReactivateUserAsync(httpContext, id))
        .WithName("ReactivateUser")
        .WithOpenApi(op =>
        {
            AddRoleHeader(op, "Caller role: 1=Manager, 2=Admin");
            return op;
        })
        .RequireAuthorization("ManagerOrAbove");

        app.MapDelete("/users/{id:guid}/purge", (HttpContext httpContext, Guid id, IUserWorkflowService workflow) =>
            workflow.PurgeUserAsync(httpContext, id))
        .WithName("PurgeUser")
        .WithOpenApi(op =>
        {
            AddRoleHeader(op, "Caller role: 1=Manager, 2=Admin");
            return op;
        })
        .RequireAuthorization("ManagerOrAbove");

        return app;
    }
    private static void AddRoleHeader(OpenApiOperation operation, string description)
    {
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Role",
            In = ParameterLocation.Header,
            Required = true,
            Description = description,
            Schema = new OpenApiSchema { Type = "string", Example = new OpenApiString("2") }
        });
    }
}
