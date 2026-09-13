namespace EvidenceChain.Api.Features.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth").AllowAnonymous();

        group.MapPost("/token", IssueToken.Handle)
            .WithName("IssueToken")
            .WithSummary("Emite un JWT para un usuario demo (sin contraseña).")
            .Produces<IssueToken.Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/users", ListDemoUsers.Handle)
            .WithName("ListDemoUsers")
            .WithSummary("Usuarios demo disponibles para iniciar sesión.")
            .Produces<ListDemoUsers.Response>();

        return group;
    }
}
