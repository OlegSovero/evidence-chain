namespace EvidenceChain.Api.Domain.Transfers;

public enum TransferAction
{
    Accept,
    Reject,
}

public sealed record TransferTransition(
    bool IsAllowed,
    TransferStatus Current,
    TransferStatus? Next,
    CustodyEventType? EventType)
{
    public static TransferTransition Allowed(TransferStatus current, TransferStatus next, CustodyEventType eventType) =>
        new(true, current, next, eventType);

    public static TransferTransition NotAllowed(TransferStatus current) =>
        new(false, current, null, null);
}

// Pendiente -> Aceptada | Rechazada. Both targets are terminal.
public static class TransferStateMachine
{
    public static bool IsTerminal(TransferStatus status) => status != TransferStatus.Pendiente;

    public static TransferTransition Apply(TransferStatus current, TransferAction action) => (current, action) switch
    {
        (TransferStatus.Pendiente, TransferAction.Accept) =>
            TransferTransition.Allowed(current, TransferStatus.Aceptada, CustodyEventType.TransferenciaAceptada),
        (TransferStatus.Pendiente, TransferAction.Reject) =>
            TransferTransition.Allowed(current, TransferStatus.Rechazada, CustodyEventType.TransferenciaRechazada),
        _ => TransferTransition.NotAllowed(current),
    };
}
