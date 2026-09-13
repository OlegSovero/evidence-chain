using System.Security.Cryptography;
using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Hashing;

namespace EvidenceChain.Api.Seed;

public static class SeedGenerator
{
    private static readonly string[] DeviceTypes =
    [
        "Laptop HP EliteBook 840 G8",
        "Laptop Dell Latitude 5420",
        "Smartphone Samsung Galaxy S22",
        "Smartphone Apple iPhone 13 Pro",
        "Disco duro externo Western Digital 2TB",
        "Disco duro externo Seagate Expansion 1TB",
        "Unidad de estado sólido Kingston SSD 480GB",
        "Unidad SSD NVMe Samsung 980 Pro 1TB",
        "Memoria USB SanDisk Ultra 64GB",
        "Memoria USB Kingston DataTraveler 128GB",
        "Tablet Apple iPad Pro 11 pulgadas",
        "Tablet Samsung Galaxy Tab S8",
        "Servidor rack Dell PowerEdge R640",
        "Servidor NAS Synology DiskStation DS920+",
        "Router Cisco Catalyst 2960",
        "Grabadora digital Sony ICD-UX570",
        "Videograbador DVR Hikvision 16 canales",
        "Tarjeta SIM Movistar 4G",
        "Tarjeta MicroSD SanDisk Extreme 256GB",
        "Dron DJI Mavic Air 2 con tarjeta de memoria",
    ];

    private static readonly string[] AcquisitionContexts =
    [
        "incautado en allanamiento de oficina central",
        "asegurado en escena de crimen primario",
        "levantado durante inspección ocular vehicular",
        "decomisado en operativo aduanero interinstitucional",
        "extraído de bóveda financiera en diligencia fiscal",
        "hallado en registro personal de imputado",
        "entregado voluntariamente por testigo clave",
        "asegurado durante orden judicial de descerraje",
        "desmontado de rack de telecomunicaciones",
        "remitido por fiscalía provincial corporativa",
    ];

    private static readonly string[] InvestigationCases =
    [
        "Caso 'Consorcio Vial' (Exp. 2026-0812)",
        "Caso 'Red Financiera' (Exp. 2026-1145)",
        "Caso 'Operación Frontera' (Exp. 2026-0421)",
        "Caso 'Licitaciones Públicas' (Exp. 2026-0933)",
        "Caso 'Lavado Corporativo' (Exp. 2026-1502)",
        "Caso 'Fraude Informático' (Exp. 2026-0278)",
        "Caso 'Cártel de Servicios' (Exp. 2026-0649)",
        "Caso 'Desvío de Fondos' (Exp. 2026-1890)",
    ];

    private static readonly string[] TransferReasons =
    [
        "Solicitud de extracción de imágenes forenses y copias espejo",
        "Remisión a laboratorio para peritaje de análisis de metadatos",
        "Traslado a custodia fría en bóveda de máxima seguridad",
        "Petición de inspección técnica especializada de hardware",
        "Remisión a fiscalía para audiencia de visualización de prueba",
        "Retorno a custodia institucional tras culminación de pericia",
        "Traslado por reorganización de almacén central de evidencias",
        "Solicitud de cotejo pericial y extracción de bases de datos",
    ];

    private static readonly string[] AcceptanceNotes =
    [
        "Recepción conforme de evidencia con precinto de seguridad intacto",
        "Ingreso registrado en bóveda forense; precintos verificados",
        "Evidencia recibida para inicio de peritaje informático",
        "Recepción y almacenamiento en gaveta de seguridad asignada",
        "Custodia aceptada tras verificación física de número de serie",
    ];

    private static readonly string[] RejectionNotes =
    [
        "Rechazado: formato de solicitud incompleto sin firma de fiscal",
        "Rechazado: inconsistencia en el código de precinto de seguridad",
        "Rechazado: bóveda temporal sin espacio para resguardo de dispositivo",
        "Rechazado: destinatario no autorizado para la custodia de este ítem",
    ];

