using System.Security.Claims;
using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Anomalies;
using EvidenceChain.Api.Features.Auth;
using EvidenceChain.Api.Infrastructure.Http;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Transfers;

public static class RequestTransfer
{
    public sealed record Request(int EvidenceId, int ToCustodianId, string Reason);

    public const int MaxReasonLength = 500;

    public static async Task<IResult> Handle(
        Request request,
        AppDbContext db,
        ClaimsPrincipal user,
        TimeProvider timeProvider,
        PendingTransferRule rule,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > MaxReasonLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["reason"] = [$"El motivo es obligatorio y admite hasta {MaxReasonLength} caracteres."],
            });
        }

        var evidence = await db.Evidence.SingleOrDefaultAsync(e => e.Id == request.EvidenceId, cancellationToken);
        if (evidence is null)
        {
            return ApiProblems.NotFound($"No existe la evidencia {request.EvidenceId}.");
        }

        var destinationExists = await db.Users.AnyAsync(u => u.Id == request.ToCustodianId, cancellationToken);
        if (!destinationExists)
        {
            return ApiProblems.UnprocessableEntity("Custodio destino inválido",
                $"No existe el usuario {request.ToCustodianId}.", "unknown-custodian");
        }

        if (request.ToCustodianId == evidence.CurrentCustodianId)
        {
            return ApiProblems.UnprocessableEntity("Custodio destino inválido",
                "El custodio destino ya es el custodio actual de la evidencia.", "same-custodian");
        }

        var existing = await db.CustodyTransfers.AsNoTracking()
            .SingleOrDefaultAsync(t => t.EvidenceId == evidence.Id && t.Status == TransferStatus.Pendiente,
                cancellationToken);
        if (existing is not null)
        {
            return PendingConflict(await TransferPresenter.BuildAsync(db, existing, evidence, rule, cancellationToken));
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var userId = user.GetUserId();
        var transfer = new CustodyTransfer
        {
            Id = Guid.CreateVersion7(),
            EvidenceId = evidence.Id,
            FromCustodianId = evidence.CurrentCustodianId,
            ToCustodianId = request.ToCustodianId,
            RequestedByUserId = userId,
            Status = TransferStatus.Pendiente,
            Reason = reason,
            RequestedAtUtc = UtcDateTime.TruncateToMilliseconds(now),
        };
        db.CustodyTransfers.Add(transfer);

        await ChainAppender.AppendAsync(db, evidence, CustodyEventType.TransferenciaSolicitada, userId,
            evidence.CurrentCustodianId, request.ToCustodianId, transfer.Id, reason, now, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Lost the race against a concurrent request for the same evidence: either the
            // filtered unique index (one pending per evidence) or (EvidenceId, Sequence).
            db.ChangeTracker.Clear();
            var winner = await db.CustodyTransfers.AsNoTracking()
                .SingleOrDefaultAsync(t => t.EvidenceId == evidence.Id && t.Status == TransferStatus.Pendiente,
                    cancellationToken);

            return winner is not null
                ? PendingConflict(await TransferPresenter.BuildAsync(db, winner, rule, cancellationToken))
                : ApiProblems.Conflict("evidence-changed", "La evidencia cambió durante la solicitud",
                    "Otra operación modificó la cadena de custodia de esta evidencia. Vuelve a intentarlo.", null);
        }

        var response = await TransferPresenter.BuildAsync(db, transfer, evidence, rule, cancellationToken);
        http.Response.Headers.ETag = response.Version;
        return TypedResults.Created($"/api/v1/custody-transfers/{transfer.Id}", response);
    }

    private static IResult PendingConflict(TransferResponse pending) =>
        ApiProblems.Conflict("transfer-already-pending", "La evidencia ya tiene una transferencia pendiente",
            $"La transferencia {pending.Id} hacia {pending.ToCustodian.UserName} sigue pendiente desde " +
            $"{pending.RequestedAtUtc:yyyy-MM-dd'T'HH:mm:ss'Z'}.",
            pending.ToCurrentState());
}
