namespace EvidenceChain.Api.Domain;

public class CustodyEvent
{
    public long Id { get; set; }
    public int EvidenceId { get; set; }
    public int Sequence { get; set; }
    public CustodyEventType EventType { get; set; }
    public int ActorUserId { get; set; }
    public int? FromCustodianId { get; set; }
    public int? ToCustodianId { get; set; }
    public Guid? TransferId { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public required byte[] PreviousHash { get; set; }
    public byte[] Hash { get; set; } = [];
}
