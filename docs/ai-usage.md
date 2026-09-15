# AI Usage

_Máximo una página. Detalle completo en `context/ai-log.md`._

## Herramientas usadas y en qué partes

| Herramienta | Partes del proyecto |
|---|---|
| Claude (Cowork / Claude Code) | Análisis del enunciado, plan de arquitectura, contexto para agentes (`AGENTS.md`, skill de proyecto) |
| Claude Code | Backend completo: dominio (hash, máquina de estados, regla de anomalía), modelo EF y migración, los 7 endpoints del contrato + JWT/idempotencia/concurrencia, tests unitarios y de integración, corrección de `openapi.yaml`, despliegue y troubleshooting en Azure App Service |
| Gemini (Antigravity) | Seeder determinista e idempotente (`SqlBulkCopy`, reasentamiento de `IDENTITY`), tests de seed, documentación de base de datos |
| Claude Code | Scaffolding y features del frontend (Vite + React 19 + TS): cliente API, autenticación demo, bandeja con filtros en URL y keyset, detalle con línea de tiempo y verify, modal de transferencia accesible, optimista/rollback, tests con MSW |

Dos agentes trabajaron en ramas separadas (`main` para backend/API, `feat/deterministic-seed` para el seeder) y se integraron por merge; el registro completo, con fecha y autor de cada sugerencia, está en `context/ai-log.md`.

## Una sugerencia aceptada y por qué

Al detectar que `openapi.yaml` tenía 5 schemas colisionados (varios `record Response`/`Request` anidados en distintos archivos de `Features/` recibían el mismo id corto del generador de OpenAPI de ASP.NET Core), se evaluaron dos caminos: renombrar los cinco records, o configurar `AddOpenApi(options => options.CreateSchemaReferenceId = ...)` para prefijar el id con el nombre de la clase contenedora. Se aceptó la segunda opción: un único archivo tocado (`Program.cs`) frente a modificar al menos 7-8 archivos si se renombraba (los records, los `.Produces<T>()` de cada grupo de endpoints, y los tests de integración que deserializan por ese tipo), y es una corrección **sistémica** — como la convención del repo es un `Request`/`Response` anidado por endpoint, sin este fix cualquier endpoint nuevo puede volver a colisionar. De paso, la misma corrección destapó una segunda colisión no documentada (`IssueToken.Request` vs `RequestTransfer.Request`) que el reporte original no mencionaba.

## Una sugerencia rechazada o corregida, por qué era inadecuada y qué se hizo en su lugar

La instrucción original de la Fase 4 pedía generar los tipos del cliente frontend con `openapi-typescript` a partir de `openapi.yaml`, como es práctica habitual. Se rechazó: en ese momento `openapi.yaml` tenía el bug de schemas colisionados descrito arriba (`ListDemoUsers`, `GetChain`, `VerifyChain` y `ListMyPendingTransfers` compartían por error el schema `Response` de `IssueToken`), y además tipaba todos los IDs de ruta como `number | string` por cómo interpreta los parámetros enteros. Generar los tipos tal cual habría dejado 4 endpoints del frontend mal tipados **sin que el compilador lo detectara** — el error se habría descubierto recién en tiempo de ejecución, contra datos reales. En su lugar, se escribieron los tipos a mano en `frontend/src/api/types.ts` a partir del código real de `Features/*.cs`, verificados campo por campo contra la API real con `curl` (auth, detalle con anomalía, verify con hash roto, ciclo de idempotencia 201/replay/422, ciclo de accept 428/409/200/409). El bug de `openapi.yaml` se corrigió después en el backend (ver arriba); los tipos a mano del frontend no dependían de esa corrección y no hubo que tocarlos.

## Una parte revisada especialmente antes de aceptar código generado

El despliegue a Azure App Service: no se dio por bueno con "el comando de deploy terminó sin error". `az webapp deployment source config-zip` marcó éxito, pero antes de reportarlo como resuelto se verificó independientemente: `status: 4` (Success) en `az webapp log deployment list`, los logs de arranque del contenedor mostrando "Application started" (no solo "Container created"), y una llamada real a `GET /health` contra la URL pública confirmando **conexión efectiva a Azure SQL** — no solo que el proceso hubiera arrancado. La misma disciplina se aplicó al corregir CORS entre Vercel y Azure: en vez de asumir que cambiar la variable de entorno bastaba, se simuló con `curl` un *preflight* real (`OPTIONS` con `Origin` y `Access-Control-Request-*`) y se confirmó que tanto esa respuesta como la petición real llevaran `Access-Control-Allow-Origin` correcto, antes de declarar el bug cerrado.
