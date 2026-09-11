# Plan de arquitectura — Prueba EY "Evidence Chain"

Fuente de verdad: `context/enunciado.md`. Estado y siguientes pasos: `context/progreso.md`.

## 1. Stack y versiones

| Capa | Elección | Motivo |
|---|---|---|
| Backend | .NET 10 (LTS) + ASP.NET Core Minimal APIs + EF Core 10 | Cumple ".NET 8+". .NET 8 sale de soporte en nov-2026; .NET 10 es el LTS vigente. |
| BD | SQL Server 2022 en Docker (local) → Azure SQL Database (demo/prod) | Mismo motor; solo cambia la cadena de conexión. |
| Frontend | React 19 + TypeScript + Vite (SPA), React Router, TanStack Query | El enunciado pide SPA; Next.js añade SSR que no se necesita. |
| Tests back | xUnit + WebApplicationFactory + Testcontainers (SQL Server real) | rowversion, índices únicos e idempotencia solo se prueban de verdad contra SQL Server. |
| Tests front | Vitest + Testing Library + MSW | Simular respuestas lentas/obsoletas y 409. |
| Deploy | SPA en Vercel; API en Azure App Service (Linux B1) + Azure SQL (free offer) | Además suma el extra "despliegue en Azure". |

## 2. Arquitectura backend: monolito modular con vertical slices

Un solo proyecto API desplegable + un proyecto de tests. Organizado por funcionalidad, con el dominio puro (sin EF) separado para poder probarlo unitariamente.

Descartado: Clean Architecture de 4 proyectos + MediatR + AutoMapper. En 20–25 h la ceremonia (interfaces, mappers, handlers) consume tiempo que el evaluador no premia ("no buscamos un CRUD extenso"). MediatR y AutoMapper además pasaron a licencia comercial en 2025. Señal para cambiar: si aparece un segundo equipo/bounded context o un segundo punto de entrada (worker, cola), extraer Domain/Infrastructure a proyectos propios.

```text
backend/
  EvidenceChain.sln
  src/EvidenceChain.Api/
    Program.cs
    Domain/                      # C# puro, sin EF ni ASP.NET
      Evidence.cs  CustodyEvent.cs  CustodyTransfer.cs
      Hashing/CanonicalEventSerializer.cs  ChainHasher.cs  ChainVerifier.cs
      Transfers/TransferStateMachine.cs
      Anomalies/PendingTransferRule.cs
    Features/                    # un archivo por endpoint
      Auth/IssueToken.cs
      Evidence/ListEvidence.cs  GetEvidence.cs  KeysetCursor.cs
      Chain/GetChain.cs  VerifyChain.cs
      Transfers/RequestTransfer.cs  AcceptTransfer.cs  RejectTransfer.cs  ListMyPendingTransfers.cs
    Infrastructure/
      Persistence/AppDbContext.cs  Configurations/  Migrations/
      Idempotency/IdempotencyEndpointFilter.cs  IdempotencyStore.cs
      Http/ProblemDetailsSetup.cs  ETag.cs
    Seed/DeterministicSeeder.cs  # dotnet run -- seed
  tests/EvidenceChain.Tests/
    Unit/  (hash, verificador, máquina de estados, regla de anomalía)
    Integration/ (cadena alterada, idempotencia, 409)
```

Transversales: `AddProblemDetails()` + manejador de excepciones → todo 4xx/5xx en `application/problem+json`; `TimeProvider` inyectado (anomalía y tests deterministas); todo en UTC; OpenAPI nativo de ASP.NET Core exportado a `openapi.yaml` en build; `/health`; CORS configurable por entorno.

## 3. Modelo de datos (SQL Server)

La fuente de verdad serán las migraciones EF Core; este DDL es la referencia.

