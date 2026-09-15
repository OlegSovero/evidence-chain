---
name: ey-evidence-chain
description: Use for any work on the EY Evidence Chain technical test (.NET + SQL Server + React/TS custody-chain app) to keep code, docs and priorities aligned with the challenge spec and the agreed plan.
---

# EY Evidence Chain — guía de trabajo

Las reglas generales están en `AGENTS.md`. Esta skill añade el checklist de cumplimiento y el flujo de trabajo por tarea.

## Antes de cada tarea

1. Lee `context/progreso.md` para saber en qué fase estamos y cuál es la siguiente tarea.
2. Relee en `context/enunciado.md` la sección que cubre la tarea. El enunciado es la fuente de verdad; si la petición lo contradice, avisa antes de implementar.
3. Revisa en `context/plan-arquitectura.md` la decisión que aplica (esquema, hash, paginación, concurrencia, idempotencia, frontend).

## Checklist de cumplimiento

Recorrido 1 — Revisar evidencia:
- [ ] La bandeja muestra código, descripción, custodio actual, fecha del último evento y estado de integridad.
- [ ] Filtra por texto, custodio y estado; ordena por fecha; los filtros se conservan en la URL.
- [ ] Paginación en servidor con keyset, justificada en decisions.md.
- [ ] El detalle muestra la línea de tiempo y resalta la anomalía.
- [ ] La regla de anomalía marca la transferencia no aceptada dentro del plazo configurable, con severidad y explicación legible.
- [ ] Los eventos son append-only y cada uno se encadena con el hash del anterior.
- [ ] `GET /api/v1/evidence/{id}/chain/verify` indica si la cadena es íntegra o cuál es el primer evento inválido.

Recorrido 2 — Transferir custodia:
- [ ] Máquina de estados explícita: Pendiente → Aceptada | Rechazada.
- [ ] Concurrencia optimista: un conflicto devuelve 409 con el estado actual.
- [ ] `Idempotency-Key` en POST; `If-Match` en accept y reject.
- [ ] En React, la solicitud aparece pendiente al instante; ante error o 409 se reconcilia o revierte y nunca queda como confirmada sin confirmación del servidor.
- [ ] Estados de carga, vacío y error; el modal funciona con teclado, cierra con Escape y devuelve el foco.

Transversales:
- [ ] Los 7 endpoints del contrato mínimo existen y `openapi.yaml` coincide con la implementación.
- [ ] El seed es reproducible: 1.000 evidencias y 10.000 eventos, con un caso íntegro, un evento alterado y una transferencia vencida.
- [ ] JWT local con los roles Investigador, Custodio y Supervisor; la autorización se valida en el servidor.
- [ ] 4xx/5xx en `application/problem+json`; el 409 incluye el estado actual.
- [ ] Pruebas de backend: cadena alterada, idempotencia y conflicto de concurrencia.
- [ ] Pruebas de frontend: una respuesta obsoleta no reemplaza el filtro actual; rollback o reconciliación ante 409.
- [ ] `docs/`: overview (≤1 pág), decisions (≤2 págs, 4 decisiones con opción, alternativa, costo y señal de cambio, más la arquitectura Azure con costo mensual), code-map (7 capacidades), ai-usage (≤1 pág, incluye una sugerencia rechazada) y ai-code-review (AI-REVIEW-01).
- [ ] El README explica cómo levantar, poblar y probar. No hay secretos en el repo.
- [ ] La SPA está en Vercel consumiendo la API pública real.

## Después de cada tarea

1. Ejecuta el build y los tests.
2. Marca el ítem en `context/progreso.md` y añade una línea a la bitácora.
3. Actualiza la fila correspondiente de `docs/code-map.md` si la tarea toca una de las 7 capacidades.
4. Anota en `context/ai-log.md` cualquier sugerencia de IA relevante que se haya aceptado o rechazado.
5. Propón un commit pequeño con Conventional Commits, sin trailer `Claude-Session: ...` ni ninguna otra referencia a la sesión de Claude en el mensaje (más allá de esto no cambies retroactivamente los commits ya pusheados que sí lo llevan, salvo que Oleg lo pida explícitamente).
