# Registro de uso de IA

Materia prima de `docs/ai-usage.md`. El enunciado exige al menos una sugerencia de IA **rechazada o corregida**, con el motivo y lo que se hizo en su lugar. Anota los casos a medida que ocurran; no los reconstruyas al final.

## Herramientas

| Herramienta | Uso |
|---|---|
| Claude (Cowork / Claude Code) | Análisis del enunciado, plan de arquitectura, contexto para agentes |
| Claude Code | Scaffolding del backend, modelo EF y migración, dominio de hash, tests unitarios y de integración |
| Claude Code | Fase 3: máquina de estados, regla de anomalía, JWT demo, endpoints, idempotencia, concurrencia optimista, tests de integración de la API y `openapi.yaml` |
| Gemini (Antigravity) | Seeder determinista e idempotente, optimización con SqlBulkCopy, tests unitarios de seed y documentación de base de datos |
| Claude Code | Fase 4: scaffolding del frontend (Vite + React + TS), cliente API, autenticación demo, bandeja con filtros en URL y keyset, detalle con línea de tiempo y verify, modal de transferencia accesible, bandeja del custodio con optimista/rollback, pruebas con MSW |

## Sugerencias aceptadas

| Fecha | Herramienta | Sugerencia | Por qué se aceptó |
|---|---|---|---|
| 2026-09-13 | Claude Code | Insertar una fila *placeholder* en `IdempotencyRecords` dentro de la misma transacción **antes** de ejecutar la operación, y persistir solo respuestas 2xx | Una carrera con la misma clave bloquea en la PK hasta que la primera confirma y luego repite la respuesta guardada; un 409/422 no "quema" la clave y el cliente puede reintentar |
| 2026-09-13 | Claude Code | El cursor keyset lleva la dirección de orden y se rechaza si no coincide con `sort` | Evita páginas incoherentes cuando el cliente cambia el orden sin descartar el cursor; el cursor sigue siendo opaco |
| 2026-09-13 | Claude Code | La anomalía se calcula al leer (`PendingTransferRule` con `TimeProvider`) y se expone en detalle, bandeja de transferencias y en el evento de solicitud de la línea de tiempo | Cambiar el umbral no exige reprocesar datos y los tests la fijan con un reloj falso |
| 2026-09-13 | Claude Code | 401/403 de la autenticación también en `application/problem+json` vía `UseStatusCodePages` | El enunciado exige `problem+json` para todo 4xx/5xx, no solo para los errores que emiten los handlers |
| 2026-09-13 | Claude Code | Modal de transferencia con el elemento `<dialog>` nativo (`.showModal()`) en vez de una librería como Radix | El navegador ya atrapa el foco dentro del diálogo y cierra con Escape sin código propio; solo hubo que sincronizar `open` con `showModal()`/`close()` y devolver el foco al botón que lo abrió en el evento `close` |
| 2026-09-13 | Claude Code | Bandeja de evidencias: pila de cursores keyset en el cliente (no en la URL) para ofrecer "Anterior", reseteada al cambiar cualquier filtro | La API solo expone `nextCursor` (paginación hacia adelante); guardar la pila en estado de componente evita inventar un cursor "hacia atrás" que la API no puede dar, y coincide con la limitación ya documentada en `decisions.md` ("no se puede saltar a la página N") |
| 2026-09-14 | Claude Code | Corregir el schema colisionado de `openapi.yaml` configurando `AddOpenApi(options => options.CreateSchemaReferenceId = ...)` en vez de renombrar los cinco records `Response`/`Request` anidados | Un solo archivo tocado (`Program.cs`) contra al menos 7-8 si se renombraba (los propios records, los `.Produces<T>()` de `AuthEndpoints`/`EvidenceEndpoints`/`TransferEndpoints`, y los tests de integración que deserializan por ese tipo); además es una corrección sistémica: como la convención del repo es un `Request`/`Response` anidado por endpoint, sin este fix cualquier endpoint nuevo puede volver a colisionar. De paso destapó una segunda colisión no documentada (`IssueToken.Request` vs `RequestTransfer.Request`) que el reporte original no mencionaba |
| 2026-09-12 | Claude Code | En el JSON canónico, `type` va como valor numérico del enum y los campos ausentes como `null` explícito (nunca omitidos) | Renombrar un miembro del enum o añadir un campo opcional no puede cambiar hashes ya guardados; el formato queda fijado por dos tests *golden* |
| 2026-09-12 | Claude Code | Conversor EF que trunca a milisegundos **al escribir** además de truncar antes de firmar | `datetime2(3)` redondea (.0009999 ms → .001); sin el truncado en escritura el hash firmado en memoria no coincidía con el evento leído. Verificado con un test de ida y vuelta en SQL Server |
| 2026-09-12 | Claude Code | Declarar el trigger en el modelo EF (`HasTrigger`) | SQL Server rechaza el `OUTPUT` que EF usa en los INSERT cuando la tabla tiene triggers; con la declaración EF cambia de estrategia |
| 2026-09-12 | Claude Code | `dotnet-ef` como herramienta local (`dotnet-tools.json`) en vez de global | Versión fijada en el repo y reproducible con `dotnet tool restore` |
| 2026-09-12 | Gemini | Inserción en bulto con `SqlBulkCopy` tipado en `DataTable`s en memoria y reasentamiento de secuencias con `DBCC CHECKIDENT` | Carga 1.000 evidencias y más de 10.000 eventos en menos de 2 segundos (frente a minutos con EF Core fila por fila), reasentando las secuencias de identidad para no colisionar con operaciones posteriores de la API |
| 2026-09-12 | Gemini | Desactivación transitoria de `TR_CustodyEvents_AppendOnly` en transacción para alterar notas de evento 2 | Permite insertar la cadena íntegra primero y luego aplicar la modificación fraudulenta, demostrando que la verificación forense SHA-256 canónica detecta manipulaciones a nivel de base de datos |

