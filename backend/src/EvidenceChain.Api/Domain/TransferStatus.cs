namespace EvidenceChain.Api.Domain;

public enum TransferStatus : byte
{
    Pendiente = 0,
    Aceptada = 1,
    Rechazada = 2,
}
