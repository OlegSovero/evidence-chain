using System.Security.Claims;
using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Anomalies;
using EvidenceChain.Api.Features.Auth;
using EvidenceChain.Api.Infrastructure.Http;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Transfers;

// GET /custody-transfers?status=pending&mine=true: the custodian's inbox. mine=false
// (everything) is reserved to supervisors.
public static class ListMyPendingTransfers
{
    public sealed record Response(IReadOnlyList<TransferResponse> Items);

    public static async Task<IResult> Handle(
        AppDbContext db,
        ClaimsPrincipal user,
        PendingTransferRule rule,
        CancellationToken cancellationToken,
        string? status = null,
        bool? mine = null)
    {
        var wanted = (status ?? "pending").Trim().ToLowerInvariant() switch
        {
            "pending" or "pendiente" => TransferStatus.Pendiente,
            "accepted" or "aceptada" => TransferStatus.Aceptada,
            "rejected" or "rechazada" => TransferStatus.Rechazada,
            _ => (TransferStatus?)null,
        };
        if (wanted is null)
        {
            return ApiProblems.BadRequest("Estado inválido", "status admite pending, accepted o rejected.",
                "invalid-status");
        }

        var onlyMine = mine ?? true;
        if (!onlyMine && !user.IsSupervisor())
        {
            return ApiProblems.Forbidden("Solo un Supervisor puede listar las transferencias de todos los custodios.");
        }

        var query = db.CustodyTransfers.AsNoTracking().Where(t => t.Status == wanted);
        if (onlyMine)
        {
            var userId = user.GetUserId();
            query = query.Where(t => t.ToCustodianId == userId);
        }

        var transfers = await query
            .OrderBy(t => t.RequestedAtUtc).ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(new Response(await TransferPresenter.BuildManyAsync(db, transfers, rule, cancellationToken)));
    }
}
