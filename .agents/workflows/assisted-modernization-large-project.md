---
name: Modernización asistida de proyecto grande
description: Workflow for Modernización asistida de proyecto grande
---

# Workflow: Modernización asistida de proyecto grande

## Objetivo

Modernizar un proyecto grande o legacy de forma segura, incremental y bajo control del usuario.

## Fase 0: Análisis inicial

Inspecciona el proyecto sin modificar código.

Identifica:

- Estructura de la solución.
- Proyectos incluidos.
- Versiones de framework.
- Puntos de entrada principales.
- Tecnología de interfaz de usuario.
- Tecnología de base de datos.
- Dependencias externas.
- Archivos de configuración.
- Sistema de compilación y despliegue.
- Proyectos de pruebas.
- Zonas de alto riesgo.

Salida esperada:

- Diagnóstico breve del proyecto.
- Inventario de módulos.
- Mapa de riesgos.
- Estrategia de modernización sugerida.

No modifiques código en esta fase.

## Fase 1: Mapa de módulos

Agrupa los archivos por áreas funcionales.

Ejemplos de grupos:

- Pantallas de UI.
- Controladores.
- Servicios.
- Acceso a datos.
- Informes.
- Modelos o entidades.
- Helpers y utilidades.
- Configuración.
- Integraciones legacy.
- Pruebas.

Para cada módulo, estima:

- Complejidad.
- Riesgo.
- Acoplamiento.
- Valor de modernización.
- Prioridad recomendada.
- Nivel de confianza.

## Fase 2: Backlog de modernización

Crea un backlog de tareas pequeñas y controladas.

Cada tarea debe incluir:

- Título.
- Alcance.
- Archivos afectados.
- Motivo.
- Solución propuesta.
- Nivel de riesgo.
- Beneficio esperado.
- Método de validación.
- Nivel de confianza.

Cada tarea debe ser lo bastante pequeña como para poder revisarse manualmente.

## Fase 3: Selección de alcance por el usuario

Antes de cambiar código, espera a que el usuario seleccione una tarea o módulo.

Puedes recomendar la mejor primera tarea, pero no debes iniciar modificaciones amplias automáticamente.

Buenas primeras tareas suelen ser:

- Eliminar lógica duplicada de bajo riesgo.
- Extraer un servicio aislado.
- Mejorar logging.
- Mejorar gestión de excepciones.
- Detectar código muerto.
- Refactorizar una funcionalidad pequeña.
- Optimizar una consulta SQL problemática.
- Extraer lógica de un único manejador de evento de UI.

Evita empezar por:

- Renombrados globales.
- Reescritura completa de arquitectura.
- Sustitución de ORM.
- Migración completa de framework.
- Cambios masivos de namespaces.
- Rediseño de esquema de base de datos.

## Fase 4: Implementación controlada

Modifica solo los archivos aprobados.

Reglas:

- Mantén los cambios pequeños.
- Conserva el comportamiento existente.
- Añade logs cuando proceda.
- Mejora la gestión de excepciones cuando proceda.
- No mezcles refactorizaciones no relacionadas.
- No cambies comportamiento público de forma silenciosa.
- No continúes hacia otros módulos sin aprobación.

## Fase 5: Verificación

Después de cada implementación, proporciona:

- Archivos modificados.
- Qué se ha cambiado.
- Por qué se ha cambiado.
- Qué comportamiento se conserva.
- Riesgos.
- Pruebas manuales recomendadas.
- Pruebas automáticas, si existen.
- Siguiente tarea recomendada.

## Fase 6: Iteración

Repite este ciclo:

1. Seleccionar siguiente tarea.
2. Implementar.
3. Verificar.
4. Informar.
5. Detenerse.

La modernización debe avanzar mediante incrementos controlados.

