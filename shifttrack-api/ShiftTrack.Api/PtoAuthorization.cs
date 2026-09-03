using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ShiftTrack.Application;

namespace ShiftTrack.Api;

internal sealed class PtoReviewRequirement : IAuthorizationRequirement
{
}

internal readonly record struct PtoReviewResource(int TargetUserRole, int RequestedByRole);

internal sealed class PtoReviewAuthorizationHandler : AuthorizationHandler<PtoReviewRequirement, PtoReviewResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PtoReviewRequirement requirement,
        PtoReviewResource resource)
    {
        var roleClaim = context.User.FindFirstValue(ClaimTypes.Role) ?? context.User.FindFirstValue("role");
        if (!int.TryParse(roleClaim, out var callerRole))
        {
            return Task.CompletedTask;
        }

        if (RoleHelpers.IsAdmin(callerRole))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (!RoleHelpers.CanReviewPto(callerRole))
        {
            return Task.CompletedTask;
        }

        if (!RoleHelpers.CanManagerManageRole(resource.TargetUserRole))
        {
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
