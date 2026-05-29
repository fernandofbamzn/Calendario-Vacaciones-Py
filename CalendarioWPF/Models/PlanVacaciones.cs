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
        /// Diccionario con fechas festivas por departamento.
        /// Clave: Nombre del departamento. Valor: Lista de fechas en formato "dd/MM/yyyy".
        /// </summary>
        [JsonPropertyName("festivosDepartamento")]
        public Dictionary<string, List<string>> FestivosDepartamento { get; set; } = new();

        /// <summary>
        /// Diccionario de trabajadores indexado por nombre. Cada entrada contiene sus días asignados y cupos.
        /// </summary>
        [JsonPropertyName("trabajadores")]
        public Dictionary<string, InfoTrabajador> Trabajadores { get; set; } = new();

        /// <summary>
        /// Lista de nombres de departamentos gestionables.
        /// Los trabajadores se asignan a uno de estos departamentos.
        /// Se usa para cierres de empresa en lote y para aplicar incompatibilidades por grupo.
        /// Por defecto contiene "General".
        /// </summary>
        [JsonPropertyName("departamentos")]
        public List<string> Departamentos { get; set; } = new() { "General" };

        /// <summary>
        /// Diccionario de reglas de incompatibilidad de vacaciones.
        /// Clave: nombre del trabajador.
        /// Valor: lista de nombres de trabajadores con los que no puede coincidir en vacaciones.
        /// Las reglas son bidireccionales (si A es incompatible con B, B también lo es con A).
        /// Los solapes generan avisos no bloqueantes, no impiden la asignación.
        /// </summary>
        [JsonPropertyName("incompatibilidades")]
        public Dictionary<string, List<string>> Incompatibilidades { get; set; } = new();

        /// <summary>
        /// Diccionario de fechas de cierre de empresa.
        /// Clave: nombre del departamento (o "Todos").
        /// Valor: lista de fechas ("dd/MM/yyyy") de cierre.
        /// Las fechas de cierre no generan incompatibilidades.
        /// </summary>
        [JsonPropertyName("cierres")]
        public Dictionary<string, List<string>> Cierres { get; set; } = new();

        /// <summary>
        /// Lista de departamentos cuyos miembros son automáticamente incompatibles entre sí.
        /// </summary>
        [JsonPropertyName("departamentosIncompatibles")]
        public List<string> DepartamentosIncompatibles { get; set; } = new();

        /// <summary>
        /// Colores asignados a cada departamento para su representación en PDF.
        /// </summary>
        [JsonPropertyName("departamentosColores")]
        public Dictionary<string, string> DepartamentosColores { get; set; } = new();
    }
}
