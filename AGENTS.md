# AGENTS.md — Evidence Chain (prueba técnica EY)

Este archivo es el contexto común para cualquier agente de IA (Claude Code, Gemini CLI, Codex, Cursor…) que trabaje en este repositorio. `CLAUDE.md` y `GEMINI.md` solo lo importan. Si cambias una regla, cámbiala aquí.

## Qué es el proyecto

Aplicación forense de **cadena de custodia de evidencia digital**. Demuestra que una evidencia no fue alterada (eventos append-only encadenados por hash) ni transferida indebidamente (transferencias con máquina de estados, concurrencia optimista e idempotencia).

Es una prueba técnica para el puesto de Desarrollador Senior Full Stack. Se evalúa el criterio técnico, no el volumen: dos recorridos completos de React a SQL Server, bien resueltos y bien justificados.

## Fuente de verdad (leer antes de trabajar)

Si hay conflicto, gana el primer archivo de esta lista.

1. `context/enunciado.md`: el enunciado oficial. **Nunca lo modifiques.** Si una tarea lo contradice, avisa antes de implementarla.
2. `context/plan-arquitectura.md`: las decisiones acordadas (stack, esquema, hash, paginación, concurrencia, cronograma).
3. `context/progreso.md`: qué está hecho, qué sigue y qué está bloqueado. Léelo al empezar y actualízalo al terminar.
4. `context/ai-log.md`: el registro de sugerencias de IA aceptadas y rechazadas, que alimenta `docs/ai-usage.md`.

## Stack

- Backend: .NET 10 (LTS), ASP.NET Core Minimal APIs, EF Core, SQL Server 2022 (Docker en local, Azure SQL en la demo).
- Frontend: React 19, TypeScript y Vite (SPA) con React Router y TanStack Query. Se despliega en Vercel.
- Tests: xUnit + Testcontainers (SQL Server real) en el backend; Vitest + Testing Library + MSW en el frontend.
- Repositorio: monorepo en GitLab.

## Mapa del repositorio

```text
AGENTS.md  CLAUDE.md  GEMINI.md      contexto para agentes
.claude/skills/ey-evidence-chain/     skill de proyecto para Claude Code
context/                              enunciado, plan, progreso, registro de IA (no es entregable)
backend/src/EvidenceChain.Api/
  Domain/          C# puro: hash, verificación, máquina de estados, regla de anomalía (sin EF ni ASP.NET)
  Features/        un archivo por endpoint: Auth, Evidence, Chain, Transfers
  Infrastructure/  EF Core, migraciones, idempotencia, problem+json, ETag
  Seed/            seeder determinista (1.000 evidencias, 10.000 eventos)
backend/tests/EvidenceChain.Tests/    Unit/ e Integration/
frontend/src/      api/ auth/ features/evidence/ features/transfers/ components/
database/          script SQL idempotente exportado de las migraciones y notas del seed
docs/              ENTREGABLE: overview, decisions, code-map, ai-usage, ai-code-review
openapi.yaml       contrato generado; debe coincidir con la implementación
docker-compose.yml SQL Server local
```

> Estado actual: solo existe el contexto. Las carpetas de código se crean en la fase de scaffolding (ver `context/progreso.md`).

## Comandos

Hay que completarlos cuando exista el scaffolding. No inventes comandos que no estén aquí o en el README.

```bash
docker compose up -d                                   # SQL Server local
dotnet build backend/EvidenceChain.sln
dotnet test backend/EvidenceChain.sln
dotnet run --project backend/src/EvidenceChain.Api -- seed
cd frontend && npm install && npm run dev
cd frontend && npm test
```

## Reglas no negociables

- `CustodyEvents` es append-only. Nunca se generan UPDATE ni DELETE sobre eventos; una corrección es un evento nuevo.
- Hash: SHA-256 sobre JSON canónico UTF-8 con orden de campos fijo y versión `v`. Las fechas se truncan a milisegundos y se guardan en `DATETIME2(3)`. El génesis usa 32 bytes en cero. El orden de la cadena lo da `Sequence`. **No cambies el formato canónico sin actualizar `decisions.md` y regenerar el seed.**
- Todo en UTC y `TimeProvider` inyectado. Nunca uses `DateTime.Now`.
- Una escritura que añade evento, cambia custodio y actualiza transferencia va en **una sola transacción**.
- Concurrencia: `ROWVERSION` como ETag. Sin `If-Match` se responde 428; si la versión no coincide o la transición es inválida, 409 con `currentState`.
- Idempotencia: `Idempotency-Key` con alcance por usuario. La misma key con otro body da 422.
- Errores 4xx/5xx siempre en `application/problem+json`.
- La autorización se valida en el servidor con los roles `Investigador`, `Custodio` y `Supervisor`.
- SQL siempre parametrizado (EF o `FromSql` interpolado); nunca `FromSqlRaw` con concatenación.
- Nada de secretos en el repo: `dotnet user-secrets` en local y variables de entorno o Key Vault en Azure.
- Nada que Azure SQL no soporte (SQL Agent, consultas cross-database).
- No se agregan extras (subida de archivos, rate limiting, i18n, segunda regla de anomalía) hasta completar el alcance obligatorio.
- No se introducen MediatR, AutoMapper ni capas o proyectos nuevos sin actualizar el plan.

## Convenciones

- Código, identificadores y commits en inglés. Documentación y textos de UI en español.
- Commits pequeños con Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`, `chore:`), porque el historial Git es entregable.
- Un endpoint = un archivo en `Features/`. La lógica de negocio va en `Domain/` y se prueba unitariamente.
- Frontend: el estado de los filtros vive en la URL; las llamadas pasan `AbortSignal`; las mutaciones optimistas siempre tienen rollback.

## Flujo de trabajo para agentes

1. Lee `context/progreso.md` y toma la siguiente tarea en orden de prioridad.
2. Relee la sección de `context/enunciado.md` que cubre esa tarea.
3. Implementa el mínimo que cumple el enunciado, con su prueba si figura en "Pruebas mínimas".
4. Ejecuta build y tests antes de dar algo por terminado.
5. Actualiza `context/progreso.md` y, si corresponde, la fila de `docs/code-map.md`.
6. Si una sugerencia de IA fue aceptada o rechazada de forma relevante, anótala en `context/ai-log.md`.
7. Pregunta al humano antes de cambiar una decisión del plan, borrar archivos o tocar el enunciado.

## Prioridades

1. Hash encadenado, verify y seed con los 3 casos (íntegro, alterado, transferencia vencida).
2. Transferencias: máquina de estados, 409 e idempotencia.
3. Bandeja con keyset y filtros, y detalle con línea de tiempo y anomalía.
4. Frontend: filtros en la URL, actualización optimista con rollback, modal accesible, estados de carga, vacío y error.
5. Pruebas mínimas.
6. Despliegue: Vercel más la API pública.
7. `docs/`, `openapi.yaml` y README.
