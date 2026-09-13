namespace EvidenceChain.Api.Domain.Hashing;

public static class ChainVerifier
{
    public static ChainVerificationResult Verify(IEnumerable<CustodyEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var previousHash = ChainHasher.Genesis;
        var expectedSequence = 1;

        foreach (var custodyEvent in events.OrderBy(e => e.Sequence))
        {
            if (custodyEvent.Sequence != expectedSequence)
            {
                return ChainVerificationResult.Invalid(custodyEvent.Sequence, ChainFailureReason.SequenceGap);
            }

            if (!custodyEvent.PreviousHash.AsSpan().SequenceEqual(previousHash))
            {
                return ChainVerificationResult.Invalid(custodyEvent.Sequence, ChainFailureReason.BrokenLink);
            }

            var recomputed = ChainHasher.ComputeHash(custodyEvent);
            if (!recomputed.AsSpan().SequenceEqual(custodyEvent.Hash))
            {
                return ChainVerificationResult.Invalid(custodyEvent.Sequence, ChainFailureReason.HashMismatch);
            }

            previousHash = custodyEvent.Hash;
            expectedSequence++;
        }

        return ChainVerificationResult.Valid;
    }
}
