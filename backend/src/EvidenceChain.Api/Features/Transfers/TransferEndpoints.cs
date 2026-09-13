using EvidenceChain.Api.Features.Auth;
using EvidenceChain.Api.Infrastructure.Idempotency;

namespace EvidenceChain.Api.Features.Transfers;

public static class TransferEndpoints
{
    public static RouteGroupBuilder MapTransferEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/custody-transfers").WithTags("Transfers").RequireAuthorization();

        group.MapGet("/", ListMyPendingTransfers.Handle)
            .WithName("ListTransfers")
            .WithSummary("Bandeja de transferencias del custodio actual (status=pending&mine=true).")
            .Produces<ListMyPendingTransfers.Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetTransfer.Handle)
            .WithName("GetTransfer")
            .WithSummary("Estado actual de una transferencia (con ETag).")
            .Produces<TransferResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", RequestTransfer.Handle)
            .RequireAuthorization(Policies.TransferRequester)
            .AddEndpointFilter<IdempotencyEndpointFilter<RequestTransfer.Request>>()
            .WithName("RequestTransfer")
            .WithSummary("Solicita una transferencia de custodia. Requiere Idempotency-Key.")
            .Produces<TransferResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/accept", AcceptTransfer.Handle)
            .WithName("AcceptTransfer")
            .WithSummary("Acepta la transferencia y mueve la custodia. Requiere If-Match.")
            .Produces<TransferResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        group.MapPost("/{id:guid}/reject", RejectTransfer.Handle)
            .WithName("RejectTransfer")
            .WithSummary("Rechaza la transferencia; la custodia no cambia. Requiere If-Match.")
            .Produces<TransferResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        return group;
    }
}
