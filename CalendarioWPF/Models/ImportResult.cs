namespace CalendarioWPF.Models
{
    /// <summary>
    /// Estructura que encapsula el resultado de una operación de importación de datos.
    /// Se devuelve por <c>DataManager.ImportarDesdeTexto</c> para informar del resultado
    /// sin lanzar excepciones para casos de importación parcial.
    /// </summary>
    public class ImportResult
    {
        /// <summary>
        /// Cadena descriptiva del tipo de importación realizada.
        /// Ejemplos: "Consolidado Completo", "Vacaciones JSON", "Festivos CSV".
        /// </summary>
        public string Tipo { get; set; } = "";

        /// <summary>
        /// Mensaje de resultado detallado, mostrando el resumen de entidades importadas.
        /// </summary>
        public string Msg { get; set; } = "";

        /// <summary>
        /// Plan de vacaciones resultante tras aplicar la importación sobre el estado previo.
        /// </summary>
        public PlanVacaciones DatosActualizados { get; set; } = null!;
    }
}
