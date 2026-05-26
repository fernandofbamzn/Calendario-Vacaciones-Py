---
name: Database Change
description: Workflow for Database Change
---

# Workflow: Database Change

## Objetivo

Modificar comportamiento relacionado con base de datos de forma segura.

## Pasos

1. Identificar motor de base de datos.
2. Localizar tablas, vistas, procedimientos o consultas afectadas.
3. Determinar tipo de cambio:
   - lectura,
   - escritura,
   - migración de esquema,
   - migración de datos,
   - optimización.

4. Comprobar compatibilidad con código existente.

5. Para cambios de datos:
   - Usar transacciones.
   - Proponer backup/rollback.
   - Evitar operaciones destructivas sin confirmación.

6. Para consultas:
   - Preservar columnas devueltas.
   - Preservar filtros.
   - Comparar resultados old/new si es posible.

7. Para rendimiento:
   - Explicar mejora esperada.
   - Evitar cambios accidentales de negocio.
   - Añadir logging de duración si el proceso lo justifica.