## Sugerencias rechazadas o corregidas

| Fecha | Herramienta | Sugerencia | Por qué era inadecuada | Qué se hizo en su lugar |
|---|---|---|---|---|
| 2026-09-12 | Claude Code | `.gitignore` generado con el nombre del enunciado entre comillas (`"01-PRUEBA-CANDIDATO 2.md"`) | Git trata las comillas como caracteres literales: el archivo nunca se ignoró (apareció en `git status`) | Patrón sin comillas; los espacios no necesitan escape en `.gitignore` |
| 2026-09-13 | openapi-typescript (vía Claude Code) | Generar los tipos del frontend con `openapi-typescript` a partir de `openapi.yaml`, como pedía la instrucción original | El `openapi.yaml` exportado tiene un bug real del backend: `ListDemoUsers`, `GetChain`, `VerifyChain` y `ListTransfers` comparten por error el schema `Response` (sus records anidados C# se llaman igual y el generador de OpenAPI de ASP.NET Core colisiona por el nombre corto del tipo), y además tipa todos los IDs como `number \| string` por el patrón de ruta de los enteros. Usar el resultado tal cual habría dejado 4 endpoints mal tipados sin que el compilador lo detectara | Tipos escritos a mano en `frontend/src/api/types.ts` a partir del código real de `Features/*.cs`; verificados campo por campo contra la API real con `curl` (auth, detalle con anomalía, verify con hash roto, 201/replay/422 de idempotencia, 428/409/200/409 de accept). El bug de `openapi.yaml` queda pendiente de corregir en el backend (namespacing de los schemas de respuesta) |

Candidatos típicos en este reto (confirmar solo si ocurren de verdad): formato canónico con separadores `|` (ambiguo si el texto contiene el separador), `DateTime.Now` en lugar de UTC, paginación por offset, hash calculado antes de truncar la precisión de la fecha, `FromSqlRaw` con interpolación, `async void`.

## Código revisado con especial cuidado

| Área | Qué se verificó |
|---|---|
| Hash de eventos y precisión de fecha | El hash golden se calculó fuera del código (PowerShell, SHA-256 sobre los bytes UTF-8 del JSON esperado) y el test de integración usa ticks sub-milisegundo que `datetime2(3)` redondearía |
| Migración generada por EF | Que `IsDescending` en `IX_Evidence_Keyset` se tradujera a `DESC` real en el script SQL, el índice filtrado `WHERE [Status] = 0` y `Restrict` en todas las FK a `Users` |
| Concurrencia en accept/reject | El test envía dos respuestas con el mismo `If-Match`: la segunda debe ser 409 y su `currentState.version` debe ser exactamente el ETag que devolvió la primera; además la cadena resultante se verifica válida y `CurrentCustodianId` cambia solo al aceptar |
| CORS y cabeceras | `WithExposedHeaders("ETag", "Location", "Idempotent-Replayed")`: sin esto la SPA no podría leer el ETag que necesita para `If-Match` |
| `openapi.yaml` tras el fix de schemas | Que las 11 operaciones quedaran con `$ref` distintos (no solo que compilara); comparado campo por campo contra las respuestas reales de la API con el seed cargado (`auth/token`, `auth/users`, `chain`, `chain/verify` sobre el caso alterado, `custody-transfers?mine=true`), no solo contra el yaml en sí |
