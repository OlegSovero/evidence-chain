# Prueba técnica - Desarrollador(a) Senior Full Stack

## Evidence Chain: integridad y transferencia de custodia

### Contexto

Un equipo forense necesita registrar evidencia digital y demostrar que no se alteró ni se transfirió indebidamente. Construye una aplicación pequeña que permita revisar evidencias, consultar su historial de custodia y transferir una evidencia entre responsables.

Buscamos criterio técnico y capacidad de resolver problemas reales. No buscamos un CRUD extenso ni una interfaz de marketing.

## Condiciones

- Plazo: **5 días calendario**. Dedicación esperada: **20 a 25 horas**.
- Stack: **.NET 8+**, **SQL Server**, **React 18+** y **TypeScript**.
- IA: permitida. Debes explicar una decisión en la que una sugerencia de IA no era adecuada.
- Entrega: comparte el código fuente de frontend y backend, el historial Git disponible, la carpeta `/docs` y las instrucciones para levantar la solución. Puedes entregarlos en uno o varios repositorios, y/o complementar con un ZIP.
- Despliegue para la sesión: durante la presentación, la SPA debe estar disponible en **Vercel** y consumir una API pública de tu solución. Puedes elegir dónde alojar la API; debe funcionar integrada, no con datos simulados.
- Presentación: 30 minutos, sin live coding. Debes tener la aplicación desplegada y el proyecto abierto. No necesitas compartir URLs públicas antes de la sesión.

## Entrega esperada

Puedes organizar la solución como prefieras: monorepo, repositorio de frontend + repositorio de backend, o repositorios complementados con un ZIP. No evaluamos la estructura de carpetas elegida.

Como mínimo, debemos poder acceder a:

- Código fuente de frontend React y backend .NET.
- Migraciones, scripts o seed reproducible de la base de datos.
- Contrato OpenAPI o Swagger generado.
- Carpeta `/docs` con los archivos definidos en este enunciado.
- `README.md` con los pasos para levantar, poblar y probar la solución.

No incluyas secretos, contraseñas, tokens ni archivos de configuración con credenciales a menos que sean necesarias para levantar el proyecto.

### Estructura sugerida (no obligatoria)

Puedes entregar un monorepo con una estructura similar a esta.

```text
/
    frontend/           Proyecto React + TypeScript
    backend/            API .NET
    database/           Migraciones, scripts o seed reproducible
    docs/               Documentación solicitada
    openapi.yaml        Contrato de API
    README.md           Instrucciones de ejecución
```

## El reto

Implementa los siguientes dos recorridos completos, desde React hasta SQL Server.

### 1. Revisar una evidencia

La aplicación muestra una bandeja de evidencias con código, descripción, custodio actual, fecha de último evento y estado de integridad.

- La bandeja debe filtrar por texto, custodio y estado; ordenar por fecha; y conservar filtros en la URL.
- El backend debe paginar en servidor. Si usas keyset pagination, explica por qué; si eliges otra estrategia, justifícala.
- La vista de detalle muestra una línea de tiempo de eventos de custodia y resalta una anomalía.
- Implementa una regla: una transferencia que no haya sido aceptada dentro de un plazo configurable debe aparecer como anomalía, con severidad y explicación legible.
- Los eventos de custodia son append-only. Cada evento se encadena con el hash del anterior.
- Expón `GET /api/v1/evidence/{id}/chain/verify`, que indique si la cadena es íntegra y, si no, el primer evento inválido.

### 2. Transferir custodia

Un investigador solicita transferir una evidencia a otro custodio y el destinatario la acepta o rechaza.

- Define y aplica una máquina de estados explícita.
- Usa concurrencia optimista. Si dos usuarios actúan sobre la misma transferencia, una operación debe recibir `409 Conflict` con información suficiente para que la interfaz explique qué ocurrió.
- Las escrituras deben ser idempotentes mediante `Idempotency-Key`.
- En React, la solicitud debe verse de inmediato como pendiente. Ante error o `409`, debe reconciliarse o revertirse de manera clara; no debe quedar como confirmada si el servidor no la aceptó.
- La interfaz debe manejar carga, vacío y error. El modal de transferencia debe funcionar por teclado, cerrar con Escape y devolver el foco al elemento que lo abrió.

## Restricciones técnicas

