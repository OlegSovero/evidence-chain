using System.Net.Http.Json;
using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Infrastructure.Persistence;

namespace EvidenceChain.Tests.Integration.Api;

internal sealed record Actors(User Investigator, User CustodianA, User CustodianB, User Supervisor);

internal static class TestData
{
    public static readonly DateTime Base = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    public static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    public static async Task<Actors> SeedActorsAsync(AppDbContext db, string suffix)
    {
        var actors = new Actors(
            new User { UserName = $"inv-{suffix}", DisplayName = "Investigador de prueba", Role = Roles.Investigador },
            new User { UserName = $"cusA-{suffix}", DisplayName = "Custodio A", Role = Roles.Custodio },
            new User { UserName = $"cusB-{suffix}", DisplayName = "Custodio B", Role = Roles.Custodio },
            new User { UserName = $"sup-{suffix}", DisplayName = "Supervisor de prueba", Role = Roles.Supervisor });

        db.Users.AddRange(actors.Investigator, actors.CustodianA, actors.CustodianB, actors.Supervisor);
        await db.SaveChangesAsync();
        return actors;
    }

    public static async Task<Evidence> SeedEvidenceAsync(
        AppDbContext db, string code, User custodian, User registeredBy, DateTime at, string description = "Evidencia de prueba")
    {
        var evidence = new Evidence
        {
            Code = code,
            Description = description,
            CurrentCustodianId = custodian.Id,
            LastEventAtUtc = at,
            CreatedAtUtc = at,
        };
        db.Evidence.Add(evidence);
        await db.SaveChangesAsync();

        await ChainAppender.AppendAsync(db, evidence, CustodyEventType.Registrada, registeredBy.Id, null,
            custodian.Id, null, "Registro inicial", at, CancellationToken.None);
        await db.SaveChangesAsync();
        return evidence;
    }

    public static async Task<CustodyTransfer> SeedPendingTransferAsync(
        AppDbContext db, Evidence evidence, User from, User to, User requestedBy, DateTime requestedAt)
    {
        var transfer = new CustodyTransfer
        {
            Id = Guid.CreateVersion7(),
            EvidenceId = evidence.Id,
            FromCustodianId = from.Id,
            ToCustodianId = to.Id,
            RequestedByUserId = requestedBy.Id,
            Status = TransferStatus.Pendiente,
            Reason = "Análisis en laboratorio",
            RequestedAtUtc = requestedAt,
        };
        db.CustodyTransfers.Add(transfer);
        await ChainAppender.AppendAsync(db, evidence, CustodyEventType.TransferenciaSolicitada, requestedBy.Id,
            from.Id, to.Id, transfer.Id, transfer.Reason, requestedAt, CancellationToken.None);
        await db.SaveChangesAsync();
        return transfer;
    }

    public static HttpRequestMessage Post(string url, object? body, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return request;
    }
}
