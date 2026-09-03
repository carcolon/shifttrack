using Microsoft.EntityFrameworkCore;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Infrastructure.Persistence;

namespace ShiftTrack.Api;

internal static class ApplicationBuilderExtensions
{
    internal static async Task ApplyDatabaseMigrationsAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShiftTrackDbContext>();
        var schemaValidator = scope.ServiceProvider.GetRequiredService<ShiftTrackSchemaValidator>();
        await schemaValidator.ValidateBaselineSchemaAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    internal static WebApplication UseShiftTrackSecurityHeaders(this WebApplication app)
    {
        app.Use(async (httpContext, next) =>
        {
            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            httpContext.Response.Headers["X-Frame-Options"] = "DENY";
            httpContext.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            httpContext.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            httpContext.Response.Headers["Content-Security-Policy"] =
                "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            await next();
        });

        return app;
    }

    internal static WebApplication UseShiftTrackRequestGuards(this WebApplication app, StartupOptions options)
    {
        app.Use(async (httpContext, next) =>
        {
            httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "X-CSRF-Token");
            if (httpContext.Request.Cookies.TryGetValue(options.CsrfCookieName, out var csrfToken) &&
                !string.IsNullOrWhiteSpace(csrfToken))
            {
                httpContext.Response.Headers["X-CSRF-Token"] = csrfToken;
            }

            if (IsAnonymousAllowedPath(httpContext.Request.Path))
            {
                await next();
                return;
            }

            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (IsStateChangingMethod(httpContext.Request.Method) &&
                !ShouldSkipCsrf(httpContext.Request.Path) &&
                !IsCsrfValid(httpContext, options.CsrfCookieName))
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(new ErrorResponse("Invalid CSRF token."));
                return;
            }

            await next();
        });

        return app;
    }

    internal static async Task WarmUpScheduleStateAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var weekStart = ResolveWeekStart(DateTime.UtcNow.Date);
        _ = await users.GetCoverageSnapshotAsync(weekStart);
        _ = await users.GetScheduleOverridesAsync(weekStart, weekStart.AddDays(6));
    }
}
