namespace EvidenceChain.Api.Domain;

public static class Roles
{
    public const string Investigador = "Investigador";
    public const string Custodio = "Custodio";
    public const string Supervisor = "Supervisor";

    public static readonly string[] All = [Investigador, Custodio, Supervisor];
}

public class User
{
    public int Id { get; set; }
    public required string UserName { get; set; }
    public required string DisplayName { get; set; }
    public required string Role { get; set; }
}
