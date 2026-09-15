# AI Code Review

## AI-REVIEW-01

Revisión del bloque C# del enunciado. No se ejecutó; análisis estático.

```csharp
public async void AcceptPendingTransfersAsync()
{
    var transfers = await _db.CustodyTransfers
        .Where(transfer => transfer.Status == TransferStatus.Pending)
        .ToListAsync();

    await Task.WhenAll(transfers.Select(async transfer =>
    {
        transfer.AcceptedAtUtc = DateTime.Now;
        transfer.Status = TransferStatus.Accepted;
        await _db.SaveChangesAsync();
    }));
}

public IEnumerable<CustodyTransfer> Search(string name)
{
    return _db.CustodyTransfers
        .FromSqlRaw($"SELECT * FROM CustodyTransfers WHERE CustodianName = '{name}'")
        .ToList();
}
```

### Defecto 1 — `DbContext` compartido entre tareas concurrentes

**Severidad:** Crítica.

**Impacto:** `Task.WhenAll` lanza una `SaveChangesAsync()` por cada transferencia **en paralelo, sobre la misma instancia de `_db`**. `DbContext` no es *thread-safe*: dos operaciones concurrentes sobre la misma instancia lanzan `InvalidOperationException` ("A second operation started on this context before a previous operation completed") o, peor, corrompen el *change tracker* sin lanzar nada, guardando estado inconsistente sin avisar. Con más de una transferencia pendiente, este método falla o corrompe datos de forma no determinista.

**Corrección propuesta:** actualizar todas las entidades en memoria dentro del `foreach`/`Select` (sin `await` ahí) y llamar a `SaveChangesAsync()` **una sola vez**, fuera del bucle:

```csharp
foreach (var transfer in transfers)
{
    transfer.AcceptedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
    transfer.Status = TransferStatus.Accepted;
}
await _db.SaveChangesAsync();
```

### Defecto 2 — `async void`

**Severidad:** Alta.

**Impacto:** una excepción dentro de un método `async void` no puede ser capturada por quien lo invoca (no hay `Task` que awaitear); en ASP.NET Core termina en el manejador de excepciones no controladas del proceso. Tampoco se puede esperar su finalización ni probarlo de forma determinista en un test.

**Corrección propuesta:** `public async Task AcceptPendingTransfersAsync()`. `async void` solo es válido para *event handlers*.

### Defecto 3 — `DateTime.Now` en vez de UTC

**Severidad:** Alta.

**Impacto:** la hora local del servidor queda grabada en `AcceptedAtUtc` (un campo que su propio nombre declara UTC). Además de la inconsistencia obvia, cualquier comparación posterior contra otras fechas del sistema (todas en UTC) queda desfasada por el huso horario del servidor — exactamente el tipo de bug que la regla de anomalía (`PendingTransferRule`) o la verificación de la cadena de hash detectarían como inconsistente.

**Corrección propuesta:** usar `TimeProvider` inyectado (como en el resto del proyecto): `_timeProvider.GetUtcNow().UtcDateTime`. Nunca `DateTime.Now`/`DateTime.UtcNow` directo, para que el valor sea determinista en tests.

### Defecto 4 — Aceptación masiva sin máquina de estados, sin evento de custodia y sin autorización

**Severidad:** Crítica.

**Impacto:** el método muta `Status` y `AcceptedAtUtc` directamente, sin pasar por ninguna máquina de estados, sin verificar quién es el actor ni si tiene permiso sobre *esa* transferencia puntual (en este dominio, solo el custodio destinatario puede aceptar), sin comprobar concurrencia (ninguna verificación tipo `RowVersion`/`If-Match`) y — el más grave para un sistema de cadena de custodia — **sin añadir el evento correspondiente a la cadena append-only ni actualizar el custodio actual de la evidencia**. Una aceptación que no queda registrada como evento es, para efectos forenses, una transferencia que nunca ocurrió: rompe la garantía central del sistema.

**Corrección propuesta:** cada aceptación debe pasar por la misma ruta que el endpoint `POST /accept` (máquina de estados, verificación de actor, `ChainAppender` para el evento, actualización de `Evidence.CurrentCustodianId`), todo en una transacción. Una operación "masiva" real debería iterar llamando a esa lógica por transferencia (o replicarla explícitamente), nunca saltársela.

### Defecto 5 — Inyección SQL vía `FromSqlRaw` con interpolación

**Severidad:** Crítica.

**Impacto:** `name` se concatena directo dentro del SQL (`$"...'{name}'"`). Un valor como `' OR '1'='1` o `'; DROP TABLE CustodyTransfers; --` se ejecuta como SQL arbitrario. Vulnerabilidad de inyección SQL clásica (OWASP Top 10).

**Corrección propuesta:** usar `FromSqlInterpolated` (EF Core parametriza automáticamente los `{}` de un `FormattableString`) o, más simple, LINQ puro:

```csharp
_db.CustodyTransfers.Where(t => t.CustodianName == name)
```

### Defecto 6 — `Search` es síncrono y bloqueante

**Severidad:** Media.

**Impacto:** `.ToList()` en vez de `.ToListAsync()` bloquea el hilo que lo invoca mientras espera la base de datos — en un contexto web (endpoint HTTP), eso consume un hilo del pool innecesariamente bajo carga.

**Corrección propuesta:** `public async Task<IEnumerable<CustodyTransfer>> SearchAsync(string name)` usando `ToListAsync()`.
