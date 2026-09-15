# Overview

_Máximo una página._

## Qué se terminó y qué quedó fuera

**Terminado**, los dos recorridos obligatorios completos de React a SQL Server:

- **Revisar evidencia**: bandeja con filtros (texto/custodio/estado) en la URL, orden por fecha, paginación keyset en servidor; detalle con línea de tiempo, `GET /chain/verify` y anomalía resaltada; cadena de hash append-only (trigger `INSTEAD OF UPDATE, DELETE` + SHA-256 encadenado), con caso íntegro, evento alterado y transferencia vencida en el seed.
- **Transferir custodia**: máquina de estados explícita (`Pendiente → Aceptada | Rechazada`), concurrencia optimista con `ROWVERSION`/`If-Match` → 409 con `currentState`, `Idempotency-Key` en las escrituras, optimista con rollback en el frontend, modal accesible por teclado.
- Los 7 endpoints del contrato mínimo, JWT local con los 3 roles, `problem+json` en todos los 4xx/5xx, `openapi.yaml` exportado y verificado contra la implementación real.
- Backend desplegado en Azure App Service (Linux) conectado a Azure SQL real; frontend desplegado en Vercel consumiendo esa API pública (no datos simulados).
- Seed determinista: 1.000 evidencias, ~10.000 eventos, 12 usuarios, semilla fija.

**Quedó fuera** (explícitamente no pedido por el enunciado, o listado como extra): carga de archivos, rate limiting, i18n, una segunda regla de anomalía, IaC para Azure. Tampoco se implementó Application Insights/Key Vault en el despliegue real (sí se documentan como propuesta en `decisions.md` §4) — se priorizó cerrar los dos recorridos obligatorios dentro del tiempo disponible.

**Autenticación, deliberadamente simplificada** (el enunciado mismo lo pide así: "Autenticación simplificada... La autorización se valida en servidor"). El selector de usuario demo emite un JWT sin contraseña; no hay refresh token ni gestión de sesión más allá de expiración fija (480 min) y `sessionStorage` (se pierde al cerrar la pestaña, a propósito). Lo que sí es real: la autorización por rol (`Investigador`/`Custodio`/`Supervisor`) se valida en el servidor en cada endpoint, no solo en la UI. En producción el emisor del token sería un proveedor de identidad (Entra ID, Auth0) en vez de este endpoint de demo.

**Qué se decidió no probar, y por qué.** No se mide cobertura. Se testeó lo que el enunciado pide explícitamente (cadena alterada, idempotencia, conflicto de concurrencia en backend; respuesta obsoleta y rollback ante 409 en frontend) más los casos de error más probables de cada pieza nueva (403/428 en accept-reject, cursor de otro orden, colisión de `Idempotency-Key`). No se agregaron tests de UI end-to-end (Playwright/Cypress) ni de carga: con 20-25 h de alcance, priorizar tests unitarios/de integración rápidos (94 en backend, corren en ~20 s con Testcontainers reales) dio más señal por hora invertida que una suite E2E, que además hubiera dependido de tener el despliegue de Azure/Vercel listo desde antes.

## Cómo levantar el proyecto, ejecutar el seed y las pruebas

Instrucciones completas y verificadas en `README.md`. Resumen:

```bash
docker compose up -d                                              # SQL Server local
dotnet ef database update --project backend/src/EvidenceChain.Api # migración
dotnet run --project backend/src/EvidenceChain.Api -- seed        # 1.000 evidencias + eventos
dotnet test backend/EvidenceChain.sln                              # 94 tests (necesita Docker)
dotnet run --project backend/src/EvidenceChain.Api                 # API en :5059

cd frontend && npm install && npm run dev                          # SPA en :5173
cd frontend && npm test                                            # 2 tests (Vitest + MSW)
```

Producción real, sin datos simulados: backend en Azure App Service (`GET /health` valida conexión real a Azure SQL) y frontend en Vercel (`https://evidence-chain-frontend.vercel.app`) consumiéndolo.

## Medición de la consulta principal

`GET /api/v1/evidence` (bandeja con paginación keyset, índice `IX_Evidence_Keyset`) contra la API real en Azure, con el seed completo de 1.000 evidencias cargado en Azure SQL. 8 llamadas consecutivas (`pageSize=20`, primera excluida como *warm-up*), medidas de punta a punta desde un cliente externo — incluye la latencia de red real hacia Azure, no solo ejecución SQL:

```
Tiempos (ms): 319.6, 326.8, 318.4, 315.9, 315.8, 315.6, 314.9, 317.0
Promedio: 318 ms   ·   Máximo observado: 327 ms
```

La ejecución del `SELECT` en sí (cubierta por índice, `TOP(pageSize+1)`, sin *table scan*) es una fracción pequeña de ese total; el resto es *round-trip* HTTP/TLS hacia West US 3. No se aisló el tiempo puro de SQL Server con `SET STATISTICS TIME`/*Query Performance Insight* de Azure SQL — quedó fuera por tiempo, ver "qué se decidió no probar" arriba.
