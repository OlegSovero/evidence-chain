# Code Map

_Máximo una página._

| Capacidad | Archivo / módulo | Punto de entrada |
|---|---|---|
| Encadenado y verificación de hash | `backend/src/EvidenceChain.Api/Domain/Hashing/`: `CanonicalEventSerializer.cs` (JSON canónico v1), `ChainHasher.cs` (SHA-256, génesis), `ChainVerifier.cs` (recorrido por `Sequence`). Trigger append-only en `Infrastructure/Persistence/Migrations/…_InitialCreate.cs` | `ChainHasher.ComputeHash(evento)` al escribir; `ChainVerifier.Verify(eventos)` al leer, expuesto por `GET /api/v1/evidence/{id}/chain/verify` (`Features/Chain/VerifyChain.cs`, pendiente Fase 3) |
| Máquina de estados y concurrencia | | |
| Idempotencia | | |
| Regla de anomalía | | |
| Consulta paginada | | |
| Filtros de URL y cancelación de solicitudes | | |
| Actualización optimista y manejo de 409 | | |