```sql
CREATE TABLE dbo.Users (
  Id          INT IDENTITY PRIMARY KEY,
  UserName    NVARCHAR(50)  NOT NULL UNIQUE,
  DisplayName NVARCHAR(150) NOT NULL,
  Role        NVARCHAR(20)  NOT NULL CHECK (Role IN ('Investigador','Custodio','Supervisor'))
);

CREATE TABLE dbo.Evidence (
  Id                    INT IDENTITY PRIMARY KEY,
  Code                  NVARCHAR(20)  NOT NULL UNIQUE,      -- EV-000001
  Description           NVARCHAR(500) NOT NULL,
  CurrentCustodianId    INT NOT NULL REFERENCES dbo.Users(Id),
  LastEventAtUtc        DATETIME2(3)  NOT NULL,             -- desnormalizado para la bandeja
  IntegrityStatus       TINYINT NOT NULL,                   -- 0 NoVerificada, 1 Integra, 2 Comprometida (caché de /verify)
  IntegrityCheckedAtUtc DATETIME2(3)  NULL,
  CreatedAtUtc          DATETIME2(3)  NOT NULL
);
CREATE INDEX IX_Evidence_Keyset ON dbo.Evidence (LastEventAtUtc DESC, Id DESC)
  INCLUDE (Code, Description, CurrentCustodianId, IntegrityStatus);
CREATE INDEX IX_Evidence_Custodian_Keyset ON dbo.Evidence (CurrentCustodianId, LastEventAtUtc DESC, Id DESC);

CREATE TABLE dbo.CustodyEvents (                           -- append-only
  Id              BIGINT IDENTITY PRIMARY KEY,
  EvidenceId      INT NOT NULL REFERENCES dbo.Evidence(Id),
  Sequence        INT NOT NULL,                             -- 1..n por evidencia = orden estable
  EventType       TINYINT NOT NULL,                         -- Registrada, TransferenciaSolicitada, Aceptada, Rechazada
  ActorUserId     INT NOT NULL REFERENCES dbo.Users(Id),
  FromCustodianId INT NULL,
  ToCustodianId   INT NULL,
  TransferId      UNIQUEIDENTIFIER NULL,
  Notes           NVARCHAR(500) NULL,
  OccurredAtUtc   DATETIME2(3) NOT NULL,
  PreviousHash    BINARY(32) NOT NULL,                      -- 32 bytes en cero para el génesis
  Hash            BINARY(32) NOT NULL,
  CONSTRAINT UX_CustodyEvents_Evidence_Seq UNIQUE (EvidenceId, Sequence)  -- impide bifurcar la cadena
);
GO
CREATE TRIGGER TR_CustodyEvents_AppendOnly ON dbo.CustodyEvents
INSTEAD OF UPDATE, DELETE AS BEGIN THROW 50001, N'CustodyEvents es append-only', 1; END;
GO

CREATE TABLE dbo.CustodyTransfers (
  Id                UNIQUEIDENTIFIER PRIMARY KEY,
  EvidenceId        INT NOT NULL REFERENCES dbo.Evidence(Id),
  FromCustodianId   INT NOT NULL REFERENCES dbo.Users(Id),
  ToCustodianId     INT NOT NULL REFERENCES dbo.Users(Id),
  RequestedByUserId INT NOT NULL REFERENCES dbo.Users(Id),
  Status            TINYINT NOT NULL,                       -- 0 Pendiente, 1 Aceptada, 2 Rechazada
  Reason            NVARCHAR(500) NOT NULL,
  RequestedAtUtc    DATETIME2(3) NOT NULL,
  RespondedAtUtc    DATETIME2(3) NULL,
  RespondedByUserId INT NULL REFERENCES dbo.Users(Id),
  ResponseNote      NVARCHAR(500) NULL,
  RowVersion        ROWVERSION NOT NULL,                    -- ETag / If-Match
  CONSTRAINT CK_Transfer_DistinctCustodians CHECK (FromCustodianId <> ToCustodianId)
);
CREATE UNIQUE INDEX UX_Transfer_OnePendingPerEvidence ON dbo.CustodyTransfers (EvidenceId) WHERE Status = 0;
CREATE INDEX IX_Transfer_PendingAge ON dbo.CustodyTransfers (Status, RequestedAtUtc) INCLUDE (EvidenceId, ToCustodianId);

CREATE TABLE dbo.IdempotencyRecords (
  UserId         INT NOT NULL,
  IdempotencyKey NVARCHAR(100) NOT NULL,
  RequestHash    BINARY(32) NOT NULL,                       -- misma key + otro body => 422
  StatusCode     INT NOT NULL,
  ResponseBody   NVARCHAR(MAX) NOT NULL,
  CreatedAtUtc   DATETIME2(3) NOT NULL,
  CONSTRAINT PK_Idempotency PRIMARY KEY (UserId, IdempotencyKey)
);
```

