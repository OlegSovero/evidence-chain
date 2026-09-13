using EvidenceChain.Api.Features.Evidence;

namespace EvidenceChain.Tests.Unit.Evidence;

public class KeysetCursorTests
{
    private static readonly DateTime At = new(2026, 9, 1, 14, 5, 0, 123, DateTimeKind.Utc);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Round_trip_preserves_timestamp_id_and_direction(bool descending)
    {
        var encoded = new KeysetCursor(At, 42, descending).Encode();

        Assert.True(KeysetCursor.TryDecode(encoded, out var decoded));
        Assert.Equal(At, decoded.LastEventAtUtc);
        Assert.Equal(DateTimeKind.Utc, decoded.LastEventAtUtc.Kind);
        Assert.Equal(42, decoded.Id);
        Assert.Equal(descending, decoded.Descending);
    }

    [Fact]
    public void Cursor_is_opaque_base64url_without_padding_or_separators()
    {
        var encoded = new KeysetCursor(At, 42, true).Encode();

        Assert.DoesNotContain(':', encoded);
        Assert.DoesNotContain('=', encoded);
        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!!")]
    [InlineData("MTIzOjQy")]
    [InlineData("YWJjOjQyOmQ")]
    [InlineData("LTE6NDI6ZA")]
    public void Invalid_or_tampered_values_are_rejected(string? value)
    {
        Assert.False(KeysetCursor.TryDecode(value, out _));
    }
}
