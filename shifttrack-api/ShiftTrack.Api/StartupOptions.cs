using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ShiftTrack.Api;

internal sealed class StartupOptions
{
    internal string FrontendBaseUrl { get; init; } = string.Empty;
    internal string[] AllowedOrigins { get; init; } = Array.Empty<string>();
    internal string JwtSigningKey { get; init; } = string.Empty;
    internal string JwtIssuer { get; init; } = string.Empty;
    internal string JwtAudience { get; init; } = string.Empty;
    internal string AuthCookieName { get; init; } = string.Empty;
    internal string CsrfCookieName { get; init; } = string.Empty;
    internal bool IsSecureCookie { get; init; }
    internal TimeSpan SessionTimeout { get; init; } = TimeSpan.FromMinutes(60);
    internal TimeSpan SessionRotationThreshold { get; init; } = TimeSpan.FromMinutes(10);
    internal string EntraTenantId { get; init; } = string.Empty;
    internal string EntraClientId { get; init; } = string.Empty;
    internal string EntraClientSecret { get; init; } = string.Empty;
    internal ConfigurationManager<OpenIdConnectConfiguration>? EntraConfigManager { get; init; }
}
