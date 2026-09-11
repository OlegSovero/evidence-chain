# Progreso

Actualizar al terminar cada sesión de trabajo (humano o agente). Formato: `- [x]` hecho, `- [ ]` pendiente, `- [!]` bloqueado (con motivo).

## Estado actual

Fase: **planificación terminada, entorno confirmado, pendiente de scaffolding**.
Entorno local verificado (2026-09-11): .NET SDK 10.0.302, Git 2.55, Node v24.21.0, npm 11.19.0, Docker 29.7.2 + Compose v5.5.1 instalados. Docker Desktop pendiente de reinicio de Windows para terminar la instalación (el daemon no corre todavía).
Siguiente paso: confirmar con Oleg antes de crear el scaffolding de la Fase 1.

## Fase 0 — Planificación y contexto

- [x] Análisis del enunciado
- [x] Plan de arquitectura (`context/plan-arquitectura.md`)
- [x] Contexto para agentes (AGENTS.md, CLAUDE.md, GEMINI.md, skill)
- [ ] Revisión del plan por Oleg
- [x] Repositorio GitHub creado: https://github.com/OlegSovero/evidence-chain (privado)
- [x] Primer commit con el contexto (rama `main`, pusheado por Oleg)

## Fase 1 — Scaffolding (viernes)

- [ ] `.gitignore`, `README.md` base, `docker-compose.yml` (SQL Server 2022)
- [ ] Solución .NET: `EvidenceChain.Api` y `EvidenceChain.Tests`
- [ ] Frontend Vite react-ts con React Router y TanStack Query
- [ ] Carpeta `docs/` con los 5 archivos vacíos del enunciado

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
