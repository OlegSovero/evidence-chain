using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Anomalies;

namespace EvidenceChain.Tests.Unit.Anomalies;

public class PendingTransferRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 11, 0, 0, TimeSpan.Zero);
    private static readonly PendingTransferRule Rule = new(new FixedTimeProvider(Now), thresholdHours: 48);

    [Theory]
    [InlineData(TransferStatus.Aceptada)]
    [InlineData(TransferStatus.Rechazada)]
    public void Resolved_transfers_are_never_anomalies(TransferStatus status)
    {
        Assert.Null(Rule.Evaluate(status, Now.UtcDateTime.AddHours(-500), "lab.forense"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(47.99)]
    [InlineData(48)]
    public void Pending_within_threshold_is_not_an_anomaly(double hoursAgo)
    {
        Assert.Null(Rule.Evaluate(TransferStatus.Pendiente, Now.UtcDateTime.AddHours(-hoursAgo), "lab.forense"));
    }

    [Fact]
    public void Pending_past_threshold_is_media_with_readable_message()
    {
        var anomaly = Rule.Evaluate(TransferStatus.Pendiente, Now.UtcDateTime.AddHours(-73), "lab.forense");

        Assert.NotNull(anomaly);
        Assert.Equal(AnomalySeverity.Media, anomaly.Severity);
        Assert.Equal(73, anomaly.HoursPending);
        Assert.Equal(48, anomaly.ThresholdHours);
        Assert.Equal("Transferencia a lab.forense pendiente desde hace 73 h (umbral 48 h)", anomaly.Message);
        Assert.Equal(PendingTransferRule.Name, anomaly.Rule);
        Assert.Equal(Now.UtcDateTime, anomaly.EvaluatedAtUtc);
    }

    [Theory]
    [InlineData(96, AnomalySeverity.Media)]
    [InlineData(96.001, AnomalySeverity.Alta)]
    [InlineData(200, AnomalySeverity.Alta)]
    public void Severity_becomes_alta_past_twice_the_threshold(double hoursAgo, AnomalySeverity expected)
    {
        var anomaly = Rule.Evaluate(TransferStatus.Pendiente, Now.UtcDateTime.AddHours(-hoursAgo), "x");

        Assert.NotNull(anomaly);
        Assert.Equal(expected, anomaly.Severity);
    }

    [Fact]
    public void Hours_are_truncated_not_rounded()
    {
        var anomaly = Rule.Evaluate(TransferStatus.Pendiente, Now.UtcDateTime.AddHours(-49.9), "x");

        Assert.Equal(49, anomaly!.HoursPending);
    }

    [Fact]
    public void Threshold_is_configurable()
    {
        var rule = new PendingTransferRule(new FixedTimeProvider(Now), thresholdHours: 1);

        var anomaly = rule.Evaluate(TransferStatus.Pendiente, Now.UtcDateTime.AddMinutes(-61), "x");

        Assert.NotNull(anomaly);
        Assert.Contains("(umbral 1 h)", anomaly.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Threshold_must_be_positive(int threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingTransferRule(new FixedTimeProvider(Now), threshold));
    }
}
