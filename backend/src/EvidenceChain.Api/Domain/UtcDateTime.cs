namespace EvidenceChain.Api.Domain;

public static class UtcDateTime
{
    // SQL Server rounds datetime2(3) on write; truncating before hashing and before
    // saving keeps the signed value and the stored value identical.
    public static DateTime TruncateToMilliseconds(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }
}
