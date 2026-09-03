using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;

namespace ShiftTrack.Api;

internal static class CompanyEndpoints
{
    internal static WebApplication MapCompanyEndpoints(this WebApplication app)
    {
        app.MapGet("/companies", async Task<IResult> (HttpContext httpContext, IUserRepository users, bool includeInactive = false) =>
        {
            var callerUser = await ResolveCallerUserAsync(httpContext, users);
            if (callerUser is null || !RoleHelpers.IsKnownRole(callerUser.Role))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (callerUser.IsSystemHidden)
            {
                var companies = await users.GetCompaniesAsync(includeInactive);
                return Results.Ok(companies.Select(item => new CompanyResponse(item.Name, item.IsActive)));
            }

            var allowed = CompanyScopeHelpers.ResolveCompanies(callerUser)
                .Where(company => !string.IsNullOrWhiteSpace(company))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(company => company, StringComparer.OrdinalIgnoreCase)
                .Select(company => new CompanyResponse(company, true))
                .ToArray();
            return Results.Ok(allowed);
        }).RequireAuthorization();

        app.MapPost("/companies", async Task<IResult> (HttpContext httpContext, UpsertCompanyRequest request, IUserRepository users) =>
        {
            var callerUser = await ResolveCallerUserAsync(httpContext, users);
            if (callerUser is null || !callerUser.IsSystemHidden)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var name = NormalizeCompanyName(request.Name);
            if (name is null)
            {
                return Results.BadRequest(new ErrorResponse("Company name is required."));
            }

            await users.UpsertCompanyAsync(name, true);
            return Results.Ok(new CompanyResponse(name, true));
        }).RequireAuthorization();

        app.MapPatch("/companies/status", async Task<IResult> (HttpContext httpContext, SetCompanyStatusRequest request, IUserRepository users) =>
        {
            var callerUser = await ResolveCallerUserAsync(httpContext, users);
            if (callerUser is null || !callerUser.IsSystemHidden)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var name = NormalizeCompanyName(request.Name);
            if (name is null)
            {
                return Results.BadRequest(new ErrorResponse("Company name is required."));
            }

            await users.SetCompanyActiveAsync(name, request.IsActive);
            return Results.Ok(new CompanyResponse(name, request.IsActive));
        }).RequireAuthorization();

        app.MapPatch("/companies/name", async Task<IResult> (HttpContext httpContext, RenameCompanyRequest request, IUserRepository users) =>
        {
            var callerUser = await ResolveCallerUserAsync(httpContext, users);
            if (callerUser is null || !callerUser.IsSystemHidden)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var currentName = NormalizeCompanyName(request.CurrentName);
            var newName = NormalizeCompanyName(request.NewName);
            if (currentName is null || newName is null)
            {
                return Results.BadRequest(new ErrorResponse("Current and new company names are required."));
            }

            await users.RenameCompanyAsync(currentName, newName);
            return Results.Ok(new CompanyResponse(newName, true));
        }).RequireAuthorization();

        app.MapGet("/companies/operations", async Task<IResult> (HttpContext httpContext, IUserRepository users, string? company = null, bool includeInactive = false) =>
        {
            var callerUser = await ResolveCallerUserAsync(httpContext, users);
            if (callerUser is null || !RoleHelpers.IsKnownRole(callerUser.Role))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var normalizedCompany = NormalizeCompanyName(company);
            var allowedCompanies = callerUser.IsSystemHidden
                ? null
                : CompanyScopeHelpers.ResolveCompanies(callerUser);

            if (normalizedCompany is not null && allowedCompanies is not null && !allowedCompanies.Contains(normalizedCompany, StringComparer.OrdinalIgnoreCase))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var operations = await users.GetCompanyOperationsAsync(normalizedCompany, includeInactive && callerUser.IsSystemHidden);
            if (allowedCompanies is not null)
            {
                operations = operations.Where(operation => allowedCompanies.Contains(operation.CompanyName, StringComparer.OrdinalIgnoreCase));
            }

            return Results.Ok(operations.Select(item => new CompanyOperationResponse(item.CompanyName, item.Name, item.IsActive)));
        }).RequireAuthorization();

        app.MapPost("/companies/operations", async Task<IResult> (HttpContext httpContext, UpsertCompanyOperationRequest request, IUserRepository users) =>
        {
            var callerUser = await ResolveCallerUserAsync(httpContext, users);
            if (callerUser is null || !callerUser.IsSystemHidden)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var companyName = NormalizeCompanyName(request.CompanyName);
            var operationName = NormalizeOperationName(request.Name);
            if (companyName is null || operationName is null)
            {
                return Results.BadRequest(new ErrorResponse("Company and operation are required."));
            }

            await users.UpsertCompanyAsync(companyName, true);
            await users.UpsertCompanyOperationAsync(companyName, operationName, true);
            return Results.Ok(new CompanyOperationResponse(companyName, operationName, true));
        }).RequireAuthorization();

        app.MapPatch("/companies/operations/status", async Task<IResult> (HttpContext httpContext, SetCompanyOperationStatusRequest request, IUserRepository users) =>
        {
            var callerUser = await ResolveCallerUserAsync(httpContext, users);
            if (callerUser is null || !callerUser.IsSystemHidden)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var companyName = NormalizeCompanyName(request.CompanyName);
            var operationName = NormalizeOperationName(request.Name);
            if (companyName is null || operationName is null)
            {
                return Results.BadRequest(new ErrorResponse("Company and operation are required."));
            }

            await users.SetCompanyOperationActiveAsync(companyName, operationName, request.IsActive);
            return Results.Ok(new CompanyOperationResponse(companyName, operationName, request.IsActive));
        }).RequireAuthorization();

        app.MapPatch("/companies/operations/name", async Task<IResult> (HttpContext httpContext, RenameCompanyOperationRequest request, IUserRepository users) =>
        {
            var callerUser = await ResolveCallerUserAsync(httpContext, users);
            if (callerUser is null || !callerUser.IsSystemHidden)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var companyName = NormalizeCompanyName(request.CompanyName);
            var currentName = NormalizeOperationName(request.CurrentName);
            var newName = NormalizeOperationName(request.NewName);
            if (companyName is null || currentName is null || newName is null)
            {
                return Results.BadRequest(new ErrorResponse("Company, current operation, and new operation are required."));
            }

            await users.RenameCompanyOperationAsync(companyName, currentName, newName);
            return Results.Ok(new CompanyOperationResponse(companyName, newName, true));
        }).RequireAuthorization();

        return app;
    }

    private static async Task<ShiftTrack.Domain.Entities.User?> ResolveCallerUserAsync(HttpContext httpContext, IUserRepository users)
    {
        if (!TryGetCallerContext(httpContext, out var callerContext))
        {
            return null;
        }

        if (callerContext.UserId.HasValue)
        {
            var byId = await users.GetByIdAsync(callerContext.UserId.Value);
            if (byId is not null && byId.IsActive)
            {
                return byId;
            }
        }

        return string.IsNullOrWhiteSpace(callerContext.Email)
            ? null
            : await users.GetByEmailAsync(callerContext.Email);
    }

    private static string? NormalizeCompanyName(string? name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOperationName(string? name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