    public static SeedData Generate(int randomSeed = SeedConstants.DefaultRandomSeed)
    {
        var rnd = new Random(randomSeed);

        // 1. Usuarios (12 usuarios deterministas)
        var users = SeedConstants.Users.Select(u => new User
        {
            Id = u.Id,
            UserName = u.UserName,
            DisplayName = u.DisplayName,
            Role = u.Role,
        }).ToList();

        var investigators = users.Where(u => u.Role == Roles.Investigador).ToList();
        var custodians = users.Where(u => u.Role == Roles.Custodio).ToList();
        var supervisors = users.Where(u => u.Role == Roles.Supervisor).ToList();

        var allEvidence = new List<Evidence>(SeedConstants.TotalEvidences);
        var allTransfers = new List<CustodyTransfer>();
        var allEvents = new List<CustodyEvent>();

        long eventIdCounter = 1;

        // -------------------------------------------------------------
        // CASO 1: EVD-DEMO-INTACT (Id 1)
        // Evidencia con cadena íntegra y múltiples eventos secuenciales
        // -------------------------------------------------------------
        var (ev1, trs1, evts1) = GenerateCase1Intact(ref eventIdCounter);
        allEvidence.Add(ev1);
        allTransfers.AddRange(trs1);
        allEvents.AddRange(evts1);

        // -------------------------------------------------------------
        // CASO 2: EVD-DEMO-TAMPER (Id 2)
        // Evidencia con evento intermedio alterado (Notes modificado sin recalcular hash)
        // -------------------------------------------------------------
        var (ev2, trs2, evts2) = GenerateCase2Tampered(ref eventIdCounter);
        allEvidence.Add(ev2);
        allTransfers.AddRange(trs2);
        allEvents.AddRange(evts2);

        // -------------------------------------------------------------
        // CASO 3: EVD-DEMO-ANOMALY (Id 3)
        // Evidencia con transferencia PENDIENTE muy por encima del umbral de 48 h
        // -------------------------------------------------------------
        var (ev3, trs3, evts3) = GenerateCase3Anomaly(ref eventIdCounter);
        allEvidence.Add(ev3);
        allTransfers.AddRange(trs3);
        allEvents.AddRange(evts3);

        // -------------------------------------------------------------
        // Resto de evidencias (Id 4 a 1000) para alcanzar ~10.000 eventos
        // -------------------------------------------------------------
        const int targetTotalEvents = 10_000;

        for (int id = 4; id <= SeedConstants.TotalEvidences; id++)
        {
            int remainingEvidences = SeedConstants.TotalEvidences - id + 1;
            int neededEvents = targetTotalEvents - allEvents.Count;
            double desiredPerEvidence = (double)neededEvents / remainingEvidences;

            // Cada transferencia resuelta aporta 2 eventos; el evento de registro aporta 1.
            // Si la última transferencia queda pendiente, aporta 1 evento.
            int desiredTransfers = Math.Clamp((int)Math.Round((desiredPerEvidence - 1.0) / 2.0), 1, 7);

            // Variabilidad determinista con el generador
            int transferCount = Math.Clamp(desiredTransfers + rnd.Next(-1, 2), 1, 7);

            // Decidir si la última transferencia queda pendiente (aprox 3% de evidencias)
            bool lastTransferPending = (id % 33 == 0);

            var (ev, trs, evts) = GenerateGeneralEvidence(
                id,
                transferCount,
                lastTransferPending,
                rnd,
                users,
                investigators,
                custodians,
                supervisors,
                ref eventIdCounter);

            allEvidence.Add(ev);
            allTransfers.AddRange(trs);
            allEvents.AddRange(evts);
        }

        return new SeedData
        {
            Users = users,
            Evidence = allEvidence,
            CustodyTransfers = allTransfers,
            CustodyEvents = allEvents,
        };
    }

