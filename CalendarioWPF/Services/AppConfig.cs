using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Modelo de configuración local de la aplicación.
    /// Se persiste en 'app_config.json' de forma independiente a los datos de vacaciones.
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Texto del pie de página que aparece en las exportaciones PDF.
        /// </summary>
        [JsonPropertyName("pie_pagina_pdf")]
        public string PiePaginaPdf { get; set; } = "Gestor de Vacaciones Pro";

        /// <summary>
        /// Orientación predeterminada del PDF Mensual: "Portrait" o "Landscape".
        /// </summary>
        [JsonPropertyName("orientacion_pdf")]
        public string OrientacionPdf { get; set; } = "Portrait";

        /// <summary>
        /// Si es true, se oculta la fila de cómputos netos al pie de la tabla Gantt.
        /// </summary>
        [JsonPropertyName("ocultar_computo_gantt")]
        public bool OcultarComputoGantt { get; set; } = false;

        /// <summary>
        /// Lista de meses (1-12) a mostrar en la Vista Calendario y en el PDF Mensual.
        /// Por defecto: Junio a Septiembre.
        /// </summary>
        [JsonPropertyName("meses_a_mostrar")]
        public List<int> MesesAMostrar { get; set; } = new() { 6, 7, 8, 9 };

        /// <summary>
        /// Si es true, al exportar a PDF se ocultarán los meses que no tengan días marcados (vacaciones o festivos).
        /// </summary>
        [JsonPropertyName("ocultar_meses_sin_dias")]
        public bool OcultarMesesSinDias { get; set; } = false;

        /// <summary>
        /// Si es true, se fuerza un salto de página por cada año y antes del resumen en el PDF mensual.
        /// </summary>
        [JsonPropertyName("forzar_salto_pagina")]
        public bool ForzarSaltoPagina { get; set; } = true;

        /// <summary>
        /// Si es true, se genera un archivo PDF separado para cada año de vacaciones seleccionado en la exportación.
        /// </summary>
        [JsonPropertyName("exportar_multiples_pdfs")]
        public bool ExportarMultiplesPdfs { get; set; } = false;

        /// <summary>
        /// Lista de años específicos a exportar. Si está vacía, se exportan todos los años con datos.
        /// </summary>
        [JsonPropertyName("anos_a_exportar")]
        public List<int> AnosAExportar { get; set; } = new();

        /// <summary>
        /// Fallback de compatibilidad para leer la propiedad anterior 'años_a_exportar'.
        /// </summary>
        [JsonPropertyName("años_a_exportar")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<int>? AnosAExportarFallback
        {
            get => null;
            set { if (value != null) AnosAExportar = value; }
        }
    }

    /// <summary>
    /// Gestor estático de persistencia para la configuración local de la aplicación.
    /// </summary>
    public static class AppConfigManager
    {
        private const string ConfigFilename = "app_config.json";

        /// <summary>
        /// Carga la configuración desde el archivo local. Si no existe, devuelve valores por defecto.
        /// </summary>
        public static AppConfig Cargar()
        {
            try
            {
                if (File.Exists(ConfigFilename))
                {
                    string json = File.ReadAllText(ConfigFilename, Encoding.UTF8);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch
            {
                // Si falla la lectura, devolver configuración por defecto sin interrumpir la app
            }
            return new AppConfig();
        }

        /// <summary>
        /// Guarda la configuración actual en el archivo local.
        /// </summary>
        public static void Guardar(AppConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilename, json, Encoding.UTF8);
            }
            catch
            {
                // Error silencioso al guardar configuración local
            }
        }
    }
}