- Seed reproducible de **1.000 evidencias y 10.000 eventos**. Incluye al menos un caso íntegro, un evento alterado para verificar la cadena y una transferencia vencida para probar la anomalía.
- El algoritmo de hash y la persistencia son elección tuya; explica el formato canónico que firmas y cómo garantizas orden estable.
- Autenticación simplificada: JWT local con los roles `Investigador`, `Custodio` y `Supervisor`. La autorización se valida en servidor.
- Errores 4xx/5xx en `application/problem+json`. El `409` debe incluir el estado actual.
- Debes proponer una arquitectura Azure para producción en `decisions.md`: cómputo, base de datos, observabilidad, secretos y almacenamiento de evidencia. Incluye separación de ambientes, primer indicador/alerta operativa y una estimación de costo mensual. No necesitas una cuenta Azure para cumplir este punto.
- No es necesario implementar carga de archivos, Azure, rate limiting, i18n ni una segunda regla de anomalías. Puedes incluirlos como extras, pero no sustituyen los dos recorridos obligatorios.

## Contrato mínimo de API

| Verbo | Ruta | Uso |
|---|---|---|
| `GET` | `/api/v1/evidence` | Bandeja: filtros, orden y paginación |
| `GET` | `/api/v1/evidence/{id}` | Detalle de evidencia |
| `GET` | `/api/v1/evidence/{id}/chain` | Línea de tiempo de eventos |
| `GET` | `/api/v1/evidence/{id}/chain/verify` | Estado y primer punto de ruptura |
| `POST` | `/api/v1/custody-transfers` | Solicita una transferencia; requiere `Idempotency-Key` |
| `POST` | `/api/v1/custody-transfers/{id}/accept` | Acepta; requiere `If-Match` |
| `POST` | `/api/v1/custody-transfers/{id}/reject` | Rechaza; requiere `If-Match` |

Publica un `openapi.yaml` o Swagger generado que coincida con la implementación.

## Documentación requerida

Manténla corta y específica. En la raíz, crea esta carpeta:

```text
/docs
  overview.md
  decisions.md
  code-map.md
  ai-usage.md
    ai-code-review.md
```

### `overview.md` (máximo una página)

- Qué terminaste y qué dejaste fuera.
- Cómo levantar el proyecto, ejecutar seed y pruebas.
- Una captura o descripción breve de cómo mediste la consulta principal.

### `decisions.md` (máximo dos páginas)

Documenta cuatro decisiones: persistencia/encadenado de hash, paginación, concurrencia/actualización optimista y arquitectura Azure de producción. Para cada una incluye opción elegida, alternativa descartada, costo asumido y qué señal indicaría que debes cambiarla.

Para la decisión de Azure, cubre cómputo, Azure SQL, Blob Storage, Key Vault y Application Insights; separación de ambientes; el primer indicador o alerta que configurarías; y una estimación mensual para un equipo pequeño. No es necesario desplegar en Azure.

### `code-map.md` (máximo una página)

Tabla con estas capacidades, el archivo que contiene la lógica y el punto de entrada:

| Capacidad | Archivo / módulo | Punto de entrada |
|---|---|---|
| Encadenado y verificación de hash | | |
| Máquina de estados y concurrencia | | |
| Idempotencia | | |
| Regla de anomalía | | |
| Consulta paginada | | |
| Filtros de URL y cancelación de solicitudes | | |
| Actualización optimista y manejo de 409 | | |

### `ai-usage.md` (máximo una página)

- Herramientas de IA utilizadas y en qué partes.
- Una sugerencia que aceptaste y por qué.
- **Una sugerencia que rechazaste o corregiste**, por qué era inadecuada y qué hiciste en su lugar.
- Una parte que revisaste especialmente antes de aceptar código generado.

## Pruebas mínimas

- Backend: cadena alterada, idempotencia y conflicto de concurrencia.
- Frontend: respuesta de búsqueda obsoleta no reemplaza el filtro actual, y rollback/reconciliación ante `409`.

No medimos cobertura. Explica qué decidiste no probar y por qué.

## Revisión de código asistido

**Ejercicio AI-REVIEW-01.** Incluye en `docs/ai-code-review.md` una revisión del siguiente bloque C#. Indica defecto, severidad, impacto y corrección propuesta. No lo ejecutes.

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

## Extras

Subida de archivos con hash en streaming, URL firmada, segunda regla de anomalías, medición con 200k eventos, despliegue en Azure/IaC, rate limiting, i18n o accesibilidad ampliada. No los implementes antes de completar el alcance obligatorio.

### Despliegue adicional en Azure

El despliegue real de la solución en Azure es opcional y suma evidencia de experiencia operativa. Si lo realizas, comparte acceso de solo lectura al Resource Group o muestra la solución desplegada durante la sesión. No compartas secretos, credenciales ni permisos de propietario.