# Decisions

_Máximo dos páginas. Cuatro decisiones: opción elegida, alternativa descartada, costo asumido y señal de cambio._

## 1. Persistencia y encadenado de hash

**Opción elegida.** Cada `CustodyEvent` guarda `PreviousHash` y `Hash` (`binary(32)`). `Hash = SHA-256(JSON canónico UTF-8 del evento)` y el génesis usa 32 bytes en cero como `prev`. Formato canónico `v:1` (`Domain/Hashing/CanonicalEventSerializer.cs`), sin espacios y con orden de campos fijo:

```json
{"v":1,"evidenceId":42,"seq":1,"type":0,"actor":7,"from":null,"to":7,"transferId":null,"notes":null,"occurredAtUtc":"2026-09-01T14:05:00.000Z","prev":"0000…0000"}
```

Reglas del formato: `type` es el valor numérico del enum (renombrar un miembro en C# no cambia hashes); los campos ausentes se escriben como `null`, nunca se omiten; `transferId` en formato `D` minúsculas; `notes` con el escape JSON mínimo (`"`, `\`, controles) y UTF-8 sin `\uXXXX`; `occurredAtUtc` en UTC truncada a milisegundos (`yyyy-MM-ddTHH:mm:ss.fffZ`); `prev` en hex minúsculas. Dos tests *golden* fijan la cadena exacta y el SHA-256 calculado fuera del código: si cambian, el formato cambió.

**Alternativa descartada.** Texto con separadores (`id|seq|notes|…`): ambiguo si `notes` contiene el separador y sin versión ni tipos explícitos. También se descartó serializar la entidad por reflexión con `JsonSerializer`: el orden de campos dependería del orden de las propiedades en la clase. Se escribe con `Utf8JsonWriter` campo a campo.

**Orden estable.** `Sequence` (1..n por evidencia) con índice único `(EvidenceId, Sequence)`: no se puede insertar dos veces la misma posición ni bifurcar la cadena. `ChainVerifier` recorre por `Sequence` y exige contigüidad (`SequenceGap`), enlace con el hash anterior (`BrokenLink`) y recomputación del hash (`HashMismatch`), devolviendo el primer evento inválido. `CustodyEvents` tiene un trigger `INSTEAD OF UPDATE, DELETE` creado en la migración: append-only por regla de base de datos, no solo por disciplina de aplicación. Un único punto de escritura (`ChainAppender`) calcula `Sequence`, enlaza y firma.

**Costo asumido.** Las fechas se truncan a milisegundos antes de firmar y antes de guardar (`datetime2(3)` redondea, no trunca; un test de integración con Testcontainers guarda un evento con ticks sub-milisegundo y verifica que el hash sigue válido tras leerlo). Cambiar cualquier regla del formato invalida todos los hashes: exige `v:2`, migración de datos y regenerar el seed.

**Límite honesto y señal de cambio.** Quien pueda reescribir toda la tabla (deshabilitando el trigger) puede recalcular la cadena completa y quedará consistente: el hash detecta alteraciones parciales o accidentales, no a un administrador con acceso total. En producción se ancla con HMAC cuya clave vive en Key Vault (sin la clave no se puede refirmar), publicando el hash de cabeza fuera de la base, o con tablas *ledger* de Azure SQL. Señal para cambiar: un requisito de no repudio frente al propio DBA o auditoría externa de la cadena.

## 2. Paginación

**Opción elegida.** Keyset sobre `(LastEventAtUtc, Id)` en `GET /api/v1/evidence` (`Features/Evidence/ListEvidence.cs`). El cursor es opaco (base64url de `ticks:id:dirección`) y viaja con la dirección de orden, así un cursor de `desc` no puede reutilizarse con `asc`. La consulta pide `pageSize + 1` filas para saber si hay otra página sin un `COUNT` aparte; el índice `IX_Evidence_Keyset (LastEventAtUtc DESC, Id DESC) INCLUDE (Code, Description, CurrentCustodianId, IntegrityStatus)` la cubre por completo. Tamaño 20 por defecto, tope 100. Filtros: `q` sobre código y descripción, `custodianId`, `status`.

**Alternativa descartada.** `OFFSET … FETCH`: el coste crece con la profundidad de la página y, cuando entran eventos mientras el usuario navega (en custodia entran todo el tiempo y cambian `LastEventAtUtc`), la página siguiente repite u omite filas. Keyset es estable ante inserciones porque continúa desde la última clave vista, no desde una posición.

**Costo asumido.** No hay "ir a la página N" ni total de páginas; la interfaz ofrece siguiente/anterior. La búsqueda por texto usa `LIKE '%q%'` (no aprovecha índice); aceptable con 1.000 filas.

**Señal de cambio.** Más de ~100k evidencias o p95 > 300 ms en la bandeja con filtro de texto: Full-Text Search o columna de búsqueda normalizada. Si el negocio exige saltar a una página concreta, híbrido offset dentro de rangos de fecha.

## 3. Concurrencia y actualización optimista

**Opción elegida.** `ROWVERSION` de `CustodyTransfers` expuesto como ETag fuerte (`"0x…"`, `Infrastructure/Http/ETag.cs`) en cada respuesta. `accept` y `reject` exigen `If-Match`; sin cabecera → 428. EF fija la versión esperada como valor original y el `UPDATE` lleva `WHERE RowVersion = @esperada`: 0 filas → `DbUpdateConcurrencyException` → 409 `application/problem+json` con `currentState` (`transferId`, `status`, `version`, `respondedBy`, `respondedAtUtc`) releído de la base para que la interfaz explique qué pasó. Una transición inválida según `TransferStateMachine` (ya aceptada/rechazada) produce el mismo 409. Solo el custodio destinatario responde (403 para otros). Aceptar mueve `CurrentCustodianId`; ambas respuestas añaden el evento y actualizan `LastEventAtUtc` en una única transacción.

`POST /custody-transfers` es idempotente por usuario con `Idempotency-Key` (`Infrastructure/Idempotency/IdempotencyEndpointFilter.cs`): se inserta una fila placeholder en `IdempotencyRecords` dentro de la misma transacción que la operación, de modo que un reintento concurrente espera en la clave primaria hasta que el primero confirma y luego repite la respuesta guardada (código, cuerpo, `ETag`, `Location`, cabecera `Idempotent-Replayed`). La misma clave con otro cuerpo → 422. Solo se persisten respuestas 2xx: un 409 o 422 no "quema" la clave. Una segunda solicitud pendiente para la misma evidencia devuelve 409 con el estado de la existente; la carrera que escapa a la comprobación previa la ataja el índice único filtrado y se traduce (errores 2601/2627) al mismo 409.

**Alternativa descartada.** Bloqueo pesimista (`UPDLOCK` al abrir la transferencia): mantiene bloqueos durante una interacción humana de duración indefinida. "Último gana" sin versión: dos custodios responderían y ambos verían 200. `412 Precondition Failed` sería el código canónico para un `If-Match` que no coincide, pero el enunciado exige 409 y se documenta.

**Costo asumido.** El cliente debe conservar y reenviar el ETag; un ETag caducado obliga a recargar (la respuesta 409 ya trae la versión vigente). El hash de idempotencia se calcula sobre el cuerpo ya deserializado (no sobre los bytes crudos), por lo que dos JSON equivalentes con distinto formato cuentan como el mismo cuerpo. `IdempotencyRecords` crece con cada escritura; no hay purga.

**Señal de cambio.** Conflictos 409 frecuentes sobre la misma transferencia (métrica) → asignación explícita de trabajo o cola. Un segundo canal de escritura (worker, integración) → mover idempotencia a un almacén compartido con TTL. Volumen de `IdempotencyRecords` → job de purga por `CreatedAtUtc`.

## 4. Arquitectura Azure de producción

Cómputo, Azure SQL, Blob Storage, Key Vault y Application Insights; separación de ambientes; primer indicador/alerta; estimación mensual para un equipo pequeño.