    private static (Evidence, List<CustodyTransfer>, List<CustodyEvent>) GenerateCase1Intact(ref long eventIdCounter)
    {
        var evidence = new Evidence
        {
            Id = SeedConstants.IntactEvidenceId,
            Code = SeedConstants.IntactEvidenceCode,
            Description = "Teléfono celular Samsung Galaxy S23 asegurado en allanamiento de oficina principal",
            CurrentCustodianId = 5, // lab.forense tras transferencia aceptada
            CreatedAtUtc = UtcDateTime.TruncateToMilliseconds(SeedConstants.AnchorDateUtc.AddDays(-14)),
            IntegrityStatus = EvidenceIntegrityStatus.Integra,
        };

        var transfers = new List<CustodyTransfer>();
        var events = new List<CustodyEvent>();

        // Evento 1: Registrada por c.rivas (Id 1)
        var t0 = evidence.CreatedAtUtc;
        var e1 = CreateEvent(eventIdCounter++, evidence.Id, 1, CustodyEventType.Registrada,
            actorId: 1, fromId: null, toId: 1, transferId: null,
            notes: "Registro inicial e incautación de teléfono móvil en allanamiento",
            occurredAtUtc: t0, previousHash: ChainHasher.Genesis);
        events.Add(e1);
        evidence.Events.Add(e1);

        // Transferencia 1: 1 -> 5 (lab.forense) [Aceptada]
        var t1Req = UtcDateTime.TruncateToMilliseconds(t0.AddHours(3));
        var t1Resp = UtcDateTime.TruncateToMilliseconds(t0.AddHours(7));
        var tr1 = new CustodyTransfer
        {
            Id = new Guid("11111111-1111-1111-1111-111111111111"),
            EvidenceId = evidence.Id,
            FromCustodianId = 1,
            ToCustodianId = 5,
            RequestedByUserId = 1,
            Status = TransferStatus.Aceptada,
            Reason = "Peritaje forense digital de extracción de mensajería y almacenamiento interno",
            RequestedAtUtc = t1Req,
            RespondedAtUtc = t1Resp,
            RespondedByUserId = 5,
            ResponseNote = "Recepción conforme de dispositivo en laboratorio forense",
        };
        transfers.Add(tr1);

        // Evento 2: TransferenciaSolicitada
        var e2 = CreateEvent(eventIdCounter++, evidence.Id, 2, CustodyEventType.TransferenciaSolicitada,
            actorId: 1, fromId: 1, toId: 5, transferId: tr1.Id,
            notes: tr1.Reason, occurredAtUtc: t1Req, previousHash: e1.Hash);
        events.Add(e2);
        evidence.Events.Add(e2);

        // Evento 3: TransferenciaAceptada
        var e3 = CreateEvent(eventIdCounter++, evidence.Id, 3, CustodyEventType.TransferenciaAceptada,
            actorId: 5, fromId: 1, toId: 5, transferId: tr1.Id,
            notes: "Custodia aceptada y transferida satisfactoriamente a laboratorio",
            occurredAtUtc: t1Resp, previousHash: e2.Hash);
        events.Add(e3);
        evidence.Events.Add(e3);

        // Transferencia 2: 5 -> 6 (m.quispe) [Rechazada]
        var t2Req = UtcDateTime.TruncateToMilliseconds(t0.AddDays(2));
        var t2Resp = UtcDateTime.TruncateToMilliseconds(t0.AddDays(2).AddHours(4));
        var tr2 = new CustodyTransfer
        {
            Id = new Guid("11111111-1111-1111-1111-222222222222"),
            EvidenceId = evidence.Id,
            FromCustodianId = 5,
            ToCustodianId = 6,
            RequestedByUserId = 5,
            Status = TransferStatus.Rechazada,
            Reason = "Traslado a bóveda secundaria de resguardo temporal",
            RequestedAtUtc = t2Req,
            RespondedAtUtc = t2Resp,
            RespondedByUserId = 6,
            ResponseNote = "Bóveda secundaria sin espacio disponible para custodia fría",
        };
        transfers.Add(tr2);

        // Evento 4: TransferenciaSolicitada
        var e4 = CreateEvent(eventIdCounter++, evidence.Id, 4, CustodyEventType.TransferenciaSolicitada,
            actorId: 5, fromId: 5, toId: 6, transferId: tr2.Id,
            notes: tr2.Reason, occurredAtUtc: t2Req, previousHash: e3.Hash);
        events.Add(e4);
        evidence.Events.Add(e4);

        // Evento 5: TransferenciaRechazada
        var e5 = CreateEvent(eventIdCounter++, evidence.Id, 5, CustodyEventType.TransferenciaRechazada,
            actorId: 6, fromId: 5, toId: 6, transferId: tr2.Id,
            notes: "Transferencia rechazada; custodia permanece con el custodio remitente",
            occurredAtUtc: t2Resp, previousHash: e4.Hash);
        events.Add(e5);
        evidence.Events.Add(e5);

        evidence.LastEventAtUtc = e5.OccurredAtUtc;
        evidence.IntegrityCheckedAtUtc = e5.OccurredAtUtc;

        return (evidence, transfers, events);
    }

