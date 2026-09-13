namespace EvidenceChain.Api.Infrastructure.Http;

// Strong ETag built from SQL Server ROWVERSION: "0x00000000000007D1".
public static class ETag
{
    private const string Prefix = "0x";

    public static string From(byte[] rowVersion)
    {
        ArgumentNullException.ThrowIfNull(rowVersion);
        return $"\"{Prefix}{Convert.ToHexString(rowVersion)}\"";
    }

    public static bool TryParse(string? value, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var span = value.AsSpan().Trim();
        if (span.StartsWith("W/", StringComparison.Ordinal))
        {
            return false;
        }

        if (span.Length >= 2 && span[0] == '"' && span[^1] == '"')
        {
            span = span[1..^1];
        }

        if (!span.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        span = span[Prefix.Length..];
        if (span.Length != 16)
        {
            return false;
        }

        try
        {
            rowVersion = Convert.FromHexString(span);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
