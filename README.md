# Evidence Chain

Aplicación forense de cadena de custodia de evidencia digital (prueba técnica EY). Contexto completo para agentes y humanos en [`AGENTS.md`](./AGENTS.md); decisiones de arquitectura en [`context/plan-arquitectura.md`](./context/plan-arquitectura.md); estado y próximos pasos en [`context/progreso.md`](./context/progreso.md).

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

La API nunca lee secretos desde `appsettings*.json`. En Development, ASP.NET Core carga automáticamente los *user secrets* del proyecto.

```bash
dotnet user-secrets init --project backend/src/EvidenceChain.Api

# Cadena de conexión a la SQL Server local (usa la misma contraseña que pusiste en .env)
dotnet user-secrets set "ConnectionStrings:Sql" "Server=localhost,1433;User Id=sa;Password=<tu-password>;TrustServerCertificate=True;Encrypt=True;" --project backend/src/EvidenceChain.Api

# Clave para firmar los JWT locales (pendiente de usar hasta la Fase 3 - Auth)
dotnet user-secrets set "Jwt:Key" "<una-clave-larga-y-aleatoria>" --project backend/src/EvidenceChain.Api
```

En Azure, estos mismos valores se configuran como variables de entorno de la App Service (o Key Vault referenciado), nunca en el repo.

## 3. Compilar y probar

```bash
dotnet build backend/EvidenceChain.sln
dotnet test backend/EvidenceChain.sln
```

El proyecto de tests levanta su propio SQL Server efímero con Testcontainers para las pruebas de integración (necesita Docker corriendo), independiente del contenedor de `docker compose`.

## 4. Ejecutar la API

```bash
dotnet run --project backend/src/EvidenceChain.Api
```

`GET /health` devuelve `200 {"status":"healthy"}` si puede conectarse a SQL Server, o `503` si no.

## Seed de datos (pendiente)

El seeder determinista (1.000 evidencias, 10.000 eventos, con un caso íntegro, uno alterado y una transferencia vencida) se agrega en la Fase 2. Cuando exista:

```bash
dotnet run --project backend/src/EvidenceChain.Api -- seed
```

## Frontend (pendiente)

El scaffolding de `frontend/` (Vite + React + TypeScript) se agrega en una tarea posterior de la Fase 1/4. Cuando exista:

```bash
cd frontend && npm install && npm run dev
cd frontend && npm test
```

## Estructura

Ver el mapa completo del repositorio en [`AGENTS.md`](./AGENTS.md#mapa-del-repositorio).
