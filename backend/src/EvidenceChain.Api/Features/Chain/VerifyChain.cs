using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Hashing;
using EvidenceChain.Api.Infrastructure.Http;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Chain;

public static class VerifyChain
{
    public sealed record InvalidEvent(int Sequence, ChainFailureReason Reason);

    public sealed record Response(
        int EvidenceId,
        bool IsValid,
        EvidenceIntegrityStatus IntegrityStatus,
        DateTime CheckedAtUtc,
        int EventCount,
        InvalidEvent? FirstInvalidEvent);

    public static async Task<IResult> Handle(
        int id, AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var evidence = await db.Evidence.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (evidence is null)
        {
            return ApiProblems.NotFound($"No existe la evidencia {id}.");
        }

        var events = await db.CustodyEvents.AsNoTracking()
            .Where(e => e.EvidenceId == id)
            .OrderBy(e => e.Sequence)
            .ToListAsync(cancellationToken);

        var result = ChainVerifier.Verify(events);
        var checkedAt = UtcDateTime.TruncateToMilliseconds(timeProvider.GetUtcNow().UtcDateTime);

        // Cached verdict for the inbox column; /verify itself always recomputes from the events.
        evidence.IntegrityStatus = result.IsValid
            ? EvidenceIntegrityStatus.Integra
            : EvidenceIntegrityStatus.Comprometida;
        evidence.IntegrityCheckedAtUtc = checkedAt;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(
            evidence.Id,
            result.IsValid,
            evidence.IntegrityStatus,
            checkedAt,
            events.Count,
            result.IsValid ? null : new InvalidEvent(result.FirstInvalidSequence!.Value, result.Reason!.Value)));
    }
}
