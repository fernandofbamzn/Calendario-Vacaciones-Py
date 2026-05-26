---
name: VB.NET to C# Migration
description: Skill for VB.NET to C# Migration
---

# Skill: VB.NET to C# Migration

## Usar cuando

- Se traduzca VB.NET a C#.
- Se modernicen proyectos .NET Framework antiguos.
- Se extraiga lógica de negocio desde aplicaciones VB.NET.
- Se prepare migración gradual a .NET moderno.

## Proceso

1. Leer el código VB.NET.
2. Identificar entradas, salidas y efectos secundarios.
3. Detectar semántica específica de VB.
4. Traducir comportamiento fielmente.
5. Modernizar solo cuando sea seguro.
6. Añadir avisos sobre diferencias semánticas.
7. Incorporar logs en puntos críticos si procede.
8. Sugerir pruebas de validación.

## Atención especial

Revisar cuidadosamente:

- `ByRef`
- `Optional`
- `Nothing`
- `Nullable(Of T)`
- `IIf`
- `If`
- `IsNothing`
- `Date`
- `CStr`, `CInt`, `Val`
- `On Error`
- propiedades indexadas por defecto
- late-bound calls
- concatenación de strings con nulls
- índices base de colecciones

## Salida esperada

Al migrar código, incluir:

- Versión C#.
- Avisos semánticos.
- Logs o excepciones recomendadas.
- Pruebas sugeridas.
- Comportamientos que podrían no ser equivalentes.


