using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Hashing;

namespace EvidenceChain.Tests.Unit.Hashing;

internal static class TestChain
{
    public static readonly Guid TransferId = Guid.Parse("a3f1c2d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d");
    public static readonly DateTime Start = new(2026, 9, 1, 14, 5, 0, DateTimeKind.Utc);

    public static CustodyEvent GenesisEvent() => new()
    {
        EvidenceId = 42,
        Sequence = 1,
        EventType = CustodyEventType.Registrada,
        ActorUserId = 7,
        FromCustodianId = null,
        ToCustodianId = 7,
        TransferId = null,
        Notes = null,
        OccurredAtUtc = Start,
        PreviousHash = ChainHasher.Genesis,
    };

    public static List<CustodyEvent> Build(int count)
    {
        var events = new List<CustodyEvent>();
        var previousHash = ChainHasher.Genesis;

        for (var sequence = 1; sequence <= count; sequence++)
        {
            var custodyEvent = sequence == 1
                ? GenesisEvent()
                : new CustodyEvent
                {
                    EvidenceId = 42,
                    Sequence = sequence,
                    EventType = CustodyEventType.TransferenciaSolicitada,
                    ActorUserId = 7,
                    FromCustodianId = 7,
                    ToCustodianId = 12,
                    TransferId = TransferId,
                    Notes = $"Evento {sequence}",
                    OccurredAtUtc = Start.AddHours(sequence),
                    PreviousHash = previousHash,
                };

            custodyEvent.Hash = ChainHasher.ComputeHash(custodyEvent);
            previousHash = custodyEvent.Hash;
            events.Add(custodyEvent);
        }

        return events;
    }
}
