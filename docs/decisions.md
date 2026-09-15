# Decisions

_Máximo dos páginas. Cuatro decisiones: opción elegida, alternativa descartada, costo asumido y señal de cambio._

## 1. Persistencia y encadenado de hash

**Opción elegida.** Cada `CustodyEvent` guarda `PreviousHash` y `Hash` (`binary(32)`). `Hash = SHA-256(JSON canónico UTF-8 del evento)` y el génesis usa 32 bytes en cero como `prev`. Formato canónico `v:1` (`Domain/Hashing/CanonicalEventSerializer.cs`), sin espacios y con orden de campos fijo:

```json
{"v":1,"evidenceId":42,"seq":1,"type":0,"actor":7,"from":null,"to":7,"transferId":null,"notes":null,"occurredAtUtc":"2026-09-01T14:05:00.000Z","prev":"0000…0000"}
```

Reglas del formato: `type` es el valor numérico del enum (renombrar un miembro en C# no cambia hashes); los campos ausentes se escriben como `null`, nunca se omiten; `transferId` en formato `D` minúsculas; `notes` con el escape JSON mínimo (`"`, `\`, controles) y UTF-8 sin `\uXXXX`; `occurredAtUtc` en UTC truncada a milisegundos (`yyyy-MM-ddTHH:mm:ss.fffZ`); `prev` en hex minúsculas. Dos tests *golden* fijan la cadena exacta y el SHA-256 calculado fuera del código: si cambian, el formato cambió.

**Alternativa descartada.** Texto con separadores (`id|seq|notes|…`): ambiguo si `notes` contiene el separador y sin versión ni tipos explícitos. También reflexión con `JsonSerializer` (el orden de campos dependería del orden de las propiedades en la clase). Se escribe con `Utf8JsonWriter` campo a campo.

**Orden estable.** `Sequence` (1..n por evidencia) con índice único `(EvidenceId, Sequence)`. `ChainVerifier` recorre por `Sequence` y exige contigüidad (`SequenceGap`), enlace con el hash anterior (`BrokenLink`) y recomputación del hash (`HashMismatch`), devolviendo el primer evento inválido. `CustodyEvents` tiene un trigger `INSTEAD OF UPDATE, DELETE`: append-only por regla de base de datos, no solo por disciplina de aplicación. Un único punto de escritura (`ChainAppender`) calcula `Sequence`, enlaza y firma.

**Costo asumido.** Las fechas se truncan a milisegundos antes de firmar y de guardar (`datetime2(3)` redondea, no trunca; un test de integración lo verifica con ticks sub-milisegundo). Cambiar cualquier regla del formato invalida todos los hashes: exige `v:2`, migración de datos y regenerar el seed.

**Límite honesto y señal de cambio.** Quien reescriba toda la tabla (deshabilitando el trigger) puede recalcular la cadena completa y quedará consistente: el hash detecta alteraciones parciales, no a un administrador con acceso total. En producción se ancla con HMAC cuya clave vive en Key Vault, publicando el hash de cabeza fuera de la base, o con tablas *ledger* de Azure SQL. Señal para cambiar: un requisito de no repudio frente al propio DBA o auditoría externa.

## 2. Paginación

**Opción elegida.** Keyset sobre `(LastEventAtUtc, Id)` en `GET /api/v1/evidence` (`Features/Evidence/ListEvidence.cs`). El cursor es opaco (base64url de `ticks:id:dirección`) y viaja con la dirección de orden, así un cursor de `desc` no puede reutilizarse con `asc`. La consulta pide `pageSize + 1` filas para saber si hay otra página sin un `COUNT` aparte; el índice `IX_Evidence_Keyset (LastEventAtUtc DESC, Id DESC) INCLUDE (Code, Description, CurrentCustodianId, IntegrityStatus)` la cubre por completo. Tamaño 20 por defecto, tope 100. Filtros: `q` sobre código y descripción, `custodianId`, `status`.

**Alternativa descartada.** `OFFSET … FETCH`: el coste crece con la profundidad de la página y, cuando entran eventos mientras el usuario navega (en custodia entran todo el tiempo y cambian `LastEventAtUtc`), la página siguiente repite u omite filas. Keyset es estable ante inserciones porque continúa desde la última clave vista, no desde una posición.

**Costo asumido.** No hay "ir a la página N" ni total de páginas; la interfaz ofrece siguiente/anterior. La búsqueda por texto usa `LIKE '%q%'` (no aprovecha índice); aceptable con 1.000 filas.

**Señal de cambio.** Más de ~100k evidencias o p95 > 300 ms en la bandeja con filtro de texto: Full-Text Search o columna de búsqueda normalizada. Si el negocio exige saltar a una página concreta, híbrido offset dentro de rangos de fecha.

## 3. Concurrencia y actualización optimista

**Opción elegida.** `ROWVERSION` de `CustodyTransfers` expuesto como ETag fuerte (`"0x…"`, `Infrastructure/Http/ETag.cs`). `accept`/`reject` exigen `If-Match`; sin cabecera → 428. EF fija la versión esperada como valor original (`UPDATE … WHERE RowVersion = @esperada`): 0 filas → `DbUpdateConcurrencyException` → 409 con `currentState` (`transferId`, `status`, `version`, `respondedBy`, `respondedAtUtc`) releído de la base. Una transición inválida (ya aceptada/rechazada) produce el mismo 409. Solo el custodio destinatario responde (403 para otros); ambas respuestas añaden el evento y actualizan `LastEventAtUtc` en una única transacción.

`POST /custody-transfers` es idempotente por usuario con `Idempotency-Key`: se inserta una fila placeholder en `IdempotencyRecords` dentro de la misma transacción, así un reintento concurrente espera en la clave primaria hasta que el primero confirma y repite la respuesta guardada (`ETag`, `Location`, `Idempotent-Replayed`). La misma clave con otro cuerpo → 422; solo se persisten 2xx (un 409/422 no "quema" la clave). Una segunda solicitud pendiente para la misma evidencia → 409; la carrera que escapa a esa comprobación la ataja un índice único filtrado.

**Alternativa descartada.** Bloqueo pesimista (`UPDLOCK`): mantiene bloqueos durante una interacción humana de duración indefinida. "Último gana" sin versión: dos custodios responderían y ambos verían 200. `412` sería el código canónico para un `If-Match` que no coincide, pero el enunciado exige 409.

**Costo asumido.** El cliente debe conservar y reenviar el ETag (el 409 ya trae la versión vigente). El hash de idempotencia se calcula sobre el cuerpo deserializado, no sobre bytes crudos. `IdempotencyRecords` crece con cada escritura; no hay purga.

**Señal de cambio.** 409 frecuentes sobre la misma transferencia → asignación explícita de trabajo o cola. Segundo canal de escritura (worker, integración) → idempotencia en almacén compartido con TTL. Volumen de `IdempotencyRecords` → job de purga por `CreatedAtUtc`.

## 4. Arquitectura Azure de producción

**Opción elegida.** Para un equipo pequeño con este perfil de tráfico (herramienta interna forense, uso intermitente, no consumidor masivo), se propone:

| Componente | Servicio | Nivel |
|---|---|---|
| Cómputo | Azure App Service (Linux) | **B1** (Basic, 1 core/1.75 GB) — no F1 |
| Base de datos | Azure SQL Database | **Serverless**, General Purpose, Gen5, 1 vCore, auto-pause deshabilitado en prod |
| Almacenamiento de evidencia | Azure Blob Storage | Hot tier + *immutable storage* (WORM) |
| Secretos | Azure Key Vault | Standard, referenciado por *Managed Identity* |
| Observabilidad | Application Insights | Pay-as-you-go |

Esta demo corre en el nivel **gratuito** (App Service F1 + Azure SQL *free*) a costo cero; la tabla de arriba es lo recomendado para un uso real.

**Cómputo.** F1 no tiene SLA, comparte cómputo con otros inquilinos y no soporta *deployment slots* (swap sin downtime) ni dominio propio con certificado; B1 sí. Se descartó Container Apps (más barato en tráfico intermitente, pero exige empaquetar como contenedor — ceremonia que no se justifica para una API de este tamaño; reconsiderar si el tráfico es realmente esporádico).

**Azure SQL.** Serverless porque el perfil (equipo pequeño, no 24/7) encaja mejor con facturación por segundo que con vCores fijos reservados. Auto-pause **deshabilitado** en producción (a diferencia de esta demo) para no exponer a usuarios reales a la latencia de "despertar" la base; sí tendría sentido en *staging*. Costo asumido: sin auto-pause se paga el mínimo reservado aunque no haya tráfico.

**Blob Storage (WORM).** Fuera del alcance obligatorio, pero la arquitectura de producción debe anticipar la carga de archivos: *immutable blob storage* con retención por tiempo complementa el hash encadenado — el hash demuestra integridad del *registro*, WORM la del *archivo* original.

**Key Vault y ambientes.** `Jwt:Key`, contraseña de SQL y claves futuras (SAS de Blob) vivirían en Key Vault vía *Managed Identity*, sin que el operador las vea en texto plano. En esta demo van como variables de entorno del App Service (cumple "nada de secretos en el repo", no llega al nivel de Key Vault). Ambientes: `dev` (local, ya implementado), `staging` (*deployment slot* del mismo plan + base propia) y `production`, con *swap* de slot para desplegar sin downtime y poder revertir.

**Observabilidad y primera alerta.** Application Insights; la primera alerta es una **prueba de disponibilidad** de Azure Monitor contra `GET /health` cada 5 min desde 3+ regiones (falla en 2+ = alerta) — la señal más básica ("¿sigue vivo y conectado?"), literalmente lo que valida `CanConnectAsync()`. Segunda alerta, ya con dominio instrumentado: tasa de `GET /chain/verify` con `isValid:false` por encima de un umbral (en un sistema sano ronda 0).

**Estimación mensual (USD, aproximada).**

| Componente | Estimado/mes |
|---|---|
| App Service B1 | ~$13 |
| Azure SQL Serverless (1 vCore + almacenamiento) | ~$15–30 |
| Blob Storage (Hot, pocos GB) | ~$1–5 |
| Key Vault | <$1 |
| Application Insights (dentro del *free tier*) | $0–5 |
| **Total** | **~$30–55/mes** |

Cifras de lista pública, orden de magnitud, no cotización. Más caro que la demo ($0 en niveles *free*), pero la demo no tiene SLA. Señal de cambio: equipo/tráfico crecen → App Service **S1** y Azure SQL **Provisioned**.

Detalle del troubleshooting de despliegue (SCM Basic Auth, formato de zip, CORS Vercel↔Azure) en `context/progreso.md` y `context/ai-log.md`, fuera de este documento por el límite de dos páginas.
