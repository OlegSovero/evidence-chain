using System.Security.Claims;
using EvidenceChain.Api.Domain.Anomalies;
using EvidenceChain.Api.Domain.Transfers;
using EvidenceChain.Api.Infrastructure.Persistence;

namespace EvidenceChain.Api.Features.Transfers;

public static class AcceptTransfer
{
    public static Task<IResult> Handle(
        Guid id,
        RespondTransferRequest? request,
        AppDbContext db,
        ClaimsPrincipal user,
        TimeProvider timeProvider,
        PendingTransferRule rule,
        HttpContext http,
        CancellationToken cancellationToken) =>
        TransferResponder.Respond(id, TransferAction.Accept, request, db, user, timeProvider, rule, http,
            cancellationToken);
}
