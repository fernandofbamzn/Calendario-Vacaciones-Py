using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CalendarioWPF
{
    public class PlanVacaciones
    {
        [JsonPropertyName("titulo_pagina")]
        public string TituloPagina { get; set; } = "Planificación de Vacaciones";

        [JsonPropertyName("year")]
        public int Year { get; set; } = System.DateTime.Today.Year;

        [JsonPropertyName("festivos")]
        public List<string> Festivos { get; set; } = new();

        [JsonPropertyName("trabajadores")]
        public Dictionary<string, InfoTrabajador> Trabajadores { get; set; } = new();
    }

    public class InfoTrabajador
    {
        [JsonPropertyName("vacaciones")]
        public List<string> Vacaciones { get; set; } = new();

        [JsonPropertyName("dias_base")]
        public int DiasBase { get; set; } = 22;

        [JsonPropertyName("dias_extras")]
        public int DiasExtras { get; set; } = 0;

        [JsonPropertyName("imputaciones")]
        public Dictionary<string, int> Imputaciones { get; set; } = new();
    }
}
