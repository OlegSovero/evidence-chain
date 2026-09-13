namespace EvidenceChain.Api.Domain;

public class IdempotencyRecord
{
    public int UserId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required byte[] RequestHash { get; set; }
    public int StatusCode { get; set; }
    public required string ResponseBody { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
