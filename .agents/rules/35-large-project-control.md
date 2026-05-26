---
trigger: model_decision
description: Reglas para la gestión y modernización controlada de proyectos de gran envergadura, priorizando el análisis previo y la ejecución incremental.
---

# Control de proyectos grandes

## Principio principal

Nunca modernices un proyecto grande en una única pasada.

Los proyectos grandes deben tratarse como un proceso asistido, incremental y controlado.

## Cuándo considerar un proyecto como grande

Trata un proyecto como grande cuando tenga alguna de estas características:

- Más de 30 archivos de código fuente.
- Más de 5.000 líneas de código.
- Varias aplicaciones o proyectos dentro de la misma solución.
- Varias pantallas, módulos o áreas funcionales.
- Acceso significativo a base de datos.
- Código legacy con reglas de negocio poco claras.
- Responsabilidades mezcladas en formularios, controladores o servicios.
- Uso real en producción o impacto crítico.

## Comportamiento obligatorio

Cuando trabajes con un proyecto grande, debes:

1. Analizar antes de modificar.
2. Crear un inventario del proyecto.
3. Identificar módulos y zonas de riesgo.
4. Proponer un plan por fases.
5. Esperar a que el usuario elija la primera fase o módulo.
6. Modificar solo el alcance seleccionado.
7. Resumir cada cambio realizado.
8. Proponer pasos de validación antes de continuar.

## Comportamiento prohibido

No debes:

- Reescribir el proyecto completo de una vez.
- Aplicar refactorizaciones automáticas amplias sobre archivos no relacionados.
- Renombrar muchas clases, archivos o métodos sin aprobación.
- Cambiar la arquitectura global sin un plan de migración.
- Introducir nuevos frameworks o librerías sin justificación.
- Mezclar cambios de base de datos, UI y lógica de negocio en una misma pasada sin control.
- Convertir todo el código a un nuevo patrón automáticamente.
- Eliminar código legacy salvo que el reemplazo y la validación estén claros.

## Unidad segura de trabajo

Una unidad segura de trabajo debería ser normalmente una de estas:

- Una clase.
- Un formulario, ventana o página.
- Un controlador.
- Un servicio.
- Un repositorio.
- Un caso de uso.
- Una consulta SQL o procedimiento almacenado.
- Un flujo funcional concreto.
- Un pequeño grupo de archivos estrechamente relacionados.

## Salida obligatoria antes de modificar código

Antes de cambiar un proyecto grande, proporciona:

1. Resumen del proyecto.
2. Arquitectura detectada.
3. Módulos principales.
4. Mapa de riesgos.
5. Fases propuestas.
6. Primer paso recomendado.
7. Archivos propuestos para el primer cambio.
8. Beneficio esperado.
9. Estrategia de validación.

## Modelo de aprobación de cambios

Para proyectos grandes, usa este modelo:

- Analiza libremente.
- Propón libremente.
- No hagas modificaciones amplias sin aprobación explícita.
- Modifica solo el alcance aprobado.
- Después de cada cambio, detente e informa.
- Continúa solo cuando se elija el siguiente alcance.

## Nivel de confianza

Para cada fase propuesta, incluye un nivel de confianza de 0.0 a 1.0.

Si la confianza es inferior a 0.8:

- Explica qué parte es incierta.
- Recomienda más análisis antes de modificar código.

Si la confianza es inferior a 0.6:

- No modifiques código todavía.
- Solicita más contexto, pruebas, logs o ejemplos.