    private static (Evidence, List<CustodyTransfer>, List<CustodyEvent>) GenerateCase2Tampered(ref long eventIdCounter)
    {
        var evidence = new Evidence
        {
            Id = SeedConstants.TamperedEvidenceId,
            Code = SeedConstants.TamperedEvidenceCode,
            Description = "Disco duro externo Seagate 2TB con información contable corporativa",
            CurrentCustodianId = 5, // lab.forense
            CreatedAtUtc = UtcDateTime.TruncateToMilliseconds(SeedConstants.AnchorDateUtc.AddDays(-10)),
            IntegrityStatus = EvidenceIntegrityStatus.Comprometida,
        };

        var transfers = new List<CustodyTransfer>();
        var events = new List<CustodyEvent>();

        // Evento 1: Registrada por a.torres (Id 2)
        var t0 = evidence.CreatedAtUtc;
        var e1 = CreateEvent(eventIdCounter++, evidence.Id, 1, CustodyEventType.Registrada,
            actorId: 2, fromId: null, toId: 2, transferId: null,
            notes: "Registro inicial de disco duro incautado en allanamiento",
            occurredAtUtc: t0, previousHash: ChainHasher.Genesis);
        events.Add(e1);
        evidence.Events.Add(e1);

        // Transferencia 1: 2 -> 5 (lab.forense) [Aceptada]
        var t1Req = UtcDateTime.TruncateToMilliseconds(t0.AddHours(2));
        var t1Resp = UtcDateTime.TruncateToMilliseconds(t0.AddHours(6));
        var tr1 = new CustodyTransfer
        {
            Id = new Guid("22222222-2222-2222-2222-111111111111"),
            EvidenceId = evidence.Id,
            FromCustodianId = 2,
            ToCustodianId = 5,
            RequestedByUserId = 2,
            Status = TransferStatus.Aceptada,
            Reason = SeedConstants.TamperedOriginalNotes,
            RequestedAtUtc = t1Req,
            RespondedAtUtc = t1Resp,
            RespondedByUserId = 5,
            ResponseNote = "Clonación completada satisfactoriamente con hash de imagen verificado",
        };
        transfers.Add(tr1);

        // Evento 2: TransferenciaSolicitada (se calcula el hash con las notas originales)
        var e2 = CreateEvent(eventIdCounter++, evidence.Id, 2, CustodyEventType.TransferenciaSolicitada,
            actorId: 2, fromId: 2, toId: 5, transferId: tr1.Id,
            notes: SeedConstants.TamperedOriginalNotes, occurredAtUtc: t1Req, previousHash: e1.Hash);
        events.Add(e2);
        evidence.Events.Add(e2);

        // Evento 3: TransferenciaAceptada
        var e3 = CreateEvent(eventIdCounter++, evidence.Id, 3, CustodyEventType.TransferenciaAceptada,
            actorId: 5, fromId: 2, toId: 5, transferId: tr1.Id,
            notes: "Custodia aceptada para análisis forense digital",
            occurredAtUtc: t1Resp, previousHash: e2.Hash);
        events.Add(e3);
        evidence.Events.Add(e3);

        // Transferencia 2: 5 -> 7 (boveda.central) [Pendiente]
        var t2Req = UtcDateTime.TruncateToMilliseconds(t0.AddDays(1));
        var tr2 = new CustodyTransfer
        {
            Id = new Guid("22222222-2222-2222-2222-222222222222"),
            EvidenceId = evidence.Id,
            FromCustodianId = 5,
            ToCustodianId = 7,
            RequestedByUserId = 5,
            Status = TransferStatus.Pendiente,
            Reason = "Traslado a bóveda central de evidencias tras peritaje contable",
            RequestedAtUtc = t2Req,
            RespondedAtUtc = null,
            RespondedByUserId = null,
            ResponseNote = null,
        };
        transfers.Add(tr2);

        // Evento 4: TransferenciaSolicitada
        var e4 = CreateEvent(eventIdCounter++, evidence.Id, 4, CustodyEventType.TransferenciaSolicitada,
            actorId: 5, fromId: 5, toId: 7, transferId: tr2.Id,
            notes: tr2.Reason, occurredAtUtc: t2Req, previousHash: e3.Hash);
        events.Add(e4);
        evidence.Events.Add(e4);

        evidence.LastEventAtUtc = e4.OccurredAtUtc;
        evidence.IntegrityCheckedAtUtc = e4.OccurredAtUtc;

        // IMPORTANTE: Modificamos e2.Notes con el texto alterado SIN recalcular su hash.
        // Esto produce la discrepancia criptográfica requerida para probar la detección
        // de manipulación en /chain/verify (ChainFailureReason.HashMismatch en Secuencia 2).
        e2.Notes = SeedConstants.TamperedAlteredNotes;

        return (evidence, transfers, events);
    }

