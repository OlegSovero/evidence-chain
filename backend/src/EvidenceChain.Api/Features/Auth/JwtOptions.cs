namespace EvidenceChain.Api.Features.Auth;

public sealed class JwtOptions
{
    public const int MinKeyLength = 32;

    // Never in appsettings: dotnet user-secrets locally, App Service settings / Key Vault in Azure.
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "evidence-chain";
    public string Audience { get; set; } = "evidence-chain";
    public int ExpiresMinutes { get; set; } = 480;
}
