namespace EvidenceChain.Api.Domain.Hashing;

public sealed record ChainVerificationResult(bool IsValid, int? FirstInvalidSequence, ChainFailureReason? Reason)
{
    public static ChainVerificationResult Valid { get; } = new(true, null, null);

    public static ChainVerificationResult Invalid(int sequence, ChainFailureReason reason) =>
        new(false, sequence, reason);
}
