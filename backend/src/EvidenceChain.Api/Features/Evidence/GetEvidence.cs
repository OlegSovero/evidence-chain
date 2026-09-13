using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Anomalies;
using EvidenceChain.Api.Features.Common;
using EvidenceChain.Api.Features.Transfers;
using EvidenceChain.Api.Infrastructure.Http;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Evidence;

public static class GetEvidence
{
    public static async Task<IResult> Handle(
        int id, AppDbContext db, PendingTransferRule rule, CancellationToken cancellationToken)
    {
        var evidence = await db.Evidence.AsNoTracking()
            .Include(e => e.CurrentCustodian)
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (evidence is null)
        {
            return ApiProblems.NotFound($"No existe la evidencia {id}.");
        }

        var eventCount = await db.CustodyEvents.CountAsync(e => e.EvidenceId == id, cancellationToken);

        var pending = await db.CustodyTransfers.AsNoTracking()
            .SingleOrDefaultAsync(t => t.EvidenceId == id && t.Status == TransferStatus.Pendiente, cancellationToken);
        var pendingTransfer = pending is null
            ? null
            : await TransferPresenter.BuildAsync(db, pending, evidence, rule, cancellationToken);

        return Results.Ok(new EvidenceDetail(
            evidence.Id,
            evidence.Code,
            evidence.Description,
            UserSummary.From(evidence.CurrentCustodian!),
            evidence.LastEventAtUtc,
            evidence.IntegrityStatus,
            evidence.IntegrityCheckedAtUtc,
            evidence.CreatedAtUtc,
            eventCount,
            pendingTransfer));
    }
}
