using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Features.Common;
using EvidenceChain.Api.Infrastructure.Http;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Evidence;

public static class ListEvidence
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static async Task<IResult> Handle(
        AppDbContext db,
        CancellationToken cancellationToken,
        string? q = null,
        int? custodianId = null,
        string? status = null,
        string? sort = null,
        string? cursor = null,
        int? pageSize = null)
    {
        var descending = sort?.ToLowerInvariant() switch
        {
            null or "" or "desc" => true,
            "asc" => false,
            _ => (bool?)null,
        };
        if (descending is null)
        {
            return ApiProblems.BadRequest("Orden inválido", "sort admite 'asc' o 'desc'.", "invalid-sort");
        }

        EvidenceIntegrityStatus? integrityStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<EvidenceIntegrityStatus>(status, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return ApiProblems.BadRequest("Estado inválido",
                    "status admite NoVerificada, Integra o Comprometida.", "invalid-status");
            }

            integrityStatus = parsed;
        }

        KeysetCursor? keyset = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!KeysetCursor.TryDecode(cursor, out var decoded))
            {
                return ApiProblems.BadRequest("Cursor inválido", "El cursor no es válido.", "invalid-cursor");
            }

            if (decoded.Descending != descending.Value)
            {
                return ApiProblems.BadRequest("Cursor inválido",
                    "El cursor pertenece a otro orden; vuelve a la primera página.", "invalid-cursor");
            }

            keyset = decoded;
        }

        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);

        IQueryable<Domain.Evidence> query = db.Evidence.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e => e.Code.Contains(term) || e.Description.Contains(term));
        }

        if (custodianId is { } custodian)
        {
            query = query.Where(e => e.CurrentCustodianId == custodian);
        }

        if (integrityStatus is { } wanted)
        {
            query = query.Where(e => e.IntegrityStatus == wanted);
        }

        if (keyset is { } after)
        {
            query = descending.Value
                ? query.Where(e => e.LastEventAtUtc < after.LastEventAtUtc
                    || (e.LastEventAtUtc == after.LastEventAtUtc && e.Id < after.Id))
                : query.Where(e => e.LastEventAtUtc > after.LastEventAtUtc
                    || (e.LastEventAtUtc == after.LastEventAtUtc && e.Id > after.Id));
        }

        query = descending.Value
            ? query.OrderByDescending(e => e.LastEventAtUtc).ThenByDescending(e => e.Id)
            : query.OrderBy(e => e.LastEventAtUtc).ThenBy(e => e.Id);

        var rows = await query
            .Take(size + 1)
            .Select(e => new EvidenceListItem(
                e.Id,
                e.Code,
                e.Description,
                new UserSummary(e.CurrentCustodian!.Id, e.CurrentCustodian.UserName, e.CurrentCustodian.DisplayName),
                e.LastEventAtUtc,
                e.IntegrityStatus))
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (rows.Count > size)
        {
            rows.RemoveAt(rows.Count - 1);
            var last = rows[^1];
            nextCursor = new KeysetCursor(last.LastEventAtUtc, last.Id, descending.Value).Encode();
        }

        return Results.Ok(new EvidencePage(rows, nextCursor));
    }
}
