using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Hashing;
using EvidenceChain.Api.Seed;

namespace EvidenceChain.Tests.Unit;

public class SeedTests
{
    [Fact]
    public void SeedGenerator_IsDeterministic()
    {
        var runA = SeedGenerator.Generate(SeedConstants.DefaultRandomSeed);
        var runB = SeedGenerator.Generate(SeedConstants.DefaultRandomSeed);

        Assert.Equal(runA.TotalUsers, runB.TotalUsers);
        Assert.Equal(runA.TotalEvidence, runB.TotalEvidence);
        Assert.Equal(runA.TotalTransfers, runB.TotalTransfers);
        Assert.Equal(runA.TotalEvents, runB.TotalEvents);

        for (int i = 0; i < runA.TotalUsers; i++)
        {
            Assert.Equal(runA.Users[i].Id, runB.Users[i].Id);
            Assert.Equal(runA.Users[i].UserName, runB.Users[i].UserName);
            Assert.Equal(runA.Users[i].Role, runB.Users[i].Role);
        }

        for (int i = 0; i < runA.TotalEvidence; i++)
        {
            var eA = runA.Evidence[i];
            var eB = runB.Evidence[i];

            Assert.Equal(eA.Id, eB.Id);
            Assert.Equal(eA.Code, eB.Code);
            Assert.Equal(eA.CurrentCustodianId, eB.CurrentCustodianId);
            Assert.Equal(eA.LastEventAtUtc, eB.LastEventAtUtc);
            Assert.Equal(eA.IntegrityStatus, eB.IntegrityStatus);
        }

        for (int i = 0; i < runA.TotalEvents; i++)
        {
            var evtA = runA.CustodyEvents[i];
            var evtB = runB.CustodyEvents[i];

            Assert.Equal(evtA.Id, evtB.Id);
            Assert.Equal(evtA.EvidenceId, evtB.EvidenceId);
            Assert.Equal(evtA.Sequence, evtB.Sequence);
            Assert.Equal(evtA.EventType, evtB.EventType);
            Assert.Equal(evtA.OccurredAtUtc, evtB.OccurredAtUtc);
            Assert.Equal(evtA.PreviousHash, evtB.PreviousHash);
            Assert.Equal(evtA.Hash, evtB.Hash);
            Assert.Equal(evtA.Notes, evtB.Notes);
        }
    }

    [Fact]
    public void SeedGenerator_ProducesExpectedVolume()
    {
        var data = SeedGenerator.Generate(SeedConstants.DefaultRandomSeed);

        Assert.Equal(12, data.TotalUsers);
        Assert.Equal(1000, data.TotalEvidence);

        // Volumen aproximado de 10.000 eventos (margen razonable dentro del 5%)
        Assert.InRange(data.TotalEvents, 9_500, 10_500);

        // Distribución adecuada de roles entre los 12 usuarios
        Assert.Equal(4, data.Users.Count(u => u.Role == Roles.Investigador));
        Assert.Equal(5, data.Users.Count(u => u.Role == Roles.Custodio));
        Assert.Equal(3, data.Users.Count(u => u.Role == Roles.Supervisor));
    }

    [Fact]
    public void Case1_IntactEvidence_IsValid()
    {
        var data = SeedGenerator.Generate(SeedConstants.DefaultRandomSeed);
        var evidence = data.Evidence.Single(e => e.Code == SeedConstants.IntactEvidenceCode);

        Assert.Equal(SeedConstants.IntactEvidenceId, evidence.Id);
        Assert.True(evidence.Events.Count > 1);
        Assert.Equal(EvidenceIntegrityStatus.Integra, evidence.IntegrityStatus);

        var verificationResult = ChainVerifier.Verify(evidence.Events);
        Assert.True(verificationResult.IsValid);
        Assert.Null(verificationResult.Reason);

        // Custodio tras las transferencias: lab.forense (Id 5)
        Assert.Equal(5, evidence.CurrentCustodianId);
    }

