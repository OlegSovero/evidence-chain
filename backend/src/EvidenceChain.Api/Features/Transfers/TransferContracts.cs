using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Anomalies;
using EvidenceChain.Api.Features.Common;
using EvidenceChain.Api.Infrastructure.Http;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Transfers;

public sealed record EvidenceSummary(int Id, string Code, string Description)
{
    public static EvidenceSummary From(Domain.Evidence evidence) => new(evidence.Id, evidence.Code, evidence.Description);
}

// What a 409 carries so the UI can explain what happened without another round trip.
public sealed record TransferCurrentState(
    Guid TransferId,
    TransferStatus Status,
    string Version,
    string? RespondedBy,
    DateTime? RespondedAtUtc);

public sealed record TransferResponse(
    Guid Id,
    EvidenceSummary Evidence,
    TransferStatus Status,
    string Reason,
    DateTime RequestedAtUtc,
    DateTime? RespondedAtUtc,
    string? ResponseNote,
    UserSummary FromCustodian,
    UserSummary ToCustodian,
    UserSummary RequestedBy,
    UserSummary? RespondedBy,
    string Version,
    PendingTransferAnomaly? Anomaly)
{
    public TransferCurrentState ToCurrentState() =>
        new(Id, Status, Version, RespondedBy?.UserName, RespondedAtUtc);
}

public static class TransferPresenter
{
    public static TransferResponse Build(
        CustodyTransfer transfer, Domain.Evidence evidence, IReadOnlyDictionary<int, User> users, PendingTransferRule rule)
    {
        var toCustodian = users.Summary(transfer.ToCustodianId);

        return new TransferResponse(
            transfer.Id,
            EvidenceSummary.From(evidence),
            transfer.Status,
            transfer.Reason,
            transfer.RequestedAtUtc,
            transfer.RespondedAtUtc,
            transfer.ResponseNote,
            users.Summary(transfer.FromCustodianId),
            toCustodian,
            users.Summary(transfer.RequestedByUserId),
            users.SummaryOrNull(transfer.RespondedByUserId),
            ETag.From(transfer.RowVersion),
            rule.Evaluate(transfer.Status, transfer.RequestedAtUtc, toCustodian.UserName));
    }

    public static async Task<TransferResponse> BuildAsync(
        AppDbContext db, CustodyTransfer transfer, PendingTransferRule rule, CancellationToken cancellationToken)
    {
        var evidence = await db.Evidence.AsNoTracking()
            .SingleAsync(e => e.Id == transfer.EvidenceId, cancellationToken);
        return await BuildAsync(db, transfer, evidence, rule, cancellationToken);
    }

    public static async Task<TransferResponse> BuildAsync(
        AppDbContext db, CustodyTransfer transfer, Domain.Evidence evidence, PendingTransferRule rule,
        CancellationToken cancellationToken)
    {
        var users = await UserLookup.LoadAsync(db, UserIds(transfer), cancellationToken);
        return Build(transfer, evidence, users, rule);
    }

    public static async Task<List<TransferResponse>> BuildManyAsync(
        AppDbContext db, IReadOnlyList<CustodyTransfer> transfers, PendingTransferRule rule,
        CancellationToken cancellationToken)
    {
        if (transfers.Count == 0)
        {
            return [];
        }

        var evidenceIds = transfers.Select(t => t.EvidenceId).Distinct().ToArray();
        var evidence = await db.Evidence.AsNoTracking()
            .Where(e => evidenceIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);
        var users = await UserLookup.LoadAsync(db, transfers.SelectMany(UserIds), cancellationToken);

        return transfers.Select(t => Build(t, evidence[t.EvidenceId], users, rule)).ToList();
    }

    public static async Task<TransferCurrentState?> CurrentStateAsync(
        AppDbContext db, Guid transferId, PendingTransferRule rule, CancellationToken cancellationToken)
    {
        var transfer = await db.CustodyTransfers.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == transferId, cancellationToken);
        return transfer is null ? null : (await BuildAsync(db, transfer, rule, cancellationToken)).ToCurrentState();
    }

    private static IEnumerable<int?> UserIds(CustodyTransfer transfer) =>
        [transfer.FromCustodianId, transfer.ToCustodianId, transfer.RequestedByUserId, transfer.RespondedByUserId];
}
