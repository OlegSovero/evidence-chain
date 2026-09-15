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

_Pendiente de completar: cómputo, Azure SQL, Blob Storage, Key Vault y Application Insights; separación de ambientes; primer indicador/alerta; estimación mensual para un equipo pequeño._

### Despliegue del backend: autenticación básica de SCM

**Opción elegida.** El App Service (`evidencechain-api`, Linux, F1, West US 3) viene por defecto con **`SCM Basic Auth Publishing Credentials` deshabilitado** (`basicPublishingCredentialsPolicies/scm` → `allow: false`) — postura segura estándar de Azure para App Services nuevos. Para poder desplegar con `az webapp deploy --type zip` (ZipDeploy vía Kudu) desde esta máquina de desarrollo, se reactivó explícitamente:

```bash
az resource update --resource-group evidence-chain-rg --name scm --namespace Microsoft.Web \
  --resource-type basicPublishingCredentialsPolicies --parent sites/evidencechain-api \
  --set properties.allow=true
```

(Equivalente en el portal: App Service → Configuración → *General settings* → `SCM Basic Auth Publishing Credentials` → On.)

**Cómo se detectó.** Un primer intento de ZipDeploy falló silenciosamente en la etapa "Extract zip" (causa real distinta, ver abajo); al intentar leer el log detallado de Kudu con las credenciales del publish profile (usuario/contraseña) se obtuvo `401 Unauthorized`. Un segundo intento con `az webapp deploy` devolvió `Kudu Status: 400` sin crear ningún registro de deployment (`az webapp log deployment list` vacío) — la petición nunca llegó a autenticarse. `az resource show` sobre `basicPublishingCredentialsPolicies/scm` confirmó `allow: false`.

**Alternativa descartada.** Dejarlo deshabilitado y desplegar solo vía un pipeline de CI/CD autenticado con Azure AD (GitHub Actions + OIDC/Service Principal, que no depende de Basic Auth de Kudu). Es la opción correcta para producción, pero añade infraestructura de CI que no aporta al alcance de esta prueba técnica; se documenta como el camino a seguir si el proyecto continuara más allá de la entrega.

**Costo asumido.** Basic Auth de Kudu expone un usuario/contraseña con permisos de despliegue si se filtran (van en el publish profile, nunca en el repo). Aceptable para una demo de tiempo acotado con un solo desarrollador desplegando manualmente.

**Señal de cambio.** Antes de cualquier uso más allá de esta prueba técnica: desactivar de nuevo `SCM Basic Auth` y mover el despliegue a un pipeline con identidad federada (OIDC), que es el estándar recomendado por Azure y no reintroduce credenciales de larga duración.

### Causa raíz real del primer fallo de deploy (para no repetirla)

Un ZipDeploy anterior falló por dos motivos, ninguno relacionado con `web.config` (que en App Service **Linux** se ignora por completo — es config de IIS/Windows):

1. El zip se generó comprimiendo la **carpeta** `publish/` en vez de su **contenido**: todas las entradas quedaban bajo `publish/EvidenceChain.Api.dll` en vez de `EvidenceChain.Api.dll` en la raíz, así que el runtime no encontraba el ensamblado de entrada tras extraer.
2. El archivo `.deployment` incluido en el zip tenía contenido corrupto: el código PowerShell usado para generarlo (`@"..."@ | Out-File ...`) quedó escrito tal cual como contenido, en vez de solo el `[config]` / `command = ...` resultante. Kudu no podía parsearlo como INI válido.

Corrección: eliminar `.deployment` (no hace falta ningún comando custom para un ZipDeploy de binario ya compilado) y regenerar el zip comprimiendo el contenido de `publish/` (no la carpeta).

### Segunda causa: separadores de ruta de `Compress-Archive` y build en Kudu

**El problema.** Con el zip ya corregido, el deploy seguía fallando en la etapa "Building the app…", con `rsync` rechazando cada archivo dentro de una subcarpeta:

