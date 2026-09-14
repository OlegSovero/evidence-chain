# Progreso

Actualizar al terminar cada sesión de trabajo (humano o agente). Formato: `- [x]` hecho, `- [ ]` pendiente, `- [!]` bloqueado (con motivo).

## Estado actual

Fase: **Fases 2, 3 y 4 completas en `main` (dominio, modelo, seeder determinista, API y frontend), salvo el despliegue (Azure + Vercel).**
Entorno local verificado (2026-09-12): .NET SDK 10.0.302, Git 2.55, Node v24.21.0, npm 11.19.0, Docker 29.7.2 + Compose v5.5.1, todos funcionando.
Backend: 5 entidades, migración `InitialCreate` (trigger append-only), `Domain/Hashing`, `Domain/Transfers` (máquina de estados), `Domain/Anomalies` (regla con `TimeProvider`), seeder determinista (1.000 evidencias, 10.002 eventos, 12 usuarios, 3 casos demo), JWT demo (`POST /api/v1/auth/token`, `GET /api/v1/auth/users`), los 7 endpoints del contrato más `GET /custody-transfers?status=pending&mine=true` y `GET /custody-transfers/{id}`, filtro de idempotencia, ETag/If-Match, 409 con `currentState`. `database/schema.sql` y `database/README.md` al día. Tests en verde (unitarios + integración con Testcontainers). `openapi.yaml` exportado desde la API y verificado (11 rutas, 7 schemas de request/response propios todos con id único y forma correcta — ver bitácora 2026-09-14). User-secrets locales: `ConnectionStrings:Sql` (base `EvidenceChain`) y `Jwt:Key`.
Frontend (`frontend/`, Vite + React 19 + TS): cliente API con JWT y parseo de `problem+json`; tipos escritos a mano en `api/types.ts` (independiente del bug de `openapi.yaml`, ya corregido, así que no requiere cambios); selector de usuario demo; bandeja de evidencias con filtros en URL, debounce y paginación keyset; detalle con línea de tiempo, verify y anomalía resaltada; modal de transferencia accesible (`<dialog>` nativo) con Idempotency-Key estable por intento y optimista con rollback; bandeja del custodio con aceptar/rechazar (If-Match) y manejo distinto de 403/428/409. 2 tests (Vitest + Testing Library + MSW) en verde: búsqueda obsoleta no pisa el filtro actual, rollback+reconciliación ante 409 al aceptar. Build y lint limpios. Verificado a mano end-to-end contra la API real corriendo en local (auth, los 3 casos demo, ciclo completo de idempotencia 201→replay→422, ciclo completo de accept 428→409→200→409, CORS desde `localhost:5173`) — **no se pudo probar visualmente en un navegador real dentro de esta sesión** (sin herramienta de automatización de navegador disponible); falta ese passthrough humano antes de dar la UI por buena.
Siguiente paso: revisión visual en navegador por Oleg, despliegue (Azure + Vercel) y cierre de `/docs`.

## Fase 0 — Planificación y contexto

- [x] Análisis del enunciado
- [x] Plan de arquitectura (`context/plan-arquitectura.md`)
- [x] Contexto para agentes (AGENTS.md, CLAUDE.md, GEMINI.md, skill)
- [ ] Revisión del plan por Oleg
- [x] Repositorio GitHub creado: https://github.com/OlegSovero/evidence-chain (privado)
- [x] Primer commit con el contexto (rama `main`, pusheado por Oleg)

## Fase 1 — Scaffolding (viernes)

- [x] `.gitignore`, `README.md` base, `docker-compose.yml` (SQL Server 2022)
- [x] Solución .NET: `EvidenceChain.Api` y `EvidenceChain.Tests`
- [ ] Frontend Vite react-ts con React Router y TanStack Query
- [x] Carpeta `docs/` con los 5 archivos vacíos del enunciado

## Fase 2 — Dominio y datos (viernes)

- [x] Entidades y migración inicial (Users, Evidence, CustodyEvents, CustodyTransfers, IdempotencyRecords)
- [x] Trigger append-only sobre CustodyEvents
- [x] `CanonicalEventSerializer`, `ChainHasher` y `ChainVerifier` con tests unitarios (golden JSON + golden hash) y test de ida y vuelta en SQL Server
- [x] Seeder determinista: 1.000 evidencias y 10.000 eventos, con caso íntegro, evento alterado y transferencia vencida

## Fase 3 — API (sábado)

