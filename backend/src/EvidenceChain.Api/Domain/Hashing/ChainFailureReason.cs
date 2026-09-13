namespace EvidenceChain.Api.Domain.Hashing;

public enum ChainFailureReason
{
    HashMismatch,
    BrokenLink,
    SequenceGap,
}
