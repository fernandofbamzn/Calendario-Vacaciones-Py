---
trigger: always_on
---

# C# Style Rules

## Estilo general

- Usa C# moderno cuando sea compatible con el framework objetivo.
- Prioriza código legible sobre código ingenioso.
- Usa nombres explícitos y orientados al dominio.
- Mantén los métodos pequeños y con responsabilidad clara.
- Prefiere retornos tempranos cuando reduzcan anidamiento.
- Evita duplicación de lógica.
- Respeta el estilo existente si el proyecto ya tiene una convención clara.

## Nombres

Los nombres deben ser explícitos y pueden mezclar español e inglés cuando mejore la claridad del dominio.

Ejemplos válidos:

- `TutoresController`
- `Datos.cs`
- `ExportarPDFService.cs`
- `TutoriasRepository`
- `DatosTutorService`
- `AsignaturasImportController`
- `GenerarInformePDFAsync`
- `ObtenerDatosTutor`
- `GuardarTutoriasAsignatura`

Reglas:

- Usa `PascalCase` para clases, métodos y propiedades.
- Usa `camelCase` para variables locales y parámetros.
- Usa `_camelCase` para campos privados.
- Usa prefijo `I` para interfaces.
- Usa sufijo `Async` en métodos asíncronos.
- Evita abreviaturas oscuras.
- Mantén nombres históricos si cambiarlos aumenta el riesgo.
- En dominios existentes, prioriza la terminología del negocio aunque esté en español.
- En patrones técnicos habituales, usa nombres en inglés si son más reconocibles: `Controller`, `Service`, `Repository`, `ViewModel`, `Options`, `Provider`, `Factory`.

## LINQ

El uso de LINQ es aceptado tanto en sintaxis de métodos/lambdas como en sintaxis de consulta tipo SQL.

- Usa LINQ cuando mejore la expresividad.
- La sintaxis lambda es válida para filtros, proyecciones, agrupaciones y transformaciones.
- La sintaxis tipo SQL es válida cuando haga más legible una consulta compleja.
- Evita LINQ excesivamente críptico si un bucle resulta más claro.
- Ten cuidado con enumeraciones múltiples.
- Materializa con `.ToList()` o `.ToArray()` solo cuando tenga sentido.
- En consultas contra base de datos, revisa qué parte se ejecuta en servidor y qué parte en memoria.

## Null handling

- Sé explícito con la nulabilidad.
- Usa `string.IsNullOrWhiteSpace` para validar texto.
- Evita el operador null-forgiving `!` salvo que esté justificado.
- Valida datos externos en los límites del sistema.
- No ocultes posibles `NullReferenceException`; corrige la causa.

## Async

- Usa `async`/`await` para operaciones I/O-bound.
- Evita `.Result` y `.Wait()`.
- Propaga `CancellationToken` cuando sea razonable.
- Usa `ConfigureAwait(false)` en librerías cuando encaje con el proyecto.

## Excepciones

Las excepciones forman parte integral del diseño del proyecto.

- No tragues excepciones silenciosamente.
- Captura excepciones específicas cuando sea posible.
- Añade contexto útil antes de relanzar o registrar.
- Preserva el stack trace usando `throw;`, no `throw ex;`.
- No uses excepciones para flujo normal de negocio.
- Distingue entre errores esperados, validaciones y fallos excepcionales.
- Define excepciones propias solo cuando aporten semántica útil.
- En capas de infraestructura, encapsula errores externos con contexto del dominio.

Ejemplo recomendado:

```csharp
try
{
    await exportarPDFService.GenerarInformeAsync(idTutor, cancellationToken);
}
catch (PdfExportException ex)
{
    logger.LogError(ex, "Error exportando informe PDF del tutor {IdTutor}", idTutor);
    throw;
}
```

## Logging

El logging debe estar presente desde el diseño inicial, no añadirse al final.

- Usa logging estructurado siempre que sea posible.
- No concatenes strings en logs si puedes usar placeholders.
- Incluye identificadores relevantes: ids, curso, centro, usuario, operación.
- No registres secretos, contraseñas, tokens ni datos sensibles innecesarios.
- Usa niveles de log correctamente:
  - `Trace`: detalle extremo de diagnóstico.
  - `Debug`: información útil durante desarrollo.
  - `Information`: hitos normales de negocio o proceso.
  - `Warning`: situación anómala recuperable.
  - `Error`: fallo que impide completar una operación.
  - `Critical`: fallo grave del sistema.
- En procesos largos, registra inicio, fin, duración y recuentos relevantes.
- En migraciones o procesos masivos, registra métricas suficientes para auditar.

Ejemplo recomendado:

```csharp
logger.LogInformation(
    "Iniciando exportación PDF. Tutor={IdTutor}, Curso={Curso}",
    idTutor,
    curso);
```

## Comentarios

- No comentes lo obvio.
- Comenta reglas de negocio no evidentes.
- Usa documentación XML en APIs públicas cuando sea útil.
- Los comentarios y documentación deben ir en castellano salvo convención contraria del proyecto.
