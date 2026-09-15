# Base de datos y Seed de Datos — Evidence Chain

Esta carpeta contiene el script SQL idempotente de la base de datos y la documentación para inicializar y poblar el entorno de desarrollo y demostración.

## 1. Levantar SQL Server en Local

SQL Server 2022 corre en contenedor Docker según se define en `docker-compose.yml`:

```bash
docker compose up -d
```

Verifica que el contenedor esté saludable (`healthy`):

```bash
docker compose ps
```

La cadena de conexión para desarrollo local se gestiona vía `dotnet user-secrets`:

```bash
dotnet user-secrets set "ConnectionStrings:Sql" "Server=localhost,1433;Database=EvidenceChain;User Id=sa;Password=<tu-password>;TrustServerCertificate=True;Encrypt=True;" --project backend/src/EvidenceChain.Api
```

---

## 2. Aplicar Migraciones de Esquema

El esquema completo incluye las tablas `Users`, `Evidence`, `CustodyTransfers`, `CustodyEvents`, `IdempotencyRecords`, índices keyset/filtrados, restricciones de chequeo y el trigger append-only `TR_CustodyEvents_AppendOnly`.

### Opción A (Recomendada con EF Core Tools):
```bash
dotnet ef database update --project backend/src/EvidenceChain.Api
```

### Opción B (Vía script SQL directo):
Puedes ejecutar `database/schema.sql` contra tu instancia de SQL Server (es un script idempotente generado con `dotnet ef migrations script --idempotent`).

Para regenerar el script tras cualquier cambio de migración:
```bash
dotnet ef migrations script --idempotent --project backend/src/EvidenceChain.Api -o database/schema.sql
```

---

## 3. Ejecutar el Seed Determinista

El seeder es determinista (`Random(42)`), idempotente (limpia datos existentes antes de insertar) y de alto rendimiento (utiliza `SqlBulkCopy` para insertar más de 10.000 registros en menos de 3 segundos):

```bash
dotnet run --project backend/src/EvidenceChain.Api -- seed
```

### Volumen generado:
- **12 Usuarios** distribuidos entre `Investigador` (4), `Custodio` (5) y `Supervisor` (3).
- **1.000 Evidencias** (`EVD-DEMO-INTACT`, `EVD-DEMO-TAMPER`, `EVD-DEMO-ANOMALY`, `EV-000004` .. `EV-001000`).
- **4.517 Transferencias de custodia** (con estados Aceptada, Rechazada y Pendiente).
- **10.002 Eventos de custodia** firmados criptográficamente con SHA-256 canónico.

---

## 4. Casos Especiales para la Demostración

Para la evaluación técnica y la demo en vivo, se crearon 3 evidencias con identificadores y códigos nemotécnicos en los IDs 1, 2 y 3:

| Id | Código | Descripción | Estado Integridad | Propósito de la Demo |
|:---:|:---|:---|:---:|:---|
| **1** | `EVD-DEMO-INTACT` | Teléfono Samsung Galaxy S23 | **Íntegra** (`1`) | **Cadena válida multievento:** Contiene 5 eventos secuenciales con transferencias aceptadas y rechazadas. `GET /api/v1/evidence/1/chain/verify` devuelve `isValid: true`. |
| **2** | `EVD-DEMO-TAMPER` | Disco duro externo Seagate 2TB | **Comprometida** (`2`) | **Detección de alteración:** La cadena fue insertada íntegra y luego se alteró `Notes` en el evento de Secuencia 2 con el trigger temporalmente desactivado (simulando un ataque malicioso directo a la BD). `GET /api/v1/evidence/2/chain/verify` detecta `HashMismatch` en Secuencia 2 (`isValid: false`). |
| **3** | `EVD-DEMO-ANOMALY` | Servidor blade HP ProLiant | **Íntegra** (`1`) | **Regla de anomalía vencida:** Posee una transferencia en estado `Pendiente` solicitada hace >96 horas, superando ampliamente el umbral configurado de 48 horas (severidad Alta). Permite probar la regla de anomalía en la bandeja y detalle. |

---

## 5. Usuarios Demo Principales

Para autenticación y pruebas de roles:

- `c.rivas` (Id 1, Investigador): Carla Rivas
- `a.torres` (Id 2, Investigador): Alejandro Torres
- `lab.forense` (Id 5, Custodio): Laboratorio Forense Central
- `m.quispe` (Id 6, Custodio): Marco Quispe
- `boveda.central` (Id 7, Custodio): Bóveda Central de Evidencias
- `s.valdez` (Id 10, Supervisor): Sofía Valdez
