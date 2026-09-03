using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ShiftTrack.Application;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Api;

internal static class AuthEndpoints
{
    private const string CorporateEmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    internal static WebApplication MapAuthEndpoints(
        this WebApplication app,
        string frontendBaseUrl,
        string jwtSigningKey,
        string jwtIssuer,
        string jwtAudience,
        string authCookieName,
        string csrfCookieName,
        bool isSecureCookie,
        TimeSpan sessionTimeout,
        TimeSpan sessionRotationThreshold,
        string entraTenantId,
        string entraClientId,
        string entraClientSecret,
        ConfigurationManager<OpenIdConnectConfiguration>? entraConfigManager)
    {
        app.MapPost("/auth/login", async (HttpContext httpContext, LoginRequest request, IAuthService auth, IUserRepository users) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email)) return Results.BadRequest(new ErrorResponse("The Email field is required."));
            if (string.IsNullOrWhiteSpace(request.Password)) return Results.BadRequest(new ErrorResponse("The Password field is required."));

            var trimmedEmail = request.Email.Trim();
            var trimmedPassword = request.Password.Trim();
            var lockKey = BuildLoginLockoutKey(httpContext, trimmedEmail);

            if (TryGetLoginLockoutRemaining(lockKey, out var remaining))
            {
                return Results.Json(
                    new ErrorResponse($"Too many failed attempts. Try again in {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} minute(s)."),
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            if (!string.Equals(trimmedEmail, request.Email, StringComparison.Ordinal) ||
                !string.Equals(trimmedPassword, request.Password, StringComparison.Ordinal))
            {
                RegisterFailedLoginAttempt(lockKey);
                return Results.BadRequest(new ErrorResponse("Credentials for the entered email are not valid. Please check and try again."));
            }
            if (!Regex.IsMatch(trimmedEmail, CorporateEmailPattern))
            {
                RegisterFailedLoginAttempt(lockKey);
                return Results.BadRequest(new ErrorResponse("Credentials for the entered email are not valid. Please check and try again."));
            }

            var result = await auth.LoginAsync(trimmedEmail, trimmedPassword);
            if (!result.Success && result.RequirePasswordChange)
            {
                return Results.Json(new { requirePasswordChange = true, message = result.Message, email = result.Email, displayName = result.DisplayName, role = result.Role });
            }
            if (!result.Success)
            {
                RegisterFailedLoginAttempt(lockKey);
                return Results.Json(new ErrorResponse(result.Message ?? "Credentials for the entered email are not valid. Please check and try again."), statusCode: StatusCodes.Status401Unauthorized);
            }

            var user = await users.GetByEmailAsync(result.Email!);
            if (user is null || !user.IsActive)
            {
                RegisterFailedLoginAttempt(lockKey);
                return Results.Json(new ErrorResponse("Credentials for the entered email are not valid. Please check and try again."), statusCode: StatusCodes.Status401Unauthorized);
            }

            ClearLoginFailedAttempts(lockKey);
            var roleValue = user.Role;
            var perms = ApiHelpers.PermissionsForRole(roleValue);
            var token = CreateAccessToken(user, jwtSigningKey, jwtIssuer, jwtAudience, sessionTimeout);
            AppendAuthCookies(httpContext, token, authCookieName, csrfCookieName, isSecureCookie, sessionTimeout);
            return Results.Ok(new AuthResponse(user.Email, user.DisplayName ?? string.Empty, roleValue, perms, user.IsSystemHidden, user.Company, CompanyScopeHelpers.ResolveCompanies(user)));
        })
        .WithName("Login")
        .WithOpenApi()
        .RequireRateLimiting("auth");

