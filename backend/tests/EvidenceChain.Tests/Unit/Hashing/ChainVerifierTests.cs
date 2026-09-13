using EvidenceChain.Api.Domain.Hashing;

namespace EvidenceChain.Tests.Unit.Hashing;

public class ChainVerifierTests
{
    [Fact]
    public void Well_formed_chain_of_five_events_is_valid()
    {
        var chain = TestChain.Build(5);

        var result = ChainVerifier.Verify(chain);

        Assert.True(result.IsValid);
        Assert.Null(result.FirstInvalidSequence);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Empty_chain_is_valid()
    {
        Assert.True(ChainVerifier.Verify([]).IsValid);
    }

    [Fact]
    public void Events_are_verified_in_sequence_order_regardless_of_input_order()
    {
        var chain = TestChain.Build(5);
        chain.Reverse();

        Assert.True(ChainVerifier.Verify(chain).IsValid);
    }

    [Fact]
    public void Changing_notes_yields_hash_mismatch_at_that_event()
    {
        var chain = TestChain.Build(5);
        chain[2].Notes = "Nota alterada después de firmar";

        var result = ChainVerifier.Verify(chain);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.FirstInvalidSequence);
        Assert.Equal(ChainFailureReason.HashMismatch, result.Reason);
    }

    [Fact]
    public void Changing_occurred_at_yields_hash_mismatch()
    {
        var chain = TestChain.Build(5);
        chain[3].OccurredAtUtc = chain[3].OccurredAtUtc.AddMilliseconds(1);

        var result = ChainVerifier.Verify(chain);

        Assert.False(result.IsValid);
        Assert.Equal(4, result.FirstInvalidSequence);
        Assert.Equal(ChainFailureReason.HashMismatch, result.Reason);
    }

    [Fact]
    public void Sub_millisecond_change_in_occurred_at_does_not_affect_the_hash()
    {
        var chain = TestChain.Build(2);
        chain[1].OccurredAtUtc = chain[1].OccurredAtUtc.AddTicks(9_999);

        Assert.True(ChainVerifier.Verify(chain).IsValid);
    }

    [Fact]
    public void Altering_previous_hash_of_a_middle_event_yields_broken_link()
    {
        var chain = TestChain.Build(5);
        chain[2].PreviousHash = Enumerable.Repeat((byte)0xAB, 32).ToArray();

        var result = ChainVerifier.Verify(chain);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.FirstInvalidSequence);
        Assert.Equal(ChainFailureReason.BrokenLink, result.Reason);
    }

    [Fact]
    public void First_event_not_linked_to_genesis_yields_broken_link()
    {
        var chain = TestChain.Build(1);
        chain[0].PreviousHash = Enumerable.Repeat((byte)0x01, 32).ToArray();

        var result = ChainVerifier.Verify(chain);

        Assert.Equal(ChainFailureReason.BrokenLink, result.Reason);
        Assert.Equal(1, result.FirstInvalidSequence);
    }

    [Fact]
    public void Missing_sequence_number_yields_sequence_gap()
    {
        var chain = TestChain.Build(5);
        chain.RemoveAt(2);

        var result = ChainVerifier.Verify(chain);

        Assert.False(result.IsValid);
        Assert.Equal(4, result.FirstInvalidSequence);
        Assert.Equal(ChainFailureReason.SequenceGap, result.Reason);
    }

    [Fact]
    public void Chain_not_starting_at_one_yields_sequence_gap()
    {
        var chain = TestChain.Build(3);
        chain.RemoveAt(0);

        var result = ChainVerifier.Verify(chain);

        Assert.Equal(ChainFailureReason.SequenceGap, result.Reason);
        Assert.Equal(2, result.FirstInvalidSequence);
    }

    [Fact]
    public void Tampering_reports_only_the_first_invalid_event()
    {
        var chain = TestChain.Build(5);
        chain[1].Notes = "alterado";
        chain[3].Notes = "también alterado";

        var result = ChainVerifier.Verify(chain);

        Assert.Equal(2, result.FirstInvalidSequence);
    }
}
