using Microsoft.OpenApi.Models;

namespace ShiftTrack.Api;

internal static class HolidayEndpoints
{
    internal static WebApplication MapHolidayEndpoints(this WebApplication app)
    {
        app.MapGet("/holidays", (HttpRequest request, IHolidayWorkflowService workflow) =>
            workflow.GetHolidaysAsync(request))
        .WithName("GetHolidays")
        .WithOpenApi(op =>
        {
            op.Parameters.Add(new OpenApiParameter { Name = "year", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "integer" } });
            op.Parameters.Add(new OpenApiParameter { Name = "startDate", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string", Format = "date" } });
            op.Parameters.Add(new OpenApiParameter { Name = "endDate", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string", Format = "date" } });
            op.Parameters.Add(new OpenApiParameter { Name = "countryCode", In = ParameterLocation.Query, Required = false, Schema = new OpenApiSchema { Type = "string", Default = new Microsoft.OpenApi.Any.OpenApiString("CO") } });
            return op;
        })
        .RequireAuthorization("EmployeeOrAbove");

        return app;
    }
}
