# Progreso

Actualizar al terminar cada sesión de trabajo (humano o agente). Formato: `- [x]` hecho, `- [ ]` pendiente, `- [!]` bloqueado (con motivo).

## Estado actual

Fase: **Fase 1 (scaffolding de backend) completa; falta el scaffolding del frontend**.
Entorno local verificado (2026-09-12): .NET SDK 10.0.302, Git 2.55, Node v24.21.0, npm 11.19.0, Docker 29.7.2 + Compose v5.5.1, todos funcionando.
Backend compila, los tests pasan y `GET /health` responde 200 contra SQL Server real en Docker. No se implementó todavía dominio, migraciones, hash ni endpoints de negocio (eso es Fase 2/3).
Siguiente paso: scaffolding del frontend (Vite + React + TS + React Router + TanStack Query), luego Fase 2 (dominio y datos).

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

- [ ] Entidades y migración inicial (Users, Evidence, CustodyEvents, CustodyTransfers, IdempotencyRecords)
- [ ] Trigger append-only sobre CustodyEvents
- [ ] `CanonicalEventSerializer`, `ChainHasher` y `ChainVerifier` con tests unitarios
- [ ] Seeder determinista: 1.000 evidencias y 10.000 eventos, con caso íntegro, evento alterado y transferencia vencida

## Fase 3 — API (sábado)

- [ ] JWT local con roles y endpoint de token para usuarios demo
- [ ] ProblemDetails global
- [ ] `GET /api/v1/evidence` (keyset, filtros, orden)
- [ ] `GET /api/v1/evidence/{id}`, `/chain`, `/chain/verify`
- [ ] Regla de anomalía (umbral configurable, severidad, mensaje)
- [ ] `POST /api/v1/custody-transfers` con Idempotency-Key
- [ ] `POST /accept` y `/reject` con If-Match y 409 con estado actual
- [ ] Tests de integración: cadena alterada, idempotencia, 409
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