- [x] JWT local con roles y endpoint de token para usuarios demo (`Features/Auth`, políticas `TransferRequester` y `Supervisor`)
- [x] ProblemDetails global (`ApiProblems`, `UseExceptionHandler` + `UseStatusCodePages`; 401/403 también en `problem+json`)
- [x] `GET /api/v1/evidence` (keyset, filtros, orden; cursor opaco con dirección)
- [x] `GET /api/v1/evidence/{id}`, `/chain`, `/chain/verify` (verify actualiza `IntegrityStatus`/`IntegrityCheckedAtUtc`)
- [x] Regla de anomalía (umbral configurable, severidad, mensaje) — `PendingTransferRule`, calculada al leer
- [x] `POST /api/v1/custody-transfers` con Idempotency-Key (placeholder en la misma transacción; 422 si cambia el cuerpo; 2601/2627 → 409)
- [x] `POST /accept` y `/reject` con If-Match y 409 con estado actual (428 sin cabecera, 403 si no es el destinatario)
- [x] Tests de integración: cadena alterada, idempotencia, 409, 428, 403, keyset completo (88 tests en total)
- [x] `openapi.yaml` exportado y verificado contra los endpoints; `GET /custody-transfers?status=pending&mine=true` y `GET /custody-transfers/{id}` como apoyo a la UI
- [ ] Esqueleto desplegado en Azure (App Service + Azure SQL)

## Fase 4 — Frontend (domingo)

- [x] Cliente API con JWT y problem+json; tipos escritos a mano desde `Features/*.cs` (openapi-typescript descartado, ver `ai-log.md`)
- [x] Bandeja: filtros en URL, orden, paginación, estados de carga, vacío y error
- [x] Detalle: línea de tiempo, anomalía resaltada, botón de verificar cadena
- [x] Modal de transferencia accesible (`<dialog>` nativo: teclado, Escape, retorno de foco)
- [x] Aceptar/rechazar con actualización optimista, rollback y explicación del 409/428/403
- [x] Tests: respuesta obsoleta y rollback ante 409
- [ ] Despliegue en Vercel consumiendo la API pública
- [!] Revisión visual en un navegador real (bloqueado: sin herramienta de automatización de navegador en esta sesión; build+tests+curl end-to-end sí verificados)

## Fase 5 — Cierre (lunes)

- [ ] `docs/overview.md`, `decisions.md`, `code-map.md`, `ai-usage.md`, `ai-code-review.md`
- [ ] `openapi.yaml` exportado y verificado contra la implementación
- [ ] README: levantar, poblar, probar
- [ ] Medición de la consulta principal (captura para overview.md)
- [ ] Prueba de humo en producción y ensayo de la presentación

## Bitácora

