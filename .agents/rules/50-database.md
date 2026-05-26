---
trigger: model_decision
description: Reglas para el manejo de bases de datos en proyectos .NET, con foco en seguridad, rendimiento y especificidades de Oracle y SQL Server.
---

# Database Rules

## General

- No modifiques lógica de base de datos a ciegas.
- Preserva límites transaccionales.
- Identifica si el SQL es Oracle, SQL Server u otro motor.
- Usa consultas parametrizadas.
- Evita riesgos de SQL injection.
- Sé explícito con fechas y conversiones dependientes de cultura.
- Registra operaciones de escritura relevantes con suficiente contexto.

## Oracle

- Prefiere bind variables.
- Recuerda que string vacío puede tratarse como NULL.
- Considera funciones analíticas/window para operaciones bulk.
- Evita bucles fila-a-fila cuando sea posible usar SQL set-based.
- Explica implicaciones de rendimiento.
- Preserva compatibilidad con el cliente Oracle usado por el proyecto.

## SQL Server

- Usa parámetros.
- Considera índices y planes de ejecución.
- Evita cursores innecesarios.
- Usa transacciones para cambios multi-paso.
- Sé explícito con `datetime`, `date` y `datetime2`.

## Optimización

Al optimizar SQL:

1. Identifica comportamiento actual.
2. Identifica tablas y joins.
3. Revisa filtros e índices.
4. Busca operaciones fila-a-fila.
5. Propón alternativa set-based.
6. Explica riesgos.
7. Propón comparación old/new si hay cambios de datos.
