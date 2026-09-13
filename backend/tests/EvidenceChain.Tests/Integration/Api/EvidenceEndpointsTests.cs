using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Anomalies;
using EvidenceChain.Api.Domain.Hashing;
using EvidenceChain.Api.Features.Chain;
using EvidenceChain.Api.Features.Evidence;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Tests.Integration.Api;

[Collection(ApiCollection.Name)]
public class EvidenceEndpointsTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("desc")]
    [InlineData("asc")]
    public async Task Keyset_pagination_visits_every_evidence_exactly_once(string sort)
    {
        var suffix = TestData.Suffix();
        var prefix = $"K{suffix}";
        var expected = new List<int>();

        await using (var db = fixture.CreateDbContext())
        {
            var actors = await TestData.SeedActorsAsync(db, suffix);
            for (var i = 0; i < 25; i++)
            {
                // Five evidences per timestamp so the (LastEventAtUtc, Id) tie-break is exercised.
                var at = TestData.Base.AddHours(i / 5);
                var evidence = await TestData.SeedEvidenceAsync(db, $"{prefix}-{i:00}", actors.CustodianA,
                    actors.Investigator, at);
                expected.Add(evidence.Id);
            }
        }

        using var client = await ClientAsInvestigatorAsync(suffix);
        var seen = new List<EvidenceListItem>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var url = $"/api/v1/evidence?q={prefix}&sort={sort}&pageSize=7" + (cursor is null ? "" : $"&cursor={cursor}");
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await response.Content.ReadAsync<EvidencePage>();
            Assert.NotNull(page);
            Assert.InRange(page.Items.Count, 1, 7);
            seen.AddRange(page.Items);
            cursor = page.NextCursor;
            pages++;
        } while (cursor is not null);

        Assert.Equal(4, pages);
        Assert.Equal(expected.Count, seen.Count);
        Assert.Equal(expected.OrderBy(id => id), seen.Select(e => e.Id).OrderBy(id => id));
        Assert.Equal(expected.Count, seen.Select(e => e.Id).Distinct().Count());

        var ordered = sort == "desc"
            ? seen.OrderByDescending(e => e.LastEventAtUtc).ThenByDescending(e => e.Id)
            : seen.OrderBy(e => e.LastEventAtUtc).ThenBy(e => e.Id);
        Assert.Equal(ordered.Select(e => e.Id), seen.Select(e => e.Id));
    }

    [Fact]
    public async Task Cursor_from_the_other_sort_order_is_rejected()
    {
        var suffix = TestData.Suffix();
        await using (var db = fixture.CreateDbContext())
        {
            var actors = await TestData.SeedActorsAsync(db, suffix);
            await TestData.SeedEvidenceAsync(db, $"C{suffix}-1", actors.CustodianA, actors.Investigator, TestData.Base);
            await TestData.SeedEvidenceAsync(db, $"C{suffix}-2", actors.CustodianA, actors.Investigator, TestData.Base);
        }

        using var client = await ClientAsInvestigatorAsync(suffix);
        var first = await (await client.GetAsync($"/api/v1/evidence?q=C{suffix}&sort=desc&pageSize=1")).Content
            .ReadAsync<EvidencePage>();

        var response = await client.GetAsync($"/api/v1/evidence?q=C{suffix}&sort=asc&pageSize=1&cursor={first!.NextCursor}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Verify_marks_an_intact_chain_as_integra()
    {
        var suffix = TestData.Suffix();
        int evidenceId;
        await using (var db = fixture.CreateDbContext())
        {
            var actors = await TestData.SeedActorsAsync(db, suffix);
            var evidence = await TestData.SeedEvidenceAsync(db, $"I{suffix}", actors.CustodianA, actors.Investigator, TestData.Base);
            evidenceId = evidence.Id;
        }

        using var client = await ClientAsInvestigatorAsync(suffix);
        var response = await client.GetAsync($"/api/v1/evidence/{evidenceId}/chain/verify");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsync<VerifyChain.Response>();
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Null(result.FirstInvalidEvent);
        Assert.Equal(EvidenceIntegrityStatus.Integra, result.IntegrityStatus);
        Assert.Equal(1, result.EventCount);
    }

    [Fact]
    public async Task Verify_reports_the_tampered_event_and_marks_evidence_as_comprometida()
    {
        var suffix = TestData.Suffix();
        int evidenceId;
        await using (var db = fixture.CreateDbContext())
        {
            var actors = await TestData.SeedActorsAsync(db, suffix);
            var evidence = await TestData.SeedEvidenceAsync(db, $"T{suffix}", actors.CustodianA, actors.Investigator, TestData.Base);
            var transfer = await TestData.SeedPendingTransferAsync(db, evidence, actors.CustodianA, actors.CustodianB,
                actors.Investigator, TestData.Base.AddHours(1));
            await ChainAppender.AppendAsync(db, evidence, CustodyEventType.TransferenciaAceptada, actors.CustodianB.Id,
                actors.CustodianA.Id, actors.CustodianB.Id, transfer.Id, "Recibido", TestData.Base.AddHours(2),
                CancellationToken.None);
            await db.SaveChangesAsync();
            evidenceId = evidence.Id;

            var second = await db.CustodyEvents.SingleAsync(e => e.EvidenceId == evidenceId && e.Sequence == 2);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.CustodyEvents DISABLE TRIGGER TR_CustodyEvents_AppendOnly");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.CustodyEvents SET Notes = {"Motivo reescrito"} WHERE Id = {second.Id}");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.CustodyEvents ENABLE TRIGGER TR_CustodyEvents_AppendOnly");
        }

        using var client = await ClientAsInvestigatorAsync(suffix);
        var response = await client.GetAsync($"/api/v1/evidence/{evidenceId}/chain/verify");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsync<VerifyChain.Response>();
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Equal(EvidenceIntegrityStatus.Comprometida, result.IntegrityStatus);
        Assert.NotNull(result.FirstInvalidEvent);
        Assert.Equal(2, result.FirstInvalidEvent.Sequence);
        Assert.Equal(ChainFailureReason.HashMismatch, result.FirstInvalidEvent.Reason);

        var inbox = await (await client.GetAsync($"/api/v1/evidence?q=T{suffix}&status=Comprometida")).Content
            .ReadAsync<EvidencePage>();
        var item = Assert.Single(inbox!.Items);
        Assert.Equal(evidenceId, item.Id);
        Assert.Equal(EvidenceIntegrityStatus.Comprometida, item.IntegrityStatus);
    }

    [Fact]
    public async Task Detail_and_chain_expose_an_overdue_pending_transfer_as_anomaly()
    {
        var suffix = TestData.Suffix();
        int evidenceId;
        string destination;
        var requestedAt = DateTime.UtcNow.AddHours(-100);
        await using (var db = fixture.CreateDbContext())
        {
            var actors = await TestData.SeedActorsAsync(db, suffix);
            var evidence = await TestData.SeedEvidenceAsync(db, $"A{suffix}", actors.CustodianA, actors.Investigator,
                requestedAt.AddHours(-1));
            await TestData.SeedPendingTransferAsync(db, evidence, actors.CustodianA, actors.CustodianB,
                actors.Investigator, requestedAt);
            evidenceId = evidence.Id;
            destination = actors.CustodianB.UserName;
        }

        using var client = await ClientAsInvestigatorAsync(suffix);

        var detail = await (await client.GetAsync($"/api/v1/evidence/{evidenceId}")).Content.ReadAsync<EvidenceDetail>();
        Assert.NotNull(detail?.PendingTransfer);
        Assert.Equal(TransferStatus.Pendiente, detail.PendingTransfer.Status);
        var anomaly = detail.PendingTransfer.Anomaly;
        Assert.NotNull(anomaly);
        Assert.Equal(AnomalySeverity.Alta, anomaly.Severity);
        Assert.Equal(100, anomaly.HoursPending);
        Assert.Equal($"Transferencia a {destination} pendiente desde hace 100 h (umbral {ApiFixture.ThresholdHours} h)",
            anomaly.Message);
        Assert.Equal(2, detail.EventCount);

        var chain = await (await client.GetAsync($"/api/v1/evidence/{evidenceId}/chain")).Content.ReadAsync<GetChain.Response>();
        Assert.NotNull(chain);
        Assert.Equal([1, 2], chain.Events.Select(e => e.Sequence));
        Assert.Null(chain.Events[0].Anomaly);
        Assert.NotNull(chain.Events[1].Anomaly);
        Assert.Equal(CustodyEventType.TransferenciaSolicitada, chain.Events[1].EventType);
        Assert.Equal(new string('0', 64), chain.Events[0].PreviousHash);
        Assert.Equal(chain.Events[0].Hash, chain.Events[1].PreviousHash);
        Assert.Equal(64, chain.Events[1].Hash.Length);
    }

    [Fact]
    public async Task Anonymous_requests_get_401_as_problem_json()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/evidence");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Unknown_evidence_returns_404_problem()
    {
        using var client = await ClientAsInvestigatorAsync(TestData.Suffix());

        var response = await client.GetAsync("/api/v1/evidence/999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<HttpClient> ClientAsInvestigatorAsync(string suffix)
    {
        await using var db = fixture.CreateDbContext();
        var investigator = await db.Users.SingleOrDefaultAsync(u => u.UserName == $"inv-{suffix}")
            ?? (await TestData.SeedActorsAsync(db, suffix)).Investigator;
        return await fixture.ClientAsAsync(investigator);
    }
}
