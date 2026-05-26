using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CalendarioWPF.Models
{
    /// <summary>
    /// Contiene todos los datos asociados a un trabajador: sus días de vacaciones asignados,
    /// las imputaciones de cupo por fecha y los límites de días disponibles.
    /// </summary>
    public class InfoTrabajador
    {
        /// <summary>
        /// Lista de fechas en formato "dd/MM/yyyy" en las que el trabajador tiene vacaciones asignadas.
        /// Incluye fechas de cualquier año natural (pueden imputarse a cupos diferentes).
        /// </summary>
        [JsonPropertyName("vacaciones")]
        public List<string> Vacaciones { get; set; } = new();

        /// <summary>
        /// Departamento, grupo o sección a la que pertenece el trabajador.
        /// Útil para soporte multiempresa o de filtrado.
        /// </summary>
        [JsonPropertyName("departamento")]
        public string Departamento { get; set; } = "General";

        /// <summary>
        /// Número de días de vacaciones base anuales del trabajador (excluye días extra).
        /// Por defecto: 22 días laborables.
        /// </summary>
        [JsonPropertyName("dias_base")]
        public int DiasBase { get; set; } = 22;

        /// <summary>
        /// Días adicionales de vacaciones (por convenio, acuerdo, etc.). Se suman a <see cref="DiasBase"/>
        /// para calcular el total disponible del cupo.
        /// </summary>
        [JsonPropertyName("dias_extras")]
        public int DiasExtras { get; set; } = 0;

        /// <summary>
        /// Diccionario que mapea cada fecha de vacaciones ("dd/MM/yyyy") al año de cupo al que se imputa.
        /// Permite registrar vacaciones disfrutadas en un año natural pero imputadas a otro cupo
        /// (ej. vacaciones del cupo 2026 disfrutadas en enero de 2027).
        /// </summary>
        [JsonPropertyName("imputaciones")]
        public Dictionary<string, int> Imputaciones { get; set; } = new();
    }
}
