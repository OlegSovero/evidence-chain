using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Hashing;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Infrastructure.Persistence;

// The only place that creates custody events: next Sequence, link to the previous
// hash, sign, and bump the evidence's LastEventAtUtc. Caller owns the transaction.
public static class ChainAppender
{
    public static async Task<CustodyEvent> AppendAsync(
        AppDbContext db,
        Evidence evidence,
        CustodyEventType eventType,
        int actorUserId,
        int? fromCustodianId,
        int? toCustodianId,
        Guid? transferId,
        string? notes,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var last = await db.CustodyEvents.AsNoTracking()
            .Where(e => e.EvidenceId == evidence.Id)
            .OrderByDescending(e => e.Sequence)
            .Select(e => new { e.Sequence, e.Hash })
            .FirstOrDefaultAsync(cancellationToken);

        var custodyEvent = new CustodyEvent
        {
            EvidenceId = evidence.Id,
            Sequence = (last?.Sequence ?? 0) + 1,
            EventType = eventType,
            ActorUserId = actorUserId,
            FromCustodianId = fromCustodianId,
            ToCustodianId = toCustodianId,
            TransferId = transferId,
            Notes = notes,
            OccurredAtUtc = UtcDateTime.TruncateToMilliseconds(occurredAtUtc),
            PreviousHash = last?.Hash ?? ChainHasher.Genesis,
        };
        custodyEvent.Hash = ChainHasher.ComputeHash(custodyEvent);

        db.CustodyEvents.Add(custodyEvent);
        evidence.LastEventAtUtc = custodyEvent.OccurredAtUtc;

        return custodyEvent;
    }
}
