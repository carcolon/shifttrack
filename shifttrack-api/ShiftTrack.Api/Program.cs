using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ShiftTrack.Api;
using ShiftTrack.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

if (args.Any(arg => string.Equals(arg, "--migrate-database", StringComparison.OrdinalIgnoreCase)))
{
    var dbContextOptions = new DbContextOptionsBuilder<ShiftTrackDbContext>()
        .UseSqlServer(ResolveDatabaseConnectionString(builder.Configuration))
        .Options;
    await using var dbContext = new ShiftTrackDbContext(dbContextOptions);
    var schemaValidator = new ShiftTrackSchemaValidator(dbContext);
    await schemaValidator.ValidateBaselineSchemaAsync();
    await dbContext.Database.MigrateAsync();
    return;
}

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var frontendBaseUrl = ResolveFrontendBaseUrl(
    builder.Configuration["FrontendBaseUrl"],
    allowedOrigins,
    "https://witty-ocean-0bbeada0f.1.azurestaticapps.net");
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]?.Trim();
if (string.IsNullOrWhiteSpace(jwtSigningKey))
{
    if (builder.Environment.IsDevelopment())
    {
        jwtSigningKey = "dev-only-shifttrack-signing-key-change-before-production";
    }
    else
    {
        throw new InvalidOperationException("Jwt:SigningKey is required.");
    }
}

var entraTenantId = builder.Configuration["AzureAd:TenantId"]?.Trim() ?? string.Empty;
var entraClientId = builder.Configuration["AzureAd:ClientId"]?.Trim() ?? string.Empty;
var entraClientSecret = builder.Configuration["AzureAd:ClientSecret"]?.Trim() ?? string.Empty;

ConfigurationManager<OpenIdConnectConfiguration>? entraConfigManager = null;
if (!string.IsNullOrWhiteSpace(entraTenantId))
{
    var metadataAddress = $"https://login.microsoftonline.com/{entraTenantId}/v2.0/.well-known/openid-configuration";
    entraConfigManager = new ConfigurationManager<OpenIdConnectConfiguration>(
        metadataAddress,
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever { RequireHttps = true });
}

var startupOptions = new StartupOptions
{
    FrontendBaseUrl = frontendBaseUrl,
    AllowedOrigins = allowedOrigins,
    JwtSigningKey = jwtSigningKey!,
    JwtIssuer = builder.Configuration["Jwt:Issuer"]?.Trim() ?? "ShiftTrack",
    JwtAudience = builder.Configuration["Jwt:Audience"]?.Trim() ?? "ShiftTrack.Client",
    AuthCookieName = "shifttrack_at",
    CsrfCookieName = "shifttrack_csrf",
    IsSecureCookie = !builder.Environment.IsDevelopment(),
    SessionTimeout = TimeSpan.FromMinutes(builder.Configuration.GetValue<int?>("Auth:SessionTimeoutMinutes") ?? 60),
    SessionRotationThreshold = TimeSpan.FromMinutes(builder.Configuration.GetValue<int?>("Auth:SessionRotationThresholdMinutes") ?? 10),
    EntraTenantId = entraTenantId,
    EntraClientId = entraClientId,
    EntraClientSecret = entraClientSecret,
    EntraConfigManager = entraConfigManager
};

builder.Services.AddSingleton(startupOptions);
builder.Services.AddShiftTrackCoreServices(builder.Configuration);
builder.Services.AddShiftTrackAuth(startupOptions, builder.Environment.IsDevelopment());
builder.Services.AddShiftTrackRateLimiting();
builder.Services.AddShiftTrackCors(builder.Configuration, frontendBaseUrl);

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseShiftTrackSecurityHeaders();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseShiftTrackRequestGuards(startupOptions);

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();

app.MapAuthEndpoints(
    startupOptions.FrontendBaseUrl,
    startupOptions.JwtSigningKey,
    startupOptions.JwtIssuer,
    startupOptions.JwtAudience,
    startupOptions.AuthCookieName,
    startupOptions.CsrfCookieName,
    startupOptions.IsSecureCookie,
    startupOptions.SessionTimeout,
    startupOptions.SessionRotationThreshold,
    startupOptions.EntraTenantId,
    startupOptions.EntraClientId,
    startupOptions.EntraClientSecret,
    startupOptions.EntraConfigManager);
app.MapUserEndpoints(startupOptions.FrontendBaseUrl);
app.MapCompanyEndpoints();
app.MapCoverageRuleEndpoints();
app.MapReportEndpoints();
app.MapScheduleEndpoints(startupOptions.FrontendBaseUrl);
app.MapRequestExportEndpoints();
app.MapHolidayEndpoints();
app.MapAssistantEndpoints();

if (builder.Configuration.GetValue("DatabaseMigrations:RunOnStartup", builder.Environment.IsDevelopment()))
{
    await TryApplyDatabaseMigrationsAsync(app);
}

app.Run();

static async Task TryApplyDatabaseMigrationsAsync(WebApplication app)
{
    try
    {
        await app.ApplyDatabaseMigrationsAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database migrations failed during application startup.");
    }
}

static string ResolveFrontendBaseUrl(string? configuredBaseUrl, IEnumerable<string> allowedOrigins, string fallbackBaseUrl)
{
    if (IsUsableFrontendUrl(configuredBaseUrl))
    {
        return configuredBaseUrl!.Trim().TrimEnd('/');
    }

    var firstAllowedPublicOrigin = allowedOrigins
        .Where(IsUsableFrontendUrl)
        .Select(origin => origin!.Trim().TrimEnd('/'))
        .FirstOrDefault();

    return firstAllowedPublicOrigin ?? fallbackBaseUrl;
}

static bool IsUsableFrontendUrl(string? value)
{
    if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
    {
        return false;
    }

    var host = uri.Host.Trim().ToLowerInvariant();
    return host != "localhost" && host != "127.0.0.1" && host != "::1";
}

static string ResolveDatabaseConnectionString(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    return string.IsNullOrWhiteSpace(connectionString)
        ? "Server=localhost\\SQLEXPRESS;Database=ShiftTrackDb;Trusted_Connection=True;TrustServerCertificate=True;"
        : connectionString;
}