```
rsync: failed to stat "/home/site/wwwroot/de\Microsoft.Data.SqlClient.resources.dll": Invalid argument (22)
rsync: failed to stat "/home/site/wwwroot/runtimes\win-x64\native\Microsoft.Data.SqlClient.SNI.dll": Invalid argument (22)
```

`Compress-Archive` de PowerShell (Windows) escribió las rutas internas del zip con `\` en vez de `/`. El estándar ZIP exige `/`; en Linux, `\` es un carácter de archivo normal, no un separador — así que `de\Microsoft...dll` se interpreta como un nombre de archivo plano, no como `de/Microsoft...dll` dentro de una carpeta `de`, y la sincronización de Kudu no puede reconciliarlo. Solo los archivos en la raíz del zip (sin subcarpeta) se habían librado hasta ahora.

**Opción elegida.** Generar el zip con `tar` (incluido en Windows 10/11, basado en `libarchive`, siempre usa `/`) en vez de `Compress-Archive`:

```powershell
cd backend\publish
tar -a -c -f ..\publish.zip *
```

Además, se fijó `SCM_DO_BUILD_DURING_DEPLOYMENT=false` en el App Service:

```bash
az webapp config appsettings set --resource-group evidence-chain-rg --name evidencechain-api \
  --settings SCM_DO_BUILD_DURING_DEPLOYMENT=false
```

**Por qué esto además del zip.** Sin este ajuste, Kudu trata cualquier ZipDeploy como código **fuente** y lo pasa por su pipeline de build/sincronización (Oryx + `rsync`) — el mismo pipeline donde reventó el error de `\`. El binario ya se compiló en local con `dotnet publish`; pedirle a Azure que lo "compile" de nuevo es trabajo duplicado y, en este caso, la causa directa del fallo. Con la variable en `false`, Kudu solo extrae el zip a `wwwroot` y arranca — sin Oryx, sin `rsync`, sin el problema de separadores.

**Cuándo sí conviene que Kudu compile (`SCM_DO_BUILD_DURING_DEPLOYMENT=true`, el default).** Solo cuando Azure es el *único* mecanismo de build — por ejemplo, integración Git directa de App Service (`git push` de código fuente sin pipeline externo) o una demo rápida sin CI. En cualquier proyecto con un pipeline de CI/CD real (GitHub Actions, Azure Pipelines), el build vive en el runner (`dotnet build`/`test`/`publish` como steps explícitos, con sus propios logs y artefactos versionados) y App Service solo recibe el artefacto ya compilado y probado — dejar `true` en ese caso duplicaría el build y rompería la garantía de "lo desplegado es justo lo que pasó CI".

**Alternativa descartada.** Seguir con `Compress-Archive` normalizando manualmente los separadores de cada entrada del zip (posible vía `System.IO.Compression.ZipFile` a bajo nivel), o cambiar a 7-Zip. Más frágil y con más pasos que simplemente usar `tar`, que ya viene instalado y resuelve el problema en el origen.

**Costo asumido.** `tar` en Windows es un wrapper de `bsdtar`/`libarchive`; funciona igual en cualquier Windows 10 (1803+)/11 sin instalar nada, pero es una dependencia menos "nativa" de PowerShell que `Compress-Archive` — cualquier script de empaquetado debe usar `tar`, no `Compress-Archive`, para este proyecto.

**Señal de cambio.** En cuanto exista un pipeline de CI/CD (GitHub Actions), este empaquetado manual desaparece: el runner (Linux, normalmente) no tiene el problema de separadores de Windows, y el `dotnet publish` + `az webapp deploy`/`actions-deploy` del workflow reemplaza estos pasos manuales.

**Verificado.** Tras aplicar ambos cambios, el deploy quedó en `status: 4` (Success) en `az webapp log deployment list`, el contenedor arrancó ("Now listening on: http://[::]:8080", "Application started") y `GET /health` respondió `200 {"status":"healthy"}` contra la URL pública, confirmando conexión real a Azure SQL.

**Nota aparte, sin resolver.** `az webapp deploy --type zip` (el comando recomendado, no deprecado) devolvía `Kudu Status: 400` con cuerpo vacío en cada intento, incluso con Basic Auth ya habilitado y el zip corregido — mientras que el comando deprecado `az webapp deployment source config-zip` sí completó el deploy con éxito usando el mismo zip. No se investigó la causa exacta de ese 400 (posiblemente una particularidad del endpoint `/api/publish` de OneDeploy en este App Service); si se retoma el despliegue manual, usar `config-zip` pese al aviso de deprecación, o preferir un pipeline de CI/CD que no dependa de ninguno de los dos.

### CORS entre el frontend en Vercel y el backend en Azure

**El problema.** Tras desplegar el frontend en Vercel, la API en Azure devolvía CORS bloqueado. La causa **no** estaba en `Program.cs`, sino en cómo se configuró la variable de entorno: `Cors__AllowedOrigins` en Azure tenía el valor literal `["https://*.vercel.app"]` — un string con sintaxis de array JSON, no un array real. Las variables de entorno de .NET **no parsean JSON**; para vincular un array (`Cors:AllowedOrigins` es `string[]` en `appsettings.json`), hace falta el formato indexado: `Cors__AllowedOrigins__0`, `__1`, etc. Con un solo string plano, `GetSection("Cors:AllowedOrigins").Get<string[]>()` no encuentra hijos indexados y el array queda vacío.

Además, el string usaba `*` como comodín de subdominio (`https://*.vercel.app`), algo que `policy.WithOrigins(...)` de ASP.NET Core **no soporta** — hace comparación exacta de string contra el header `Origin` del navegador, sin *glob* ni regex.

