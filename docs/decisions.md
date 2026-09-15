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

**Opción elegida.** Para un equipo pequeño con este perfil de tráfico (herramienta interna forense, uso intermitente, no consumidor masivo), se propone:

| Componente | Servicio | Nivel |
|---|---|---|
| Cómputo | Azure App Service (Linux) | **B1** (Basic, 1 core/1.75 GB) — no F1 |
| Base de datos | Azure SQL Database | **Serverless**, General Purpose, Gen5, 1 vCore, auto-pause deshabilitado en prod |
| Almacenamiento de evidencia | Azure Blob Storage | Hot tier + *immutable storage* (WORM) |
| Secretos | Azure Key Vault | Standard, referenciado por *Managed Identity* |
| Observabilidad | Application Insights | Pay-as-you-go |

Esta demo corre en el nivel **gratuito** (App Service F1 + Azure SQL con la oferta *free*) porque cumple el objetivo de esta prueba técnica a costo cero; la tabla de arriba es lo que se recomendaría para un uso real más allá de la evaluación.

**Cómputo — por qué B1 y no F1 ni Container Apps.** F1 (usado en esta demo) no tiene SLA, comparte cómputo con otros inquilinos, tiene una cuota diaria de CPU muy ajustada y no soporta *deployment slots* (necesarios para *swap* sin downtime) ni dominios personalizados con certificado. B1 es el primer nivel con SLA, cómputo dedicado y slots. Se descartó Azure Container Apps (serverless, escala a cero, pago por uso): más barato en tráfico intermitente, pero exige empaquetar la API como contenedor (Dockerfile, Azure Container Registry) — ceremonia de infraestructura que no se justifica para una sola API .NET de este tamaño. Señal para reconsiderar: tráfico realmente esporádico (picos raros, largos períodos sin uso) donde escalar a cero ahorre más de lo que cuesta la complejidad de contenedores.

**Azure SQL — por qué Serverless y no Provisioned.** El perfil de uso (equipo pequeño, no 24/7) encaja mejor con facturación por segundo de vCore que con vCores fijos reservados. Se mantiene auto-pause **deshabilitado** en producción (a diferencia de esta demo) para no exponer a los usuarios reales a la latencia de "despertar" la base (decenas de segundos) en la primera consulta tras inactividad; sí tiene sentido dejarlo activo en un ambiente de *staging* que se usa a diario pero no continuamente. Costo asumido: sin auto-pause, se paga por el mínimo de cómputo reservado aunque no haya tráfico en ese momento.

**Blob Storage — por qué WORM.** Aunque la carga de archivos queda fuera del alcance obligatorio de este reto, la arquitectura de producción debe anticiparla: el propósito del sistema es demostrar que una evidencia no fue alterada, y eso aplica igual de bien (o mejor) al archivo digital original que a sus metadatos. *Immutable blob storage* con políticas de retención por tiempo (*legal hold*) impide modificar o borrar un blob durante el período configurado, a nivel de la propia plataforma de almacenamiento — complementa, no reemplaza, el hash encadenado de `CustodyEvents`: el hash demuestra integridad del *registro*, WORM demuestra integridad del *archivo*.

**Key Vault y separación de ambientes.** `Jwt:Key`, la contraseña de SQL y cualquier clave futura (SAS de Blob Storage) viven en Key Vault, referenciadas desde la configuración del App Service (`@Microsoft.KeyVault(SecretUri=...)`) con *Managed Identity* — ni siquiera el operador que configura el App Service ve el secreto en texto plano en el portal. En esta demo, por simplicidad y para no sumar la fricción de configurar Managed Identity + Key Vault dentro del tiempo de la prueba técnica, los secretos van directo como variables de entorno del App Service (lo que ya cumple la regla no negociable de "nada de secretos en el repo", solo no llega al nivel de Key Vault). Ambientes: `dev` (local, Docker + `dotnet run`, ya implementado), `staging` (un *deployment slot* del mismo App Service Plan — más barato que un App Service separado — más una base de datos Azure SQL propia) y `production`. El *swap* de slot a producción es la forma de desplegar sin downtime y de poder revertir instantáneamente si algo falla.

**Application Insights y primer indicador/alerta.** El primero que se configuraría: una **prueba de disponibilidad** (*Availability Test*) de Azure Monitor contra `GET /health` cada 5 minutos desde 3+ regiones, con alerta si falla en 2+ regiones de forma consecutiva — es la señal más básica posible ("¿el servicio sigue vivo y conectado a la base?", que es literalmente lo que `/health` valida con `CanConnectAsync()`) y debe existir antes que cualquier alerta más específica del dominio. Como segunda alerta, ya con Application Insights instrumentado, tendría sentido una específica del dominio: tasa de `GET /chain/verify` con `isValid:false` por encima de un umbral — en un sistema sano esa tasa debería ser ~0; un salto indicaría o una manipulación real de datos, o (más probable en la práctica) un bug introducido en un despliegue reciente al formato canónico del hash.

**Estimación mensual (USD, aproximada, equipo pequeño con tráfico bajo/intermitente).**

| Componente | Estimado/mes |
|---|---|
| App Service B1 (Linux) | ~$13 |
| Azure SQL Serverless (1 vCore, uso intermitente + almacenamiento) | ~$15–30 |
| Blob Storage (Hot, pocos GB, uso bajo) | ~$1–5 |
| Key Vault (Standard, pocas operaciones) | <$1 |
| Application Insights (dentro del *free tier* de ingesta de 5 GB/mes) | $0–5 |
| **Total** | **~$30–55/mes** |

Cifras de lista pública, sin descuentos de compromiso ni Reserved Instances; una estimación de orden de magnitud, no una cotización. Costo asumido de esta propuesta: es más caro que la demo actual ($0 en niveles *free*), pero la demo no tiene SLA ni es apta para tráfico real. Señal de cambio: si el equipo crece o el tráfico deja de ser intermitente, subir a App Service **S1** (Standard, con autoscale real) y a Azure SQL **Provisioned** (vCores fijos, más predecible bajo carga sostenida).

Detalle completo del troubleshooting de despliegue (SCM Basic Auth, formato de zip, CORS Vercel↔Azure) en `context/progreso.md` y `context/ai-log.md` — no se repite aquí para respetar el límite de dos páginas de este documento.
