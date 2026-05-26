---
name: C# Refactoring
description: Skill for C# Refactoring
---

# Skill: C# Refactoring

## Usar cuando

- El código sea difícil de leer.
- Haya lógica duplicada.
- Un método sea demasiado largo.
- Existan condicionales anidados.
- La lógica de negocio esté mezclada con UI o acceso a datos.
- El usuario pida simplificar o modernizar código C#.

## Proceso

1. Leer el código relevante.
2. Identificar comportamiento actual.
3. Detectar riesgos y casos borde.
4. Proponer un refactor pequeño.
5. Preservar comportamiento público.
6. Aplicar el cambio.
7. Revisar excepciones y logs.
8. Sugerir pruebas o verificación manual.

## Refactorings preferidos

- Extraer método.
- Extraer clase o servicio.
- Sustituir condicionales anidados por guard clauses.
- Sustituir magic strings/numbers por constantes.
- Introducir DTOs cuando aporten claridad.
- Unificar bloques duplicados.
- Mejorar nombres.
- Simplificar null checks.
- Separar reglas de negocio de infraestructura.

## Evitar

- Grandes reescrituras sin pruebas.
- Cambiar comportamiento silenciosamente.
- Introducir abstracciones sin beneficio.
- Sobrediseñar código simple.


