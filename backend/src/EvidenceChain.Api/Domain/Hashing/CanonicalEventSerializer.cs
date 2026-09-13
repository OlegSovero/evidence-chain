using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EvidenceChain.Api.Domain.Hashing;

// Canonical form v1 (field order is part of the contract; see docs/decisions.md):
// {"v":1,"evidenceId":..,"seq":..,"type":..,"actor":..,"from":..,"to":..,
//  "transferId":..,"notes":..,"occurredAtUtc":"yyyy-MM-ddTHH:mm:ss.fffZ","prev":"<hex>"}
public static class CanonicalEventSerializer
{
    public const int Version = 1;

    private const string OccurredAtFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
    };

    public static byte[] Serialize(CustodyEvent custodyEvent)
    {
        ArgumentNullException.ThrowIfNull(custodyEvent);

        if (custodyEvent.PreviousHash.Length != ChainHasher.HashLength)
        {
            throw new ArgumentException(
                $"PreviousHash must be {ChainHasher.HashLength} bytes.", nameof(custodyEvent));
        }

        var buffer = new ArrayBufferWriter<byte>(256);
        using var writer = new Utf8JsonWriter(buffer, WriterOptions);

        writer.WriteStartObject();
        writer.WriteNumber("v", Version);
        writer.WriteNumber("evidenceId", custodyEvent.EvidenceId);
        writer.WriteNumber("seq", custodyEvent.Sequence);
        writer.WriteNumber("type", (byte)custodyEvent.EventType);
        writer.WriteNumber("actor", custodyEvent.ActorUserId);
        WriteNullableNumber(writer, "from", custodyEvent.FromCustodianId);
        WriteNullableNumber(writer, "to", custodyEvent.ToCustodianId);

        if (custodyEvent.TransferId is { } transferId)
        {
            writer.WriteString("transferId", transferId.ToString("D"));
        }
        else
        {
            writer.WriteNull("transferId");
        }

        if (custodyEvent.Notes is null)
        {
            writer.WriteNull("notes");
        }
        else
        {
            writer.WriteString("notes", custodyEvent.Notes);
        }

        writer.WriteString("occurredAtUtc", FormatOccurredAt(custodyEvent.OccurredAtUtc));
        writer.WriteString("prev", Convert.ToHexStringLower(custodyEvent.PreviousHash));
        writer.WriteEndObject();
        writer.Flush();

        return buffer.WrittenSpan.ToArray();
    }

    public static string SerializeToJson(CustodyEvent custodyEvent) =>
        Encoding.UTF8.GetString(Serialize(custodyEvent));

    public static string FormatOccurredAt(DateTime value) =>
        UtcDateTime.TruncateToMilliseconds(value).ToString(OccurredAtFormat, CultureInfo.InvariantCulture);

    private static void WriteNullableNumber(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(name, number);
        }
        else
        {
            writer.WriteNull(name);
        }
    }
}
