using EvidenceChain.Api.Domain;

namespace EvidenceChain.Api.Seed;

public static class SeedConstants
{
    public const int DefaultRandomSeed = 42;
    public const int TotalEvidences = 1000;

    public const string IntactEvidenceCode = "EVD-DEMO-INTACT";
    public const string TamperedEvidenceCode = "EVD-DEMO-TAMPER";
    public const string AnomalyEvidenceCode = "EVD-DEMO-ANOMALY";

    public const int IntactEvidenceId = 1;
    public const int TamperedEvidenceId = 2;
    public const int AnomalyEvidenceId = 3;

    public const int TamperedSequence = 2;
    public const string TamperedOriginalNotes = "Solicitud de clonación bit a bit y peritaje contable";
    public const string TamperedAlteredNotes = "MANIPULADO: Alteración de notas del peritaje sin actualizar hash";

    // Fixed UTC anchor for 100% deterministic runs
    public static readonly DateTime AnchorDateUtc = new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);

    public record SeedUserDefinition(int Id, string UserName, string DisplayName, string Role);

    public static readonly IReadOnlyList<SeedUserDefinition> Users =
    [
        // 4 Investigadores
        new(1, "c.rivas", "Carla Rivas", Roles.Investigador),
        new(2, "a.torres", "Alejandro Torres", Roles.Investigador),
        new(3, "d.morales", "Diana Morales", Roles.Investigador),
        new(4, "j.perez", "Juan Pérez", Roles.Investigador),

        // 5 Custodios
        new(5, "lab.forense", "Laboratorio Forense Central", Roles.Custodio),
        new(6, "m.quispe", "Marco Quispe", Roles.Custodio),
        new(7, "boveda.central", "Bóveda Central de Evidencias", Roles.Custodio),
        new(8, "r.guardia", "Rosa Guardia", Roles.Custodio),
        new(9, "e.custodio", "Esteban Custodio", Roles.Custodio),

        // 3 Supervisores
        new(10, "s.valdez", "Sofía Valdez", Roles.Supervisor),
        new(11, "h.mendoza", "Hugo Mendoza", Roles.Supervisor),
        new(12, "p.castillo", "Patricia Castillo", Roles.Supervisor),
    ];
}
