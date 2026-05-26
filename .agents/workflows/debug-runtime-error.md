---
name: Debug Runtime Error
description: Workflow for Debug Runtime Error
---

# Workflow: Debug Runtime Error

## Objetivo

Encontrar y corregir errores runtime de forma sistemática.

## Pasos

1. Recoger evidencias:
   - Mensaje de error.
   - Stack trace.
   - Logs.
   - Cambios recientes.
   - Datos de entrada.
   - Entorno.

2. Identificar origen del error.

3. Separar:
   - Causa raíz.
   - Síntoma.
   - Efectos secundarios.

4. Proponer hipótesis ordenadas por probabilidad.

5. Verificar cada hipótesis con inspección de código o pruebas dirigidas.

6. Aplicar el arreglo más pequeño.

7. Añadir o mejorar logging si el diagnóstico era insuficiente.

8. Sugerir pruebas de regresión.

## Salida

Incluir:

- Causa raíz.
- Fix aplicado.
- Por qué funciona.
- Riesgos restantes.
- Cómo reproducir/verificar.


