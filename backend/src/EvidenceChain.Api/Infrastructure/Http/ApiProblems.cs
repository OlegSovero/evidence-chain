namespace EvidenceChain.Api.Infrastructure.Http;

// Every 4xx produced by handlers goes through here so the shape is uniform
// (application/problem+json, stable "type" URIs, currentState on 409).
public static class ApiProblems
{
    public const string TypeBase = "https://evidence-chain/errors/";

    public static IResult NotFound(string detail) =>
        Results.Problem(detail, statusCode: StatusCodes.Status404NotFound, title: "Recurso no encontrado",
            type: TypeBase + "not-found");

    public static IResult BadRequest(string title, string detail, string type = "bad-request") =>
        Results.Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: title, type: TypeBase + type);

    public static IResult Forbidden(string detail) =>
        Results.Problem(detail, statusCode: StatusCodes.Status403Forbidden, title: "Operación no permitida",
            type: TypeBase + "forbidden");

    public static IResult UnprocessableEntity(string title, string detail, string type = "unprocessable") =>
        Results.Problem(detail, statusCode: StatusCodes.Status422UnprocessableEntity, title: title,
            type: TypeBase + type);

    public static IResult PreconditionRequired() =>
        Results.Problem("La operación requiere la cabecera If-Match con el ETag actual de la transferencia.",
            statusCode: StatusCodes.Status428PreconditionRequired, title: "Falta la cabecera If-Match",
            type: TypeBase + "precondition-required");

    public static IResult Conflict(string type, string title, string detail, object? currentState) =>
        Results.Problem(detail, statusCode: StatusCodes.Status409Conflict, title: title, type: TypeBase + type,
            extensions: new Dictionary<string, object?> { ["currentState"] = currentState });
}