**Opción elegida.** Corregir solo la variable de Azure al formato correcto, con el dominio **exacto** de producción de Vercel (que es estable entre despliegues, no cambia con cada `git push`):

```bash
az webapp config appsettings set --resource-group evidence-chain-rg --name evidencechain-api \
  --settings "Cors__AllowedOrigins__0=https://evidence-chain-frontend.vercel.app"
```

No se tocó `Program.cs`: como el dominio de producción no cambia, `WithOrigins` con el valor exacto es suficiente y más simple que `SetIsOriginAllowed` con lógica de comodín (que sí habría hecho falta para aceptar además los *preview deployments* de Vercel, con subdominios aleatorios por PR — no necesario para esta entrega).

**Alternativa descartada.** `SetIsOriginAllowed(origin => origin.EndsWith(".vercel.app"))` para aceptar cualquier subdominio de Vercel. Más flexible (cubriría previews), pero también más permisivo de lo necesario y sin beneficio real cuando solo existe un dominio de producción fijo que consumir.

**Costo asumido.** Si en el futuro se prueban *preview deployments* de Vercel (subdominios distintos por rama/PR) contra esta misma API, habrá que añadir cada uno a mano (`Cors__AllowedOrigins__1`, `__2`…) o migrar a `SetIsOriginAllowed` con comprobación de sufijo.

**Señal de cambio.** Si se automatiza el despliegue de previews de Vercel contra este backend, cambiar a `SetIsOriginAllowed` con una comprobación de sufijo (`.EndsWith(".vercel.app")`) en vez de seguir añadiendo orígenes exactos uno por uno.

**Verificado.** Con `curl` simulando un preflight real (`OPTIONS /api/v1/evidence` con `Origin: https://evidence-chain-frontend.vercel.app` y `Access-Control-Request-Method/Headers`): `204` con `Access-Control-Allow-Origin` reflejando el origen exacto. La respuesta real (`GET` sin token) también lleva el header CORS — el `401 Unauthorized` que se veía era comportamiento esperado (sin `Authorization`, no relacionado con CORS), no un bug adicional.

**Nota aparte.** La URL de Vercel usada durante el troubleshooting inicial (con un sufijo aleatorio por-deployment, `evidence-chain-frontend-<hash>-evidence-chain.vercel.app`) está protegida por "Vercel Authentication" (redirige a `vercel.com/sso-api`) y no debe compartirse con los evaluadores. El dominio de **producción** (`evidence-chain-frontend.vercel.app`, sin sufijo) queda exento de esa protección y es público — es el que hay que usar.
