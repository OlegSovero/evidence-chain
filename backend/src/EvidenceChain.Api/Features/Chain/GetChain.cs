using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Anomalies;
using EvidenceChain.Api.Features.Common;
using EvidenceChain.Api.Infrastructure.Http;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Chain;

public static class GetChain
{
    public sealed record ChainEvent(
        long Id,
        int Sequence,
        CustodyEventType EventType,
        UserSummary Actor,
        UserSummary? FromCustodian,
        UserSummary? ToCustodian,
        Guid? TransferId,
        string? Notes,
        DateTime OccurredAtUtc,
        string PreviousHash,
        string Hash,
        PendingTransferAnomaly? Anomaly);

    public sealed record Response(int EvidenceId, string Code, IReadOnlyList<ChainEvent> Events);

    public static async Task<IResult> Handle(
        int id, AppDbContext db, PendingTransferRule rule, CancellationToken cancellationToken)
    {
        var evidence = await db.Evidence.AsNoTracking()
            .Select(e => new { e.Id, e.Code })
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (evidence is null)
        {
            return ApiProblems.NotFound($"No existe la evidencia {id}.");
        }

        var events = await db.CustodyEvents.AsNoTracking()
            .Where(e => e.EvidenceId == id)
            .OrderBy(e => e.Sequence)
            .ToListAsync(cancellationToken);

        var pending = await db.CustodyTransfers.AsNoTracking()
            .SingleOrDefaultAsync(t => t.EvidenceId == id && t.Status == TransferStatus.Pendiente, cancellationToken);

        var users = await UserLookup.LoadAsync(db,
            events.SelectMany(e => new int?[] { e.ActorUserId, e.FromCustodianId, e.ToCustodianId }),
            cancellationToken);

        var items = events.Select(e => new ChainEvent(
            e.Id,
            e.Sequence,
            e.EventType,
            users.Summary(e.ActorUserId),
            users.SummaryOrNull(e.FromCustodianId),
            users.SummaryOrNull(e.ToCustodianId),
            e.TransferId,
            e.Notes,
            e.OccurredAtUtc,
            Convert.ToHexStringLower(e.PreviousHash),
            Convert.ToHexStringLower(e.Hash),
            AnomalyFor(e, pending, users, rule))).ToList();

        return Results.Ok(new Response(evidence.Id, evidence.Code, items));
    }

    // The anomaly is pinned to the request event of the overdue pending transfer.
    private static PendingTransferAnomaly? AnomalyFor(
        CustodyEvent custodyEvent, CustodyTransfer? pending, IReadOnlyDictionary<int, User> users,
        PendingTransferRule rule)
    {
        if (pending is null
            || custodyEvent.EventType != CustodyEventType.TransferenciaSolicitada
            || custodyEvent.TransferId != pending.Id)
        {
            return null;
        }

        return rule.Evaluate(pending.Status, pending.RequestedAtUtc, users.Summary(pending.ToCustodianId).UserName);
    }
}
