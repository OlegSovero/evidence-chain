# Progreso

Actualizar al terminar cada sesión de trabajo (humano o agente). Formato: `- [x]` hecho, `- [ ]` pendiente, `- [!]` bloqueado (con motivo).

## Estado actual

Fase: **Fases 2 y 3 completas en `main` (dominio, modelo, seeder determinista y API completa), salvo el despliegue en Azure. Falta el scaffolding del frontend (Fase 4).**
Entorno local verificado (2026-09-12): .NET SDK 10.0.302, Git 2.55, Node v24.21.0, npm 11.19.0, Docker 29.7.2 + Compose v5.5.1, todos funcionando.
Backend: 5 entidades, migración `InitialCreate` (trigger append-only), `Domain/Hashing`, `Domain/Transfers` (máquina de estados), `Domain/Anomalies` (regla con `TimeProvider`), seeder determinista (1.000 evidencias, 10.002 eventos, 12 usuarios, 3 casos demo), JWT demo (`POST /api/v1/auth/token`, `GET /api/v1/auth/users`), los 7 endpoints del contrato más `GET /custody-transfers?status=pending&mine=true` y `GET /custody-transfers/{id}`, filtro de idempotencia, ETag/If-Match, 409 con `currentState`. `database/schema.sql` y `database/README.md` al día. Tests en verde (unitarios + integración con Testcontainers). `openapi.yaml` exportado desde la API y verificado (11 rutas). User-secrets locales: `ConnectionStrings:Sql` (base `EvidenceChain`) y `Jwt:Key`.
Siguiente paso: scaffolding del frontend (Fase 4) y esqueleto en Azure.

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

- [ ] Cliente API con JWT y problem+json; tipos generados desde openapi.yaml
- [ ] Bandeja: filtros en URL, orden, paginación, estados de carga, vacío y error
- [ ] Detalle: línea de tiempo, anomalía resaltada, botón de verificar cadena
- [ ] Modal de transferencia accesible (teclado, Escape, retorno de foco)
- [ ] Aceptar/rechazar con actualización optimista, rollback y explicación del 409
- [ ] Tests: respuesta obsoleta y rollback ante 409
- [ ] Despliegue en Vercel consumiendo la API pública

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
