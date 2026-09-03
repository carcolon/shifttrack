using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal static class AuthHelpers
{
    internal static bool ShouldRotateSession(ClaimsPrincipal principal, TimeSpan rotationThreshold)
    {
        var exp = principal.FindFirstValue("exp");
        if (!long.TryParse(exp, out var expUnix))
        {
            return false;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        return expiresAt - DateTimeOffset.UtcNow <= rotationThreshold;
    }

    internal static async Task<EntraCodeExchangeResult> ExchangeEntraCodeForIdTokenAsync(
        HttpClient httpClient,
        string tenantId,
        string clientId,
        string clientSecret,
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        var endpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
        var firstAttempt = await ExecuteEntraCodeExchangeAsync(
            httpClient,
            endpoint,
            clientId,
            clientSecret,
            code,
            redirectUri,
            codeVerifier,
            includeClientSecret: !string.IsNullOrWhiteSpace(clientSecret),
            cancellationToken);

        var response = firstAttempt.Response;
        var payload = firstAttempt.Payload;

        if (!response.IsSuccessStatusCode)
        {
            if (!string.IsNullOrWhiteSpace(clientSecret) &&
                payload.Contains("AADSTS700025", StringComparison.OrdinalIgnoreCase))
            {
                var retryAttempt = await ExecuteEntraCodeExchangeAsync(
                    httpClient,
                    endpoint,
                    clientId,
                    clientSecret,
                    code,
                    redirectUri,
                    codeVerifier,
                    includeClientSecret: false,
                    cancellationToken);
                response = retryAttempt.Response;
                payload = retryAttempt.Payload;
            }

            return new EntraCodeExchangeResult(
                false,
                null,
                $"Microsoft code exchange failed ({(int)response.StatusCode}).");
        }

        try
        {
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.TryGetProperty("id_token", out var idTokenElement))
            {
                var idToken = idTokenElement.GetString();
                if (!string.IsNullOrWhiteSpace(idToken))
                {
                    return new EntraCodeExchangeResult(true, idToken, null);
                }
            }
        }
        catch
        {
            // Handled below.
        }

        return new EntraCodeExchangeResult(false, null, "Microsoft token response is invalid.");
    }

    internal static string? TryParseEntraExchangeError(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            if (root.TryGetProperty("error_description", out var description))
            {
                var text = description.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }

            if (root.TryGetProperty("error", out var errorCode))
            {
                var text = errorCode.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }
        catch
        {
            // Ignore parser failures and keep generic message.
        }

        return null;
    }

    internal static bool IsAnonymousAllowedPath(PathString path)
    {
        if (path.StartsWithSegments("/auth/login")) return true;
        if (path.StartsWithSegments("/auth/entra-login")) return true;
        if (path.StartsWithSegments("/auth/entra-code-login")) return true;
        if (path.StartsWithSegments("/auth/forgot-password")) return true;
        if (path.StartsWithSegments("/auth/reset-password/exchange")) return true;
        if (path.StartsWithSegments("/auth/reset-password/complete")) return true;
        if (path.StartsWithSegments("/auth/reset-password")) return true;
        if (path.StartsWithSegments("/auth/force-change-password")) return true;
        if (path.StartsWithSegments("/healthz")) return true;
        if (path.StartsWithSegments("/swagger")) return true;
        if (path.StartsWithSegments("/favicon.ico")) return true;
        return false;
    }

    internal static bool IsStateChangingMethod(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    internal static bool ShouldSkipCsrf(PathString path)
    {
        if (path.StartsWithSegments("/hubs/schedule")) return true;
        if (path.StartsWithSegments("/auth/login")) return true;
        if (path.StartsWithSegments("/auth/entra-login")) return true;
        if (path.StartsWithSegments("/auth/entra-code-login")) return true;
        if (path.StartsWithSegments("/auth/forgot-password")) return true;
        if (path.StartsWithSegments("/auth/reset-password")) return true;
        if (path.StartsWithSegments("/auth/force-change-password")) return true;
        return false;
    }

    internal static bool IsCsrfValid(HttpContext httpContext, string csrfCookieName)
    {
        if (!httpContext.Request.Cookies.TryGetValue(csrfCookieName, out var csrfCookie) ||
            string.IsNullOrWhiteSpace(csrfCookie))
        {
            if (httpContext.Request.Headers.TryGetValue("Authorization", out var authorizationValue) &&
                authorizationValue.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        if (!httpContext.Request.Headers.TryGetValue("X-CSRF-Token", out var csrfHeader))
        {
            return false;
        }

        return string.Equals(csrfCookie, csrfHeader.ToString(), StringComparison.Ordinal);
    }

    internal static void AppendAuthCookies(HttpContext httpContext, string token, string authCookieName, string csrfCookieName, bool secure, TimeSpan sessionTimeout)
    {
        var cookieSecurity = ResolveCookieSecurity(httpContext, secure);
        var authCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = cookieSecurity.Secure,
            SameSite = cookieSecurity.SameSite,
            Expires = DateTimeOffset.UtcNow.Add(sessionTimeout),
            IsEssential = true,
            Path = "/"
        };
        httpContext.Response.Cookies.Append(authCookieName, token, authCookieOptions);

        var csrfToken = Guid.NewGuid().ToString("N");
        var csrfCookieOptions = new CookieOptions
        {
            HttpOnly = false,
            Secure = cookieSecurity.Secure,
            SameSite = cookieSecurity.SameSite,
            Expires = DateTimeOffset.UtcNow.Add(sessionTimeout),
            IsEssential = true,
            Path = "/"
        };
        httpContext.Response.Cookies.Append(csrfCookieName, csrfToken, csrfCookieOptions);
        httpContext.Response.Headers["X-CSRF-Token"] = csrfToken;
    }

    internal static void ClearAuthCookies(HttpContext httpContext, string authCookieName, string csrfCookieName, bool secure)
    {
        var cookieSecurity = ResolveCookieSecurity(httpContext, secure);
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = cookieSecurity.Secure,
            SameSite = cookieSecurity.SameSite,
            Path = "/"
        };
        httpContext.Response.Cookies.Delete(authCookieName, cookieOptions);

        var csrfCookieOptions = new CookieOptions
        {
            HttpOnly = false,
            Secure = cookieSecurity.Secure,
            SameSite = cookieSecurity.SameSite,
            Path = "/"
        };
        httpContext.Response.Cookies.Delete(csrfCookieName, csrfCookieOptions);
    }

    private static (bool Secure, SameSiteMode SameSite) ResolveCookieSecurity(HttpContext httpContext, bool configuredSecure)
    {
        var isHttpsRequest =
            httpContext.Request.IsHttps ||
            string.Equals(httpContext.Request.Headers["X-Forwarded-Proto"].ToString(), "https", StringComparison.OrdinalIgnoreCase) ||
            httpContext.Request.Headers.ContainsKey("X-ARR-SSL");

        var effectiveSecure = configuredSecure || isHttpsRequest;
        return effectiveSecure
            ? (true, SameSiteMode.None)
            : (false, SameSiteMode.Lax);
    }

    internal static string CreateAccessToken(User user, string signingKey, string issuer, string audience, TimeSpan sessionTimeout)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString(CultureInfo.InvariantCulture)),
            new("role", user.Role.ToString(CultureInfo.InvariantCulture))
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-5),
            expires: DateTime.UtcNow.Add(sessionTimeout),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    internal static bool TryReadRoleFromPrincipal(ClaimsPrincipal user, out int role)
    {
        role = -1;
        var roleClaim = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role");
        return int.TryParse(roleClaim, out role);
    }

    internal static bool TryGetCallerContext(HttpContext httpContext, out CallerContext caller)
    {
        caller = default;
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var roleClaim = httpContext.User.FindFirstValue(ClaimTypes.Role) ?? httpContext.User.FindFirstValue("role");
        if (!int.TryParse(roleClaim, out var role))
        {
            return false;
        }

        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = null;
        if (Guid.TryParse(userIdClaim, out var parsedUserId))
        {
            userId = parsedUserId;
        }

        caller = new CallerContext
        {
            Role = role,
            UserId = userId,
            Email = httpContext.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Name = httpContext.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty
        };
        return true;
    }

    internal static async Task<ClaimsPrincipal> ValidateEntraIdTokenAsync(
        string idToken,
        string clientId,
        string tenantId,
        ConfigurationManager<OpenIdConnectConfiguration> configManager)
    {
        var config = await configManager.GetConfigurationAsync(CancellationToken.None);

        var validIssuerV2 = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        var validIssuerSts = $"https://sts.windows.net/{tenantId}/";
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { validIssuerV2, validIssuerSts },
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(idToken, tokenValidationParameters, out _);
    }

    internal static string? GetClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static async Task<(HttpResponseMessage Response, string Payload)> ExecuteEntraCodeExchangeAsync(
        HttpClient httpClient,
        string endpoint,
        string clientId,
        string clientSecret,
        string code,
        string redirectUri,
        string codeVerifier,
        bool includeClientSecret,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
            ["scope"] = "openid profile email"
        };
        if (includeClientSecret)
        {
            form["client_secret"] = clientSecret;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };
        var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response, payload);
    }
}