| Fecha | Quién | Qué se hizo |
|---|---|---|
| 2026-09-11 | Oleg + Claude | Análisis del enunciado, plan de arquitectura y contexto para agentes |
| 2026-09-11 | Oleg + Claude | `.gitignore`, `git init`, primer commit y push a `main`; verificado el entorno local (.NET 10, Git, Node LTS, Docker); `.claude/settings.json` sin co-autoría de Claude en commits; convención de workflow (agente commitea, Oleg pushea) documentada en `AGENTS.md` |
| 2026-09-12 | Oleg + Claude | Fase 1 (backend): `docker-compose.yml` + `.env`/`.env.example` (SQL Server 2022, healthy); solución `backend/EvidenceChain.sln` (.sln clásico) con `EvidenceChain.Api` (Minimal APIs) y `EvidenceChain.Tests` (xUnit); estructura de carpetas del plan con `.gitkeep`; `Program.cs` transversal (ProblemDetails, OpenAPI, CORS por config, `AppDbContext` vacío, `GET /health` con `CanConnectAsync`); `ConnectionStrings:Sql` vía `dotnet user-secrets` (no en el repo); test de humo de `/health` con Testcontainers; `docs/` con los 5 archivos vacíos; `README.md`. Build sin warnings, tests en verde, `/health` verificado en vivo (200) contra el contenedor real. Frontend queda pendiente. |
| 2026-09-12 | Oleg + Claude | Fase 2: enums (`tinyint`), 5 entidades en `Domain/`, configuraciones EF (datetime2(3), binary(32), rowversion, índices keyset/filtrado, checks, Restrict), conversor UTC + truncado a ms, migración `InitialCreate` con trigger append-only vía `migrationBuilder.Sql`, `dotnet-ef` como herramienta local, `database/schema.sql`. `Domain/Hashing` (serializador canónico v1 con `Utf8JsonWriter`, SHA-256, verificador con `SequenceGap`/`BrokenLink`/`HashMismatch`). 27 tests: golden JSON y golden hash, casos de alteración, ida y vuelta en SQL Server con ticks sub-ms, trigger rechaza UPDATE/DELETE. Borrador de la decisión de hash en `docs/decisions.md` y fila en `code-map.md`. |
| 2026-09-12 | Oleg + Gemini | Fase 2: seeder determinista (semilla 42) e idempotente con 1.000 evidencias, 10.002 eventos de custodia y 12 usuarios. Tres casos demo: `EVD-DEMO-INTACT` (íntegra, 5 eventos), `EVD-DEMO-TAMPER` (evento 2 alterado con trigger append-only desactivado temporalmente) y `EVD-DEMO-ANOMALY` (transferencia pendiente > 96 h). Carga masiva ultrarrápida con `SqlBulkCopy` (< 2 s). Tests unitarios de determinismo, coherencia y casos especiales en `SeedTests.cs` (33 tests en verde). `database/schema.sql` actualizado y `database/README.md` generado. |
| 2026-09-13 | Oleg + Claude | Fase 3 (API, en `main` sin tocar `Seed/` ni `database/`): `TransferStateMachine` y `PendingTransferRule` (TimeProvider, Media/Alta, mensaje en español); `ETag`, `ApiProblems` (409 siempre con `currentState`), `SqlErrors`, `ChainAppender`; filtro `Idempotency-Key` por usuario con placeholder transaccional y replay (`Idempotent-Replayed`); JWT demo (`/auth/token`, `/auth/users`, claves `sub`/`name`/`role`); endpoints de evidencia (keyset con cursor opaco, detalle con anomalía, chain con hashes hex, verify que cachea `IntegrityStatus`) y transferencias (POST 201 + ETag, accept/reject con If-Match → 428/403/409, bandeja `mine=true`, `GET /{id}`). 88 tests (unitarios de dominio/ETag/cursor + integración: cadena alterada, idempotencia, carrera de If-Match, 428, 403, keyset). `openapi.yaml` exportado de `/openapi/v1.yaml`; humo contra el seed local: `EVD-DEMO-TAMPER` inválida en seq 2, `EVD-DEMO-ANOMALY` con anomalía Alta. `decisions.md` §2 y §3, `code-map.md` (4 filas), README y `.http`. |
| 2026-09-13 | Oleg + Claude | Fase 4 (frontend, en `main`, partiendo del merge de `feat/deterministic-seed` que ya traía la API completa): scaffolding Vite + React 19 + TS con react-router-dom y TanStack Query; `openapi-typescript` probado y descartado por un bug de `openapi.yaml` (schema `Response` colisionado en 4 endpoints, ver `ai-log.md`) — tipos a mano en `api/types.ts`; cliente `fetch` con JWT, `AbortSignal` y `ApiError` tipado desde `problem+json`; `AuthProvider`/`UserSwitcher` (token por `sessionStorage`, `queryClient.clear()` al cambiar de usuario); bandeja de evidencias (`useEvidenceFilters` con `useSearchParams` y debounce, paginación keyset con pila de cursores en cliente, estados de carga/vacío/error); detalle con `ChainTimeline`, `VerifyChainButton` (resalta el evento roto) y aviso de anomalía; `TransferDialog` con `<dialog>` nativo (foco atrapado, Escape, retorno de foco), Idempotency-Key estable por intento y optimista con rollback; `PendingInboxPage` (aceptar/rechazar con If-Match, optimista, mensajes distintos para 403/428/409). 2 tests (Vitest + Testing Library + MSW): búsqueda obsoleta no reemplaza el filtro actual, rollback+reconciliación ante 409. Build, lint y tests en verde. Verificado a mano con `curl` end-to-end contra la API real (auth, 3 casos demo, idempotencia 201→replay→422, accept 428→409→200→409, CORS); **sin revisión visual en navegador** (no había herramienta de automatización de navegador disponible en esta sesión). README (sección "Frontend"), `code-map.md` (2 filas) y `ai-log.md` al día. |
| 2026-09-14 | Oleg + Claude | Corrección del bug de `openapi.yaml`: `AddOpenApi` ahora configura `CreateSchemaReferenceId` para prefijar el id de schema de los tipos anidados con el nombre de su clase contenedora (`IssueTokenResponse`, `GetChainResponse`, `VerifyChainResponse`, `ListDemoUsersResponse`, `ListMyPendingTransfersResponse`, `IssueTokenRequest`, `RequestTransferRequest`…), sin renombrar ningún record de C#. De paso se encontró y corrigió una segunda colisión no documentada (`IssueToken.Request` vs `RequestTransfer.Request`, que hacía que el cuerpo de `POST /custody-transfers` en el yaml mostrara `{userName}` en vez de `{evidenceId, toCustodianId, reason}`). `openapi.yaml` regenerado y las 11 operaciones verificadas a mano contra la API real con el seed cargado (incluye `EVD-DEMO-TAMPER` → `HashMismatch` en seq 2). Build y 94 tests en verde. Un solo archivo tocado (`Program.cs`); `frontend/`, `Seed/` y `database/` sin cambios. |
