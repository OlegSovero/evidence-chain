using System.Security.Cryptography;

namespace EvidenceChain.Api.Domain.Hashing;

public static class ChainHasher
{
    public const int HashLength = 32;

    public static byte[] Genesis => new byte[HashLength];

    public static byte[] ComputeHash(CustodyEvent custodyEvent) =>
        SHA256.HashData(CanonicalEventSerializer.Serialize(custodyEvent));
}
