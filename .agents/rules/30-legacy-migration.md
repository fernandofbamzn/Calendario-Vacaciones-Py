---
trigger: model_decision
description: Reglas para la migración y modernización de código legacy, incluyendo estrategias de análisis, migración incremental y consideraciones específicas para conversiones de lenguaje.
---

# Legacy Migration Rules

## Objetivo

Ayudar a modernizar aplicaciones antiguas de forma segura, conservando comportamiento.

## Principios de migración

- Primero entender el comportamiento existente.
- No reescribir todo por defecto.
- Preferir migración incremental.
- Comparar comportamiento antiguo y nuevo.
- Crear adaptadores intermedios cuando sea útil.
- Identificar reglas de negocio antes de cambiar estructura.
- Preservar casos borde salvo que se rechacen explícitamente.
- Añadir logging al migrar para facilitar comparación y diagnóstico.

## Análisis legacy

Al analizar código antiguo, identifica:

1. Puntos de entrada.
2. Eventos de UI.
3. Reglas de negocio.
4. Acceso a base de datos.
5. Dependencias externas.
6. Estado global o compartido.
7. Efectos secundarios.
8. Gestión de errores.
9. Logs existentes o ausencia de logs.
10. Cuellos de botella.
11. Código seguro de reemplazar.

## Estrategias posibles

Propón una de estas estrategias por módulo:

- Traducción directa.
- Refactor in-place.
- Extraer servicio.
- Encapsular legacy detrás de una interfaz.
- Sustituir módulo completo.
- Strangler pattern.
- Reescritura solo después de documentar o probar comportamiento.

## Migración controlada

En proyectos legacy grandes, la migración debe ser incremental.

No migres el proyecto completo automáticamente.

Orden recomendado:

1. Compilar y estabilizar.
2. Documentar el comportamiento actual.
3. Identificar módulos.
4. Añadir logs y gestión de excepciones donde falten.
5. Extraer lógica de negocio desde UI y manejadores de eventos.
6. Añadir pruebas o scripts de comparación.
7. Sustituir infraestructura gradualmente.
8. Modernizar framework o lenguaje solo cuando el comportamiento esté protegido.

## VB.NET a C#

Al migrar VB.NET a C#:

- Preserva comportamiento primero.
- Cuidado con:
  - `Nothing`
  - valores nullable
  - propiedades por defecto
  - late binding
  - `ByRef`
  - parámetros opcionales
  - comparaciones de string
  - fechas
  - `IIf` vs `If`
  - índices de colecciones
- Sustituye idioms VB por C# idiomático solo cuando el comportamiento esté claro.
- Añade logs en puntos críticos si la migración lo permite.