    private static (Evidence, List<CustodyTransfer>, List<CustodyEvent>) GenerateCase3Anomaly(ref long eventIdCounter)
    {
        // Solicitud pendiente con >96 h de antigüedad (muy por encima del umbral de 48 h)
        var requestedAt = UtcDateTime.TruncateToMilliseconds(SeedConstants.AnchorDateUtc.AddHours(-96));
        var createdAt = UtcDateTime.TruncateToMilliseconds(requestedAt.AddDays(-2));

        var evidence = new Evidence
        {
            Id = SeedConstants.AnomalyEvidenceId,
            Code = SeedConstants.AnomalyEvidenceCode,
            Description = "Servidor blade HP ProLiant incautado en sala de servidores corporativa",
            CurrentCustodianId = 1, // c.rivas (no se ha movido porque la transferencia está pendiente)
            CreatedAtUtc = createdAt,
            IntegrityStatus = EvidenceIntegrityStatus.Integra,
        };

        var transfers = new List<CustodyTransfer>();
        var events = new List<CustodyEvent>();

        // Evento 1: Registrada
        var e1 = CreateEvent(eventIdCounter++, evidence.Id, 1, CustodyEventType.Registrada,
            actorId: 1, fromId: null, toId: 1, transferId: null,
            notes: "Incautación de servidor blade en centro de cómputo empresarial",
            occurredAtUtc: createdAt, previousHash: ChainHasher.Genesis);
        events.Add(e1);
        evidence.Events.Add(e1);

        // Transferencia 1: 1 -> 5 [Pendiente > 96h]
        var tr1 = new CustodyTransfer
        {
            Id = new Guid("33333333-3333-3333-3333-111111111111"),
            EvidenceId = evidence.Id,
            FromCustodianId = 1,
            ToCustodianId = 5,
            RequestedByUserId = 1,
            Status = TransferStatus.Pendiente,
            Reason = "Requerimiento urgente de extracción de imágenes de arreglos RAID",
            RequestedAtUtc = requestedAt,
            RespondedAtUtc = null,
            RespondedByUserId = null,
            ResponseNote = null,
        };
        transfers.Add(tr1);

        // Evento 2: TransferenciaSolicitada
        var e2 = CreateEvent(eventIdCounter++, evidence.Id, 2, CustodyEventType.TransferenciaSolicitada,
            actorId: 1, fromId: 1, toId: 5, transferId: tr1.Id,
            notes: tr1.Reason, occurredAtUtc: requestedAt, previousHash: e1.Hash);
        events.Add(e2);
        evidence.Events.Add(e2);

        evidence.LastEventAtUtc = e2.OccurredAtUtc;
        evidence.IntegrityCheckedAtUtc = e2.OccurredAtUtc;

        return (evidence, transfers, events);
    }

