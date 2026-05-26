using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CalendarioWPF.Models
{
    /// <summary>
    /// Modelo de configuración de la aplicación. Se persiste de forma independiente
    /// al plan de vacaciones, en el archivo 'app_config.json'.
    /// Controla aspectos de presentación y comportamiento de exportación.
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Texto del pie de página que aparece al pie de cada hoja en los reportes PDF exportados.
        /// </summary>
        [JsonPropertyName("pie_pagina_pdf")]
        public string PiePaginaPdf { get; set; } = "Gestor de Vacaciones Pro";

        /// <summary>
        /// Orientación de las páginas en el PDF Mensual. Valores válidos: "Portrait" (vertical) o "Landscape" (horizontal).
        /// </summary>
        [JsonPropertyName("orientacion_pdf")]
        public string OrientacionPdf { get; set; } = "Portrait";

        /// <summary>
        /// Si es <c>true</c>, se oculta la fila de totales netos al pie de la tabla Gantt (en UI y en exportaciones).
        /// </summary>
        [JsonPropertyName("ocultar_computo_gantt")]
        public bool OcultarComputoGantt { get; set; } = false;

        /// <summary>
        /// Lista de números de mes (1-12) que se muestran en la Vista Calendario y en el PDF Mensual.
        /// Por defecto muestra los meses de verano: Junio (6) a Septiembre (9).
        /// </summary>
        [JsonPropertyName("meses_a_mostrar")]
        public List<int> MesesAMostrar { get; set; } = new() { 6, 7, 8, 9 };

        /// <summary>
        /// Si es <c>true</c>, el PDF Mensual omitirá los meses de la configuración que no tengan
        /// ninguna vacación ni festivo marcado.
        /// </summary>
        [JsonPropertyName("ocultar_meses_sin_dias")]
        public bool OcultarMesesSinDias { get; set; } = false;

        /// <summary>
        /// Si es <c>true</c>, fuerza un salto de página antes de cada sección de resumen en el PDF Mensual.
        /// Si es <c>false</c>, los meses y el resumen se encadenan sin salto forzado.
        /// </summary>
        [JsonPropertyName("forzar_salto_pagina")]
        public bool ForzarSaltoPagina { get; set; } = true;

        /// <summary>
        /// Si es <c>true</c>, genera un archivo PDF independiente por cada año de cupo con datos.
        /// Si es <c>false</c>, todos los años se consolidan en un único PDF paginado.
        /// </summary>
        [JsonPropertyName("exportar_multiples_pdfs")]
        public bool ExportarMultiplesPdfs { get; set; } = false;

        /// <summary>
        /// Lista explícita de años de cupo a exportar. Si está vacía, se exportan
        /// automáticamente todos los años que contienen datos de vacaciones.
        /// </summary>
        [JsonPropertyName("anos_a_exportar")]
        public List<int> AnosAExportar { get; set; } = new();

        /// <summary>
        /// Propiedad de compatibilidad hacia atrás. Lee la clave antigua 'años_a_exportar' (con ñ)
        /// que se escribía en versiones previas del software, transfiriéndola a <see cref="AnosAExportar"/>.
        /// Se ignora completamente al serializar para evitar la escritura de caracteres Unicode escapados.
        /// </summary>
        [JsonPropertyName("años_a_exportar")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<int>? AnosAExportarFallback
        {
            get => null;
            set { if (value != null) AnosAExportar = value; }
        }
    }
}
