---
name: WPF Modernization
description: Skill for WPF Modernization
---

# Skill: WPF Modernization

## Usar cuando

- Se trabaje en aplicaciones WPF.
- Se mejore code-behind.
- Se creen componentes UI reutilizables.
- Se mueva lógica hacia MVVM.
- Se actualicen pantallas legacy.
- Se construya UI dinámicamente desde código.

## Supuestos del proyecto

- Algunos proyectos pueden construir UI desde código en lugar de XAML.
- Se deben reutilizar helpers y estilos existentes.
- No se debe forzar migración completa a MVVM si el proyecto no está preparado.
- La modernización incremental es preferible.

## Proceso

1. Identificar patrón UI actual.
2. Detectar lógica de negocio en eventos.
3. Extraer lógica a métodos, servicios o ViewModels.
4. Preservar comportamiento visual.
5. Reutilizar estilos y helpers existentes.
6. Revisar gestión de errores y logs en acciones relevantes.
7. Mantener nombres coherentes con el dominio.

## Recomendaciones

- Event handlers finos.
- Helpers UI consistentes.
- Evitar duplicar layout.
- Validación cerca del ViewModel o capa de servicio.
- Commands cuando sean prácticos.
- Data binding cuando simplifique la pantalla.


