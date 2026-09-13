using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Hashing;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Tests.Integration.Persistence;

public class CustodyEventRoundTripTests(MsSqlContainerFixture fixture) : IClassFixture<MsSqlContainerFixture>
{
    // .0009999 ms: without truncation before saving, datetime2(3) would round this up
    // to .001 and the stored event would no longer match the hash signed in memory.
    private static readonly DateTime OccurredAt =
        new DateTime(2026, 9, 1, 14, 5, 0, DateTimeKind.Utc).AddTicks(9_999);

    [Fact]
    public async Task Hash_signed_before_saving_is_still_valid_after_reading_back_from_sql_server()
    {
        var (evidenceId, hashBeforeSave) = await SeedRegisteredEvidenceAsync();

        await using var db = fixture.CreateDbContext();
        var stored = await db.CustodyEvents.AsNoTracking()
            .Where(e => e.EvidenceId == evidenceId)
            .OrderBy(e => e.Sequence)
            .ToListAsync();

        var custodyEvent = Assert.Single(stored);
        Assert.Equal(DateTimeKind.Utc, custodyEvent.OccurredAtUtc.Kind);
        Assert.Equal(UtcDateTime.TruncateToMilliseconds(OccurredAt), custodyEvent.OccurredAtUtc);
        Assert.Equal(hashBeforeSave, custodyEvent.Hash);
        Assert.Equal(hashBeforeSave, ChainHasher.ComputeHash(custodyEvent));
        Assert.True(ChainVerifier.Verify(stored).IsValid);
    }

    [Fact]
    public async Task Updating_a_custody_event_is_rejected_by_the_append_only_trigger()
    {
        var (evidenceId, _) = await SeedRegisteredEvidenceAsync();

        await using var db = fixture.CreateDbContext();
        var custodyEvent = await db.CustodyEvents.SingleAsync(e => e.EvidenceId == evidenceId);
        custodyEvent.Notes = "Intento de reescribir la historia";

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sqlException = Assert.IsType<SqlException>(exception.InnerException);
        Assert.Equal(50001, sqlException.Number);
    }

    [Fact]
    public async Task Deleting_a_custody_event_is_rejected_by_the_append_only_trigger()
    {
        var (evidenceId, _) = await SeedRegisteredEvidenceAsync();

        await using var db = fixture.CreateDbContext();
        var custodyEvent = await db.CustodyEvents.SingleAsync(e => e.EvidenceId == evidenceId);
        db.CustodyEvents.Remove(custodyEvent);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sqlException = Assert.IsType<SqlException>(exception.InnerException);
        Assert.Equal(50001, sqlException.Number);
    }

    private async Task<(int EvidenceId, byte[] HashBeforeSave)> SeedRegisteredEvidenceAsync()
    {
        await using var db = fixture.CreateDbContext();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var custodian = new User
        {
            UserName = $"c.rivas-{suffix}",
            DisplayName = "Carla Rivas",
            Role = Roles.Investigador,
        };
        var evidence = new Evidence
        {
            Code = $"EV-{suffix}",
            Description = "Laptop HP ProBook 450",
            CurrentCustodian = custodian,
            LastEventAtUtc = OccurredAt,
            CreatedAtUtc = OccurredAt,
        };
        db.Evidence.Add(evidence);
        await db.SaveChangesAsync();

        var custodyEvent = new CustodyEvent
        {
            EvidenceId = evidence.Id,
            Sequence = 1,
            EventType = CustodyEventType.Registrada,
            ActorUserId = custodian.Id,
            ToCustodianId = custodian.Id,
            Notes = "Registro inicial en bóveda",
            OccurredAtUtc = OccurredAt,
            PreviousHash = ChainHasher.Genesis,
        };
        custodyEvent.Hash = ChainHasher.ComputeHash(custodyEvent);
        db.CustodyEvents.Add(custodyEvent);
        await db.SaveChangesAsync();

        return (evidence.Id, custodyEvent.Hash);
    }
}
