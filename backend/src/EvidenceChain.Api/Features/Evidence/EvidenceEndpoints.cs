using EvidenceChain.Api.Features.Chain;

namespace EvidenceChain.Api.Features.Evidence;

public static class EvidenceEndpoints
{
    public static RouteGroupBuilder MapEvidenceEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/evidence").WithTags("Evidence").RequireAuthorization();

        group.MapGet("/", ListEvidence.Handle)
            .WithName("ListEvidence")
            .WithSummary("Bandeja de evidencias con filtros, orden por fecha y paginación keyset.")
            .Produces<EvidencePage>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:int}", GetEvidence.Handle)
            .WithName("GetEvidence")
            .WithSummary("Detalle de una evidencia con su transferencia pendiente y anomalía.")
            .Produces<EvidenceDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:int}/chain", GetChain.Handle)
            .WithName("GetChain")
            .WithSummary("Línea de tiempo de eventos de custodia con sus hashes.")
            .Produces<GetChain.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:int}/chain/verify", VerifyChain.Handle)
            .WithName("VerifyChain")
            .WithSummary("Recalcula la cadena y devuelve el primer evento inválido si lo hay.")
            .Produces<VerifyChain.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