    [Fact]
    public void Case2_TamperedEvidence_FailsVerification_DueToHashMismatch()
    {
        var data = SeedGenerator.Generate(SeedConstants.DefaultRandomSeed);
        var evidence = data.Evidence.Single(e => e.Code == SeedConstants.TamperedEvidenceCode);

        Assert.Equal(SeedConstants.TamperedEvidenceId, evidence.Id);
        Assert.Equal(EvidenceIntegrityStatus.Comprometida, evidence.IntegrityStatus);

        // El evento en Secuencia 2 fue alterado en memoria
        var tamperedEvent = evidence.Events.Single(e => e.Sequence == SeedConstants.TamperedSequence);
        Assert.Equal(SeedConstants.TamperedAlteredNotes, tamperedEvent.Notes);

        var verificationResult = ChainVerifier.Verify(evidence.Events);
        Assert.False(verificationResult.IsValid);
        Assert.Equal(SeedConstants.TamperedSequence, verificationResult.FirstInvalidSequence);
        Assert.Equal(ChainFailureReason.HashMismatch, verificationResult.Reason);
    }

    [Fact]
    public void Case3_AnomalyEvidence_HasOverduePendingTransfer()
    {
        var data = SeedGenerator.Generate(SeedConstants.DefaultRandomSeed);
        var evidence = data.Evidence.Single(e => e.Code == SeedConstants.AnomalyEvidenceCode);

        Assert.Equal(SeedConstants.AnomalyEvidenceId, evidence.Id);

        var transfer = Assert.Single(data.CustodyTransfers, t => t.EvidenceId == evidence.Id);
        Assert.Equal(TransferStatus.Pendiente, transfer.Status);

        // Solicitada hace más de 48 horas con respecto a la fecha ancla
        var age = SeedConstants.AnchorDateUtc - transfer.RequestedAtUtc;
        Assert.True(age > TimeSpan.FromHours(48), $"La transferencia debe superar 48h de antigüedad, pero tiene {age.TotalHours}h");

        // Al estar pendiente, la custodia física aún no se ha movido
        Assert.Equal(transfer.FromCustodianId, evidence.CurrentCustodianId);
    }

    [Fact]
    public void DerivedFields_And_ChainHashes_AreCoherent_AcrossAllEvidences()
    {
        var data = SeedGenerator.Generate(SeedConstants.DefaultRandomSeed);

        foreach (var evidence in data.Evidence)
        {
            Assert.NotEmpty(evidence.Events);

            // 1. Secuencia contigua empezando en 1
            for (int s = 0; s < evidence.Events.Count; s++)
            {
                Assert.Equal(s + 1, evidence.Events[s].Sequence);
            }

            // 2. Encadenamiento génesis y previo
            Assert.Equal(ChainHasher.Genesis, evidence.Events[0].PreviousHash);
            for (int s = 1; s < evidence.Events.Count; s++)
            {
                Assert.Equal(evidence.Events[s - 1].Hash, evidence.Events[s].PreviousHash);
            }

            // 3. LastEventAtUtc coincide con la fecha del último evento
            Assert.Equal(evidence.Events[^1].OccurredAtUtc, evidence.LastEventAtUtc);

            // 4. CurrentCustodianId coincide con el ToCustodianId del último evento que movió la custodia
            int expectedCustodianId = evidence.Events
                .Where(e => e.EventType is CustodyEventType.Registrada or CustodyEventType.TransferenciaAceptada)
                .OrderByDescending(e => e.Sequence)
                .First()
                .ToCustodianId!.Value;

            Assert.Equal(expectedCustodianId, evidence.CurrentCustodianId);

            // 5. Verificación de hash canónico (todas íntegras salvo el caso especial de alteración)
            if (evidence.Code == SeedConstants.TamperedEvidenceCode)
            {
                Assert.Equal(EvidenceIntegrityStatus.Comprometida, evidence.IntegrityStatus);
                Assert.False(ChainVerifier.Verify(evidence.Events).IsValid);
            }
            else
            {
                Assert.Equal(EvidenceIntegrityStatus.Integra, evidence.IntegrityStatus);
                var verify = ChainVerifier.Verify(evidence.Events);
                Assert.True(verify.IsValid, $"Evidencia {evidence.Code} falló verificación en seq {verify.FirstInvalidSequence}: {verify.Reason}");
            }
        }
    }
}
