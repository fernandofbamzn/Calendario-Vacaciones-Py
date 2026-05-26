---
trigger: model_decision
description: Reglas de pragmatismo para proyectos .NET, enfocadas en evitar el sobrediseño y promover soluciones simples y directas, con criterios claros para cuándo abstraer y cuándo no hacerlo.
---

# Pragmatism Rules

## Principio principal

No sobrediseñar.

## Guías

- Usa soluciones simples primero.
- No introduzcas Clean Architecture por defecto.
- No introduzcas CQRS salvo que exista complejidad real.
- No introduzcas patrones Mediator por defecto.
- No introduzcas AutoMapper por defecto.
- No crees interfaces para cada clase automáticamente.
- No dividas proyectos salvo que mejore claramente la mantenibilidad.
- Prefiere código claro y directo a arquitectura abstracta.

## Cuándo abstraer

La abstracción está justificada si:

- Hay múltiples implementaciones.
- Las pruebas requieren aislamiento.
- La infraestructura externa debe ser reemplazable.
- La lógica de negocio es suficientemente compleja.
- El proyecto crecerá de forma clara.

## Cuándo no abstraer

Evita abstracción si:

- El código es simple.
- Solo hay una implementación.
- Añade más ficheros que valor.
- Dificulta depurar.
- Oculta reglas de negocio.

## Regla de equilibrio

La simplicidad no significa ausencia de calidad.

Incluso en soluciones simples deben existir:

- Nombres claros.
- Control de errores.
- Logging suficiente.
- Separación mínima de responsabilidades.
- Pruebas o verificaciones manuales razonables.
