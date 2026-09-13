using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace EvidenceChain.Api.Features.Evidence;

// Opaque cursor = base64url("{ticks}:{id}:{d|a}"). The sort direction travels with
// it so a cursor from one ordering cannot be replayed against the other.
public readonly record struct KeysetCursor(DateTime LastEventAtUtc, int Id, bool Descending)
{
    public string Encode()
    {
        var payload = string.Create(CultureInfo.InvariantCulture,
            $"{LastEventAtUtc.Ticks}:{Id}:{(Descending ? 'd' : 'a')}");
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string? value, out KeysetCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string payload;
        try
        {
            payload = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value.AsSpan().Trim()));
        }
        catch (FormatException)
        {
            return false;
        }

        var parts = payload.Split(':');
        if (parts.Length != 3
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || parts[2] is not ("d" or "a")
            || ticks > DateTime.MaxValue.Ticks)
        {
            return false;
        }

        cursor = new KeysetCursor(new DateTime(ticks, DateTimeKind.Utc), id, parts[2] == "d");
        return true;
    }
}
