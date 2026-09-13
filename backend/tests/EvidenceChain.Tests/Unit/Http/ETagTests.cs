using EvidenceChain.Api.Infrastructure.Http;

namespace EvidenceChain.Tests.Unit.Http;

public class ETagTests
{
    private static readonly byte[] RowVersion = [0, 0, 0, 0, 0, 0, 0x0C, 0x83];

    [Fact]
    public void From_produces_a_quoted_strong_etag_with_0x_prefix()
    {
        Assert.Equal("\"0x0000000000000C83\"", ETag.From(RowVersion));
    }

    [Theory]
    [InlineData("\"0x0000000000000C83\"")]
    [InlineData("0x0000000000000C83")]
    [InlineData("  \"0x0000000000000c83\" ")]
    public void TryParse_accepts_quoted_unquoted_and_lowercase(string header)
    {
        Assert.True(ETag.TryParse(header, out var parsed));
        Assert.Equal(RowVersion, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("W/\"0x0000000000000C83\"")]
    [InlineData("\"0000000000000C83\"")]
    [InlineData("\"0x0C83\"")]
    [InlineData("\"0x0000000000000CZZ\"")]
    public void TryParse_rejects_weak_wildcard_and_malformed_values(string? header)
    {
        Assert.False(ETag.TryParse(header, out var parsed));
        Assert.Empty(parsed);
    }

    [Fact]
    public void Round_trip_preserves_bytes()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        Assert.True(ETag.TryParse(ETag.From(bytes), out var parsed));
        Assert.Equal(bytes, parsed);
    }
}
