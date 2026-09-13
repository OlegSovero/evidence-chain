using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Auth;

// Anonymous on purpose: the SPA shows a "log in as" picker before any token exists.
public static class ListDemoUsers
{
    public sealed record Response(IReadOnlyList<IssueToken.DemoUser> Items);

    public static async Task<IResult> Handle(AppDbContext db, CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .OrderBy(u => u.Role).ThenBy(u => u.UserName)
            .Select(u => new IssueToken.DemoUser(u.Id, u.UserName, u.DisplayName, u.Role))
            .ToListAsync(cancellationToken);

        return Results.Ok(new Response(users));
    }
}