Notas: todas las FK con `ON DELETE NO ACTION` (varias FK a Users en la misma tabla). El seed desactiva el trigger solo para crear el "evento alterado", lo que demuestra por qué el hash es necesario además de los controles de BD.

## 4. Decisiones técnicas clave (borrador para decisions.md)

**Hash encadenado.** SHA-256 sobre JSON canónico UTF-8 con orden de campos fijo:
`{"v":1,"evidenceId":..,"seq":..,"type":..,"actor":..,"from":..,"to":..,"transferId":..,"notes":..,"occurredAtUtc":"yyyy-MM-ddTHH:mm:ss.fffZ","prev":"<hex>"}`.
JSON en lugar de texto con separadores para evitar ambigüedad si `notes` contiene el separador. Fechas truncadas a milisegundos antes de hashear y guardadas en `DATETIME2(3)` (si no, el viaje de ida y vuelta a SQL cambia los ticks y rompe el hash). Orden estable = `Sequence` con índice único. Verificación: recorrer por `Sequence`, validar secuencia contigua, `PreviousHash == hash anterior` y recomputar `Hash`; devolver el primer evento inválido con motivo (`HashMismatch`, `BrokenLink`, `SequenceGap`).
Límite honesto: un atacante con acceso de escritura total puede recalcular toda la cadena. Evolución en producción: HMAC con clave en Key Vault, anclar el hash de cabeza fuera de la BD o tablas Ledger de Azure SQL.

**Paginación.** Keyset por `(LastEventAtUtc, Id)` con cursor opaco base64. Estable ante inserciones concurrentes (en custodia llegan eventos todo el tiempo) y coste constante por página gracias al índice. Coste asumido: no se puede saltar a la página N. Búsqueda por texto con `LIKE '%x%'` no usa índice; aceptable con 1.000 filas, señal para cambiar: >100k filas o p95 > 300 ms → Full-Text Search.

**Máquina de estados de transferencia.** `Pendiente → Aceptada | Rechazada` (terminales). Solo una pendiente por evidencia (índice filtrado). Solicita: Investigador/Supervisor. Acepta/rechaza: solo el custodio destinatario. Cada transición añade un evento a la cadena y, al aceptar, cambia `CurrentCustodianId`, todo en una transacción. Transición inválida → 409 con estado actual.

**Concurrencia optimista.** `ROWVERSION` expuesto como ETag. `If-Match` obligatorio (sin él → 428). Versión distinta → `DbUpdateConcurrencyException` → 409 `problem+json` con `currentState` (status, version, respondedBy, respondedAt). Se usa 409 y no 412 porque lo exige el enunciado; se documenta.

**Idempotencia.** Filtro de endpoint sobre `POST /custody-transfers`. Clave con alcance por usuario. En la misma transacción se inserta el registro de idempotencia, la transferencia y el evento. Reintento con misma key y mismo body → se devuelve la respuesta guardada; mismo key con otro body → 422; carrera simultánea → la PK hace que la segunda espere y luego repita la respuesta guardada.

