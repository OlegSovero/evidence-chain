namespace EvidenceChain.Api.Domain.Anomalies;

public enum AnomalySeverity
{
    Media = 1,
    Alta = 2,
}

public sealed record PendingTransferAnomaly(
    string Rule,
    AnomalySeverity Severity,
    string Message,
    int HoursPending,
    int ThresholdHours,
    DateTime EvaluatedAtUtc);

public sealed class AnomalyOptions
{
    public int PendingTransferThresholdHours { get; set; } = 48;
}

public sealed class PendingTransferRule
{
    public const string Name = "PendingTransferOverdue";

    private readonly TimeProvider _timeProvider;

    public PendingTransferRule(TimeProvider timeProvider, int thresholdHours)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thresholdHours);

        _timeProvider = timeProvider;
        ThresholdHours = thresholdHours;
    }

    public int ThresholdHours { get; }

    public PendingTransferAnomaly? Evaluate(TransferStatus status, DateTime requestedAtUtc, string destinationName)
    {
        if (status != TransferStatus.Pendiente)
        {
            return null;
        }

        var now = UtcDateTime.TruncateToMilliseconds(_timeProvider.GetUtcNow().UtcDateTime);
        var age = now - UtcDateTime.TruncateToMilliseconds(requestedAtUtc);
        var threshold = TimeSpan.FromHours(ThresholdHours);

        if (age <= threshold)
        {
            return null;
        }

        var severity = age > threshold * 2 ? AnomalySeverity.Alta : AnomalySeverity.Media;
        var hours = (int)Math.Floor(age.TotalHours);

        return new PendingTransferAnomaly(
            Name,
            severity,
            $"Transferencia a {destinationName} pendiente desde hace {hours} h (umbral {ThresholdHours} h)",
            hours,
            ThresholdHours,
            now);
    }
}
