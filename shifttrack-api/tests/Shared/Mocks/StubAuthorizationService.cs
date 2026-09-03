using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ShiftTrack.Tests.Shared.Mocks;

public sealed class StubAuthorizationService : IAuthorizationService
{
    private readonly bool _authorized;

    public StubAuthorizationService(bool authorized = true)
    {
        _authorized = authorized;
    }

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
        => Task.FromResult(_authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
        => Task.FromResult(_authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());
}
