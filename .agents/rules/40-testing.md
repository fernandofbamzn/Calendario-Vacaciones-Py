---
trigger: model_decision
description: Reglas de testing para proyectos .NET, con énfasis en la protección del comportamiento, estrategias para código legacy y validación de migraciones.
---

# Testing Rules

## Principios

- Las pruebas deben proteger comportamiento, no implementación accidental.
- En legacy, documenta comportamiento antes de cambiarlo.
- Para bugs, añade una prueba que falle antes del arreglo cuando sea posible.
- Si no se pueden automatizar pruebas, propone una verificación manual concreta.

## Tipos de pruebas

Considera:

- Unitarias para lógica pura.
- Integración para base de datos, APIs y servicios externos.
- Pruebas de regresión para migraciones.
- Pruebas manuales guiadas para UI WPF si no hay infraestructura de UI testing.

## Reglas

- No añadas frameworks de testing sin revisar el stack existente.
- No crees pruebas frágiles dependientes del orden si no es necesario.
- Usa datos de prueba explícitos.
- Cubre casos borde relevantes.
- Verifica también rutas de error cuando sean importantes.
