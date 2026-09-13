# Recorrido de datos: una evidencia de principio a fin

Sirve como guion de la demo de 30 minutos y como referencia de qué escribe cada operación. Usuarios del seed usados aquí: `7 c.rivas` (Investigador), `12 lab.forense` (Custodio), `19 m.quispe` (Custodio).

## T0 · Registro

```
Evidence
Id  Code       Description             CurrentCustodianId  LastEventAtUtc       IntegrityStatus
42  EV-000042  Laptop HP ProBook 450   7                   2026-09-01 14:05:00  0 NoVerificada

CustodyEvents
Id    EvidenceId Sequence EventType   Actor From To TransferId OccurredAtUtc        PreviousHash Hash
9001  42         1        Registrada  7     NULL 7  NULL       2026-09-01 14:05:00  0x0000…0000  0x8f2a…
```

`TransferId` es NULL: el evento de registro no nace de un trámite.

## T1 · Solicitud de transferencia

La custodia **no** se mueve todavía.

```
CustodyTransfers
Id       Evidence From To RequestedBy Status       RequestedAtUtc       RespondedAtUtc RowVersion
a3f1-…   42       7    12 7           0 Pendiente  2026-09-01 16:40:00  NULL           0x00000A1F

CustodyEvents (nuevo)
9002  42  2  TransferenciaSolicitada  7  7  12  a3f1-…  2026-09-01 16:40:00  0x8f2a…  0x41bd…

Evidence
CurrentCustodianId  7                    ← sin cambios
LastEventAtUtc      2026-09-01 16:40:00  ← actualizado
```

Desde aquí, el índice único filtrado `UX_Transfer_OnePendingPerEvidence` impide otra transferencia pendiente para la evidencia 42.

## T2 · Aceptación (una sola transacción, tres tablas)

```
CustodyTransfers (UPDATE)
a3f1-…  Status 1 Aceptada  RespondedAtUtc 2026-09-02 09:12:00  RespondedBy 12  RowVersion 0x00000B47

CustodyEvents (INSERT)
9003  42  3  TransferenciaAceptada  12  7  12  a3f1-…  2026-09-02 09:12:00  0x41bd…  0x7c09…

Evidence (UPDATE)
CurrentCustodianId  12                   ← ahora sí cambia
LastEventAtUtc      2026-09-02 09:12:00
```

`RowVersion` la incrementa SQL Server, nadie la escribe. Si falla cualquiera de las tres operaciones, se revierten las tres: de lo contrario quedaría un hueco en `Sequence` o un custodio desincronizado.

## T3 · Rechazo

```
CustodyTransfers
b8c4-…  12 → 19  Status 2 Rechazada  ResponseNote "Sin espacio en bóveda"

CustodyEvents (dos filas)
9004  42  4  TransferenciaSolicitada  12  12  19  b8c4-…  2026-09-05 11:00:00  0x7c09…  0x2e55…
9005  42  5  TransferenciaRechazada   19  12  19  b8c4-…  2026-09-05 18:25:00  0x2e55…  0xd130…

Evidence
CurrentCustodianId  12                   ← el rechazo no mueve la custodia
LastEventAtUtc      2026-09-05 18:25:00  ← pero sí hubo actividad
```

Aceptar mueve el custodio, rechazar no; ambos escriben evento, porque los dos son hechos con valor forense.

## T4 · Anomalía

```
CustodyTransfers
c9d2-…  42  12 → 19  0 Pendiente  RequestedAtUtc 2026-09-08 10:00:00
```

Con umbral de 48 h y "ahora" = 2026-09-11 11:00, lleva 73 h pendiente:

```sql
WHERE Status = 0 AND RequestedAtUtc < @ahora - @umbral
```

No se persiste nada: la anomalía se calcula al leer. Cambiar el umbral recalcula todo sin reprocesar datos.

## Conflicto de concurrencia

```
A  GET     → ETag "0x00000C83"
B  GET     → ETag "0x00000C83"
A  accept  If-Match 0x00000C83 → UPDATE … WHERE RowVersion = 0x00000C83 → 1 fila → 200, RowVersion 0x00000D15
B  reject  If-Match 0x00000C83 → UPDATE … WHERE RowVersion = 0x00000C83 → 0 filas → 409
```

```json
{
  "type": "https://evidence-chain/errors/transfer-conflict",
  "title": "La transferencia ya fue resuelta",
  "status": 409,
  "detail": "m.quispe aceptó esta transferencia el 2026-09-11T11:04:22Z.",
  "currentState": {
    "status": "Aceptada",
    "version": "0x00000D15",
    "respondedBy": "m.quispe",
    "respondedAtUtc": "2026-09-11T11:04:22Z"
  }
}
```

## Por qué `Evidence` duplica tres columnas

`CurrentCustodianId`, `LastEventAtUtc` e `IntegrityStatus` son derivables de la cadena:

```sql
SELECT TOP 1 ToCustodianId
FROM CustodyEvents
WHERE EvidenceId = 42 AND EventType IN (Registrada, TransferenciaAceptada)
ORDER BY Sequence DESC;
```

Sirve para una evidencia, no para una bandeja de mil con orden por fecha: no se indexa una columna que no existe, y la paginación keyset dejaría de funcionar. Con las columnas guardadas, la consulta principal es un solo recorrido de índice:

```sql
SELECT TOP (@size) e.Id, e.Code, e.Description, u.DisplayName, e.LastEventAtUtc, e.IntegrityStatus
FROM Evidence e
JOIN Users u ON u.Id = e.CurrentCustodianId
WHERE (e.LastEventAtUtc < @cursorAt
   OR (e.LastEventAtUtc = @cursorAt AND e.Id < @cursorId))
ORDER BY e.LastEventAtUtc DESC, e.Id DESC;
```

El trato: se duplican tres columnas y a cambio se actualizan **siempre** dentro de la transacción que escribe el evento. La fuente de verdad sigue siendo la cadena; `/chain/verify` recalcula desde los eventos e ignora `IntegrityStatus`.
