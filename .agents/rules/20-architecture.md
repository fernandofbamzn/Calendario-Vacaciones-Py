---
trigger: model_decision
description: Reglas de arquitectura para proyectos .NET, incluyendo principios generales, consideraciones específicas para aplicaciones de escritorio y web, gestión de dependencias y observabilidad mínima.
---

# Architecture Rules

## Principios generales

- Separa UI, lógica de negocio y acceso a datos.
- Evita colocar lógica de negocio directamente en eventos de UI.
- Usa servicios/casos de uso para comportamiento de aplicación.
- Aísla detalles de infraestructura.
- Evita estado global mutable.
- Mantén dependencias explícitas.
- Diseña excepciones y logs como parte del flujo técnico normal del proyecto.

## Aplicaciones de escritorio

Para WPF:

- Prefiere MVVM en desarrollo nuevo.
- Evita code-behind grande.
- Los comandos deben delegar en ViewModels o servicios.
- La construcción de UI desde código es válida si el proyecto usa ese patrón.
- Reutiliza helpers visuales, estilos y diccionarios existentes.
- No mezcles layout con reglas de negocio.

## Aplicaciones web

- Mantén controladores finos.
- Mueve lógica de negocio a servicios.
- Valida entradas en los límites HTTP/API.
- Usa DTOs cuando ayuden a separar contrato externo y modelo interno.
- No expongas entidades de base de datos directamente salvo en aplicaciones simples y controladas.

## Dependencias

- No añadas paquetes NuGet sin beneficio claro.
- Prefiere librerías estándar de .NET cuando sean suficientes.
- Si añades una dependencia, explica:
  - por qué se necesita,
  - alternativas consideradas,
  - impacto en despliegue,
  - compatibilidad con el framework objetivo.

## Observabilidad mínima

Todo proyecto serio debe tener:

- Logging configurado desde el arranque.
- Gestión coherente de excepciones.
- Mensajes de error útiles para diagnóstico.
- Trazabilidad de operaciones críticas.
- Registro de duración en procesos relevantes.
