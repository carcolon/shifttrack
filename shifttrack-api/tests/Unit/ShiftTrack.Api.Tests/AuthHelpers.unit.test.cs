using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ShiftTrack.Api.Tests;

public sealed class AuthHelpersTests
{
    [Fact]
    public async Task ExchangeEntraCodeForIdTokenAsync_ReturnsGenericError_WhenMicrosoftReturnsAadstsDetails()
    {
        const string aadstsPayload = """
        {
          "error": "invalid_grant",
          "error_description": "AADSTS9002313: Invalid request. Trace ID: trace Correlation ID: correlation"
        }
        """;
        using var httpClient = new HttpClient(new StaticResponseHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(aadstsPayload, Encoding.UTF8, "application/json")
            }));

        var result = await AuthHelpers.ExchangeEntraCodeForIdTokenAsync(
            httpClient,
            "tenant-id",
            "client-id",
            "client-secret",
            "bad-code",
            "https://app.example.com/entra-callback",
            "verifier",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Microsoft code exchange failed (400).", result.ErrorMessage);
        Assert.DoesNotContain("AADSTS", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Trace ID", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Correlation ID", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppendAuthCookies_UsesCrossSiteSecureCookies_ForHttpsRequestsEvenWhenConfiguredAsDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";

        AuthHelpers.AppendAuthCookies(context, "jwt-value", "shifttrack_at", "shifttrack_csrf", secure: false, TimeSpan.FromMinutes(60));

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("shifttrack_at=jwt-value", setCookie);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=none", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppendAuthCookies_UsesLaxNonSecureCookies_ForLocalHttpDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";

        AuthHelpers.AppendAuthCookies(context, "jwt-value", "shifttrack_at", "shifttrack_csrf", secure: false, TimeSpan.FromMinutes(60));

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("shifttrack_at=jwt-value", setCookie);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StaticResponseHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_response);
    }
}
