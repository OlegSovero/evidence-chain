using EvidenceChain.Api.Domain;

namespace EvidenceChain.Api.Seed;

public sealed class SeedData
{
    public required IReadOnlyList<User> Users { get; init; }
    public required IReadOnlyList<Evidence> Evidence { get; init; }
    public required IReadOnlyList<CustodyTransfer> CustodyTransfers { get; init; }
    public required IReadOnlyList<CustodyEvent> CustodyEvents { get; init; }

    public int TotalUsers => Users.Count;
    public int TotalEvidence => Evidence.Count;
    public int TotalTransfers => CustodyTransfers.Count;
    public int TotalEvents => CustodyEvents.Count;
}
