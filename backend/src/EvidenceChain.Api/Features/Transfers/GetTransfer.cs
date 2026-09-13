using EvidenceChain.Api.Domain.Anomalies;
using EvidenceChain.Api.Infrastructure.Http;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Features.Transfers;

// Target of the 201 Location header and the UI's reconciliation call after a 409.
public static class GetTransfer
{
    public static async Task<IResult> Handle(
        Guid id, AppDbContext db, PendingTransferRule rule, HttpContext http, CancellationToken cancellationToken)
    {
        var transfer = await db.CustodyTransfers.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (transfer is null)
        {
            return ApiProblems.NotFound($"No existe la transferencia {id}.");
        }

        var response = await TransferPresenter.BuildAsync(db, transfer, rule, cancellationToken);
        http.Response.Headers.ETag = response.Version;
        return Results.Ok(response);
    }
}
