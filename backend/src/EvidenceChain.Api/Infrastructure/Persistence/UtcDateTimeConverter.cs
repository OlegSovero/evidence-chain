using EvidenceChain.Api.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EvidenceChain.Api.Infrastructure.Persistence;

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => UtcDateTime.TruncateToMilliseconds(value),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
{
    public static readonly UtcDateTimeConverter Instance = new();
}
