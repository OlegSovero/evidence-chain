using System.Security.Claims;
using EvidenceChain.Api.Domain;

namespace EvidenceChain.Api.Features.Auth;

public static class CurrentUser
{
    public const string UserIdClaim = "sub";
    public const string UserNameClaim = "name";
    public const string RoleClaim = "role";
    public const string DisplayNameClaim = "displayName";

    public static int GetUserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst(UserIdClaim)?.Value, out var id)
            ? id
            : throw new InvalidOperationException("The authenticated principal has no user id claim.");

    public static string GetUserName(this ClaimsPrincipal principal) =>
        principal.FindFirst(UserNameClaim)?.Value ?? string.Empty;

    public static bool IsSupervisor(this ClaimsPrincipal principal) =>
        principal.IsInRole(Roles.Supervisor);
}
