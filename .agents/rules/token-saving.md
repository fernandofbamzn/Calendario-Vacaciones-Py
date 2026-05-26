---
trigger: always_on
---

## Arquitectura Basada en Interfaces (Token-Saving Workflow)
* **Regla:** Cada servicio, gestor o componente lógico de negocio importante debe estar respaldado por una **Interfaz (`IInterface.cs`)** independiente.
* **Documentación en la Interfaz:** Todos los métodos, propiedades y parámetros de la interfaz deben incluir documentación técnica en formato **XML (`/// <summary>`) completa y precisa en castellano**.
* **Propósito:** Esto permite que futuros agentes de IA lean únicamente la interfaz para comprender el comportamiento y las firmas del componente, reduciendo significativamente el consumo de tokens de contexto al evitar la lectura innecesaria de archivos de implementación extensos (`.cs`).