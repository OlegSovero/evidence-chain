# Registro de uso de IA

Materia prima de `docs/ai-usage.md`. El enunciado exige al menos una sugerencia de IA **rechazada o corregida**, con el motivo y lo que se hizo en su lugar. Anota los casos a medida que ocurran; no los reconstruyas al final.

## Herramientas

| Herramienta | Uso |
|---|---|
| Claude (Cowork / Claude Code) | Análisis del enunciado, plan de arquitectura, contexto para agentes |
| Claude Code | Scaffolding del backend, modelo EF y migración, dominio de hash, tests unitarios y de integración |
| Gemini (Antigravity) | Seeder determinista e idempotente, optimización con SqlBulkCopy, tests unitarios de seed y documentación de base de datos |

## Sugerencias aceptadas

| Fecha | Herramienta | Sugerencia | Por qué se aceptó |
|---|---|---|---|
| 2026-09-12 | Claude Code | En el JSON canónico, `type` va como valor numérico del enum y los campos ausentes como `null` explícito (nunca omitidos) | Renombrar un miembro del enum o añadir un campo opcional no puede cambiar hashes ya guardados; el formato queda fijado por dos tests *golden* |
| 2026-09-12 | Claude Code | Conversor EF que trunca a milisegundos **al escribir** además de truncar antes de firmar | `datetime2(3)` redondea (.0009999 ms → .001); sin el truncado en escritura el hash firmado en memoria no coincidía con el evento leído. Verificado con un test de ida y vuelta en SQL Server |
| 2026-09-12 | Claude Code | Declarar el trigger en el modelo EF (`HasTrigger`) | SQL Server rechaza el `OUTPUT` que EF usa en los INSERT cuando la tabla tiene triggers; con la declaración EF cambia de estrategia |
| 2026-09-12 | Claude Code | `dotnet-ef` como herramienta local (`dotnet-tools.json`) en vez de global | Versión fijada en el repo y reproducible con `dotnet tool restore` |
| 2026-09-12 | Gemini | Inserción en bulto con `SqlBulkCopy` tipado en `DataTable`s en memoria y reasentamiento de secuencias con `DBCC CHECKIDENT` | Carga 1.000 evidencias y más de 10.000 eventos en menos de 2 segundos (frente a minutos con EF Core fila por fila), reasentando las secuencias de identidad para no colisionar con operaciones posteriores de la API |
| 2026-09-12 | Gemini | Desactivación transitoria de `TR_CustodyEvents_AppendOnly` en transacción para alterar notas de evento 2 | Permite insertar la cadena íntegra primero y luego aplicar la modificación fraudulenta, demostrando que la verificación forense SHA-256 canónica detecta manipulaciones a nivel de base de datos |

## Sugerencias rechazadas o corregidas

| Fecha | Herramienta | Sugerencia | Por qué era inadecuada | Qué se hizo en su lugar |
|---|---|---|---|---|
| 2026-09-12 | Claude Code | `.gitignore` generado con el nombre del enunciado entre comillas (`"01-PRUEBA-CANDIDATO 2.md"`) | Git trata las comillas como caracteres literales: el archivo nunca se ignoró (apareció en `git status`) | Patrón sin comillas; los espacios no necesitan escape en `.gitignore` |

Candidatos típicos en este reto (confirmar solo si ocurren de verdad): formato canónico con separadores `|` (ambiguo si el texto contiene el separador), `DateTime.Now` en lugar de UTC, paginación por offset, hash calculado antes de truncar la precisión de la fecha, `FromSqlRaw` con interpolación, `async void`.

## Código revisado con especial cuidado

| Área | Qué se verificó |
|---|---|
| Hash de eventos y precisión de fecha | El hash golden se calculó fuera del código (PowerShell, SHA-256 sobre los bytes UTF-8 del JSON esperado) y el test de integración usa ticks sub-milisegundo que `datetime2(3)` redondearía |
| Migración generada por EF | Que `IsDescending` en `IX_Evidence_Keyset` se tradujera a `DESC` real en el script SQL, el índice filtrado `WHERE [Status] = 0` y `Restrict` en todas las FK a `Users` |
