namespace EvidenceChain.Api.Domain;

public class CustodyTransfer
{
    public Guid Id { get; set; }
    public int EvidenceId { get; set; }
    public int FromCustodianId { get; set; }
    public int ToCustodianId { get; set; }
    public int RequestedByUserId { get; set; }
    public TransferStatus Status { get; set; }
    public required string Reason { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
    public int? RespondedByUserId { get; set; }
    public string? ResponseNote { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
