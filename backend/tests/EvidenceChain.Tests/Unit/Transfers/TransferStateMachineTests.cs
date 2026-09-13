using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Transfers;

namespace EvidenceChain.Tests.Unit.Transfers;

public class TransferStateMachineTests
{
    [Fact]
    public void Pending_can_be_accepted()
    {
        var transition = TransferStateMachine.Apply(TransferStatus.Pendiente, TransferAction.Accept);

        Assert.True(transition.IsAllowed);
        Assert.Equal(TransferStatus.Aceptada, transition.Next);
        Assert.Equal(CustodyEventType.TransferenciaAceptada, transition.EventType);
    }

    [Fact]
    public void Pending_can_be_rejected()
    {
        var transition = TransferStateMachine.Apply(TransferStatus.Pendiente, TransferAction.Reject);

        Assert.True(transition.IsAllowed);
        Assert.Equal(TransferStatus.Rechazada, transition.Next);
        Assert.Equal(CustodyEventType.TransferenciaRechazada, transition.EventType);
    }

    [Theory]
    [InlineData(TransferStatus.Aceptada, TransferAction.Accept)]
    [InlineData(TransferStatus.Aceptada, TransferAction.Reject)]
    [InlineData(TransferStatus.Rechazada, TransferAction.Accept)]
    [InlineData(TransferStatus.Rechazada, TransferAction.Reject)]
    public void Terminal_states_reject_every_action_without_throwing(TransferStatus current, TransferAction action)
    {
        var transition = TransferStateMachine.Apply(current, action);

        Assert.False(transition.IsAllowed);
        Assert.Equal(current, transition.Current);
        Assert.Null(transition.Next);
        Assert.Null(transition.EventType);
    }

    [Theory]
    [InlineData(TransferStatus.Pendiente, false)]
    [InlineData(TransferStatus.Aceptada, true)]
    [InlineData(TransferStatus.Rechazada, true)]
    public void Only_pending_is_not_terminal(TransferStatus status, bool terminal)
    {
        Assert.Equal(terminal, TransferStateMachine.IsTerminal(status));
    }
}
