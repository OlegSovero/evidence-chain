using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Hashing;
using EvidenceChain.Api.Features.Chain;
using EvidenceChain.Api.Features.Transfers;
using EvidenceChain.Api.Infrastructure.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Tests.Integration.Api;

[Collection(ApiCollection.Name)]
public class TransferEndpointsTests(ApiFixture fixture)
{
    private sealed record Scenario(Actors Actors, Evidence Evidence);

    [Fact]
    public async Task Same_idempotency_key_creates_one_transfer_and_replays_the_response()
    {
        var scenario = await SeedAsync();
        using var client = await fixture.ClientAsAsync(scenario.Actors.Investigator);
        var body = RequestBody(scenario);
        var key = Guid.NewGuid().ToString();

        var first = await client.SendAsync(TestData.Post("/api/v1/custody-transfers", body, ("Idempotency-Key", key)));
        var second = await client.SendAsync(TestData.Post("/api/v1/custody-transfers", body, ("Idempotency-Key", key)));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(first.Headers.Contains(IdempotencyEndpointFilter<object>.ReplayedHeaderName));
        Assert.True(second.Headers.Contains(IdempotencyEndpointFilter<object>.ReplayedHeaderName));
        Assert.Equal(first.Headers.ETag, second.Headers.ETag);
        Assert.Equal(first.Headers.Location, second.Headers.Location);

        var created = await first.Content.ReadAsync<TransferResponse>();
        var replayed = await second.Content.ReadAsync<TransferResponse>();
        Assert.NotNull(created);
        Assert.Equal(created, replayed);
        Assert.Equal(TransferStatus.Pendiente, created.Status);
        Assert.Equal(created.Version, first.Headers.ETag!.ToString());
        Assert.Equal($"/api/v1/custody-transfers/{created.Id}", first.Headers.Location!.ToString());

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.CustodyTransfers.CountAsync(t => t.EvidenceId == scenario.Evidence.Id));
        Assert.Equal(2, await db.CustodyEvents.CountAsync(e => e.EvidenceId == scenario.Evidence.Id));
        var evidence = await db.Evidence.SingleAsync(e => e.Id == scenario.Evidence.Id);
        Assert.Equal(scenario.Actors.CustodianA.Id, evidence.CurrentCustodianId);
        Assert.Equal(created.RequestedAtUtc, evidence.LastEventAtUtc);
    }

    [Fact]
    public async Task Same_idempotency_key_with_a_different_body_returns_422()
    {
        var scenario = await SeedAsync();
        using var client = await fixture.ClientAsAsync(scenario.Actors.Investigator);
        var key = Guid.NewGuid().ToString();

        var first = await client.SendAsync(TestData.Post("/api/v1/custody-transfers", RequestBody(scenario), ("Idempotency-Key", key)));
        var second = await client.SendAsync(TestData.Post("/api/v1/custody-transfers",
            RequestBody(scenario) with { Reason = "Otro motivo" }, ("Idempotency-Key", key)));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Missing_idempotency_key_returns_400()
    {
        var scenario = await SeedAsync();
        using var client = await fixture.ClientAsAsync(scenario.Actors.Investigator);

        var response = await client.PostAsJsonAsync("/api/v1/custody-transfers", RequestBody(scenario));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_custodio_cannot_request_a_transfer()
    {
        var scenario = await SeedAsync();
        using var client = await fixture.ClientAsAsync(scenario.Actors.CustodianA);

        var response = await client.SendAsync(TestData.Post("/api/v1/custody-transfers", RequestBody(scenario),
            ("Idempotency-Key", Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Second_pending_request_for_the_same_evidence_returns_409_with_current_state()
    {
        var scenario = await SeedAsync();
        using var client = await fixture.ClientAsAsync(scenario.Actors.Investigator);

        var first = await client.SendAsync(TestData.Post("/api/v1/custody-transfers", RequestBody(scenario),
            ("Idempotency-Key", Guid.NewGuid().ToString())));
        var created = await first.Content.ReadAsync<TransferResponse>();
        var second = await client.SendAsync(TestData.Post("/api/v1/custody-transfers",
            RequestBody(scenario) with { Reason = "Segundo intento" }, ("Idempotency-Key", Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        var state = problem.GetProperty("currentState");
        Assert.Equal(created!.Id, state.GetProperty("transferId").GetGuid());
        Assert.Equal("Pendiente", state.GetProperty("status").GetString());
        Assert.Equal(created.Version, state.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Accept_moves_custody_and_a_stale_reject_gets_409_with_current_state()
    {
        var scenario = await SeedAsync();
        var created = await RequestAsync(scenario);
        using var custodianB = await fixture.ClientAsAsync(scenario.Actors.CustodianB);

        var accept = await custodianB.SendAsync(TestData.Post($"/api/v1/custody-transfers/{created.Id}/accept",
            new { note = "Recibido en bóveda" }, ("If-Match", created.Version)));
        var reject = await custodianB.SendAsync(TestData.Post($"/api/v1/custody-transfers/{created.Id}/reject",
            new { note = "Cambio de opinión" }, ("If-Match", created.Version)));

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var accepted = await accept.Content.ReadAsync<TransferResponse>();
        Assert.NotNull(accepted);
        Assert.Equal(TransferStatus.Aceptada, accepted.Status);
        Assert.NotEqual(created.Version, accepted.Version);
        Assert.Equal(accepted.Version, accept.Headers.ETag!.ToString());
        Assert.Equal(scenario.Actors.CustodianB.UserName, accepted.RespondedBy?.UserName);
        Assert.Null(accepted.Anomaly);

        Assert.Equal(HttpStatusCode.Conflict, reject.StatusCode);
        Assert.Equal("application/problem+json", reject.Content.Headers.ContentType?.MediaType);
        var problem = await reject.Content.ReadFromJsonAsync<JsonElement>();
        var state = problem.GetProperty("currentState");
        Assert.Equal("Aceptada", state.GetProperty("status").GetString());
        Assert.Equal(accepted.Version, state.GetProperty("version").GetString());
        Assert.Equal(scenario.Actors.CustodianB.UserName, state.GetProperty("respondedBy").GetString());
        Assert.NotEqual(JsonValueKind.Null, state.GetProperty("respondedAtUtc").ValueKind);

        await using var db = fixture.CreateDbContext();
        var evidence = await db.Evidence.SingleAsync(e => e.Id == scenario.Evidence.Id);
        Assert.Equal(scenario.Actors.CustodianB.Id, evidence.CurrentCustodianId);
        var events = await db.CustodyEvents.Where(e => e.EvidenceId == evidence.Id).OrderBy(e => e.Sequence).ToListAsync();
        Assert.Equal([CustodyEventType.Registrada, CustodyEventType.TransferenciaSolicitada, CustodyEventType.TransferenciaAceptada],
            events.Select(e => e.EventType));
        Assert.True(ChainVerifier.Verify(events).IsValid);
        Assert.Equal(events[^1].OccurredAtUtc, evidence.LastEventAtUtc);
    }

    [Fact]
    public async Task Reject_records_the_event_but_keeps_the_current_custodian()
    {
        var scenario = await SeedAsync();
        var created = await RequestAsync(scenario);
        using var custodianB = await fixture.ClientAsAsync(scenario.Actors.CustodianB);

        var reject = await custodianB.SendAsync(TestData.Post($"/api/v1/custody-transfers/{created.Id}/reject",
            new { note = "Sin espacio en bóveda" }, ("If-Match", created.Version)));

        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var rejected = await reject.Content.ReadAsync<TransferResponse>();
        Assert.Equal(TransferStatus.Rechazada, rejected!.Status);
        Assert.Equal("Sin espacio en bóveda", rejected.ResponseNote);

        await using var db = fixture.CreateDbContext();
        var evidence = await db.Evidence.SingleAsync(e => e.Id == scenario.Evidence.Id);
        Assert.Equal(scenario.Actors.CustodianA.Id, evidence.CurrentCustodianId);
        Assert.Equal(3, await db.CustodyEvents.CountAsync(e => e.EvidenceId == evidence.Id));
    }

    [Fact]
    public async Task Responding_without_if_match_returns_428()
    {
        var scenario = await SeedAsync();
        var created = await RequestAsync(scenario);
        using var custodianB = await fixture.ClientAsAsync(scenario.Actors.CustodianB);

        var response = await custodianB.PostAsJsonAsync($"/api/v1/custody-transfers/{created.Id}/accept", new { });

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Only_the_destination_custodian_can_respond()
    {
        var scenario = await SeedAsync();
        var created = await RequestAsync(scenario);
        using var custodianA = await fixture.ClientAsAsync(scenario.Actors.CustodianA);

        var response = await custodianA.SendAsync(TestData.Post($"/api/v1/custody-transfers/{created.Id}/accept",
            null, ("If-Match", created.Version)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Inbox_lists_only_pending_transfers_addressed_to_the_current_custodian()
    {
        var scenario = await SeedAsync();
        var created = await RequestAsync(scenario);
        using var custodianB = await fixture.ClientAsAsync(scenario.Actors.CustodianB);
        using var custodianA = await fixture.ClientAsAsync(scenario.Actors.CustodianA);

        var inboxB = await (await custodianB.GetAsync("/api/v1/custody-transfers?status=pending&mine=true")).Content
            .ReadAsync<ListMyPendingTransfers.Response>();
        var inboxA = await (await custodianA.GetAsync("/api/v1/custody-transfers?status=pending&mine=true")).Content
            .ReadAsync<ListMyPendingTransfers.Response>();
        var everything = await custodianA.GetAsync("/api/v1/custody-transfers?status=pending&mine=false");

        var item = Assert.Single(inboxB!.Items, t => t.Id == created.Id);
        Assert.Equal(scenario.Evidence.Code, item.Evidence.Code);
        Assert.DoesNotContain(inboxA!.Items, t => t.Id == created.Id);
        Assert.Equal(HttpStatusCode.Forbidden, everything.StatusCode);
    }

    private static RequestTransfer.Request RequestBody(Scenario scenario) =>
        new(scenario.Evidence.Id, scenario.Actors.CustodianB.Id, "Análisis en laboratorio");

    private async Task<Scenario> SeedAsync()
    {
        var suffix = TestData.Suffix();
        await using var db = fixture.CreateDbContext();
        var actors = await TestData.SeedActorsAsync(db, suffix);
        var evidence = await TestData.SeedEvidenceAsync(db, $"X{suffix}", actors.CustodianA, actors.Investigator, TestData.Base);
        return new Scenario(actors, evidence);
    }

    private async Task<TransferResponse> RequestAsync(Scenario scenario)
    {
        using var client = await fixture.ClientAsAsync(scenario.Actors.Investigator);
        var response = await client.SendAsync(TestData.Post("/api/v1/custody-transfers", RequestBody(scenario),
            ("Idempotency-Key", Guid.NewGuid().ToString())));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadAsync<TransferResponse>())!;
    }
}
