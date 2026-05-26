---
name: SQL Optimization
description: Skill for SQL Optimization
---

# Skill: SQL Optimization

## Usar cuando

- Una consulta sea lenta.
- Existan bucles que ejecutan SQL repetidamente.
- Haya operaciones masivas.
- Se trabaje con Oracle o SQL Server.

## Proceso

1. Identificar motor de base de datos.
2. Entender resultado esperado.
3. Localizar tablas, joins y filtros.
4. Detectar operaciones fila-a-fila.
5. Proponer versión set-based si procede.
6. Revisar índices necesarios.
7. Proponer comparación de resultados old/new.
8. Añadir logging de duración y recuentos cuando sea útil.

## Precauciones

- No cambiar semántica de negocio por optimizar.
- No asumir índices inexistentes.
- No usar sintaxis específica de un motor sin verificar.
- En operaciones de escritura, proponer transacción y rollback.


