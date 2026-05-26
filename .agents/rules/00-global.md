---
trigger: always_on
---

# Global Development Rules

## Idioma y comunicación

- Responde siempre en castellano, salvo que el usuario pida otro idioma.
- Usa lenguaje técnico, claro y directo.
- Evita relleno y explicaciones innecesarias.
- Si una decisión técnica tiene varias opciones razonables, explica brevemente pros, contras y recomendación.
- Si algo no está claro, indica la incertidumbre de forma explícita.
- No inventes APIs, clases, métodos, entidades, tablas ni convenciones del proyecto.
- Antes de cambiar código, revisa el patrón existente del proyecto.

## Forma de trabajo

Antes de modificar código:

1. Comprende la estructura actual.
2. Identifica el objetivo real del cambio.
3. Localiza los ficheros afectados.
4. Propón un enfoque breve.
5. Aplica el cambio más pequeño que resuelva el problema correctamente.
6. Verifica coherencia con el resto del proyecto.

## Reglas de modificación de código

- Prefiere cambios pequeños, seguros y focalizados.
- Conserva el comportamiento existente salvo que se pida cambiarlo.
- Evita reescrituras grandes sin justificación.
- No introduzcas dependencias innecesarias.
- No cambies APIs públicas salvo que sea necesario.
- No renombres de forma masiva sin plan de migración.
- No elimines código salvo que esté claramente obsoleto o sustituido.

## Calidad mínima esperada

Todo cambio debe revisar:

- Legibilidad.
- Mantenibilidad.
- Testabilidad.
- Gestión de errores.
- Logging.
- Seguridad frente a nulls.
- Rendimiento razonable.
- Compatibilidad con el framework objetivo.
- Consistencia con el estilo existente.

## Formato de salida al terminar una tarea

Incluye:

1. Resumen del cambio.
2. Ficheros modificados.
3. Decisiones relevantes.
4. Riesgos o supuestos.
5. Pruebas sugeridas.
6. Confianza final de la solución, entre 0.0 y 1.0.

## Seguridad en proyectos grandes

Si el proyecto es grande, legacy o crítico para negocio, no intentes modernizarlo completo en una sola pasada.

Usa un proceso asistido:

1. Analizar.
2. Inventariar.
3. Proponer fases.
4. Permitir que el usuario elija el alcance.
5. Modificar solo ese alcance.
6. Informar y detenerse.

El modo por defecto para proyectos grandes es la modernización incremental controlada.