        app.MapPost("/auth/entra-login", async (HttpContext httpContext, EntraLoginRequest request, IAuthService auth, IUserRepository users) =>
        {
            if (string.IsNullOrWhiteSpace(request.IdToken))
            {
                return Results.BadRequest(new ErrorResponse("Entra token is required."));
            }
            if (string.IsNullOrWhiteSpace(entraTenantId) || string.IsNullOrWhiteSpace(entraClientId) || entraConfigManager is null)
            {
                return Results.Problem("Microsoft Entra is not configured.", statusCode: StatusCodes.Status500InternalServerError);
            }

            ClaimsPrincipal principal;
            try
            {
                principal = await ValidateEntraIdTokenAsync(request.IdToken, entraClientId, entraTenantId, entraConfigManager);
            }
            catch
            {
                return Results.Json(new ErrorResponse("Invalid Microsoft token."), statusCode: StatusCodes.Status401Unauthorized);
            }

            var oid = GetClaimValue(principal, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier");
            var email = GetClaimValue(
                principal,
                "preferred_username",
                ClaimTypes.Upn,
                ClaimTypes.Email,
                "upn",
                "email",
                "unique_name");
            var displayName = principal.FindFirstValue("name");

            if (!Guid.TryParse(oid, out var objectId) || string.IsNullOrWhiteSpace(email))
            {
                return Results.Json(new ErrorResponse("Microsoft token is missing required claims."), statusCode: StatusCodes.Status401Unauthorized);
            }
            if (!Regex.IsMatch(email.Trim(), CorporateEmailPattern))
            {
                return Results.Json(new ErrorResponse("This Microsoft account is not authorized to access ShiftTrack."), statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await auth.LoginWithEntraAsync(objectId, email.Trim(), displayName);
            if (!result.Success)
            {
                return Results.Json(new ErrorResponse(result.Message ?? "Unauthorized."), statusCode: StatusCodes.Status401Unauthorized);
            }

            var user = await users.GetByEmailAsync(result.Email!);
            if (user is null || !user.IsActive)
            {
                return Results.Json(new ErrorResponse("This Microsoft account is not authorized to access ShiftTrack."), statusCode: StatusCodes.Status401Unauthorized);
            }

            var roleValue = user.Role;
            var token = CreateAccessToken(user, jwtSigningKey, jwtIssuer, jwtAudience, sessionTimeout);
            AppendAuthCookies(httpContext, token, authCookieName, csrfCookieName, isSecureCookie, sessionTimeout);
            return Results.Ok(new AuthResponse(user.Email, user.DisplayName ?? string.Empty, roleValue, ApiHelpers.PermissionsForRole(roleValue), user.IsSystemHidden, user.Company, CompanyScopeHelpers.ResolveCompanies(user)));
        })
        .WithName("EntraLogin")
        .WithOpenApi()
        .RequireRateLimiting("auth");

        app.MapPost("/auth/entra-code-login", async (
            HttpContext httpContext,
            EntraCodeLoginRequest request,
            IAuthService auth,
            IUserRepository users,
            IHttpClientFactory httpClientFactory) =>
        {
            if (string.IsNullOrWhiteSpace(request.Code) ||
                string.IsNullOrWhiteSpace(request.CodeVerifier) ||
                string.IsNullOrWhiteSpace(request.RedirectUri))
            {
                return Results.BadRequest(new ErrorResponse("Microsoft login request is invalid."));
            }

            if (string.IsNullOrWhiteSpace(entraTenantId) ||
                string.IsNullOrWhiteSpace(entraClientId) ||
                string.IsNullOrWhiteSpace(entraClientSecret) ||
                entraConfigManager is null)
            {
                return Results.Problem("Microsoft Entra is not configured.", statusCode: StatusCodes.Status500InternalServerError);
            }

            if (!Uri.TryCreate(request.RedirectUri.Trim(), UriKind.Absolute, out var redirectUri))
            {
                return Results.BadRequest(new ErrorResponse("Microsoft redirect URI is invalid."));
            }

            var allowedRedirectOrigins = BuildAllowedRedirectOrigins(frontendBaseUrl, app.Services.GetRequiredService<StartupOptions>().AllowedOrigins);
            var redirectOrigin = redirectUri.GetLeftPart(UriPartial.Authority);
            if (!allowedRedirectOrigins.Contains(redirectOrigin, StringComparer.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new ErrorResponse("Microsoft redirect origin is not allowed."));
            }

            var exchange = await ExchangeEntraCodeForIdTokenAsync(
                httpClientFactory.CreateClient(),
                entraTenantId,
                entraClientId,
                entraClientSecret,
                request.Code.Trim(),
                redirectUri.ToString(),
                request.CodeVerifier.Trim(),
                httpContext.RequestAborted);

            if (!exchange.Success || string.IsNullOrWhiteSpace(exchange.IdToken))
            {
                return Results.Json(
                    new ErrorResponse(exchange.ErrorMessage ?? "Microsoft code exchange failed."),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            ClaimsPrincipal principal;
            try
            {
                principal = await ValidateEntraIdTokenAsync(exchange.IdToken, entraClientId, entraTenantId, entraConfigManager);
            }
            catch
            {
                return Results.Json(new ErrorResponse("Invalid Microsoft token."), statusCode: StatusCodes.Status401Unauthorized);
            }

            var oid = GetClaimValue(principal, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier");
            var email = GetClaimValue(
                principal,
                "preferred_username",
                ClaimTypes.Upn,
                ClaimTypes.Email,
                "upn",
                "email",
                "unique_name");
            var displayName = principal.FindFirstValue("name");

            if (!Guid.TryParse(oid, out var objectId) || string.IsNullOrWhiteSpace(email))
            {
                return Results.Json(new ErrorResponse("Microsoft token is missing required claims."), statusCode: StatusCodes.Status401Unauthorized);
            }
            if (!Regex.IsMatch(email.Trim(), CorporateEmailPattern))
            {
                return Results.Json(new ErrorResponse("This Microsoft account is not authorized to access ShiftTrack."), statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await auth.LoginWithEntraAsync(objectId, email.Trim(), displayName);
            if (!result.Success)
            {
                return Results.Json(new ErrorResponse(result.Message ?? "Unauthorized."), statusCode: StatusCodes.Status401Unauthorized);
            }

            var user = await users.GetByEmailAsync(result.Email!);
            if (user is null || !user.IsActive)
            {
                return Results.Json(new ErrorResponse("This Microsoft account is not authorized to access ShiftTrack."), statusCode: StatusCodes.Status401Unauthorized);
            }

            var roleValue = user.Role;
            var token = CreateAccessToken(user, jwtSigningKey, jwtIssuer, jwtAudience, sessionTimeout);
            AppendAuthCookies(httpContext, token, authCookieName, csrfCookieName, isSecureCookie, sessionTimeout);
            return Results.Ok(new AuthResponse(user.Email, user.DisplayName ?? string.Empty, roleValue, ApiHelpers.PermissionsForRole(roleValue), user.IsSystemHidden, user.Company, CompanyScopeHelpers.ResolveCompanies(user)));
        })
        .WithName("EntraCodeLogin")
        .WithOpenApi()
        .RequireRateLimiting("auth");

        app.MapGet("/auth/me", async (HttpContext httpContext, IUserRepository users) =>
        {
            if (!TryGetCallerContext(httpContext, out var callerContext))
            {
                return Results.Unauthorized();
            }

            User? currentUser = null;
            if (callerContext.UserId.HasValue)
            {
                currentUser = await users.GetByIdAsync(callerContext.UserId.Value);
            }
            if (currentUser is null && !string.IsNullOrWhiteSpace(callerContext.Email))
            {
                currentUser = await users.GetByEmailAsync(callerContext.Email);
            }
            if (currentUser is null || !currentUser.IsActive)
            {
                ClearAuthCookies(httpContext, authCookieName, csrfCookieName, isSecureCookie);
                return Results.Unauthorized();
            }

            if (ShouldRotateSession(httpContext.User, sessionRotationThreshold))
            {
                var refreshedToken = CreateAccessToken(currentUser, jwtSigningKey, jwtIssuer, jwtAudience, sessionTimeout);
                AppendAuthCookies(httpContext, refreshedToken, authCookieName, csrfCookieName, isSecureCookie, sessionTimeout);
            }

            var roleValue = currentUser.Role;
            return Results.Ok(new
            {
                email = currentUser.Email,
                displayName = currentUser.DisplayName ?? currentUser.Email,
                role = roleValue,
                permissions = ApiHelpers.PermissionsForRole(roleValue),
                isSystemHidden = currentUser.IsSystemHidden,
                company = currentUser.Company,
                companies = CompanyScopeHelpers.ResolveCompanies(currentUser)
            });
        })
        .WithName("AuthMe")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapPost("/auth/ping", async (HttpContext httpContext, IUserRepository users) =>
        {
            if (!TryGetCallerContext(httpContext, out var callerContext))
            {
                return Results.Unauthorized();
            }

            User? currentUser = null;
            if (callerContext.UserId.HasValue)
            {
                currentUser = await users.GetByIdAsync(callerContext.UserId.Value);
            }
            if (currentUser is null && !string.IsNullOrWhiteSpace(callerContext.Email))
            {
                currentUser = await users.GetByEmailAsync(callerContext.Email);
            }
            if (currentUser is null || !currentUser.IsActive)
            {
                ClearAuthCookies(httpContext, authCookieName, csrfCookieName, isSecureCookie);
                return Results.Unauthorized();
            }

            var refreshedToken = CreateAccessToken(currentUser, jwtSigningKey, jwtIssuer, jwtAudience, sessionTimeout);
            AppendAuthCookies(httpContext, refreshedToken, authCookieName, csrfCookieName, isSecureCookie, sessionTimeout);
            return Results.NoContent();
        })
        .WithName("AuthPing")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapPost("/auth/logout", (HttpContext httpContext) =>
        {
            ClearAuthCookies(httpContext, authCookieName, csrfCookieName, isSecureCookie);
            return Results.Ok(new { Message = "Logged out." });
        })
        .WithName("Logout")
        .WithOpenApi()
        .RequireAuthorization("EmployeeOrAbove");

        app.MapPost("/auth/forgot-password", async (ForgotPasswordRequest request, IAuthService auth, IUserRepository users, IEmailService emailService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email)) return Results.BadRequest(new ErrorResponse("The Email field is required."));

            var trimmedEmail = request.Email.Trim();
            if (!Regex.IsMatch(trimmedEmail, CorporateEmailPattern))
            {
                return Results.Ok(new { Message = "If the email exists, a reset link has been sent." });
            }

            var token = await auth.GenerateResetTokenAsync(trimmedEmail, TimeSpan.FromMinutes(30));
            if (token is not null)
            {
                var user = await users.GetByEmailAsync(trimmedEmail);
                var resetCode = CreateResetCode(trimmedEmail, token, TimeSpan.FromMinutes(30));
                var resetLink = ApiHelpers.BuildResetLink(frontendBaseUrl, resetCode);
                await emailService.SendResetEmailAsync(trimmedEmail, user?.DisplayName ?? trimmedEmail, resetLink);
            }

            return Results.Ok(new { Message = "If the email exists, a reset link has been sent." });
        })
        .WithName("ForgotPassword")
        .WithOpenApi()
        .RequireRateLimiting("auth");

        app.MapPost("/auth/reset-password/exchange", (ResetPasswordCodeExchangeRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return Results.BadRequest(new ErrorResponse("Reset code is required."));
            }

            if (!TryConsumeResetCode(request.Code.Trim(), out var state))
            {
                return Results.BadRequest(new ErrorResponse("Invalid or expired reset link."));
            }

            var exchangeToken = CreateResetExchangeToken(state.Email, state.ResetToken, TimeSpan.FromMinutes(10));
            return Results.Ok(new ResetPasswordCodeExchangeResponse(state.Email, exchangeToken));
        })
        .WithName("ExchangeResetCode")
        .WithOpenApi()
        .RequireRateLimiting("auth");

        app.MapPost("/auth/reset-password", async (ResetPasswordWithTokenRequest request, IAuthService auth, IResetTokenStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email)) return Results.BadRequest(new ErrorResponse("The Email field is required."));
            if (string.IsNullOrWhiteSpace(request.Token)) return Results.BadRequest(new ErrorResponse("The Token field is required."));
            if (string.IsNullOrWhiteSpace(request.NewPassword)) return Results.BadRequest(new ErrorResponse("The Password field is required."));

            var trimmedEmail = request.Email.Trim();
            var trimmedPassword = request.NewPassword.Trim();
            var trimmedToken = request.Token.Trim();

            if (!string.Equals(trimmedEmail, request.Email, StringComparison.Ordinal) ||
                !string.Equals(trimmedPassword, request.NewPassword, StringComparison.Ordinal) ||
                !string.Equals(trimmedToken, request.Token, StringComparison.Ordinal))
            {
                return Results.BadRequest(new ErrorResponse("Credentials for the entered email are not valid. Please check and try again."));
            }
            if (!Regex.IsMatch(trimmedEmail, CorporateEmailPattern))
            {
                return Results.BadRequest(new ErrorResponse("Credentials for the entered email are not valid. Please check and try again."));
            }

            var tokenValid = store.TryValidateAndConsume(trimmedEmail, trimmedToken);
            if (!tokenValid)
            {
                return Results.BadRequest(new ErrorResponse("Invalid or expired reset token."));
            }

            var result = await auth.ResetPasswordAsync(trimmedEmail, trimmedPassword);
            if (!result.Success)
            {
                return Results.BadRequest(new ErrorResponse(result.Message ?? "Unable to reset password."));
            }

            return Results.Ok(new { Message = "Password reset successful." });
        })
        .WithName("ResetPassword")
        .WithOpenApi()
        .RequireRateLimiting("auth");

        app.MapPost("/auth/reset-password/complete", async (ResetPasswordCompleteRequest request, IAuthService auth, IResetTokenStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email)) return Results.BadRequest(new ErrorResponse("The Email field is required."));
            if (string.IsNullOrWhiteSpace(request.ExchangeToken)) return Results.BadRequest(new ErrorResponse("The ExchangeToken field is required."));
            if (string.IsNullOrWhiteSpace(request.NewPassword)) return Results.BadRequest(new ErrorResponse("The Password field is required."));

            var trimmedEmail = request.Email.Trim();
            var trimmedPassword = request.NewPassword.Trim();
            var trimmedExchangeToken = request.ExchangeToken.Trim();

            if (!string.Equals(trimmedEmail, request.Email, StringComparison.Ordinal) ||
                !string.Equals(trimmedPassword, request.NewPassword, StringComparison.Ordinal) ||
                !string.Equals(trimmedExchangeToken, request.ExchangeToken, StringComparison.Ordinal))
            {
                return Results.BadRequest(new ErrorResponse("Credentials for the entered email are not valid. Please check and try again."));
            }
            if (!Regex.IsMatch(trimmedEmail, CorporateEmailPattern))
            {
                return Results.BadRequest(new ErrorResponse("Credentials for the entered email are not valid. Please check and try again."));
            }

            if (!TryConsumeResetExchangeToken(trimmedEmail, trimmedExchangeToken, out var resetToken))
            {
                return Results.BadRequest(new ErrorResponse("Invalid or expired reset session."));
            }

            var tokenValid = store.TryValidateAndConsume(trimmedEmail, resetToken);
            if (!tokenValid)
            {
                return Results.BadRequest(new ErrorResponse("Invalid or expired reset token."));
            }

            var result = await auth.ResetPasswordAsync(trimmedEmail, trimmedPassword);
            if (!result.Success)
            {
                return Results.BadRequest(new ErrorResponse(result.Message ?? "Unable to reset password."));
            }

            return Results.Ok(new { Message = "Password reset successful." });
        })
        .WithName("CompleteResetPassword")
        .WithOpenApi()
        .RequireRateLimiting("auth");

        app.MapPost("/auth/force-change-password", async (ForceChangePasswordRequest request, IAuthService auth) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email)) return Results.BadRequest(new ErrorResponse("The Email field is required."));
            if (string.IsNullOrWhiteSpace(request.NewPassword)) return Results.BadRequest(new ErrorResponse("The Password field is required."));

            var trimmedEmail = request.Email.Trim();
            var trimmedNew = request.NewPassword.Trim();
            var tokenOrPwd = (request.Token ?? request.CurrentPassword ?? string.Empty).Trim();
            var isToken = !string.IsNullOrWhiteSpace(request.Token);

            var result = await auth.ForceChangePasswordAsync(trimmedEmail, tokenOrPwd, trimmedNew, isToken);
            if (!result.Success)
            {
                return Results.BadRequest(new ErrorResponse(result.Message ?? "Unable to change password."));
            }

            return Results.Ok(new { Message = "Password updated. Please log in again." });
        })
        .WithName("ForceChangePassword")
        .WithOpenApi()
        .RequireRateLimiting("auth");

        return app;
    }

    private static string[] BuildAllowedRedirectOrigins(string frontendBaseUrl, IEnumerable<string> allowedOrigins)
    {
        var origins = allowedOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .ToList();

        if (Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var frontendUri))
        {
            origins.Add(frontendUri.GetLeftPart(UriPartial.Authority));
        }

        return origins
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
