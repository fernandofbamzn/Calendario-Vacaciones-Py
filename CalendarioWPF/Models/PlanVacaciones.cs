using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CalendarioWPF.Models
{
    /// <summary>
    /// Representa el plan completo de vacaciones de la empresa para un año de cupo determinado.
    /// Es el objeto raíz que se persiste en el archivo 'datos_vacaciones.json'.
    /// </summary>
    public class PlanVacaciones
    {
        /// <summary>
        /// Título descriptivo de la planificación, editable desde la barra superior de la aplicación.
        /// </summary>
        [JsonPropertyName("titulo_pagina")]
        public string TituloPagina { get; set; } = "Planificación de Vacaciones";

        /// <summary>
        /// Año de cupo activo. Es el año al que se imputan las nuevas vacaciones marcadas por el usuario.
        /// </summary>
        [JsonPropertyName("year")]
        public int Year { get; set; } = System.DateTime.Today.Year;

        /// <summary>
        /// Lista de fechas en formato "dd/MM/yyyy" que son festivos oficiales.
        /// Los días festivos no computan como vacaciones para ningún trabajador.
        /// </summary>
        [JsonPropertyName("festivos")]
        public List<string> Festivos { get; set; } = new();

        /// <summary>
        /// Diccionario de trabajadores indexado por nombre. Cada entrada contiene sus días asignados y cupos.
        /// </summary>
        [JsonPropertyName("trabajadores")]
        public Dictionary<string, InfoTrabajador> Trabajadores { get; set; } = new();
    }
}
