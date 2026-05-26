---
name: Migrate Legacy Project
description: Workflow for Migrate Legacy Project
---

# Workflow: Migrate Legacy Project

## Objetivo

Migrar un proyecto antiguo de forma segura hacia un lenguaje, framework o arquitectura moderna.

## Pasos

1. Inventariar proyecto:
   - Framework.
   - Lenguaje.
   - Tipo de aplicación.
   - Dependencias externas.
   - Acceso a datos.
   - Despliegue.
   - Módulos críticos.
   - Logs existentes.
   - Estrategia actual de errores.

2. Detectar riesgos:
   - Lógica crítica.
   - Comportamiento sin pruebas.
   - Efectos en base de datos.
   - Integraciones externas.
   - Complejidad UI.
   - Librerías obsoletas.

3. Clasificar módulos:
   - Mantener.
   - Refactorizar in-place.
   - Extraer.
   - Reescribir.
   - Eliminar.

4. Definir arquitectura objetivo.

5. Crear plan de migración:
   - Fase 1: compilar y estabilizar.
   - Fase 2: aislar lógica de negocio.
   - Fase 3: añadir pruebas o checks de comportamiento.
   - Fase 4: migrar módulo a módulo.
   - Fase 5: sustituir infraestructura legacy.
   - Fase 6: limpieza final.

6. Para cada módulo:
   - Documentar comportamiento actual.
   - Crear implementación equivalente.
   - Comparar resultados.
   - Validar casos borde.
   - Añadir logs de diagnóstico si procede.
   - Sustituir de forma segura.

7. Resultado final:
   - Checklist de migración.
   - Riesgos.
   - Cambios completados.
   - Trabajo pendiente.


