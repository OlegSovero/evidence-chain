using System.Security.Claims;
using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Anomalies;
using EvidenceChain.Api.Domain.Transfers;
using EvidenceChain.Api.Features.Auth;
using EvidenceChain.Api.Infrastructure.Http;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Transfers;

public sealed record RespondTransferRequest(string? Note);

// Shared by accept and reject: If-Match -> 428, wrong responder -> 403, state machine
// and ROWVERSION mismatch -> 409 with currentState. One transaction for transfer,
// event and evidence.
public static class TransferResponder
{
    public const int MaxNoteLength = 500;

    public static async Task<IResult> Respond(
        Guid id,
        TransferAction action,
        RespondTransferRequest? request,
        AppDbContext db,
        ClaimsPrincipal user,
        TimeProvider timeProvider,
        PendingTransferRule rule,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var ifMatch = http.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return ApiProblems.PreconditionRequired();
        }

        if (!ETag.TryParse(ifMatch, out var expectedVersion))
        {
            return ApiProblems.BadRequest("If-Match inválido",
                "If-Match debe contener el ETag fuerte devuelto por la API (\"0x…\").", "invalid-if-match");
        }

        var note = request?.Note?.Trim();
        if (note is { Length: > MaxNoteLength })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["note"] = [$"La nota admite hasta {MaxNoteLength} caracteres."],
            });
        }

        var transfer = await db.CustodyTransfers.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (transfer is null)
        {
            return ApiProblems.NotFound($"No existe la transferencia {id}.");
        }

        var userId = user.GetUserId();
        if (transfer.ToCustodianId != userId)
        {
            return ApiProblems.Forbidden("Solo el custodio destinatario puede aceptar o rechazar esta transferencia.");
        }

        var transition = TransferStateMachine.Apply(transfer.Status, action);
        if (!transition.IsAllowed)
        {
            var state = (await TransferPresenter.BuildAsync(db, transfer, rule, cancellationToken)).ToCurrentState();
            return InvalidTransitionConflict(state);
        }

        var evidence = await db.Evidence.SingleAsync(e => e.Id == transfer.EvidenceId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        db.Entry(transfer).Property(t => t.RowVersion).OriginalValue = expectedVersion;
        transfer.Status = transition.Next!.Value;
        transfer.RespondedAtUtc = UtcDateTime.TruncateToMilliseconds(now);
        transfer.RespondedByUserId = userId;
        transfer.ResponseNote = string.IsNullOrEmpty(note) ? null : note;

        if (action == TransferAction.Accept)
        {
            evidence.CurrentCustodianId = transfer.ToCustodianId;
        }

        await ChainAppender.AppendAsync(db, evidence, transition.EventType!.Value, userId,
            transfer.FromCustodianId, transfer.ToCustodianId, transfer.Id, transfer.ResponseNote, now,
            cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var state = await TransferPresenter.CurrentStateAsync(db, id, rule, cancellationToken);
            return StaleVersionConflict(state);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var state = await TransferPresenter.CurrentStateAsync(db, id, rule, cancellationToken);
            return StaleVersionConflict(state);
        }

        var response = await TransferPresenter.BuildAsync(db, transfer, evidence, rule, cancellationToken);
        http.Response.Headers.ETag = response.Version;
        return Results.Ok(response);
    }

    private static IResult InvalidTransitionConflict(TransferCurrentState state) =>
        ApiProblems.Conflict("transfer-already-resolved", "La transferencia ya fue resuelta",
            state.RespondedBy is null
                ? $"La transferencia está en estado {state.Status} y no admite más cambios."
                : $"{state.RespondedBy} ya la marcó como {state.Status} el " +
                  $"{state.RespondedAtUtc:yyyy-MM-dd'T'HH:mm:ss'Z'}.",
            state);

    private static IResult StaleVersionConflict(TransferCurrentState? state) =>
        ApiProblems.Conflict("transfer-version-conflict", "La transferencia cambió desde que la leíste",
            state is null
                ? "La transferencia ya no existe."
                : state.RespondedBy is null
                    ? $"La versión actual es {state.Version}. Recarga y vuelve a intentarlo."
                    : $"{state.RespondedBy} la marcó como {state.Status} el " +
                      $"{state.RespondedAtUtc:yyyy-MM-dd'T'HH:mm:ss'Z'}.",
            state);
}
