using EvidenceChain.Api.Domain.Hashing;

namespace EvidenceChain.Tests.Unit.Hashing;

public class ChainHasherTests
{
    // SHA-256 of the genesis golden JSON, computed outside this codebase
    // (PowerShell, System.Security.Cryptography.SHA256 over the UTF-8 bytes).
    private const string GenesisGoldenHashHex =
        "e8fbc027b2d643c16a0d2ba6a1202bb5cde6463745006e48426e08560a498c00";

    [Fact]
    public void Golden_hash_of_genesis_event_matches_externally_computed_value()
    {
        var hash = ChainHasher.ComputeHash(TestChain.GenesisEvent());

        Assert.Equal(GenesisGoldenHashHex, Convert.ToHexStringLower(hash));
    }

    [Fact]
    public void Hash_is_32_bytes()
    {
        Assert.Equal(ChainHasher.HashLength, ChainHasher.ComputeHash(TestChain.GenesisEvent()).Length);
    }

    [Fact]
    public void Same_event_always_produces_the_same_hash()
    {
        var first = ChainHasher.ComputeHash(TestChain.GenesisEvent());
        var second = ChainHasher.ComputeHash(TestChain.GenesisEvent());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_previous_hash_produces_a_different_hash()
    {
        var custodyEvent = TestChain.GenesisEvent();
        var original = ChainHasher.ComputeHash(custodyEvent);

        custodyEvent.PreviousHash = Enumerable.Repeat((byte)0x01, 32).ToArray();

        Assert.NotEqual(original, ChainHasher.ComputeHash(custodyEvent));
    }

    [Fact]
    public void Genesis_is_32_zero_bytes_and_a_fresh_array_each_time()
    {
        var genesis = ChainHasher.Genesis;
        genesis[0] = 0xFF;

        Assert.Equal(32, ChainHasher.Genesis.Length);
        Assert.All(ChainHasher.Genesis, b => Assert.Equal(0, b));
    }
}
