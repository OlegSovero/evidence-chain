using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Common;

public sealed record UserSummary(int Id, string UserName, string DisplayName)
{
    public static UserSummary From(User user) => new(user.Id, user.UserName, user.DisplayName);
}

public static class UserLookup
{
    public static async Task<Dictionary<int, User>> LoadAsync(
        AppDbContext db, IEnumerable<int?> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (wanted.Length == 0)
        {
            return [];
        }

        return await db.Users.AsNoTracking()
            .Where(u => wanted.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
    }

    public static UserSummary Summary(this IReadOnlyDictionary<int, User> users, int id) =>
        users.TryGetValue(id, out var user)
            ? UserSummary.From(user)
            : throw new InvalidOperationException($"User {id} was not loaded.");

    public static UserSummary? SummaryOrNull(this IReadOnlyDictionary<int, User> users, int? id) =>
        id is { } value ? users.Summary(value) : null;
}
