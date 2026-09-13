using System.Security.Cryptography;
using System.Text.Json;
using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Features.Auth;
using EvidenceChain.Api.Infrastructure.Http;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EvidenceChain.Api.Infrastructure.Idempotency;

// Idempotency-Key scoped per user. A placeholder row is inserted in the same
// transaction as the operation, so a concurrent retry blocks on the primary key
// until the first request commits and then replays the stored response.
public sealed class IdempotencyEndpointFilter<TRequest>(
    AppDbContext db,
    IOptions<JsonOptions> jsonOptions,
    TimeProvider timeProvider) : IEndpointFilter
    where TRequest : class
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";
    private const int MaxKeyLength = 100;

    private sealed record StoredResponse(string Body, string? ETag, string? Location);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers[HeaderName].ToString().Trim();

        if (key.Length == 0)
        {
            return ApiProblems.BadRequest("Falta la cabecera Idempotency-Key",
                "Las escrituras requieren una cabecera Idempotency-Key única por operación.", "idempotency-key-required");
        }

        if (key.Length > MaxKeyLength)
        {
            return ApiProblems.BadRequest("Idempotency-Key demasiado larga",
                $"La cabecera Idempotency-Key admite hasta {MaxKeyLength} caracteres.", "idempotency-key-invalid");
        }

        var userId = http.User.GetUserId();
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault()
            ?? throw new InvalidOperationException($"The endpoint does not bind a {typeof(TRequest).Name} body.");
        var serializerOptions = jsonOptions.Value.SerializerOptions;
        var requestHash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, serializerOptions));

        var existing = await db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == userId && r.IdempotencyKey == key, http.RequestAborted);
        if (existing is not null)
        {
            return Replay(existing, requestHash, http);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);

        var record = new IdempotencyRecord
        {
            UserId = userId,
            IdempotencyKey = key,
            RequestHash = requestHash,
            StatusCode = 0,
            ResponseBody = string.Empty,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.IdempotencyRecords.Add(record);

        try
        {
            await db.SaveChangesAsync(http.RequestAborted);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            await transaction.RollbackAsync(http.RequestAborted);
            db.ChangeTracker.Clear();

            var committed = await db.IdempotencyRecords.AsNoTracking()
                .SingleAsync(r => r.UserId == userId && r.IdempotencyKey == key, http.RequestAborted);
            return Replay(committed, requestHash, http);
        }

        object? result;
        try
        {
            result = await next(context);
        }
        catch
        {
            await transaction.RollbackAsync(http.RequestAborted);
            throw;
        }

        if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 } success
            && result is IValueHttpResult { Value: { } value })
        {
            var stored = new StoredResponse(
                JsonSerializer.Serialize(value, value.GetType(), serializerOptions),
                http.Response.Headers.ETag.ToString() is { Length: > 0 } etag ? etag : null,
                result.GetType().GetProperty("Location")?.GetValue(result) as string);

            record.StatusCode = success.StatusCode!.Value;
            record.ResponseBody = JsonSerializer.Serialize(stored, serializerOptions);
            await db.SaveChangesAsync(http.RequestAborted);
            await transaction.CommitAsync(http.RequestAborted);
        }
        else
        {
            await transaction.RollbackAsync(http.RequestAborted);
        }

        return result;
    }

    private IResult Replay(IdempotencyRecord record, byte[] requestHash, HttpContext http)
    {
        if (!record.RequestHash.AsSpan().SequenceEqual(requestHash))
        {
            return ApiProblems.UnprocessableEntity("Idempotency-Key reutilizada con otro cuerpo",
                "Ya existe una operación con esta Idempotency-Key y un cuerpo distinto. Usa una clave nueva.",
                "idempotency-key-reused");
        }

        var stored = JsonSerializer.Deserialize<StoredResponse>(record.ResponseBody, jsonOptions.Value.SerializerOptions)
            ?? throw new InvalidOperationException("Stored idempotent response is empty.");

        http.Response.Headers[ReplayedHeaderName] = "true";
        if (stored.ETag is not null)
        {
            http.Response.Headers.ETag = stored.ETag;
        }

        if (stored.Location is not null)
        {
            http.Response.Headers.Location = stored.Location;
        }

        return Results.Content(stored.Body, "application/json", statusCode: record.StatusCode);
    }
}
