using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Application;
using ShiftTrack.Application.Services;
using ShiftTrack.Infrastructure;
using ShiftTrack.Infrastructure.Repositories;
using ShiftTrack.Infrastructure.Security;
using ShiftTrack.Infrastructure.Services;
using EmailOptions = ShiftTrack.Application.Models.EmailOptions;

namespace ShiftTrack.Api;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddShiftTrackCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddShiftTrackPersistence(configuration);
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Enter the ShiftTrack JWT as: Bearer {token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<ICoverageRuleRepository, EfCoverageRuleRepository>();
        services.AddScoped<IHolidayRepository, EfHolidayRepository>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IResetTokenStore, EfResetTokenStore>();
        services.Configure<EmailOptions>(configuration.GetSection("EmailSettings"));
        services.AddSingleton<IEmailService, AcsEmailService>();
        services.AddHttpClient();
        services.AddSignalR();
        services.AddSingleton<IAuthorizationHandler, PtoReviewAuthorizationHandler>();
        services.AddScoped<IUserWorkflowService, UserWorkflowService>();
        services.AddScoped<IScheduleWorkflowService, ScheduleWorkflowService>();
        services.AddScoped<IHolidayWorkflowService, HolidayWorkflowService>();
        services.AddScoped<IAssistantWorkflowService, AssistantWorkflowService>();
        services.AddScoped<IRequestExportRepository, EfRequestExportRepository>();
        services.AddScoped<IRequestExportWorkflowService, RequestExportWorkflowService>();
        services.AddScoped<IRequestExportJobRunner, RequestExportJobRunner>();
        services.AddShiftTrackRequestExportJobs(configuration);
        return services;
    }

    internal static bool AreShiftTrackRequestExportJobsEnabled(this IConfiguration configuration)
    {
        var configured = configuration.GetValue<bool?>("RequestExports:BackgroundJobsEnabled");
        if (configured.HasValue)
        {
            return configured.Value;
        }

        return !string.IsNullOrWhiteSpace(ResolveDatabaseConnectionString(configuration));
    }

    private static IServiceCollection AddShiftTrackRequestExportJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ResolveDatabaseConnectionString(configuration);
        if (!configuration.AreShiftTrackRequestExportJobsEnabled() || string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IRequestExportJobQueue, DisabledRequestExportJobQueue>();
            return services;
        }

        services.AddHangfire(hangfire =>
        {
            hangfire
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = TimeSpan.FromSeconds(15)
                });
        });
        services.AddHangfireServer();
        services.AddSingleton<IRequestExportJobQueue, HangfireRequestExportJobQueue>();
        return services;
    }

    private static string? ResolveDatabaseConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }

    internal static IServiceCollection AddShiftTrackAuth(this IServiceCollection services, StartupOptions options, bool isDevelopment)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSigningKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.RequireHttpsMetadata = !isDevelopment;
                jwtOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.TryGetValue(options.AuthCookieName, out var cookieToken) &&
                            !string.IsNullOrWhiteSpace(cookieToken))
                        {
                            context.Token = cookieToken;
                            return Task.CompletedTask;
                        }

                        if (!string.IsNullOrWhiteSpace(context.Token))
                        {
                            return Task.CompletedTask;
                        }

                        if (context.HttpContext.Request.Path.StartsWithSegments("/hubs/schedule"))
                        {
                            var accessToken = context.Request.Query["access_token"].ToString();
                            if (!string.IsNullOrWhiteSpace(accessToken))
                            {
                                context.Token = accessToken;
                            }
                        }

                        return Task.CompletedTask;
                    }
                };
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = options.JwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
            });

        services.AddAuthorization(authOptions =>
        {
            authOptions.AddPolicy("EmployeeOrAbove", policy =>
                policy.RequireAssertion(ctx => TryReadRoleFromPrincipal(ctx.User, out var role) && RoleHelpers.IsKnownRole(role)));
            authOptions.AddPolicy("ManagerOrAbove", policy =>
                policy.RequireAssertion(ctx => TryReadRoleFromPrincipal(ctx.User, out var role) && RoleHelpers.CanManageUsers(role)));
            authOptions.AddPolicy("AdminOnly", policy =>
                policy.RequireAssertion(ctx => TryReadRoleFromPrincipal(ctx.User, out var role) && RoleHelpers.IsAdmin(role)));
            authOptions.AddPolicy("CanReviewPto", policy =>
                policy.Requirements.Add(new PtoReviewRequirement()));
        });

        return services;
    }

    internal static IServiceCollection AddShiftTrackRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 240,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
            options.AddFixedWindowLimiter("auth", limiterOptions =>
            {
                limiterOptions.PermitLimit = 12;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
                limiterOptions.AutoReplenishment = true;
            });
        });

        return services;
    }

    internal static IServiceCollection AddShiftTrackCors(this IServiceCollection services, IConfiguration configuration, string frontendBaseUrl)
    {
        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(options =>
        {
            options.AddPolicy("Default", policy =>
            {
                var safeOrigins = allowedOrigins
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o.Trim().TrimEnd('/'))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var frontendUri))
                {
                    safeOrigins.Add(frontendUri.GetLeftPart(UriPartial.Authority));
                }

                if (safeOrigins.Count == 0)
                {
                    throw new InvalidOperationException("CORS configuration error: no AllowedOrigins configured.");
                }

                policy.WithOrigins(safeOrigins.Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
