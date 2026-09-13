namespace EvidenceChain.Api.Domain;

public class Evidence
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Description { get; set; }
    public int CurrentCustodianId { get; set; }
    public User? CurrentCustodian { get; set; }
    public DateTime LastEventAtUtc { get; set; }
    public EvidenceIntegrityStatus IntegrityStatus { get; set; }
    public DateTime? IntegrityCheckedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<CustodyEvent> Events { get; } = [];
}
