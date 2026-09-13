# Evidence Chain

Aplicación forense de cadena de custodia de evidencia digital (prueba técnica EY). Contexto completo para agentes y humanos en [`AGENTS.md`](./AGENTS.md); decisiones de arquitectura en [`context/plan-arquitectura.md`](./context/plan-arquitectura.md) y [`docs/decisions.md`](./docs/decisions.md); estado y próximos pasos en [`context/progreso.md`](./context/progreso.md).

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (con WSL2 en Windows)
- [Node.js LTS](https://nodejs.org/) y npm (para el frontend, ver más abajo)
- Git

## 1. Levantar SQL Server (Docker)

```bash
cp .env.example .env   # y ajusta MSSQL_SA_PASSWORD si quieres
docker compose up -d
docker compose ps      # espera a que el contenedor quede "healthy"
```

La base corre en `localhost,1433`. El volumen `evidence-chain-sql-data` persiste los datos entre reinicios.

## 2. Configurar secretos locales (`dotnet user-secrets`)

La API nunca lee secretos desde `appsettings*.json`. En Development, ASP.NET Core carga automáticamente los *user secrets* del proyecto; en Azure los mismos valores van como configuración de la App Service o referencias a Key Vault.

```bash
dotnet user-secrets init --project backend/src/EvidenceChain.Api

# Cadena de conexión a la SQL Server local (misma contraseña que en .env)
dotnet user-secrets set "ConnectionStrings:Sql" "Server=localhost,1433;Database=EvidenceChain;User Id=sa;Password=<tu-password>;TrustServerCertificate=True;Encrypt=True;" --project backend/src/EvidenceChain.Api

# Clave HMAC para firmar los JWT (mínimo 32 caracteres; la API no arranca sin ella)
dotnet user-secrets set "Jwt:Key" "<una-clave-larga-y-aleatoria-de-al-menos-32-caracteres>" --project backend/src/EvidenceChain.Api
```

## 3. Crear la base de datos (migraciones EF Core)

```bash
dotnet tool restore                                               # instala dotnet-ef desde el manifiesto local del repo
dotnet ef database update --project backend/src/EvidenceChain.Api
```

Crea la base `EvidenceChain` con tablas, índices y el trigger append-only de `CustodyEvents`. Alternativa sin EF: ejecutar `database/schema.sql` (script idempotente exportado de las migraciones).

## 4. Compilar y probar

```bash
dotnet build backend/EvidenceChain.sln
dotnet test backend/EvidenceChain.sln
```

Los tests unitarios (`tests/Unit/`) no necesitan base. Los de integración (`tests/Integration/`) levantan SQL Server efímeros con Testcontainers (necesitan Docker corriendo), independientes del contenedor de `docker compose`. Cubren, entre otros: cadena alterada detectada por `verify`, idempotencia (misma clave → una sola transferencia y misma respuesta), conflicto de concurrencia (409 con `currentState`), 428 sin `If-Match`, 403 por rol o destinatario equivocado y recorrido keyset completo.

## 5. Ejecutar la API

```bash
dotnet run --project backend/src/EvidenceChain.Api
```

Escucha en `http://localhost:5059`. `GET /health` devuelve `200 {"status":"healthy"}` si conecta con la base, o `503` si no. En Development el contrato OpenAPI está en `/openapi/v1.json` y `/openapi/v1.yaml`.

### Autenticación (simplificada para la demo)

No hay contraseñas: `POST /api/v1/auth/token` con `{ "userName": "c.rivas" }` devuelve un JWT firmado con `Jwt:Key` y claims de usuario y rol (`Investigador`, `Custodio`, `Supervisor`). `GET /api/v1/auth/users` lista los usuarios demo. Todos los demás endpoints exigen `Authorization: Bearer <token>`; la autorización por rol y por destinatario se valida en el servidor.

### Endpoints

| Verbo | Ruta | Uso |
|---|---|---|
| `GET` | `/api/v1/evidence?q&custodianId&status&sort&cursor&pageSize` | Bandeja con filtros, orden por fecha y paginación keyset (`nextCursor`) |
| `GET` | `/api/v1/evidence/{id}` | Detalle con transferencia pendiente y anomalía |
| `GET` | `/api/v1/evidence/{id}/chain` | Línea de tiempo con hashes y marca de anomalía |
| `GET` | `/api/v1/evidence/{id}/chain/verify` | Recalcula la cadena; primer evento inválido si falla |
| `POST` | `/api/v1/custody-transfers` | Solicita transferencia (Investigador/Supervisor). Requiere `Idempotency-Key`; devuelve 201 + `ETag` |
| `POST` | `/api/v1/custody-transfers/{id}/accept` | Acepta (solo el custodio destinatario). Requiere `If-Match`; 428 sin ella, 409 con `currentState` si la versión no coincide |
| `POST` | `/api/v1/custody-transfers/{id}/reject` | Rechaza. Mismas reglas que `accept`; la custodia no cambia |
| `GET` | `/api/v1/custody-transfers?status=pending&mine=true` | Bandeja del custodio destinatario |
| `GET` | `/api/v1/custody-transfers/{id}` | Estado actual de una transferencia (con `ETag`) |

Todos los errores 4xx/5xx responden `application/problem+json`; el 409 incluye `currentState` con `status`, `version`, `respondedBy` y `respondedAtUtc`.

### Probar a mano

- `backend/src/EvidenceChain.Api/EvidenceChain.Api.http`: peticiones listas para la extensión REST Client de VS Code (o Visual Studio/Rider).
- Postman/Insomnia: importa `openapi.yaml` (o la URL `http://localhost:5059/openapi/v1.json` con la API en marcha).

### Regenerar `openapi.yaml`

Con la API en marcha en Development:

```powershell
Invoke-WebRequest http://localhost:5059/openapi/v1.yaml -OutFile openapi.yaml
```

## Seed de datos

El seeder determinista (1.000 evidencias, 10.000 eventos, con un caso íntegro, uno alterado y una transferencia vencida) se integra desde la rama `feat/deterministic-seed`. Cuando esté en `main`:

```bash
dotnet run --project backend/src/EvidenceChain.Api -- seed
```

## Frontend (pendiente)

El scaffolding de `frontend/` (Vite + React + TypeScript) se agrega en una tarea posterior. Cuando exista:

```bash
cd frontend && npm install && npm run dev
cd frontend && npm test
```

## Estructura

Ver el mapa completo del repositorio en [`AGENTS.md`](./AGENTS.md#mapa-del-repositorio) y las capacidades en [`docs/code-map.md`](./docs/code-map.md).
