using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Hashing;

namespace EvidenceChain.Tests.Unit.Hashing;

public class CanonicalEventSerializerTests
{
    // Golden strings: if either of these tests fails, the canonical format changed and
    // every stored hash (and the seed) is invalidated. Update docs/decisions.md first.
    private const string GenesisJson =
        "{\"v\":1,\"evidenceId\":42,\"seq\":1,\"type\":0,\"actor\":7,\"from\":null,\"to\":7," +
        "\"transferId\":null,\"notes\":null,\"occurredAtUtc\":\"2026-09-01T14:05:00.000Z\"," +
        "\"prev\":\"0000000000000000000000000000000000000000000000000000000000000000\"}";

    private const string FullJson =
        "{\"v\":1,\"evidenceId\":42,\"seq\":2,\"type\":1,\"actor\":7,\"from\":7,\"to\":12," +
        "\"transferId\":\"a3f1c2d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d\"," +
        "\"notes\":\"Traslado a \\\"bóveda\\\" \\\\ nivel 2\"," +
        "\"occurredAtUtc\":\"2026-09-01T16:40:00.123Z\"," +
        "\"prev\":\"000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f\"}";

    [Fact]
    public void Golden_genesis_event_serializes_to_exact_string()
    {
        var json = CanonicalEventSerializer.SerializeToJson(TestChain.GenesisEvent());

        Assert.Equal(GenesisJson, json);
    }

    [Fact]
    public void Golden_event_with_all_fields_serializes_to_exact_string()
    {
        var custodyEvent = new CustodyEvent
        {
            EvidenceId = 42,
            Sequence = 2,
            EventType = CustodyEventType.TransferenciaSolicitada,
            ActorUserId = 7,
            FromCustodianId = 7,
            ToCustodianId = 12,
            TransferId = TestChain.TransferId,
            Notes = "Traslado a \"bóveda\" \\ nivel 2",
            OccurredAtUtc = new DateTime(2026, 9, 1, 16, 40, 0, 123, DateTimeKind.Utc).AddTicks(4567),
            PreviousHash = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(),
        };

        var json = CanonicalEventSerializer.SerializeToJson(custodyEvent);

        Assert.Equal(FullJson, json);
    }

    [Fact]
    public void Serialized_bytes_are_utf8_without_bom()
    {
        var bytes = CanonicalEventSerializer.Serialize(TestChain.GenesisEvent());

        Assert.Equal((byte)'{', bytes[0]);
        Assert.Equal(GenesisJson.Length, bytes.Length);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Occurred_at_is_truncated_to_milliseconds_and_treated_as_utc(DateTimeKind kind)
    {
        var value = new DateTime(2026, 9, 1, 16, 40, 0, 999, kind).AddTicks(9_999);

        Assert.Equal("2026-09-01T16:40:00.999Z", CanonicalEventSerializer.FormatOccurredAt(value));
    }

    [Fact]
    public void Local_time_is_converted_to_utc_before_formatting()
    {
        var utc = new DateTime(2026, 9, 1, 16, 40, 0, DateTimeKind.Utc);
        var local = utc.ToLocalTime();

        Assert.Equal("2026-09-01T16:40:00.000Z", CanonicalEventSerializer.FormatOccurredAt(local));
    }

    [Fact]
    public void Previous_hash_must_be_32_bytes()
    {
        var custodyEvent = TestChain.GenesisEvent();
        custodyEvent.PreviousHash = new byte[31];

        Assert.Throws<ArgumentException>(() => CanonicalEventSerializer.Serialize(custodyEvent));
    }
}
