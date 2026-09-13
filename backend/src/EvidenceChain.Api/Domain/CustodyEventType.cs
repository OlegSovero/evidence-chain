namespace EvidenceChain.Api.Domain;

public enum CustodyEventType : byte
{
    Registrada = 0,
    TransferenciaSolicitada = 1,
    TransferenciaAceptada = 2,
    TransferenciaRechazada = 3,
}
