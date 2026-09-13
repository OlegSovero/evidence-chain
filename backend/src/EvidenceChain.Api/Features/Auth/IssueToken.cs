using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Infrastructure.Http;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Auth;

// Demo simplification: any known userName gets a signed JWT, no password.
// The authorization decisions still happen server-side from the token's claims.
public static class IssueToken
{
    public sealed record Request(string UserName);

    public sealed record DemoUser(int Id, string UserName, string DisplayName, string Role)
    {
        public static DemoUser From(User user) => new(user.Id, user.UserName, user.DisplayName, user.Role);
    }

    public sealed record Response(string AccessToken, string TokenType, DateTime ExpiresAtUtc, DemoUser User);

    public static async Task<IResult> Handle(
        Request request, AppDbContext db, TokenIssuer issuer, CancellationToken cancellationToken)
    {
        var userName = request.UserName?.Trim() ?? string.Empty;
        if (userName.Length == 0)
        {
            return ApiProblems.BadRequest("userName requerido", "Indica el userName de un usuario demo.");
        }

        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserName == userName, cancellationToken);
        if (user is null)
        {
            return Results.Problem($"No existe el usuario demo '{userName}'.",
                statusCode: StatusCodes.Status401Unauthorized, title: "Usuario desconocido",
                type: ApiProblems.TypeBase + "unknown-user");
        }

        var (token, expiresAtUtc) = issuer.Issue(user);
        return Results.Ok(new Response(token, "Bearer", expiresAtUtc, DemoUser.From(user)));
    }
}
