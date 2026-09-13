# Decisions

_Máximo dos páginas. Cuatro decisiones: opción elegida, alternativa descartada, costo asumido y señal de cambio._

## 1. Persistencia y encadenado de hash

**Opción elegida.** Cada `CustodyEvent` guarda `PreviousHash` y `Hash` (`binary(32)`). `Hash = SHA-256(JSON canónico UTF-8 del evento)` y el génesis usa 32 bytes en cero como `prev`. Formato canónico `v:1` (`Domain/Hashing/CanonicalEventSerializer.cs`), sin espacios y con orden de campos fijo:

```json
{"v":1,"evidenceId":42,"seq":1,"type":0,"actor":7,"from":null,"to":7,"transferId":null,"notes":null,"occurredAtUtc":"2026-09-01T14:05:00.000Z","prev":"0000…0000"}
```

Reglas del formato: `type` es el valor numérico del enum (renombrar un miembro en C# no cambia hashes); los campos ausentes se escriben como `null`, nunca se omiten; `transferId` en formato `D` minúsculas; `notes` con el escape JSON mínimo (`"`, `\`, controles) y UTF-8 sin `\uXXXX`; `occurredAtUtc` en UTC truncada a milisegundos (`yyyy-MM-ddTHH:mm:ss.fffZ`); `prev` en hex minúsculas. Dos tests *golden* fijan la cadena exacta y el SHA-256 calculado fuera del código: si cambian, el formato cambió.

**Alternativa descartada.** Texto con separadores (`id|seq|notes|…`): ambiguo si `notes` contiene el separador y sin versión ni tipos explícitos. También se descartó serializar la entidad por reflexión con `JsonSerializer`: el orden de campos dependería del orden de las propiedades en la clase. Se escribe con `Utf8JsonWriter` campo a campo.

**Orden estable.** `Sequence` (1..n por evidencia) con índice único `(EvidenceId, Sequence)`: no se puede insertar dos veces la misma posición ni bifurcar la cadena. `ChainVerifier` recorre por `Sequence` y exige contigüidad (`SequenceGap`), enlace con el hash anterior (`BrokenLink`) y recomputación del hash (`HashMismatch`), devolviendo el primer evento inválido. `CustodyEvents` tiene un trigger `INSTEAD OF UPDATE, DELETE` creado en la migración: append-only por regla de base de datos, no solo por disciplina de aplicación.

**Costo asumido.** Las fechas se truncan a milisegundos antes de firmar y antes de guardar (`datetime2(3)` redondea, no trunca; un test de integración con Testcontainers guarda un evento con ticks sub-milisegundo y verifica que el hash sigue válido tras leerlo). Cambiar cualquier regla del formato invalida todos los hashes: exige `v:2`, migración de datos y regenerar el seed.

**Límite honesto y señal de cambio.** Quien pueda reescribir toda la tabla (deshabilitando el trigger) puede recalcular la cadena completa y quedará consistente: el hash detecta alteraciones parciales o accidentales, no a un administrador con acceso total. En producción se ancla con HMAC cuya clave vive en Key Vault (sin la clave no se puede refirmar), publicando el hash de cabeza fuera de la base, o con tablas *ledger* de Azure SQL. Señal para cambiar: un requisito de no repudio frente al propio DBA o auditoría externa de la cadena.

## 2. Paginación

## 3. Concurrencia y actualización optimista

## 4. Arquitectura Azure de producción

Cómputo, Azure SQL, Blob Storage, Key Vault y Application Insights; separación de ambientes; primer indicador/alerta; estimación mensual para un equipo pequeño.