    private static (Evidence, List<CustodyTransfer>, List<CustodyEvent>) GenerateGeneralEvidence(
        int evidenceId,
        int transferCount,
        bool lastTransferPending,
        Random rnd,
        IReadOnlyList<User> users,
        IReadOnlyList<User> investigators,
        IReadOnlyList<User> custodians,
        IReadOnlyList<User> supervisors,
        ref long eventIdCounter)
    {
        var device = DeviceTypes[rnd.Next(DeviceTypes.Length)];
        var context = AcquisitionContexts[rnd.Next(AcquisitionContexts.Length)];
        var caseRef = InvestigationCases[rnd.Next(InvestigationCases.Length)];

        var initialInvestigator = investigators[rnd.Next(investigators.Count)];
        int currentCustodianId = initialInvestigator.Id;

        // Fecha de registro distribuida entre 15 y 55 días antes de la fecha ancla
        var createdAt = UtcDateTime.TruncateToMilliseconds(
            SeedConstants.AnchorDateUtc.AddDays(-rnd.Next(15, 55))
                                       .AddHours(-rnd.Next(0, 24))
                                       .AddMinutes(-rnd.Next(0, 60)));

        var evidence = new Evidence
        {
            Id = evidenceId,
            Code = $"EV-{evidenceId:D6}",
            Description = $"{device} {context} — {caseRef}",
            CurrentCustodianId = currentCustodianId,
            CreatedAtUtc = createdAt,
            IntegrityStatus = EvidenceIntegrityStatus.Integra,
        };

        var transfers = new List<CustodyTransfer>();
        var events = new List<CustodyEvent>();

        // Evento 1: Registrada
        var previousEvent = CreateEvent(eventIdCounter++, evidence.Id, 1, CustodyEventType.Registrada,
            actorId: initialInvestigator.Id, fromId: null, toId: currentCustodianId, transferId: null,
            notes: "Registro e ingreso inicial en cadena de custodia",
            occurredAtUtc: createdAt, previousHash: ChainHasher.Genesis);
        events.Add(previousEvent);
        evidence.Events.Add(previousEvent);

        var currentTimestamp = createdAt;
        int sequence = 2;

        for (int t = 0; t < transferCount; t++)
        {
            bool isLast = (t == transferCount - 1);
            bool isPending = isLast && lastTransferPending;

            // Elegir custodio destino distinto al actual
            int toCustodianId;
            do
            {
                // Mayor probabilidad de transferir a un custodio de laboratorio / bóveda
                toCustodianId = (rnd.Next(10) < 8)
                    ? custodians[rnd.Next(custodians.Count)].Id
                    : users[rnd.Next(users.Count)].Id;
            } while (toCustodianId == currentCustodianId);

            // Solicitante: investigador, supervisor o el custodio actual
            int requestedByUserId = (rnd.Next(10) < 5)
                ? currentCustodianId
                : (rnd.Next(2) == 0 ? investigators[rnd.Next(investigators.Count)].Id : supervisors[rnd.Next(supervisors.Count)].Id);

            var requestedAt = UtcDateTime.TruncateToMilliseconds(
                currentTimestamp.AddHours(rnd.Next(3, 24)).AddMinutes(rnd.Next(1, 59)));

            if (requestedAt >= SeedConstants.AnchorDateUtc.AddHours(-1))
            {
                requestedAt = UtcDateTime.TruncateToMilliseconds(SeedConstants.AnchorDateUtc.AddHours(-2));
            }

            var guidBytes = new byte[16];
            rnd.NextBytes(guidBytes);
            var transferId = new Guid(guidBytes);

            var reason = TransferReasons[rnd.Next(TransferReasons.Length)];

            if (isPending)
            {
                // Transferencia pendiente (no resuelta)
                var transfer = new CustodyTransfer
                {
                    Id = transferId,
                    EvidenceId = evidence.Id,
                    FromCustodianId = currentCustodianId,
                    ToCustodianId = toCustodianId,
                    RequestedByUserId = requestedByUserId,
                    Status = TransferStatus.Pendiente,
                    Reason = reason,
                    RequestedAtUtc = requestedAt,
                    RespondedAtUtc = null,
                    RespondedByUserId = null,
                    ResponseNote = null,
                };
                transfers.Add(transfer);

                var reqEvent = CreateEvent(eventIdCounter++, evidence.Id, sequence++,
                    CustodyEventType.TransferenciaSolicitada,
                    actorId: requestedByUserId, fromId: currentCustodianId, toId: toCustodianId,
                    transferId: transfer.Id, notes: reason,
                    occurredAtUtc: requestedAt, previousHash: previousEvent.Hash);
                events.Add(reqEvent);
                evidence.Events.Add(reqEvent);
                previousEvent = reqEvent;
                currentTimestamp = requestedAt;
            }
            else
            {
                // Transferencia resuelta: 85% Aceptada, 15% Rechazada
                bool accepted = rnd.Next(100) < 85;
                var respondedAt = UtcDateTime.TruncateToMilliseconds(
                    requestedAt.AddHours(rnd.Next(1, 18)).AddMinutes(rnd.Next(1, 59)));

                if (respondedAt >= SeedConstants.AnchorDateUtc)
                {
                    respondedAt = UtcDateTime.TruncateToMilliseconds(SeedConstants.AnchorDateUtc.AddMinutes(-10));
                }

                var responseNote = accepted
                    ? AcceptanceNotes[rnd.Next(AcceptanceNotes.Length)]
                    : RejectionNotes[rnd.Next(RejectionNotes.Length)];

                var transfer = new CustodyTransfer
                {
                    Id = transferId,
                    EvidenceId = evidence.Id,
                    FromCustodianId = currentCustodianId,
                    ToCustodianId = toCustodianId,
                    RequestedByUserId = requestedByUserId,
                    Status = accepted ? TransferStatus.Aceptada : TransferStatus.Rechazada,
                    Reason = reason,
                    RequestedAtUtc = requestedAt,
                    RespondedAtUtc = respondedAt,
                    RespondedByUserId = toCustodianId,
                    ResponseNote = responseNote,
                };
                transfers.Add(transfer);

                // Evento Solicitud
                var reqEvent = CreateEvent(eventIdCounter++, evidence.Id, sequence++,
                    CustodyEventType.TransferenciaSolicitada,
                    actorId: requestedByUserId, fromId: currentCustodianId, toId: toCustodianId,
                    transferId: transfer.Id, notes: reason,
                    occurredAtUtc: requestedAt, previousHash: previousEvent.Hash);
                events.Add(reqEvent);
                evidence.Events.Add(reqEvent);

                // Evento Resolución
                var resolutionType = accepted
                    ? CustodyEventType.TransferenciaAceptada
                    : CustodyEventType.TransferenciaRechazada;

                var resEvent = CreateEvent(eventIdCounter++, evidence.Id, sequence++,
                    resolutionType,
                    actorId: toCustodianId, fromId: currentCustodianId, toId: toCustodianId,
                    transferId: transfer.Id, notes: responseNote,
                    occurredAtUtc: respondedAt, previousHash: reqEvent.Hash);
                events.Add(resEvent);
                evidence.Events.Add(resEvent);

                if (accepted)
                {
                    currentCustodianId = toCustodianId;
                }

                previousEvent = resEvent;
                currentTimestamp = respondedAt;
            }
        }

        evidence.CurrentCustodianId = currentCustodianId;
        evidence.LastEventAtUtc = previousEvent.OccurredAtUtc;
        evidence.IntegrityCheckedAtUtc = previousEvent.OccurredAtUtc;

        return (evidence, transfers, events);
    }

    private static CustodyEvent CreateEvent(
        long id,
        int evidenceId,
        int sequence,
        CustodyEventType eventType,
        int actorId,
        int? fromId,
        int? toId,
        Guid? transferId,
        string? notes,
        DateTime occurredAtUtc,
        byte[] previousHash)
    {
        var custodyEvent = new CustodyEvent
        {
            Id = id,
            EvidenceId = evidenceId,
            Sequence = sequence,
            EventType = eventType,
            ActorUserId = actorId,
            FromCustodianId = fromId,
            ToCustodianId = toId,
            TransferId = transferId,
            Notes = notes,
            OccurredAtUtc = UtcDateTime.TruncateToMilliseconds(occurredAtUtc),
            PreviousHash = previousHash,
        };

        custodyEvent.Hash = ChainHasher.ComputeHash(custodyEvent);
        return custodyEvent;
    }
}