**Regla de anomalía.** Transferencia pendiente con antigüedad > `Anomalies:PendingTransferThreshold` (appsettings, p. ej. 48 h). Severidad: Media al superar el umbral, Alta al superar 2× el umbral. Se calcula al leer (no se persiste) con `TimeProvider`, con mensaje legible: "Transferencia a Lab Forense pendiente desde hace 73 h (umbral 48 h)".

## 5. Frontend

Vite react-ts desplegado en Vercel con `vercel.json` de rewrites a `index.html`, `VITE_API_URL` por entorno. Tipos generados desde `openapi.yaml` con `openapi-typescript`.

```text
frontend/src/
  api/          client.ts (fetch + JWT + problem+json), schema.d.ts (generado)
  auth/         selector de usuario demo por rol
  features/evidence/   InboxPage, useEvidenceFilters (URL <-> filtros), EvidenceTable, DetailPage, ChainTimeline, VerifyChainButton
  features/transfers/  TransferDialog, useRequestTransfer, PendingInboxPage, useRespondTransfer
  components/   LoadingState, EmptyState, ErrorState
```

Filtros en URL con `useSearchParams`; TanStack Query con la query key derivada de los filtros y `signal` pasado a `fetch` (cancelación y ninguna respuesta obsoleta pisa el filtro actual). Optimista con `onMutate` / `onError` (rollback) / `onSettled` (reconciliación con el servidor); ante 409 se muestra el estado real que devuelve el servidor. Modal accesible (Radix Dialog o `<dialog>` nativo): trampa de foco, Escape, retorno de foco al disparador.

## 6. Local ahora, Azure después (anticipándonos desde el día 1)

Local: `docker-compose.yml` con SQL Server 2022; secretos con `dotnet user-secrets`. Reglas para que migrar sea solo configuración: todo por variables de entorno, EF migrations como única fuente de esquema (script idempotente exportado a `database/`), nada de features que Azure SQL no tenga (SQL Agent, consultas cross-database), UTC, `/health`, CORS por configuración.
Azure (demo): App Service Linux B1 + Azure SQL Database (oferta gratuita, serverless). Desplegar un "hola mundo" conectado a la BD el sábado para descubrir temprano problemas de CORS/firewall. Antes de la presentación, "calentar" la BD serverless (auto-pausa) y la API. Precios a confirmar en la calculadora de Azure antes de escribir decisions.md.

## 7. Prioridades (orden de construcción)

1. Hash encadenado + verify + seed determinista con los 3 casos (íntegro, alterado, transferencia vencida). Es el corazón del dominio y de la demo.
2. Transferencias: máquina de estados + 409 + idempotencia (lo que más distingue a un senior).
3. Bandeja keyset con filtros + detalle con línea de tiempo y anomalía.
4. Front: filtros en URL, optimista con rollback, modal accesible, estados carga/vacío/error.
5. Pruebas mínimas del enunciado.
6. Despliegue Vercel + API pública.
7. `/docs` (5 archivos) + `openapi.yaml` + README.
Extras: ninguno hasta cerrar 1–7.

## 8. Cronograma

| Día | Horas | Objetivo |
|---|---|---|
| Viernes 11 | 3–4 | Repo monorepo, docker-compose, solución .NET, esquema + migración, hasher/verificador con tests unitarios, seeder. |
| Sábado 12 | 8–9 | Todos los endpoints, JWT/roles, problem+json, idempotencia, 409, tests de integración. Noche: API + Azure SQL desplegados. |
| Domingo 13 | 8–9 | Front completo (bandeja, detalle, verify, transferir, aceptar/rechazar), tests front, deploy Vercel integrado. |
| Lunes 14 | 4–5 | `/docs`, `openapi.yaml`, README, prueba de humo en producción, medición de la consulta principal, ensayo de 30 min. |

## 9. Registro de uso de IA

Ver `context/ai-log.md`.
